using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("phone_number")]
        [Required]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("user_name")]
        [Required]
        [StringLength(255)]
        [Display(Name = "User Name")]
        public string UserName { get; set; } = string.Empty;

        [Column("email")]
        [StringLength(255)]
        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Column("sms_enabled")]
        [Display(Name = "SMS Enabled")]
        public bool SmsEnabled { get; set; } = true;

        [Column("special_days_enabled")]
        [Display(Name = "Works on Holidays")]
        public bool SpecialDaysEnabled { get; set; } = false;

        [Column("created_at")]
        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual ICollection<SmsAudit> SmsAudits { get; set; } = new List<SmsAudit>();
    }
}