using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class ScadaLinkCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scada-link-" + Guid.NewGuid().ToString("N"));
    private readonly string _scripts;
    private readonly string _pcim;

    public ScadaLinkCheckerTests()
    {
        _scripts = Path.Combine(_root, "install", "Scripts");
        _pcim = Path.Combine(_root, "akara", "pcimutil");
        Directory.CreateDirectory(_scripts);
        Directory.CreateDirectory(_pcim);
        File.WriteAllText(Path.Combine(_scripts, "Scada_sms.bat"), "@echo off\r\nrem v2\r\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ScadaLinkStatus Check(string expectedUrl = "http://localhost:5000/api/sms/send") =>
        ScadaLinkChecker.Check(_pcim, _scripts, expectedUrl);

    private void CopyBat() => File.Copy(Path.Combine(_scripts, "Scada_sms.bat"), Path.Combine(_pcim, "Scada_sms.bat"), true);

    [Fact]
    public void NotConfigured_WhenInstallerGotNoFolder() =>
        Assert.Equal(ScadaLinkState.NotConfigured, ScadaLinkChecker.Check("", _scripts, ScadaLinkChecker.DefaultUrl).State);

    [Fact]
    public void Missing_WhenAkaraFolderHasNoBat()
    {
        var status = Check();
        Assert.Equal(ScadaLinkState.Missing, status.State);
        Assert.True(status.IsProblem);
    }

    [Fact]
    public void Outdated_WhenCopyDiffers()
    {
        File.WriteAllText(Path.Combine(_pcim, "Scada_sms.bat"), "@echo off\r\nrem v1\r\n");
        Assert.Equal(ScadaLinkState.Outdated, Check().State);
    }

    [Fact]
    public void Ok_WhenSameBatAndNoIni_DefaultsTo5000()
    {
        CopyBat();
        Assert.Equal(ScadaLinkState.Ok, Check().State);
    }

    [Fact]
    public void WrongUrl_WhenIniPostsElsewhere()
    {
        CopyBat();
        File.WriteAllText(Path.Combine(_pcim, "Scada_sms.ini"), "; installer\r\nURL=http://localhost:5000/api/sms/send\r\nCURL=C:\\x\\curl.exe\r\n");
        Assert.Equal(ScadaLinkState.WrongUrl, Check("http://localhost:8085/api/sms/send").State);
        Assert.Equal(ScadaLinkState.Ok, Check("http://127.0.0.1:5000/api/sms/send/").State);
    }

    [Fact]
    public void NotInstalled_WhenNoInstalledBatToCompare() =>
        Assert.Equal(ScadaLinkState.NotInstalled, ScadaLinkChecker.Check(_pcim, Path.Combine(_root, "nowhere"), ScadaLinkChecker.DefaultUrl).State);

    [Theory]
    [InlineData(null, "http://localhost:5000/api/sms/send")]
    [InlineData("http://localhost:8085", "http://localhost:8085/api/sms/send")]
    [InlineData("http://*:9000/;https://localhost:9001", "http://localhost:9000/api/sms/send")]
    [InlineData("http://0.0.0.0:5000", "http://localhost:5000/api/sms/send")]
    public void ExpectedUrl_FromKestrelUrls(string? urls, string expected) =>
        Assert.Equal(expected, ScadaLinkChecker.ExpectedUrl(urls));
}
