using System.Net;
using System.Text;
using SCADASMSSystem.IntegrationTests.Infrastructure;
using SCADASMSSystem.IntegrationTests.Mock;
using SCADASMSSystem.Web.Services.Providers;
using Xunit;

namespace SCADASMSSystem.IntegrationTests;

/// <summary>
/// Fast checks of the mock itself and of the profiles against it (no app, no DB, no bat).
/// They prove the mock enforces the Sheba contract using the spec samples verbatim.
/// </summary>
[Trait("Category", "MockSelfTest")]
public class MockProviderSelfTests : IAsyncLifetime
{
    private MockSmsProvider _mock = null!;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task InitializeAsync() => _mock = await MockSmsProvider.StartAsync();

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _mock.DisposeAsync();
    }

    private Task<HttpResponseMessage> PostSoapAsync(string body, string? soapAction = SoapContract.SoapAction, string contentType = "text/xml;charset=UTF-8")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _mock.SoapUrl) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        if (soapAction != null) request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
        return _http.SendAsync(request);
    }

    private static string SampleRequest => SoapContract.LoadFixture("sheba-sample-request.xml");

    [Fact]
    public async Task Spec_sample_request_is_accepted_with_spec_success_response()
    {
        _mock.Soap.Username = "User";
        _mock.Soap.Password = "XXXXXX";
        var response = await PostSoapAsync(SampleRequest);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SoapContract.SuccessEnvelope, body);
        var r = Assert.Single(_mock.Requests);
        Assert.Empty(r.ContractErrors);
        Assert.Empty(r.ContractWarnings);
        Assert.True(r.Accepted);
        Assert.Equal("0525595069", r.Phone);
        Assert.Equal("Test1", r.Message);
    }

    [Theory]
    [InlineData("missing-soapaction")]
    [InlineData("phone-with-dash")]
    [InlineData("message-too-long")]
    [InlineData("missing-messagetype")]
    [InlineData("missing-sendingsystem")]
    [InlineData("wrong-content-type")]
    [InlineData("empty-tz")]
    [InlineData("missing-firstname")]
    [InlineData("empty-lastname")]
    public async Task Contract_violations_get_success_false(string violation)
    {
        _mock.Soap.Username = "User";
        _mock.Soap.Password = "XXXXXX";
        var body = SampleRequest;
        string? action = SoapContract.SoapAction;
        var contentType = "text/xml;charset=UTF-8";
        switch (violation)
        {
            case "missing-soapaction": action = null; break;
            case "phone-with-dash": body = body.Replace("0525595069", "052-5595069"); break;
            case "message-too-long": body = body.Replace("<tem:Message>Test1", "<tem:Message>" + new string('א', 1001)); break;
            case "missing-messagetype": body = body.Replace("<tem:MessageType>SmsType1</tem:MessageType>", ""); break;
            case "missing-sendingsystem": body = body.Replace("<tem:SendingSystem>AppName</tem:SendingSystem>", ""); break;
            case "wrong-content-type": contentType = "application/soap+xml; charset=utf-8"; break;
            case "empty-tz": body = body.Replace("<tem:TZ>P</tem:TZ>", "<tem:TZ/>"); break;
            case "missing-firstname": body = body.Replace("<tem:FirstName>shay</tem:FirstName>", ""); break;
            case "empty-lastname": body = body.Replace("<tem:LastName>ma</tem:LastName>", "<tem:LastName></tem:LastName>"); break;
        }

        var response = await PostSoapAsync(body, action, contentType);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // HTTP 200 even on failure: body decides
        Assert.Contains("<Success>false</Success>", text);
        Assert.NotEmpty(Assert.Single(_mock.Requests).ContractErrors);
    }

    [Fact]
    public async Task Lenient_mode_accepts_empty_person_fields_with_a_warning()
    {
        _mock.Soap.Username = "User";
        _mock.Soap.Password = "XXXXXX";
        _mock.Soap.StrictPersonFields = false;
        var response = await PostSoapAsync(SampleRequest.Replace("<tem:TZ>P</tem:TZ>", "<tem:TZ/>"));
        Assert.Contains("<Success>true</Success>", await response.Content.ReadAsStringAsync());
        var r = Assert.Single(_mock.Requests);
        Assert.Empty(r.ContractErrors);
        Assert.Contains(r.ContractWarnings, w => w.StartsWith("TZ"));
    }

    [Fact]
    public async Task Wrong_ws_security_password_gets_soap_fault()
    {
        var response = await PostSoapAsync(SampleRequest); // sample has User/XXXXXX, mock expects other credentials
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Fault", await response.Content.ReadAsStringAsync());
        Assert.False(_mock.Requests.Single().AuthOk);
    }

    [Fact]
    public async Task Sheba_profile_rendered_by_the_app_satisfies_the_contract()
    {
        var profile = Profiles.Sheba(_mock);
        Assert.Empty(ProfileValidator.Validate(profile));
        var context = new ProfileContext("[11/11 10:00] תקלה במשאבה & \"לחץ\" <גבוה>", "050-123-4567", "85.3", "SCADA-x", "normal", 7, 1, "דנה", "כהן", "300000001");
        using var built = ProviderRequestBuilder.Build(profile, context);
        using var response = await _http.SendAsync(built.Request);
        var body = await response.Content.ReadAsStringAsync();

        var r = _mock.Requests.Single();
        Assert.Empty(r.ContractErrors);
        Assert.True(r.Accepted);
        Assert.Equal("0501234567", r.Phone);
        Assert.Equal("[11/11 10:00] תקלה במשאבה & \"לחץ\" <גבוה>", r.Message);
        Assert.True(ResponseEvaluator.Evaluate(profile, (int)response.StatusCode, body).Accepted);
        Assert.False(ResponseEvaluator.Evaluate(profile, 200, SoapContract.FailureEnvelope("boom")).Accepted);
    }

    [Fact]
    public async Task Json_profile_rendered_by_the_app_satisfies_the_rest_contract()
    {
        var profile = Profiles.JsonGateway(_mock);
        Assert.Empty(ProfileValidator.Validate(profile));
        var context = new ProfileContext("שלום \"world\" \\ end", "0501234567", null, "SCADA-abc", "normal", 7, 1, null, null, null);
        using var built = ProviderRequestBuilder.Build(profile, context);
        using var response = await _http.SendAsync(built.Request);
        var body = await response.Content.ReadAsStringAsync();

        var r = _mock.Requests.Single();
        Assert.Empty(r.ContractErrors);
        Assert.Equal("+972501234567", r.Phone);
        Assert.Equal("SCADA-abc", r.Reference);
        Assert.Equal("שלום \"world\" \\ end", r.Message);
        Assert.True(ResponseEvaluator.Evaluate(profile, (int)response.StatusCode, body).Accepted);
    }

    [Fact]
    public async Task Fault_injection_kinds_behave_as_documented()
    {
        var profile = Profiles.JsonGateway(_mock);
        async Task<(int Status, string Body, Exception? Error)> SendAsync()
        {
            using var built = ProviderRequestBuilder.Build(profile, new ProfileContext("x", "0501234567", null, "A1", null, 1, 1, null, null, null));
            try
            {
                using var resp = await _http.SendAsync(built.Request);
                return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync(), null);
            }
            catch (Exception ex) { return (0, "", ex); }
        }

        _mock.AddFault(new FaultRule { Kind = FaultKind.Http5xx, StatusCode = 503, Times = 1 });
        Assert.Equal(503, (await SendAsync()).Status);
        Assert.Equal(200, (await SendAsync()).Status);

        _mock.AddFault(new FaultRule { Kind = FaultKind.SemanticFailure, Times = 1 });
        var semantic = await SendAsync();
        Assert.Equal(200, semantic.Status);
        Assert.False(ResponseEvaluator.Evaluate(profile, semantic.Status, semantic.Body).Accepted);

        _mock.AddFault(new FaultRule { Kind = FaultKind.Drop, Times = 1 });
        Assert.IsType<HttpRequestException>((await SendAsync()).Error);

        _mock.AddFault(new FaultRule { Kind = FaultKind.Hang, Times = 1 });
        Assert.IsAssignableFrom<TaskCanceledException>((await SendAsync()).Error);

        _mock.AddFault(new FaultRule { Kind = FaultKind.AuthFailure, Times = 1 });
        Assert.Equal(401, (await SendAsync()).Status);

        _mock.AddFault(new FaultRule { Kind = FaultKind.Latency, LatencyMs = 400, Times = 1 });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(200, (await SendAsync()).Status);
        Assert.True(sw.ElapsedMilliseconds >= 380);

        Assert.Equal(2, _mock.Requests.Count(r => r.Accepted));
    }
}
