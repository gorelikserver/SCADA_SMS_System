using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("date_dimension")]
    public class DateDimension
    {
        [Key]
        [Column("date_id")]
        public int DateId { get; set; }

        [Column("full_date")]
        [Required]
        [Display(Name = "Date")]
        public DateTime FullDate { get; set; }

        [Column("day_of_week")]
        [Display(Name = "Day of Week")]
        public byte DayOfWeek { get; set; }

        [Column("day_name")]
        [StringLength(10)]
        [Display(Name = "Day Name")]
        public string DayName { get; set; } = string.Empty;

        [Column("day_of_month")]
        [Display(Name = "Day of Month")]
        public byte DayOfMonth { get; set; }

        [Column("day_of_year")]
        [Display(Name = "Day of Year")]
        public short DayOfYear { get; set; }

        [Column("week_of_year")]
        [Display(Name = "Week of Year")]
        public byte WeekOfYear { get; set; }

        [Column("month")]
        [Display(Name = "Month")]
        public byte Month { get; set; }

        [Column("month_name")]
        [StringLength(10)]
        [Display(Name = "Month Name")]
        public string MonthName { get; set; } = string.Empty;

        [Column("quarter")]
        [Display(Name = "Quarter")]
        public byte Quarter { get; set; }

        [Column("year")]
        [Display(Name = "Year")]
        public short Year { get; set; }

        [Column("is_weekend")]
        [Display(Name = "Is Weekend")]
        public bool IsWeekend { get; set; }

        [Column("hebrew_date")]
        [StringLength(50)]
        [Display(Name = "Hebrew Date")]
        public string? HebrewDate { get; set; }

        [Column("jewish_holiday")]
        [StringLength(100)]
        [Display(Name = "Jewish Holiday")]
        public string? JewishHoliday { get; set; }

        [Column("is_jewish_holiday")]
        [Display(Name = "Is Jewish Holiday")]
        public bool IsJewishHoliday { get; set; }

        [Column("is_sabbatical_holiday")]
        [Display(Name = "Is Sabbatical Holiday")]
        public bool IsSabbaticalHoliday { get; set; }
    }
}