using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Single write path for the SmsSettings section of appsettings.json.
    /// Changes are merged into the stored section (never replace it), secrets survive
    /// blank/masked posts, the previous file is backed up, and the write is atomic.
    /// </summary>
    public interface ISettingsStore
    {
        Task UpdateSmsSettingsAsync(Action<JsonObject> mutate, CancellationToken ct = default);
    }

    public class SettingsStore : ISettingsStore
    {
        public const string SectionName = "SmsSettings";
        public const int BackupsToKeep = 10;

        // One lock per process: Settings page and SOAP workbench must not interleave writes.
        private static readonly SemaphoreSlim WriteLock = new(1, 1);

        private readonly string _appSettingsPath;
        private readonly IConfiguration? _configuration;
        private readonly ILogger<SettingsStore> _logger;

        public SettingsStore(IWebHostEnvironment env, IConfiguration configuration, ILogger<SettingsStore> logger)
            : this(Path.Combine(env.ContentRootPath, "appsettings.json"), configuration, logger) { }

        public SettingsStore(string appSettingsPath, IConfiguration? configuration, ILogger<SettingsStore> logger)
        {
            _appSettingsPath = appSettingsPath;
            _configuration = configuration;
            _logger = logger;
        }

        public string BackupDirectory =>
            Path.Combine(Path.GetDirectoryName(_appSettingsPath)!, "settings-backups");

        public async Task UpdateSmsSettingsAsync(Action<JsonObject> mutate, CancellationToken ct = default)
        {
            await WriteLock.WaitAsync(ct);
            try
            {
                var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
                var root = JsonNode.Parse(json)?.AsObject()
                    ?? throw new InvalidOperationException("appsettings.json is empty or not a JSON object");

                if (root[SectionName] is not JsonObject section)
                {
                    section = new JsonObject();
                    root[SectionName] = section;
                }

                mutate(section);

                BackupCurrentFile();

                var tempPath = _appSettingsPath + ".tmp";
                await File.WriteAllTextAsync(tempPath,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
                File.Move(tempPath, _appSettingsPath, overwrite: true);

                // Reload immediately so IOptionsMonitor reflects the new values before the
                // redirect GET arrives (avoids file-watcher race).
                if (_configuration is IConfigurationRoot configRoot)
                    configRoot.Reload();
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private void BackupCurrentFile()
        {
            try
            {
                Directory.CreateDirectory(BackupDirectory);
                var backupPath = Path.Combine(BackupDirectory,
                    $"appsettings.{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
                File.Copy(_appSettingsPath, backupPath, overwrite: true);

                foreach (var old in Directory.GetFiles(BackupDirectory, "appsettings.*.json")
                             .OrderByDescending(f => f, StringComparer.Ordinal)
                             .Skip(BackupsToKeep))
                {
                    File.Delete(old);
                }
            }
            catch (Exception ex)
            {
                // A failed backup must not block saving provider settings.
                _logger.LogWarning(ex, "Could not back up appsettings.json before saving settings");
            }
        }
    }

    /// <summary>
    /// Pure merge rules for posted SmsSettings values. Kept separate from file I/O so they can be unit tested.
    /// </summary>
    public static class SmsSettingsMerge
    {
        public const string MaskedValue = "***";

        // Scalar fields that hold credentials: a blank or masked post keeps the stored value.
        public static readonly IReadOnlySet<string> SecretFields =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Password", "RestBearerToken", "RestApiKeyValue" };

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        public static bool IsBlankOrMasked(string? value) =>
            string.IsNullOrEmpty(value) || value == MaskedValue;

        /// <summary>
        /// Copies only the scalar fields that were actually posted from <paramref name="posted"/> into
        /// <paramref name="section"/>. Fields not posted keep their stored value; unknown keys are untouched.
        /// </summary>
        public static void ApplyPostedFields(JsonObject section, SmsSettings posted, IEnumerable<string> postedFieldNames)
        {
            foreach (var name in postedFieldNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var prop = typeof(SmsSettings).GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !IsScalar(prop.PropertyType))
                    continue;

                var value = prop.GetValue(posted);
                if (SecretFields.Contains(prop.Name) && IsBlankOrMasked(value as string))
                    continue;

                section[prop.Name] = JsonSerializer.SerializeToNode(value, prop.PropertyType);
            }
        }

        /// <summary>
        /// Replaces a dynamic param list with the posted one. A list that was not posted is kept.
        /// A secret row posted blank or masked keeps the stored value for the same key.
        /// Throws <see cref="JsonException"/> when the posted list is not valid JSON, so a broken
        /// client never wipes the stored list.
        /// </summary>
        public static void ApplyParamList(JsonObject section, string listName, string? postedJson)
        {
            if (postedJson == null)
                return;

            var postedList = JsonSerializer.Deserialize<List<SmsParam>>(postedJson, ReadOptions)
                ?? throw new JsonException($"{listName} must be a JSON array");

            var stored = ReadParamList(section, listName);
            foreach (var row in postedList.Where(r => r.IsSecret && IsBlankOrMasked(r.Value)))
            {
                var match = stored.FirstOrDefault(s => s.Key.Equals(row.Key, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    row.Value = match.Value;
            }

            section[listName] = JsonSerializer.SerializeToNode(postedList);
        }

        private static List<SmsParam> ReadParamList(JsonObject section, string listName)
        {
            try
            {
                return section[listName]?.Deserialize<List<SmsParam>>(ReadOptions) ?? new();
            }
            catch (JsonException)
            {
                return new();
            }
        }

        private static bool IsScalar(Type t) => t == typeof(string) || t == typeof(int) || t == typeof(bool);
    }
}
