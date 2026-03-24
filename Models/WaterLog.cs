using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class WaterLog
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime LogDate { get; set; }

        [Required]
        [Display(Name = "Amount (oz)")]
        public decimal AmountOz { get; set; }

        [Display(Name = "Daily Goal (oz)")]
        public decimal DailyGoalOz { get; set; } = 128; // Default 1 gallon
    }
}