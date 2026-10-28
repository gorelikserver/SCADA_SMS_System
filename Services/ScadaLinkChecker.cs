using System.Security.Cryptography;

namespace SCADASMSSystem.Web.Services
{
    public enum ScadaLinkState
    {
        NotConfigured, // no PCIMUTIL folder given to the installer: copied by hand, nothing to compare
        NotInstalled,  // no installed Scripts\Scada_sms.bat (development or a manual deployment)
        Ok,
        Missing,       // Akara would call a batch file that does not exist: alarms cannot arrive
        Outdated,      // a different Scada_sms.bat than the one this version installed
        WrongUrl,      // the copy posts to a different address than this service listens on
    }

    public record ScadaLinkStatus(ScadaLinkState State, string? PcimUtilDir, string Detail)
    {
        public bool IsProblem => State is ScadaLinkState.Missing or ScadaLinkState.Outdated or ScadaLinkState.WrongUrl;
    }

    /// <summary>
    /// Akara runs getalias('PCIMUTIL') + 'Scada_sms.bat', not the installed copy. The installer copies
    /// the bat (and its ini) there and passes the folder as Scada:PcimUtilDir. This compares that copy
    /// with the installed one so a stale or missing copy is visible before an alarm is lost.
    /// </summary>
    public static class ScadaLinkChecker
    {
        public const string DefaultUrl = "http://localhost:5000/api/sms/send";

        public static ScadaLinkStatus Check(string? pcimUtilDir, string installedScriptsDir, string expectedUrl)
        {
            if (string.IsNullOrWhiteSpace(pcimUtilDir))
                return new(ScadaLinkState.NotConfigured, null, "No Akara PCIMUTIL folder was given to the installer; Scada_sms.bat is maintained by hand.");

            var installedBat = Path.Combine(installedScriptsDir, "Scada_sms.bat");
            if (!File.Exists(installedBat))
                return new(ScadaLinkState.NotInstalled, pcimUtilDir, $"No installed {installedBat} to compare with.");

            var copy = Path.Combine(pcimUtilDir, "Scada_sms.bat");
            if (!File.Exists(copy))
                return new(ScadaLinkState.Missing, pcimUtilDir, $"{copy} does not exist: Akara cannot start the SMS script, so alarms are not even spooled.");

            if (!Hash(copy).SequenceEqual(Hash(installedBat)))
                return new(ScadaLinkState.Outdated, pcimUtilDir, $"{copy} differs from the installed {installedBat}. Copy it again (or rerun the installer).");

            var url = ReadIniUrl(Path.Combine(pcimUtilDir, "Scada_sms.ini")) ?? DefaultUrl;
            if (!SameUrl(url, expectedUrl))
                return new(ScadaLinkState.WrongUrl, pcimUtilDir, $"The copy posts to {url}, but this service expects {expectedUrl}. Fix URL= in Scada_sms.ini there.");

            return new(ScadaLinkState.Ok, pcimUtilDir, $"{copy} matches this version and posts to {expectedUrl}.");
        }

        /// <summary>The address Scada_sms.bat must post to, from the service's first configured URL.</summary>
        public static string ExpectedUrl(string? urls)
        {
            var first = (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(first))
                return DefaultUrl;
            // Kestrel accepts wildcards ("http://*:5000", "http://+:5000"); the bat runs on this machine.
            first = first.Replace("://*", "://localhost").Replace("://+", "://localhost").Replace("://0.0.0.0", "://localhost");
            return first.TrimEnd('/') + "/api/sms/send";
        }

        private static string? ReadIniUrl(string iniPath)
        {
            if (!File.Exists(iniPath))
                return null;
            foreach (var line in File.ReadLines(iniPath))
            {
                var t = line.Trim();
                if (t.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    return t[4..].Trim();
            }
            return null;
        }

        private static bool SameUrl(string a, string b) =>
            Uri.TryCreate(a, UriKind.Absolute, out var ua) && Uri.TryCreate(b, UriKind.Absolute, out var ub)
                ? Uri.Compare(ua, ub, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0
                  || (IsLocal(ua) && IsLocal(ub) && ua.Port == ub.Port && ua.AbsolutePath.TrimEnd('/').Equals(ub.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                : string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        private static bool IsLocal(Uri u) => u.IsLoopback || u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        private static byte[] Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return SHA256.HashData(stream);
        }
    }
}
