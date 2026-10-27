using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Settings change history and provider profile versions (rollback source).
    /// Writes never throw: a history failure is logged and must not block or fail the settings change itself.
    /// </summary>
    public interface ISettingsHistory
    {
        Task RecordChangeAsync(string area, string action, string summary, string? user);
        Task RecordVersionAsync(ProviderProfileVersion version);

        /// <summary>True when any version with this hash exists (also true when the database cannot be read, so nothing is duplicated).</summary>
        Task<bool> HasVersionAsync(string profileHash);

        Task<IReadOnlyList<SettingsChange>> GetChangesAsync(int take);
        Task<IReadOnlyList<ProviderProfileVersion>> GetVersionsAsync(int take);
        Task<ProviderProfileVersion?> GetVersionAsync(int id);
    }

    public class SettingsHistory : ISettingsHistory
    {
        private readonly SCADADbContext _db;
        private readonly ILogger<SettingsHistory> _logger;

        public SettingsHistory(SCADADbContext db, ILogger<SettingsHistory> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task RecordChangeAsync(string area, string action, string summary, string? user) =>
            SaveAsync(new SettingsChange
            {
                ChangedAt = DateTime.Now,
                ChangedBy = Truncate(user, 100),
                Area = area,
                Action = action,
                Summary = Truncate(summary, SettingsChange.MaxSummaryLength)!,
            });

        public Task RecordVersionAsync(ProviderProfileVersion version)
        {
            version.ProfileName = Truncate(version.ProfileName, 200)!;
            version.ActivatedBy = Truncate(version.ActivatedBy, 100);
            version.VerifiedBy = Truncate(version.VerifiedBy, 100);
            return SaveAsync(version);
        }

        public async Task<bool> HasVersionAsync(string profileHash)
        {
            try
            {
                return await _db.ProviderProfileVersions.AnyAsync(v => v.ProfileHash == profileHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read provider profile versions");
                return true;
            }
        }

        public async Task<IReadOnlyList<SettingsChange>> GetChangesAsync(int take) =>
            await _db.SettingsChanges.AsNoTracking()
                .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
                .Take(take).ToListAsync();

        public async Task<IReadOnlyList<ProviderProfileVersion>> GetVersionsAsync(int take) =>
            await _db.ProviderProfileVersions.AsNoTracking()
                .OrderByDescending(v => v.Id) // insertion order: a captured older profile gets its row before the one replacing it
                .Take(take).ToListAsync();

        public Task<ProviderProfileVersion?> GetVersionAsync(int id) =>
            _db.ProviderProfileVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);

        private async Task SaveAsync(object entity)
        {
            try
            {
                _db.Add(entity);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // The settings change already happened; losing its history row must not undo or fail it.
                _db.Entry(entity).State = EntityState.Detached;
                _logger.LogError(ex, "Could not write settings history ({Type})", entity.GetType().Name);
            }
        }

        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    /// <summary>
    /// Pure, testable summaries for the history. Secret variable values never appear: only that they changed.
    /// </summary>
    public static class SettingsChangeSummary
    {
        private const int MaxValueLength = 60;

        /// <summary>"TimeoutSeconds 30 → 20, TestMode false → true", or null when no listed field changed.</summary>
        public static string? DiffGeneral(SmsSettings before, SmsSettings after, IEnumerable<string> fields)
        {
            var parts = new List<string>();
            foreach (var name in fields)
            {
                var prop = typeof(SmsSettings).GetProperty(name);
                if (prop == null)
                    continue;
                var oldValue = Format(prop.GetValue(before));
                var newValue = Format(prop.GetValue(after));
                if (oldValue != newValue)
                    parts.Add($"{prop.Name} {oldValue} → {newValue}");
            }
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>
        /// What differs between two provider profiles, e.g. "Method POST → GET, URL changed, secret Password changed".
        /// URL, body, headers and auth templates are reported without values (they may embed credentials).
        /// Returns null when nothing differs.
        /// </summary>
        public static string? DiffProfile(ProviderProfile? before, ProviderProfile? after)
        {
            if (before == null || after == null)
                return null;

            var parts = new List<string>();
            void Value(string label, string? a, string? b)
            {
                if ((a ?? "") != (b ?? ""))
                    parts.Add($"{label} {Show(a)} → {Show(b)}");
            }
            void Changed(string label, bool changed)
            {
                if (changed)
                    parts.Add(label + " changed");
            }

            Value("Name", before.Name, after.Name);
            Value("Kind", before.Kind, after.Kind);
            Value("Method", before.Method, after.Method);
            Changed("URL", before.Url != after.Url);
            Value("ContentType", before.ContentType, after.ContentType);
            Changed("Body", before.Body != after.Body);
            Changed("Headers", Json(before.Headers) != Json(after.Headers));
            Value("Auth", before.Auth.Type, after.Auth.Type);
            Changed("Auth user/secret template", before.Auth.User != after.Auth.User || before.Auth.Secret != after.Auth.Secret);
            Value("PhoneFormat", before.PhoneFormat, after.PhoneFormat);
            Value("CountryCode", before.CountryCode, after.CountryCode);
            Changed("Success rule", Json(before.Success) != Json(after.Success));
            Changed("Failure rule", Json(before.Failure) != Json(after.Failure));

            var keys = before.Variables.Select(v => v.Key).Concat(after.Variables.Select(v => v.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                var a = before.Variables.FirstOrDefault(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                var b = after.Variables.FirstOrDefault(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                var secret = a?.IsSecret == true || b?.IsSecret == true;
                var label = (secret ? "secret " : "variable ") + key;
                if (a == null)
                    parts.Add(label + " added");
                else if (b == null)
                    parts.Add(label + " removed");
                else if (a.Value != b.Value || a.IsSecret != b.IsSecret)
                    parts.Add(secret ? label + " changed" : $"{label} {Show(a.Value)} → {Show(b.Value)}");
            }

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>"'Gateway' (SOAP)".</summary>
        public static string Describe(ProviderProfile profile) =>
            $"'{(profile.Name.Length > 0 ? profile.Name : "(unnamed)")}' ({profile.Kind})";

        private static string Format(object? value) => value switch
        {
            null => "(empty)",
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Show(value.ToString()),
        };

        private static string Show(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return "(empty)";
            return s.Length <= MaxValueLength ? s : s[..MaxValueLength] + "…";
        }

        private static string Json(object o) => JsonSerializer.Serialize(o);
    }
}
