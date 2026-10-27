using Microsoft.Extensions.Logging.Abstractions;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class SpoolProcessorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scada-spool-" + Guid.NewGuid().ToString("N"));
    private readonly SpoolOptions _options;
    private readonly DateTime _now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly List<SpoolAlarm> _sent = new();

    public SpoolProcessorTests()
    {
        _options = new SpoolOptions { Root = _root, MinAgeSeconds = 150, MaxAgeMinutes = 60 };
        Directory.CreateDirectory(Path.Combine(_root, "spool"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Spool(string name, string json, TimeSpan age)
    {
        var path = Path.Combine(_root, "spool", name + ".json");
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, _now - age);
        return path;
    }

    private static string AlarmJson(string alarmId, DateTime receivedUtc) =>
        $$"""{"Message":"תקלה במשאבה","GroupId":3,"AlarmId":"{{alarmId}}","Priority":"normal","Value":"85.3","ReceivedAt":"{{receivedUtc:o}}"}""";

    private SpoolProcessor Processor(int recipients, int accepted) =>
        new(_options, (alarm, _) =>
        {
            _sent.Add(alarm);
            return Task.FromResult(new CriticalSmsDeliveryResult(alarm.AlarmId, recipients, accepted, []));
        }, NullLogger.Instance);

    [Fact]
    public async Task FullyAccepted_DeletesFileAndSendsOriginalFields()
    {
        var path = Spool("A1", AlarmJson("SCADA-A1", _now.AddMinutes(-5)), TimeSpan.FromMinutes(5));

        var pass = await Processor(2, 2).ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Delivered);
        Assert.False(File.Exists(path));
        var sent = Assert.Single(_sent);
        Assert.Equal("SCADA-A1", sent.AlarmId);
        Assert.Equal("תקלה במשאבה", sent.Message);
        Assert.Equal(3, sent.GroupId);
        Assert.Equal("85.3", sent.Value);
    }

    [Fact]
    public async Task YoungFile_IsSkippedWhileBatchMayStillBeWaiting()
    {
        var path = Spool("A2", AlarmJson("SCADA-A2", _now.AddSeconds(-30)), TimeSpan.FromSeconds(30));

        await Processor(1, 1).ProcessOnceAsync(_now);

        Assert.Empty(_sent);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task PartialAcceptance_KeepsFileForRetry()
    {
        var path = Spool("A3", AlarmJson("SCADA-A3", _now.AddMinutes(-10)), TimeSpan.FromMinutes(10));

        var pass = await Processor(3, 2).ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Pending);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task NotAcceptedPastMaxAge_MovesToFailed()
    {
        Spool("A4", AlarmJson("SCADA-A4", _now.AddMinutes(-61)), TimeSpan.FromMinutes(5));

        var pass = await Processor(2, 0).ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Failed);
        Assert.True(File.Exists(Path.Combine(_root, "spool", "failed", "A4.json")));
    }

    [Fact]
    public async Task NoEligibleRecipients_IsNotSuccessAndKeepsFile()
    {
        var path = Spool("A5", AlarmJson("SCADA-A5", _now.AddMinutes(-10)), TimeSpan.FromMinutes(10));

        var pass = await Processor(0, 0).ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Pending);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SenderThrows_KeepsFileForRetry()
    {
        var path = Spool("A6", AlarmJson("SCADA-A6", _now.AddMinutes(-10)), TimeSpan.FromMinutes(10));
        var processor = new SpoolProcessor(_options, (_, _) => throw new InvalidOperationException("db down"), NullLogger.Instance);

        var pass = await processor.ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Pending);
        Assert.True(File.Exists(path));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"Message":"x","GroupId":0,"AlarmId":"A"}""")]
    [InlineData("""{"Message":"x","GroupId":3}""")]
    public async Task MalformedFile_MovesToBadWithoutSending(string json)
    {
        Spool("B1", json, TimeSpan.FromMinutes(5));

        var pass = await Processor(1, 1).ProcessOnceAsync(_now);

        Assert.Equal(1, pass.Bad);
        Assert.Empty(_sent);
        Assert.True(File.Exists(Path.Combine(_root, "spool", "bad", "B1.json")));
    }

    [Fact]
    public async Task TempFilesFromBatch_AreIgnored()
    {
        var tmp = Path.Combine(_root, "spool", "A7.tmp");
        File.WriteAllText(tmp, "{");
        File.SetLastWriteTimeUtc(tmp, _now.AddMinutes(-10));

        var pass = await Processor(1, 1).ProcessOnceAsync(_now);

        Assert.Equal(new SpoolPassResult(0, 0, 0, 0), pass);
        Assert.True(File.Exists(tmp));
    }
}
