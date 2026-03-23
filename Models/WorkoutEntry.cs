using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class WorkoutEntry
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Exercise Name")]
        public string ExerciseName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Muscle Group")]
        public string MuscleGroup { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime WorkoutDate { get; set; }

        [Required]
        [Range(1, 20)]
        public int Sets { get; set; }

        [Required]
        [Range(1, 100)]
        public int Reps { get; set; }

        [Required]
        [Range(0, 2000)]
        [Display(Name = "Weight (lbs)")]
        public decimal WeightLbs { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Completed")]
        public bool IsCompleted { get; set; }
    }
}