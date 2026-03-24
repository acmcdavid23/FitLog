using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class Exercise
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Exercise Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Muscle Group")]
        public string MuscleGroup { get; set; } = string.Empty;

        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty; // Strength, Hypertrophy, Conditioning

        [Display(Name = "Equipment")]
        public string Equipment { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Tips")]
        public string Tips { get; set; } = string.Empty;

        [Display(Name = "Recommended Sets")]
        public int RecommendedSets { get; set; }

        [Display(Name = "Recommended Reps")]
        public string RecommendedReps { get; set; } = string.Empty; // e.g. "8-12" or "3-5"

        public bool IsSystemExercise { get; set; } = true; // Admin-created vs user-created
    }
}