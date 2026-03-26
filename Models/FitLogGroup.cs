using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class FitLogGroup
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Group Name")]
        public string Name { get; set; } = string.Empty;

        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<GroupMember> Members { get; set; } = new();
    }

    public class GroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = "Member"; // Admin, Member
        public DateTime JoinedAt { get; set; } = DateTime.Now;
        public FitLogGroup? Group { get; set; }
    }
}