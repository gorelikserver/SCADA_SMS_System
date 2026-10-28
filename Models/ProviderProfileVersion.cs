using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// A provider profile that was active at some point: the rollback source for Settings > History.
    /// ProfileJson is the full profile, secrets included, exactly as it was sent with; it is never
    /// shown in the UI (export empties secrets, restore puts it into the draft server side).
    /// </summary>
    [Table("scada_provider_profile_versions")]
    public class ProviderProfileVersion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("profile_name")]
        [StringLength(200)]
        public string ProfileName { get; set; } = string.Empty;

        [Column("profile_kind")]
        [StringLength(20)]
        public string ProfileKind { get; set; } = string.Empty;

        // SHA-256 (hex) of ProfileJson, same as ProfileManagement.Hash; matches the active profile when live.
        [Column("profile_hash")]
        [Required]
        [StringLength(64)]
        public string ProfileHash { get; set; } = string.Empty;

        [Column("profile_json")]
        [Required]
        public string ProfileJson { get; set; } = string.Empty;

        [Column("activated_at")]
        public DateTime ActivatedAt { get; set; } = DateTime.Now;

        [Column("activated_by")]
        [StringLength(100)]
        public string? ActivatedBy { get; set; }

        [Column("verified_at")]
        public DateTime? VerifiedAt { get; set; }

        [Column("verified_by")]
        [StringLength(100)]
        public string? VerifiedBy { get; set; }

        // Set when the row was captured after the fact (e.g. a profile converted at startup).
        [Column("note")]
        [StringLength(500)]
        public string? Note { get; set; }
    }
}
