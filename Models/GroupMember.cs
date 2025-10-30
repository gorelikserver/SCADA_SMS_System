using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCADASMSSystem.Web.Models
{
    [Table("group_members")]
    public class GroupMember
    {
        [Key]
        [Column("group_member_id")]
        public int GroupMemberId { get; set; }

        [Column("group_id")]
        [ForeignKey(nameof(Group))]
        public int GroupId { get; set; }

        [Column("user_id")]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Column("created_at")]
        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual Group Group { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}