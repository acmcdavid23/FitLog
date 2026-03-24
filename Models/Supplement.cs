using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class Supplement
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Supplement Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Dosage")]
        public string Dosage { get; set; } = string.Empty;

        [Display(Name = "Time to Take")]
        public string TimeToTake { get; set; } = string.Empty; // Morning, Pre-workout, Post-workout, Night

        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}