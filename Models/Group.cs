using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("groups")]
    public class Group
    {
        [Key]
        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("group_name")]
        [Required]
        [StringLength(255)]
        [Display(Name = "Group Name")]
        public string GroupName { get; set; } = string.Empty;

        [Column("created_at")]
        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    }
}