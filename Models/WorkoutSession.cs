using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class WorkoutSession
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Workout Name")]
        public string SessionName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime SessionDate { get; set; }

        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        public List<WorkoutEntry> Entries { get; set; } = new();
    }
}