using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Suite-wide settings, all from environment variables so the entry-point script controls them.
///   SCADA_MOCK_IT=1               enables the suite (otherwise every test is skipped)
///   SCADA_MOCK_IT_RESULTS=path    where evidence JSON is written (default: repo test-results\mock-integration\adhoc)
///   SCADA_MOCK_IT_SQL=connstr     SQL Server connection; default: appsettings.json DefaultConnection. The database is
///                                 always a per-run scada_mock_it_&lt;run&gt;, created with migrations and dropped at the end
///   MOCK_IT_ALLOW_REMOTE_SQL=1    allow a SQL Server that is not on this machine (refused by default)
///   MOCK_IT_KEEP=1                keep site folders and the test database after the run (always kept for a failed test)
///   SCADA_MOCK_IT_APP=path        SCADASMSSystem.Web.exe to start (default: repo bin\&lt;Configuration&gt;\net9.0)
/// </summary>
public static class MockIt
{
    public const string DatabasePrefix = "scada_mock_it_";

    /// <summary>Unique per test process, so parallel or aborted runs never share or clobber a database.</summary>
    public static string DatabaseName { get; } = DatabasePrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Environment.ProcessId;

    public static bool KeepArtifacts => Environment.GetEnvironmentVariable("MOCK_IT_KEEP") == "1";

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
            EnsureLocalServer(cs);
            // Always target the dedicated test database, never the site/dev database.
            cs = Regex.Replace(cs, @"(Database|Initial Catalog)\s*=\s*[^;]*", "Database=" + DatabaseName, RegexOptions.IgnoreCase);
            if (!cs.Contains("Database=" + DatabaseName, StringComparison.OrdinalIgnoreCase))
                cs = cs.TrimEnd(';') + ";Database=" + DatabaseName;
            return cs;
        }
    }

    /// <summary>The suite drops and creates databases: refuse anything but this machine unless explicitly allowed.</summary>
    private static void EnsureLocalServer(string cs)
    {
        if (Environment.GetEnvironmentVariable("MOCK_IT_ALLOW_REMOTE_SQL") == "1") return;
        var source = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).DataSource.Trim();
        source = Regex.Replace(source, "^(tcp|np|lpc):", "", RegexOptions.IgnoreCase);
        var host = source.Split('\\', ',')[0].Trim();
        var local = host is "." or "(local)" or "localhost" or "127.0.0.1" or "::1" ||
                    host.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        if (!local)
            throw new InvalidOperationException($"Refusing to use SQL Server '{source}': the mock suite only runs against a local server. Set MOCK_IT_ALLOW_REMOTE_SQL=1 to override.");
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
        // Failure-safe cleanup at process exit: site folders of failed tests are kept (they are not
        // deleted by Site.DisposeAsync), so only an empty work root is removed unless MOCK_IT_KEEP=1.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (!KeepArtifacts && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                    Directory.Delete(root);
            }
            catch { /* best effort */ }
        };
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
