using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SCADASMSSystem.IntegrationTests.Mock;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

/// <summary>Knobs for one simulated site. Defaults are shortened timings so the suite runs in minutes.</summary>
public sealed record SiteOptions
{
    public int TimeoutSeconds { get; init; } = 3;
    public int RetryAttempts { get; init; } = 1;
    public int RateLimit { get; init; } = 100000;
    public int RateWindow { get; init; } = 60;
    public int BatMaxTimeSeconds { get; init; } = 20;
    public int SpoolPollSeconds { get; init; } = 5;
    /// <summary>Must exceed BatMaxTimeSeconds (same rule as on site: MinAgeSeconds &gt; MAXTIME).</summary>
    public int SpoolMinAgeSeconds { get; init; } = 25;
    public int SpoolMaxAgeMinutes { get; init; } = 60;
}

public sealed record BatResult(string[] Args, int ExitCode, DateTime StartedUtc, DateTime EndedUtc, string Output, bool TimedOut = false)
{
    public double DurationMs => (EndedUtc - StartedUtc).TotalMilliseconds;
}

/// <summary>
/// One simulated site in its own temp folder: a PCIMUTIL folder with a byte-identical copy of
/// Scada_sms.bat and its Scada_sms.ini, a spool root, a settings.json holding the provider profile
/// (pointing at the mock), and the real web app started as a separate process
/// (SCADASMSSystem.Web.exe) against the scada_mock_it database.
/// </summary>
public sealed class Site : IAsyncDisposable
{
    private Process? _app;
    private readonly List<BatResult> _harnessTimeouts = new();

    /// <summary>Bat calls the harness had to kill (no exit within MAXTIME + margin). Any entry fails the test.</summary>
    public IReadOnlyList<BatResult> HarnessTimeouts { get { lock (_harnessTimeouts) return _harnessTimeouts.ToList(); } }

    /// <summary>Set by the test when it failed: the site folder is then kept for debugging.</summary>
    public bool KeepFiles
    {
        get => _keepFiles;
        set { _keepFiles = value; if (value) TestDb.KeepDatabase = true; }
    }
    private bool _keepFiles = MockIt.KeepArtifacts;

    /// <summary>Harness limit for one bat process: MAXTIME plus margin for cmd/PowerShell start under load.</summary>
    public TimeSpan BatHarnessTimeout => TimeSpan.FromSeconds(Options.BatMaxTimeSeconds + 60);

    public string Kind { get; }
    public string Name { get; }
    public SiteOptions Options { get; }
    public MockSmsProvider Mock { get; }
    public string Root { get; }
    public string SpoolRoot => Path.Combine(Root, "ProgramData_SCADA_SMS");
    public string SpoolDir => Path.Combine(SpoolRoot, "spool");
    public string BatLog => Path.Combine(SpoolRoot, "Scada_sms.log");
    public string PcimUtil => Path.Combine(Root, "PCIMUTIL");
    public string BatPath => Path.Combine(PcimUtil, "Scada_sms.bat");
    public string SettingsFile => Path.Combine(Root, "config", "settings.json");
    public string LogDir => Path.Combine(Root, "logs");
    public int Port { get; private set; }
    public string ServiceUrl => $"http://localhost:{Port}";
    public ProviderProfile Profile { get; }
    public bool AppRunning => _app is { HasExited: false };

    private Site(string kind, string name, SiteOptions options, MockSmsProvider mock)
    {
        Kind = kind;
        Name = name;
        Options = options;
        Mock = mock;
        Root = Path.Combine(MockIt.WorkRoot, $"{kind}-{name}");
        Port = FreePort();
        Profile = Profiles.For(kind, mock);
    }

    static Site()
    {
        // Alarm storms run hundreds of concurrent process waits plus the mock's Kestrel in this process.
        ThreadPool.GetMinThreads(out var worker, out var io);
        ThreadPool.SetMinThreads(Math.Max(worker, 256), Math.Max(io, 256));
    }

    public static async Task<Site> CreateAsync(string kind, string name, SiteOptions? options = null)
    {
        await TestDb.EnsureReadyAsync();
        var mock = await MockSmsProvider.StartAsync();
        Site site;
        try
        {
            site = new Site(kind, name, options ?? new SiteOptions(), mock);
            site.Layout();
        }
        catch
        {
            await mock.DisposeAsync();
            throw;
        }
        mock.SpoolProbe = () => Directory.Exists(site.SpoolDir)
            ? Directory.GetFiles(site.SpoolDir, "*.json").Select(f => Path.GetFileName(f)).ToArray()
            : Array.Empty<string>();
        return site;
    }

    private void Layout()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        Directory.CreateDirectory(PcimUtil);
        Directory.CreateDirectory(SpoolRoot);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);

        // Byte-for-byte copy, as the installer does for Akara's PCIMUTIL folder.
        File.Copy(MockIt.BatSource, BatPath);
        WriteIni();

        // Safety: alarms from this suite may only ever reach the loopback mock.
        var host = new Uri(Profile.Url).Host;
        if (host != "127.0.0.1" && host != "localhost")
            throw new InvalidOperationException("Refusing to start: provider profile does not point at the local mock: " + Profile.Url);

        var settings = new
        {
            SmsSettings = new
            {
                Options.RateLimit,
                Options.RateWindow,
                DuplicateWindow = 5,
                Options.TimeoutSeconds,
                Options.RetryAttempts,
                TestMode = false,
                LogSoapXml = false,
                Profile,
            }
        };
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    private void WriteIni() =>
        File.WriteAllText(Path.Combine(PcimUtil, "Scada_sms.ini"),
            $"URL={ServiceUrl}/api/sms/send\r\nSPOOL_ROOT={SpoolRoot}\r\nMAXTIME={Options.BatMaxTimeSeconds}\r\n",
            new UTF8Encoding(false));

    /// <summary>The effective settings this site runs with (secrets masked), for the report.</summary>
    public object DescribeConfig() => new
    {
        Kind,
        ProviderUrl = Profile.Url,
        Profile.ContentType,
        Profile.PhoneFormat,
        Profile.CountryCode,
        Auth = Profile.Auth.Type,
        Headers = Profile.Headers.Select(h => $"{h.Name}: {h.Value}").ToArray(),
        Success = $"{Profile.Success.StatusCodes} {Profile.Success.Match} {Profile.Success.Expression}={Profile.Success.Expected}",
        Failure = $"{Profile.Failure.Match} {Profile.Failure.Expression}",
        Options.TimeoutSeconds,
        Options.RetryAttempts,
        Options.RateLimit,
        Options.RateWindow,
        Options.BatMaxTimeSeconds,
        Options.SpoolPollSeconds,
        Options.SpoolMinAgeSeconds,
        Options.SpoolMaxAgeMinutes,
    };

    /// <summary>
    /// Starts the app; if it dies because another process took the port between FreePort() and Kestrel's
    /// bind, picks a new port, rewrites Scada_sms.ini and retries (up to 3 times).
    /// </summary>
    public async Task StartAppAsync(TimeSpan? timeout = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await StartAppOnceAsync(timeout);
                return;
            }
            catch (InvalidOperationException) when (attempt < 3 && PortCollisionLogged())
            {
                StopApp();
                Port = FreePort();
                WriteIni();
            }
        }
    }

    private bool PortCollisionLogged()
    {
        try
        {
            return Directory.GetFiles(LogDir, "scada-sms-*.log").Any(f =>
            {
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var text = new StreamReader(fs).ReadToEnd();
                return text.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("Failed to bind", StringComparison.OrdinalIgnoreCase);
            });
        }
        catch (IOException) { return false; }
    }

    private async Task StartAppOnceAsync(TimeSpan? timeout)
    {
        if (AppRunning) return;
        var exe = MockIt.AppExe;
        if (!File.Exists(exe))
            throw new FileNotFoundException("Web app not built; build the solution first.", exe);

        // Output goes to NUL, like the Windows service (no console). A redirected pipe that is not
        // drained fast enough, or a hidden console window, makes Serilog's synchronous console sink
        // throttle the whole app. Everything is still in the Serilog file under LogDir.
        // cmd.exe only does the redirection; the app is its child, so ProcessPath (content root) is the exe.
        var psi = new ProcessStartInfo("cmd.exe", $"/d /c \"\"{exe}\" >NUL 2>&1\"")
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["Urls"] = ServiceUrl;
        psi.Environment["ConnectionStrings__DefaultConnection"] = MockIt.SqlConnectionString;
        psi.Environment["SettingsFile"] = SettingsFile;
        psi.Environment["ScadaSpool__Root"] = SpoolRoot;
        psi.Environment["ScadaSpool__PollSeconds"] = Options.SpoolPollSeconds.ToString();
        psi.Environment["ScadaSpool__MinAgeSeconds"] = Options.SpoolMinAgeSeconds.ToString();
        psi.Environment["ScadaSpool__MaxAgeMinutes"] = Options.SpoolMaxAgeMinutes.ToString();
        psi.Environment["Logging__File__Path"] = Path.Combine(LogDir, "scada-sms-.log");
        psi.Environment["Scada__PcimUtilDir"] = PcimUtil;

        _app = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _app.Start();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(120));
        while (DateTime.UtcNow < deadline)
        {
            if (_app.HasExited)
                throw new InvalidOperationException($"Web app exited during startup (code {_app.ExitCode}); see {LogDir}");
            try
            {
                var response = await http.GetAsync(ServiceUrl + "/api/sms/health");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(300);
        }
        throw new TimeoutException($"Web app did not become healthy at {ServiceUrl}; see {LogDir}");
    }

    /// <summary>Hard stop (process killed), like a crash or the service being stopped mid-flight.</summary>
    public void StopApp()
    {
        if (_app == null) return;
        try
        {
            if (!_app.HasExited)
            {
                _app.Kill(entireProcessTree: true);
                _app.WaitForExit(15000);
            }
        }
        catch (InvalidOperationException) { }
        _app.Dispose();
        _app = null;
    }

    /// <summary>
    /// Runs the bat exactly as Akara's alarm action does: cmd.exe, three arguments
    /// "DESCRIPTION" GROUP "ALM_VALUE". Extra arguments (AlarmId, priority) are for manual tests only.
    /// </summary>
    public async Task<BatResult> RunBatAsync(string description, string group, string value, params string[] extra)
    {
        var args = new[] { description, group, value }.Concat(extra).ToArray();
        var commandLine = new StringBuilder();
        commandLine.Append("/d /s /c \"\"").Append(BatPath).Append("\" \"").Append(description).Append("\" ")
            .Append(group).Append(" \"").Append(value).Append('"');
        foreach (var e in extra) commandLine.Append(" \"").Append(e).Append('"');
        commandLine.Append('"');
        return await RunCmdAsync(commandLine.ToString(), args);
    }

    /// <summary>Raw cmd.exe call, for argument-validation scenarios.</summary>
    public async Task<BatResult> RunBatRawAsync(string rawArguments)
        => await RunCmdAsync($"/d /s /c \"\"{BatPath}\" {rawArguments}\"", new[] { rawArguments });

    private async Task<BatResult> RunCmdAsync(string arguments, string[] args)
    {
        // Akara does not read the bat's output, and redirected pipes would cost a blocked thread-pool
        // thread per stream (synchronous anonymous pipes): with 200 concurrent bats that starves the
        // in-process mock. So no redirection, like Akara.
        var psi = new ProcessStartInfo("cmd.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = PcimUtil,
        };
        var started = DateTime.UtcNow;
        using var p = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(BatHarnessTimeout);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // A bat must end within MAXTIME + margin (curl --max-time bounds it). Kill cmd.exe and every
            // child (powershell.exe, curl.exe) and fail the test with the evidence.
            var note = $"Harness timeout: no exit within {BatHarnessTimeout.TotalSeconds:0} s; ";
            try
            {
                p.Kill(entireProcessTree: true);
                note += "process tree killed";
            }
            catch (Exception ex) // InvalidOperationException (already gone), Win32Exception (access denied), AggregateException...
            {
                note += $"Kill failed ({ex.GetType().Name}: {ex.Message}); ";
                note += TaskKillTree(p.Id);
            }
            try { await p.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token); }
            catch (Exception) { note += "; process still present after 15 s"; }
            var killed = new BatResult(args, -1, started, DateTime.UtcNow, note, TimedOut: true);
            lock (_harnessTimeouts) _harnessTimeouts.Add(killed);
            return killed;
        }
        var ended = DateTime.UtcNow;
        return new BatResult(args, p.ExitCode, started, ended, "");
    }

    /// <summary>Fallback when Process.Kill fails: taskkill /T /F on the bat's process tree.</summary>
    private static string TaskKillTree(int pid)
    {
        try
        {
            using var tk = Process.Start(new ProcessStartInfo("taskkill.exe", $"/T /F /PID {pid}") { UseShellExecute = false, CreateNoWindow = true })!;
            tk.WaitForExit(15000);
            return $"taskkill /T /F exit {tk.ExitCode}";
        }
        catch (Exception ex)
        {
            return $"taskkill failed ({ex.GetType().Name}: {ex.Message})";
        }
    }

    public string[] SpoolFiles() => Directory.Exists(SpoolDir)
        ? Directory.GetFiles(SpoolDir, "*.json").Select(f => Path.GetFileName(f)!).OrderBy(f => f).ToArray()
        : Array.Empty<string>();

    public string[] SpoolSubfolderFiles(string sub) => Directory.Exists(Path.Combine(SpoolDir, sub))
        ? Directory.GetFiles(Path.Combine(SpoolDir, sub)).Select(f => Path.GetFileName(f)!).ToArray()
        : Array.Empty<string>();

    public string[] BatLogLines()
    {
        if (!File.Exists(BatLog)) return Array.Empty<string>();
        using var fs = new FileStream(BatLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>AlarmId of the last bat call, from Scada_sms.log ("alarm=...").</summary>
    public string? LastLoggedAlarmId()
    {
        var line = BatLogLines().LastOrDefault(l => l.Contains(" alarm="));
        if (line == null) return null;
        var start = line.IndexOf(" alarm=", StringComparison.Ordinal) + 7;
        var end = line.IndexOf(' ', start);
        return end < 0 ? line[start..] : line[start..end];
    }

    public static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, int pollMs = 500)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(pollMs);
        }
        return await condition();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        try { StopApp(); } catch { /* best effort */ }
        try { await Mock.DisposeAsync(); } catch { /* best effort */ }
        if (!KeepFiles)
        {
            // The killed app may hold log files for a moment.
            for (var i = 0; i < 5 && Directory.Exists(Root); i++)
            {
                try { Directory.Delete(Root, recursive: true); }
                catch (IOException) { await Task.Delay(500); }
                catch (UnauthorizedAccessException) { await Task.Delay(500); }
            }
        }
    }
}
