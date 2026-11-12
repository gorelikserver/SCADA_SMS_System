using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SCADASMSSystem.IntegrationTests.Mock;

public enum FaultKind
{
    None,
    /// <summary>Extra delay, then the normal answer.</summary>
    Latency,
    /// <summary>HTTP 5xx (StatusCode, default 503) with a plain body.</summary>
    Http5xx,
    /// <summary>Never answers; the request ends only when the client gives up.</summary>
    Hang,
    /// <summary>Connection reset without any HTTP response.</summary>
    Drop,
    /// <summary>HTTP 200 with a semantic failure (SOAP Success=false / REST status=error).</summary>
    SemanticFailure,
    /// <summary>Credentials refused (SOAP Fault on HTTP 500 / REST 401), whatever was sent.</summary>
    AuthFailure,
}

/// <summary>
/// One fault-injection rule. Rules are checked in order; the first one that matches the phone,
/// still has Times left and wins its Probability roll is applied.
/// </summary>
public sealed class FaultRule
{
    public FaultKind Kind { get; init; }
    /// <summary>Only requests to this phone (as received on the wire). Null = any phone.</summary>
    public string? Phone { get; init; }
    /// <summary>How many times the rule fires; -1 = unlimited.</summary>
    public int Times { get; set; } = -1;
    public double Probability { get; init; } = 1.0;
    public int StatusCode { get; init; } = 503;
    public int LatencyMs { get; init; }

    public override string ToString() =>
        $"{Kind}{(Phone != null ? " phone=" + Phone : "")}{(Times >= 0 ? " times=" + Times : "")}" +
        $"{(Probability < 1 ? $" p={Probability:0.##}" : "")}{(Kind == FaultKind.Http5xx ? " status=" + StatusCode : "")}" +
        $"{(LatencyMs > 0 ? $" latency={LatencyMs}ms" : "")}";
}

/// <summary>Everything the mock saw and answered for one request (evidence for the report).</summary>
public sealed class RecordedRequest
{
    public long Seq { get; init; }
    public string Kind { get; init; } = "";
    public DateTime ReceivedAtUtc { get; init; }
    public double DurationMs { get; set; }
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = new();
    public string Body { get; init; } = "";
    public string? Phone { get; set; }
    public string? Message { get; set; }
    /// <summary>REST "reference" (the AlarmId). SOAP has no such field in the Sheba contract.</summary>
    public string? Reference { get; set; }
    public Dictionary<string, string?> Fields { get; } = new();
    public List<string> ContractErrors { get; } = new();
    public List<string> ContractWarnings { get; } = new();
    public bool AuthOk { get; set; }
    public string? FaultApplied { get; set; }
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = "";
    /// <summary>True when the mock answered with a success the app should treat as accepted.</summary>
    public bool Accepted { get; set; }
    /// <summary>Spool folder content at the moment the provider was called (proves spool-first).</summary>
    public string[]? SpoolSnapshot { get; set; }
}

/// <summary>
/// In-process Kestrel mock of the SMS provider. SOAP endpoint follows the Sheba Ensemble contract
/// (docs/integration/sheba-ensemble-soap-spec.md); REST endpoint is a generic JSON gateway.
/// Binds to 127.0.0.1 only: nothing ever leaves the machine.
/// </summary>
public sealed class MockSmsProvider : IAsyncDisposable
{
    public const string SoapPath = "/csp/healthshare/sms/services/Sheba.CoreInterfaces.BS.SMS.Direct1.cls";
    public const string RestPath = "/api/v1/messages";

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly object _faultLock = new();
    private readonly Random _random;
    private long _seq;
    private int _inFlight;

    /// <summary>Requests received and not yet finished (answered, dropped or abandoned).</summary>
    public int InFlight => Volatile.Read(ref _inFlight);

    public SoapContract Soap { get; } = new();
    public RestContract Rest { get; } = new();
    public List<FaultRule> Faults { get; } = new();
    public int BaseLatencyMs { get; set; }
    public Func<string[]>? SpoolProbe { get; set; }
    public string BaseUrl { get; private set; } = "";

    public IReadOnlyList<RecordedRequest> Requests => _requests.OrderBy(r => r.Seq).ToList();

    private MockSmsProvider(WebApplication app, int seed)
    {
        _app = app;
        _random = new Random(seed);
    }

    public static async Task<MockSmsProvider> StartAsync(int seed = 12345)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Limits.MaxConcurrentConnections = null;
            k.Limits.MinRequestBodyDataRate = null;
        });
        var app = builder.Build();
        var mock = new MockSmsProvider(app, seed);
        app.MapPost(SoapPath, ctx => mock.HandleAsync(ctx, "SOAP"));
        app.MapPost(RestPath, ctx => mock.HandleAsync(ctx, "REST"));
        app.MapFallback(ctx => { ctx.Response.StatusCode = 404; return Task.CompletedTask; });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        mock.BaseUrl = address.TrimEnd('/');
        return mock;
    }

    public string SoapUrl => BaseUrl + SoapPath;
    public string RestUrl => BaseUrl + RestPath;

    public void ClearRequests()
    {
        while (_requests.TryDequeue(out _)) { }
    }

    public void AddFault(FaultRule rule)
    {
        lock (_faultLock) Faults.Add(rule);
    }

    public void ClearFaults()
    {
        lock (_faultLock) Faults.Clear();
    }

    private FaultRule? PickFault(string? phone)
    {
        lock (_faultLock)
        {
            foreach (var rule in Faults)
            {
                if (rule.Phone != null && rule.Phone != phone) continue;
                if (rule.Times == 0) continue;
                if (rule.Probability < 1.0 && _random.NextDouble() >= rule.Probability) continue;
                if (rule.Times > 0) rule.Times--;
                return rule;
            }
            return null;
        }
    }

    private async Task HandleAsync(HttpContext ctx, string kind)
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _inFlight);
        try { await HandleCoreAsync(ctx, kind, sw); }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    private async Task HandleCoreAsync(HttpContext ctx, string kind, Stopwatch sw)
    {
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var record = new RecordedRequest
        {
            Seq = Interlocked.Increment(ref _seq),
            Kind = kind,
            ReceivedAtUtc = DateTime.UtcNow,
            Method = ctx.Request.Method,
            Path = ctx.Request.Path,
            Headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Body = body,
        };
        try { record.SpoolSnapshot = SpoolProbe?.Invoke(); } catch { /* evidence only */ }
        _requests.Enqueue(record);

        try
        {
            if (kind == "SOAP") Soap.Inspect(record);
            else Rest.Inspect(record);

            var fault = PickFault(record.Phone);
            record.FaultApplied = fault?.ToString();

            var latency = BaseLatencyMs + (fault?.Kind == FaultKind.Latency ? fault.LatencyMs : 0);
            if (latency > 0)
                await Task.Delay(latency, ctx.RequestAborted);

            switch (fault?.Kind)
            {
                case FaultKind.Hang:
                    record.ResponseStatus = 0;
                    record.ResponseBody = "(hung until the client gave up)";
                    try { await Task.Delay(Timeout.Infinite, ctx.RequestAborted); } catch (OperationCanceledException) { }
                    return;
                case FaultKind.Drop:
                    record.ResponseStatus = 0;
                    record.ResponseBody = "(connection dropped)";
                    ctx.Abort();
                    return;
                case FaultKind.Http5xx:
                    await WriteAsync(ctx, record, fault.StatusCode, "text/plain", $"Injected HTTP {fault.StatusCode}", accepted: false);
                    return;
            }

            var (status, contentType, responseBody, accepted) = kind == "SOAP"
                ? Soap.Respond(record, fault?.Kind)
                : Rest.Respond(record, fault?.Kind);
            await WriteAsync(ctx, record, status, contentType, responseBody, accepted);
        }
        catch (OperationCanceledException)
        {
            record.ResponseStatus = 0;
            record.ResponseBody = "(client went away)";
        }
        finally
        {
            record.DurationMs = sw.Elapsed.TotalMilliseconds;
        }
    }

    private static async Task WriteAsync(HttpContext ctx, RecordedRequest record, int status, string contentType, string body, bool accepted)
    {
        record.ResponseStatus = status;
        record.ResponseBody = body;
        record.Accepted = accepted && status == (int)HttpStatusCode.OK;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        await ctx.Response.WriteAsync(body, Encoding.UTF8);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
