using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Where the settings edited in the UI (SmsSettings: provider profile, draft, limits, test mode)
    /// are stored. Not in the install folder: an MSI upgrade deletes INSTALLDIR, and with it the
    /// provider profile, so alarms would stop after every upgrade.
    /// </summary>
    public static class SettingsLocation
    {
        public const string ConfigKey = "SettingsFile";

        /// <summary>
        /// The configured "SettingsFile" (absolute, or relative to the content root), else
        /// %ProgramData%\SCADA_SMS\config\settings.json.
        /// </summary>
        public static string Resolve(string? configured, string contentRoot)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.IsPathRooted(configured) ? configured : Path.GetFullPath(Path.Combine(contentRoot, configured));

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SCADA_SMS", "config", "settings.json");
        }

        /// <summary>
        /// First start with this version: creates the settings file from the SmsSettings section of
        /// the install folder's appsettings.json, so an existing site keeps its provider and limits.
        /// Never overwrites an existing file. Returns true when it created one.
        /// </summary>
        public static bool EnsureSeeded(string settingsPath, string installAppSettingsPath)
        {
            if (File.Exists(settingsPath))
                return false;

            JsonNode? section = null;
            if (File.Exists(installAppSettingsPath))
            {
                var root = JsonNode.Parse(File.ReadAllText(installAppSettingsPath),
                    documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                section = root?["SmsSettings"]?.DeepClone();
            }

            var seeded = new JsonObject { ["SmsSettings"] = section ?? new JsonObject() };
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var temp = settingsPath + ".tmp";
            File.WriteAllText(temp, seeded.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, settingsPath, overwrite: false);
            return true;
        }

        /// <summary>
        /// The settings file holds provider secrets. When the service (LocalSystem) creates its
        /// folder itself, nothing else set an ACL, and the folder would inherit "Users: Modify"
        /// from %ProgramData%\SCADA_SMS. Restrict it to SYSTEM and Administrators, as the MSI and
        /// install_service.bat do. Skipped for normal user accounts (development runs) so the
        /// developer is not locked out.
        /// </summary>
        public static void RestrictFolderIfService(string settingsPath)
        {
            if (!OperatingSystem.IsWindows())
                return;
            using var identity = WindowsIdentity.GetCurrent();
            if (!identity.IsSystem)
                return;

            var dir = new DirectoryInfo(Path.GetDirectoryName(settingsPath)!);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
            }
            dir.SetAccessControl(security);
        }
    }
}
