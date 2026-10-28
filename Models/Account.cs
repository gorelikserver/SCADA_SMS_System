using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// Local login for the web UI. Sites are air-gapped, so there is no external identity provider.
    /// </summary>
    [Table("scada_accounts")]
    public class Account
    {
        [Key]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Column("user_name")]
        [StringLength(64)]
        public string UserName { get; set; } = string.Empty;

        [Column("password_hash")]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("role")]
        [StringLength(16)]
        public string Role { get; set; } = AccountRoles.Viewer;

        [Column("failed_logins")]
        public int FailedLogins { get; set; }

        [Column("locked_until")]
        public DateTime? LockedUntil { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public static class AccountRoles
    {
        // Admin: everything. Viewer: dashboard, alerts (including acknowledge), audit, calendar, help.
        public const string Admin = "Admin";
        public const string Viewer = "Viewer";

        public static readonly string[] All = { Admin, Viewer };
    }
}
