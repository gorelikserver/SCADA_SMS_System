using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services.Providers;

namespace SCADASMSSystem.Web.Pages.Settings
{
    /// <summary>
    /// Provider profile editor (admin-only via the /Settings folder convention).
    /// Edit a draft, preview it, verify it with a real test send, then activate it.
    /// </summary>
    public class ProviderModel : PageModel
    {
        private const long MaxImportBytes = 256 * 1024;

        private readonly IProviderProfileService _profiles;
        private readonly ILogger<ProviderModel> _logger;

        public ProviderModel(IProviderProfileService profiles, ILogger<ProviderModel> logger)
        {
            _profiles = profiles;
            _logger = logger;
        }

        public ProfileEditorState State { get; private set; } = new(null, null, null, null);
        public IReadOnlyList<string> DraftErrors { get; private set; } = [];
        public IReadOnlyList<string> ActiveErrors { get; private set; } = [];

        // What the editor starts with (secrets masked): the draft, else a copy of the active profile.
        public string EditorJson { get; private set; } = "{}";
        public bool EditingDraft { get; private set; }

        public async Task OnGetAsync()
        {
            State = await _profiles.GetStateAsync();
            EditingDraft = State.Draft != null;
            var editing = State.Draft ?? State.Active ?? ProfileTemplates.All[0].Create();
            EditorJson = JsonSerializer.Serialize(ProfileManagement.MaskSecrets(editing));
            DraftErrors = State.Draft == null ? [] : ProfileValidator.Validate(State.Draft);
            ActiveErrors = ProfileValidator.Validate(State.Active);
        }

        public async Task<IActionResult> OnPostSaveDraftAsync([FromBody] ProviderProfile profile)
        {
            var errors = await _profiles.SaveDraftAsync(profile);
            return new JsonResult(new { saved = true, errors });
        }

        public async Task<IActionResult> OnPostPreviewAsync([FromBody] ProviderProfile profile)
        {
            var errors = await _profiles.SaveDraftAsync(profile);
            if (errors.Count > 0)
                return new JsonResult(new { saved = true, errors });

            var draft = (await _profiles.GetStateAsync()).Draft!;
            using var built = ProviderRequestBuilder.Build(draft, ProfileValidator.Sample);
            var secrets = draft.Variables.Where(v => v.IsSecret && v.Value.Length > 0).Select(v => v.Value).ToList();
            var headers = built.Request.Headers
                .Select(h => new { name = h.Key, value = h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? (h.Value.FirstOrDefault()?.Split(' ')[0] ?? "") + " ***"
                    : ProviderRequestBuilder.Mask(string.Join(", ", h.Value), secrets) })
                .Concat(built.Request.Content?.Headers.Select(h => new { name = h.Key, value = string.Join(", ", h.Value) })
                        ?? Enumerable.Empty<object>().Select(_ => new { name = "", value = "" }))
                .ToList();
            return new JsonResult(new
            {
                saved = true,
                errors,
                method = built.Request.Method.Method,
                url = built.DisplayUrl,
                headers,
                body = built.DisplayBody,
                sample = ProfileValidator.Sample,
            });
        }

        public sealed class VerifyRequest
        {
            public ProviderProfile Profile { get; set; } = new();
            public string Phone { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostVerifyAsync([FromBody] VerifyRequest request)
        {
            await _profiles.SaveDraftAsync(request.Profile);
            var message = string.IsNullOrWhiteSpace(request.Message)
                ? $"SCADA SMS provider verification {DateTime.Now:yyyy-MM-dd HH:mm}"
                : request.Message.Trim();
            var outcome = await _profiles.VerifyDraftAsync(request.Phone ?? string.Empty, message, User.Identity?.Name, HttpContext.RequestAborted);
            return new JsonResult(outcome);
        }

        public async Task<IActionResult> OnPostActivateAsync()
        {
            var (ok, message) = await _profiles.ActivateDraftAsync(User.Identity?.Name);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDiscardAsync()
        {
            await _profiles.DiscardDraftAsync();
            TempData["SuccessMessage"] = "Draft discarded. The active profile is unchanged.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostStartFromTemplateAsync(string id)
        {
            var starter = ProfileTemplates.All.FirstOrDefault(t => t.Id == id);
            if (starter == null)
                return BadRequest();
            await _profiles.SaveDraftAsync(starter.Create());
            TempData["SuccessMessage"] = $"Draft started from '{starter.Title}'. Fill in the URL and secrets, then verify.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetExportAsync(string which)
        {
            var state = await _profiles.GetStateAsync();
            var profile = which == "draft" ? state.Draft : state.Active;
            if (profile == null)
                return NotFound();
            var name = string.Concat((profile.Name.Length > 0 ? profile.Name : "provider").Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
            return File(System.Text.Encoding.UTF8.GetBytes(ProfileManagement.Export(profile)), "application/json",
                $"sms-profile-{name}-{which}.json");
        }

        public async Task<IActionResult> OnPostImportAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0 || file.Length > MaxImportBytes)
            {
                TempData["ErrorMessage"] = "Choose a profile file (JSON, up to 256 KB).";
                return RedirectToPage();
            }
            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var profile = ProfileManagement.Import(await reader.ReadToEndAsync());
                var errors = await _profiles.SaveDraftAsync(profile);
                _logger.LogWarning("{User} imported provider profile '{Name}' as draft", User.Identity?.Name, profile.Name);
                TempData["SuccessMessage"] = errors.Count == 0
                    ? $"Imported '{profile.Name}' as the draft. Verify it before activating."
                    : $"Imported '{profile.Name}' as the draft. Fill in the missing values ({errors.Count} issue(s)), then verify.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToPage();
        }
    }
}
