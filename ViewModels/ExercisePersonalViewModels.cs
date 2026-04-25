using System.ComponentModel.DataAnnotations;
using FitLog.Models;

namespace FitLog.ViewModels;

/// <summary>User-created custom exercise (CreatePersonal form).</summary>
public class ExercisePersonalCreateViewModel
{
    [Required]
    [Display(Name = "Exercise Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Muscle Group")]
    public string MuscleGroup { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Equipment")]
    public string Equipment { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Tips")]
    public string Tips { get; set; } = string.Empty;

    public Exercise ToEntity(string userId) => new()
    {
        Name = Name,
        MuscleGroup = MuscleGroup,
        Equipment = Equipment,
        Description = Description,
        Tips = Tips,
        Category = string.Empty,
        RecommendedSets = 0,
        RecommendedReps = string.Empty,
        IsSystemExercise = false,
        CreatedByUserId = userId
    };
}

/// <summary>Exercise library modal: display + add-to-session forms (no domain entity in view).</summary>
public class ExerciseModalViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tips { get; set; } = string.Empty;

    public static ExerciseModalViewModel FromEntity(Exercise e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        MuscleGroup = e.MuscleGroup,
        Equipment = e.Equipment ?? string.Empty,
        Description = e.Description ?? string.Empty,
        Tips = e.Tips ?? string.Empty
    };
}
