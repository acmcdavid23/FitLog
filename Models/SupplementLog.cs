using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class SupplementLog
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int SupplementId { get; set; }

        public Supplement? Supplement { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime LogDate { get; set; }

        public bool IsTaken { get; set; }

        public DateTime? TimeTaken { get; set; }
    }
}