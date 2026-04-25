using System.ComponentModel.DataAnnotations;

namespace FitLog.ViewModels
{
    /// <summary>Admin exercise create/edit form (system exercises).</summary>
    public class ExerciseFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Exercise Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Muscle Group")]
        public string MuscleGroup { get; set; } = string.Empty;

        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;

        [Display(Name = "Equipment")]
        public string Equipment { get; set; } = string.Empty;

        [Display(Name = "Recommended Sets")]
        public int RecommendedSets { get; set; }

        [Display(Name = "Recommended Reps")]
        public string RecommendedReps { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Tips")]
        public string Tips { get; set; } = string.Empty;

        [Display(Name = "System exercise (visible to all users)")]
        public bool IsSystemExercise { get; set; } = true;

        public static ExerciseFormViewModel FromEntity(Models.Exercise e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            MuscleGroup = e.MuscleGroup,
            Category = e.Category,
            Equipment = e.Equipment,
            RecommendedSets = e.RecommendedSets,
            RecommendedReps = e.RecommendedReps,
            Description = e.Description,
            Tips = e.Tips,
            IsSystemExercise = e.IsSystemExercise
        };

        public Models.Exercise ToExercise(string? createdByUserId)
        {
            return new Models.Exercise
            {
                Id = Id,
                Name = Name,
                MuscleGroup = MuscleGroup,
                Category = Category,
                Equipment = Equipment,
                RecommendedSets = RecommendedSets,
                RecommendedReps = RecommendedReps,
                Description = Description,
                Tips = Tips,
                IsSystemExercise = IsSystemExercise,
                CreatedByUserId = createdByUserId
            };
        }
    }
}
