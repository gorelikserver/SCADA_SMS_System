using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("scada_critical_sms_deliveries")]
    public class CriticalSmsDelivery
    {
        [Key]
        [Column("delivery_id")]
        public long DeliveryId { get; set; }

        [Column("alarm_id")]
        [StringLength(100)]
        public string AlarmId { get; set; } = string.Empty;

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("phone_number")]
        [StringLength(100)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("status")]
        [StringLength(32)]
        public string Status { get; set; } = CriticalDeliveryStatus.Sending;

        [Column("provider_status_code")]
        public int ProviderStatusCode { get; set; }

        [Column("attempts")]
        public int Attempts { get; set; }

        [Column("provider_message")]
        [StringLength(500)]
        public string? ProviderMessage { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public static class CriticalDeliveryStatus
    {
        public const string Sending = "SENDING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Indeterminate = "INDETERMINATE";
    }
}
