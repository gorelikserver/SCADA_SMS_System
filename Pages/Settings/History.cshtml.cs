using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using SCADASMSSystem.Web.Services.Providers;

namespace SCADASMSSystem.Web.Pages.Settings
{
    /// <summary>
    /// Settings change history and provider profile versions (admin-only via the /Settings folder convention).
    /// A version can be restored into the draft or exported; it never goes live without a new verification.
    /// </summary>
    public class HistoryModel : PageModel
    {
        public const int MaxChanges = 200;
        public const int MaxVersions = 100;

        private readonly ISettingsHistory _history;
        private readonly IProviderProfileService _profiles;
        private readonly ILogger<HistoryModel> _logger;

        public HistoryModel(ISettingsHistory history, IProviderProfileService profiles, ILogger<HistoryModel> logger)
        {
            _history = history;
            _profiles = profiles;
            _logger = logger;
        }

        public IReadOnlyList<SettingsChange> Changes { get; private set; } = [];
        public IReadOnlyList<ProviderProfileVersion> Versions { get; private set; } = [];

        // The newest version whose hash matches the active profile; null when none does.
        public int? ActiveVersionId { get; private set; }
        public bool HasDraft { get; private set; }

        public async Task OnGetAsync()
        {
            try
            {
                // A profile active before history existed (e.g. converted at startup) becomes a version here.
                await _profiles.EnsureActiveVersionRecordedAsync();

                var state = await _profiles.GetStateAsync();
                HasDraft = state.Draft != null;
                Changes = await _history.GetChangesAsync(MaxChanges);
                Versions = await _history.GetVersionsAsync(MaxVersions);
                if (state.Active != null)
                {
                    var hash = ProfileManagement.Hash(state.Active);
                    ActiveVersionId = Versions.FirstOrDefault(v => v.ProfileHash == hash)?.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings history");
                TempData["ErrorMessage"] = "Could not load the settings history. Check the database connection.";
            }
        }

        public async Task<IActionResult> OnPostRestoreAsync(int id)
        {
            var (ok, message) = await _profiles.RestoreVersionAsDraftAsync(id, User.Identity?.Name);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
            return ok ? RedirectToPage("/Settings/Provider") : RedirectToPage();
        }

        public async Task<IActionResult> OnGetExportAsync(int id)
        {
            var version = await _history.GetVersionAsync(id);
            var profile = version == null ? null : System.Text.Json.JsonSerializer.Deserialize<ProviderProfile>(version.ProfileJson);
            if (profile == null)
                return NotFound();
            var name = string.Concat((profile.Name.Length > 0 ? profile.Name : "provider").Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
            return File(System.Text.Encoding.UTF8.GetBytes(ProfileManagement.Export(profile)), "application/json",
                $"sms-profile-{name}-v{version!.Id}.json");
        }
    }
}
