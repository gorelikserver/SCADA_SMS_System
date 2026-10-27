using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// An operator confirmed they saw a critical alarm that was not delivered to every recipient.
    /// Acknowledged alarms no longer raise the site-wide banner.
    /// </summary>
    [Table("scada_alarm_acknowledgements")]
    public class AlarmAcknowledgement
    {
        [Key]
        [Column("alarm_id")]
        [StringLength(100)]
        public string AlarmId { get; set; } = string.Empty;

        [Column("acknowledged_at")]
        public DateTime AcknowledgedAt { get; set; } = DateTime.Now;

        [Column("acknowledged_by")]
        [StringLength(100)]
        public string? AcknowledgedBy { get; set; }

        [Column("note")]
        [StringLength(500)]
        public string? Note { get; set; }
    }
}
