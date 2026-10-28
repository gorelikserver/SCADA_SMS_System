using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// One meaningful change to the SMS settings, for the Settings > History page.
    /// The summary is human readable and never contains secret values.
    /// </summary>
    [Table("scada_settings_changes")]
    public class SettingsChange
    {
        public const int MaxSummaryLength = 2000;

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.Now;

        [Column("changed_by")]
        [StringLength(100)]
        public string? ChangedBy { get; set; }

        // General | Provider
        [Column("area")]
        [Required]
        [StringLength(30)]
        public string Area { get; set; } = string.Empty;

        // GeneralSaved, DraftImported, DraftStartedFromTemplate, DraftVerified, DraftDiscarded,
        // ProfileActivated, ProfileRestoredAsDraft
        [Column("action")]
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        [Column("summary")]
        [Required]
        [StringLength(MaxSummaryLength)]
        public string Summary { get; set; } = string.Empty;
    }

    public static class SettingsAreas
    {
        public const string General = "General";
        public const string Provider = "Provider";
    }

    public static class SettingsActions
    {
        public const string GeneralSaved = "GeneralSaved";
        public const string DraftImported = "DraftImported";
        public const string DraftStartedFromTemplate = "DraftStartedFromTemplate";
        public const string DraftVerified = "DraftVerified";
        public const string DraftDiscarded = "DraftDiscarded";
        public const string ProfileActivated = "ProfileActivated";
        public const string ProfileRestoredAsDraft = "ProfileRestoredAsDraft";
    }
}
