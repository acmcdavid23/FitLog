using System.ComponentModel.DataAnnotations;

namespace FitLog.ViewModels;

public class OnboardingStep1ViewModel
{
    public string DisplayName { get; set; } = string.Empty;
}

public class OnboardingBodyStatsViewModel
{
    [Required]
    public string WeightUnit { get; set; } = "lbs";

    [Required]
    public decimal CurrentWeight { get; set; }

    [Required]
    public decimal GoalWeight { get; set; }

    [Required]
    public decimal HeightFeet { get; set; }

    [Required]
    public decimal HeightInches { get; set; }

    [Required]
    [Range(1, 520)]
    public int GoalTimeframeWeeks { get; set; } = 12;

    [Required]
    [Range(13, 100)]
    public int Age { get; set; }

    [Required]
    public string Gender { get; set; } = "Male";
}

public class OnboardingGoalsViewModel
{
    [Required]
    public string FitnessGoal { get; set; } = string.Empty;

    [Required]
    public string BodyGoal { get; set; } = string.Empty;
}

public class OnboardingStep4PageViewModel
{
    public string BodyGoal { get; set; } = string.Empty;
    public string FitnessGoal { get; set; } = string.Empty;
    public decimal CurrentWeight { get; set; }
    public decimal GoalWeight { get; set; }
    public decimal WeeklyChange { get; set; }
    public int Weeks { get; set; }

    public int CalorieGoal { get; set; }
    public int ProteinGoal { get; set; }
    public int CarbGoal { get; set; }
    public int FatGoal { get; set; }
    public int WaterGoal { get; set; } = 128;
}
