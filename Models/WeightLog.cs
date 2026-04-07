using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class WeightLog
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime LogDate { get; set; }

        [Required]
        public decimal WeightLbs { get; set; }

        public string Notes { get; set; } = string.Empty;
    }
}