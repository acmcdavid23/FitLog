using System.ComponentModel.DataAnnotations;
using FitLog.Models;

namespace FitLog.ViewModels;

public class WeightLogRowViewModel
{
    public int Id { get; set; }
    public DateTime LogDate { get; set; }
    public decimal WeightLbs { get; set; }

    public static WeightLogRowViewModel FromEntity(WeightLog w) => new()
    {
        Id = w.Id,
        LogDate = w.LogDate,
        WeightLbs = w.WeightLbs
    };
}

public class WeightLogPageViewModel
{
    public List<WeightLogRowViewModel> Logs { get; set; } = new();
    public string WeightUnit { get; set; } = "lbs";
    public decimal GoalWeight { get; set; }
    public decimal SettingsCurrentWeight { get; set; }
}

public class WeightLogDayEntryViewModel
{
    public decimal WeightLbs { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string WeightUnit { get; set; } = "lbs";
}

public class WeightLogEditEntryViewModel
{
    public int Id { get; set; }
    public decimal WeightLbs { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string WeightUnit { get; set; } = "lbs";
}

public class UserSupplementCreateViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;
    public string TimeToTake { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Supplement ToEntity(string userId) => new()
    {
        UserId = userId,
        Name = Name.Trim(),
        Dosage = Dosage ?? string.Empty,
        TimeToTake = TimeToTake ?? string.Empty,
        Notes = Notes ?? string.Empty,
        IsActive = true
    };
}

public class SupplementJournalRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string TimeToTake { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public static SupplementJournalRowViewModel FromEntity(Supplement s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Dosage = s.Dosage,
        TimeToTake = s.TimeToTake,
        Notes = s.Notes
    };
}

public class SupplementLibraryBrowseCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string RecommendedDosage { get; set; } = string.Empty;
    public string WhenToTake { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public string InfoUrl { get; set; } = string.Empty;

    public static SupplementLibraryBrowseCardViewModel FromEntity(SupplementLibraryItem i) => new()
    {
        Name = i.Name,
        RecommendedDosage = i.RecommendedDosage,
        WhenToTake = i.WhenToTake,
        Description = i.Description,
        IsRecommended = i.IsRecommended,
        InfoUrl = i.InfoUrl
    };
}

public class SupplementJournalPageViewModel
{
    public List<SupplementJournalRowViewModel> ActiveSupplements { get; set; } = new();
    public HashSet<int> TakenSupplementIdsToday { get; set; } = new();
    public DateTime Today { get; set; }
    public List<(string Category, List<SupplementLibraryBrowseCardViewModel> Items)> LibraryByCategory { get; set; } = new();
}

public class SupplementManagePageViewModel
{
    public List<SupplementJournalRowViewModel> Supplements { get; set; } = new();
}

public class NutritionLogRowViewModel
{
    public int Id { get; set; }
    public string MealName { get; set; } = string.Empty;
    public string FoodItem { get; set; } = string.Empty;
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbs { get; set; }
    public decimal Fat { get; set; }
    public string ServingSize { get; set; } = string.Empty;

    public static NutritionLogRowViewModel FromEntity(NutritionLog n) => new()
    {
        Id = n.Id,
        MealName = n.MealName,
        FoodItem = n.FoodItem,
        Calories = n.Calories,
        Protein = n.Protein,
        Carbs = n.Carbs,
        Fat = n.Fat,
        ServingSize = n.ServingSize
    };
}

public class NutritionIndexPageViewModel
{
    public static readonly string[] AllMealNames =
    {
        "Breakfast", "Lunch", "Dinner", "Snack", "Pre-Workout", "Post-Workout"
    };

    public Dictionary<string, List<NutritionLogRowViewModel>> GroupedByMeal { get; set; } = new();
    public int TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public DateTime Today { get; set; }
    public int CalorieGoal { get; set; }
    public int ProteinGoal { get; set; }
    public int CarbGoal { get; set; }
    public int FatGoal { get; set; }
    public List<decimal> WeeklyProtein { get; set; } = new();
    public List<decimal> WeeklyCarbs { get; set; } = new();
    public List<decimal> WeeklyFat { get; set; } = new();
    public List<string> WeeklyLabels { get; set; } = new();
}

public class NutritionLogCreateViewModel
{
    [Required]
    public string MealName { get; set; } = string.Empty;

    [Required]
    public string FoodItem { get; set; } = string.Empty;

    public string ServingSize { get; set; } = string.Empty;

    /// <summary>Optional unit from the manual form; merged into ServingSize when saving.</summary>
    public string? ServingUnit { get; set; }

    [Required]
    public int Calories { get; set; }

    [Required]
    public decimal Protein { get; set; }

    [Required]
    public decimal Carbs { get; set; }

    [Required]
    public decimal Fat { get; set; }

    public NutritionLog ToEntity(string userId, DateTime logDate)
    {
        var serving = (ServingSize ?? string.Empty).Trim();
        var unit = (ServingUnit ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(unit) && !string.IsNullOrEmpty(serving))
            serving = $"{serving} {unit}".Trim();
        else if (!string.IsNullOrEmpty(unit) && string.IsNullOrEmpty(serving))
            serving = unit;

        return new NutritionLog
        {
            UserId = userId,
            LogDate = logDate,
            MealName = MealName,
            FoodItem = FoodItem,
            Calories = Calories,
            Protein = Protein,
            Carbs = Carbs,
            Fat = Fat,
            ServingSize = serving
        };
    }
}

public class ExerciseLibraryCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string? Description { get; set; }

    public static ExerciseLibraryCardViewModel FromEntity(Exercise e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        MuscleGroup = e.MuscleGroup,
        Equipment = e.Equipment ?? string.Empty,
        Description = e.Description
    };
}

public class ExerciseLibraryGroupedSectionViewModel
{
    public string MuscleGroup { get; set; } = string.Empty;
    public List<ExerciseLibraryCardViewModel> Exercises { get; set; } = new();
}

public class ExerciseLibraryIndexPageViewModel
{
    public List<ExerciseLibraryGroupedSectionViewModel> SystemGrouped { get; set; } = new();
    public List<ExerciseLibraryCardViewModel>? PersonalExercises { get; set; }
}

public class SupplementLibraryCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RecommendedDosage { get; set; } = string.Empty;
    public string WhenToTake { get; set; } = string.Empty;
    public string EvidenceLevel { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public string InfoUrl { get; set; } = string.Empty;
    public bool IsSystemItem { get; set; }

    public static SupplementLibraryCardViewModel FromEntity(SupplementLibraryItem s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Category = s.Category,
        Description = s.Description,
        RecommendedDosage = s.RecommendedDosage,
        WhenToTake = s.WhenToTake,
        EvidenceLevel = s.EvidenceLevel,
        IsRecommended = s.IsRecommended,
        InfoUrl = s.InfoUrl,
        IsSystemItem = s.IsSystemItem
    };
}

public class SupplementLibraryGroupedSectionViewModel
{
    public string Category { get; set; } = string.Empty;
    public List<SupplementLibraryCardViewModel> Items { get; set; } = new();
}

public class SupplementLibraryIndexPageViewModel
{
    public List<SupplementLibraryGroupedSectionViewModel> SystemGrouped { get; set; } = new();
    public List<SupplementLibraryCardViewModel>? PersonalSupplements { get; set; }
}

public class SupplementLibraryItemDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Benefits { get; set; } = string.Empty;
    public string RecommendedDosage { get; set; } = string.Empty;
    public string WhenToTake { get; set; } = string.Empty;
    public string EvidenceLevel { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public string InfoUrl { get; set; } = string.Empty;
    public string? PersonalizedDosing { get; set; }

    public static SupplementLibraryItemDetailsViewModel FromEntity(SupplementLibraryItem s, string? personalizedDosing) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Category = s.Category,
        Description = s.Description,
        Benefits = s.Benefits,
        RecommendedDosage = s.RecommendedDosage,
        WhenToTake = s.WhenToTake,
        EvidenceLevel = s.EvidenceLevel,
        IsRecommended = s.IsRecommended,
        InfoUrl = s.InfoUrl,
        PersonalizedDosing = personalizedDosing
    };
}
