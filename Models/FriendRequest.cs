using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class FriendRequest
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Declined
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}