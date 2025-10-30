using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("sms_audit")]
    public class SmsAudit
    {
        [Key]
        [Column("audit_id")]
        public int AuditId { get; set; }

        [Column("alarm_id")]
        [Required]
        [StringLength(100)]
        [Display(Name = "Alarm ID")]
        public string AlarmId { get; set; } = string.Empty;

        [Column("user_id")]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Column("group_id")]
        [ForeignKey(nameof(Group))]
        [Display(Name = "Group")]
        public int? GroupId { get; set; }

        [Column("phone_number")]
        [StringLength(100)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Column("alarm_description")]
        [Required]
        [Display(Name = "Alarm Description")]
        public string AlarmDescription { get; set; } = string.Empty;

        [Column("status")]
        [Required]
        [StringLength(200)]
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Column("message_status")]
        [StringLength(200)]
        [Display(Name = "Message Status")]
        public string? MessageStatus { get; set; }

        [Column("api_response")]
        [Display(Name = "API Response")]
        public string? ApiResponse { get; set; }

        [Column("created_at")]
        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Group? Group { get; set; }
    }
}