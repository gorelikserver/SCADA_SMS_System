using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Suite-wide settings, all from environment variables so the entry-point script controls them.
///   SCADA_MOCK_IT=1               enables the suite (otherwise every test is skipped)
///   SCADA_MOCK_IT_RESULTS=path    where evidence JSON is written (default: repo test-results\mock-integration\adhoc)
///   SCADA_MOCK_IT_SQL=connstr     SQL Server connection; default: appsettings.json DefaultConnection with Database=scada_mock_it
///   SCADA_MOCK_IT_APP=path        SCADASMSSystem.Web.exe to start (default: repo bin\&lt;Configuration&gt;\net9.0)
/// </summary>
public static class MockIt
{
    public const string DatabaseName = "scada_mock_it";

    public static bool Enabled => Environment.GetEnvironmentVariable("SCADA_MOCK_IT") == "1" && OperatingSystem.IsWindows();

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ResultsDir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("SCADA_MOCK_IT_RESULTS");
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(RepoRoot, "test-results", "mock-integration", "adhoc");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Per-process scratch root for site folders (spool, settings, bat copy, logs).</summary>
    public static string WorkRoot { get; } = CreateWorkRoot();

    public static string BatSource => Path.Combine(RepoRoot, "Scada_sms.bat");

    public static string AppExe
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("SCADA_MOCK_IT_APP");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            // tests\SCADASMSSystem.IntegrationTests\bin\<Configuration>\net9.0\ -> <Configuration>
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            return Path.Combine(RepoRoot, "bin", configuration, "net9.0", "SCADASMSSystem.Web.exe");
        }
    }

    public static string SqlConnectionString
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("SCADA_MOCK_IT_SQL");
            var cs = !string.IsNullOrWhiteSpace(configured) ? configured : ReadAppSettingsConnection();
            // Always target the dedicated test database, never the site/dev database.
            cs = Regex.Replace(cs, @"(Database|Initial Catalog)\s*=\s*[^;]*", "Database=" + DatabaseName, RegexOptions.IgnoreCase);
            if (!cs.Contains("Database=" + DatabaseName, StringComparison.OrdinalIgnoreCase))
                cs = cs.TrimEnd(';') + ";Database=" + DatabaseName;
            return cs;
        }
    }

    private static string ReadAppSettingsConnection()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "appsettings.json")));
        return doc.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Scada_sms.bat")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (with Scada_sms.bat) not found above " + AppContext.BaseDirectory);
    }

    private static string CreateWorkRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "scada_mock_it", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Environment.ProcessId);
        Directory.CreateDirectory(root);
        return root;
    }
}

/// <summary>Fact that runs only when SCADA_MOCK_IT=1 on Windows.</summary>
public sealed class MockItFactAttribute : FactAttribute
{
    public MockItFactAttribute()
    {
        if (!MockIt.Enabled)
            Skip = "Mock integration suite disabled: set SCADA_MOCK_IT=1 (Windows only). See tests/integration/README.md.";
    }
}
