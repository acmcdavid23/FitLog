using System.ComponentModel.DataAnnotations;
namespace FitLog.Models
{
    public class UserSettings
    {
        public int Id { get; set; }
        [Required] public string UserId { get; set; } = string.Empty;
        [Display(Name = "Username")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Username can only contain letters and numbers.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3-50 characters.")]
        public string? Username { get; set; }
        [Display(Name = "Daily Calorie Goal")] public int CalorieGoal { get; set; } = 2500;
        [Display(Name = "Daily Protein Goal (g)")] public int ProteinGoal { get; set; } = 180;
        [Display(Name = "Daily Carb Goal (g)")] public int CarbGoal { get; set; } = 300;
        [Display(Name = "Daily Fat Goal (g)")] public int FatGoal { get; set; } = 80;
        [Display(Name = "Daily Water Goal (oz)")] public int WaterGoal { get; set; } = 128;
        [Display(Name = "Display Name")] public string DisplayName { get; set; } = string.Empty;
        [Display(Name = "Weight Unit")] public string WeightUnit { get; set; } = "lbs";
        [Display(Name = "Fitness Goal")] public string FitnessGoal { get; set; } = "Hypertrophy";
        [Display(Name = "Body Goal")] public string BodyGoal { get; set; } = "Maintain";
        [Display(Name = "Current Weight")] public decimal CurrentWeight { get; set; }
        [Display(Name = "Goal Weight")] public decimal GoalWeight { get; set; }
        [Display(Name = "Height (inches)")] public decimal HeightInches { get; set; }
        [Display(Name = "Goal Timeframe (weeks)")] public int GoalTimeframeWeeks { get; set; } = 12;
        [Display(Name = "Show on Leaderboards")] public bool ShowOnLeaderboard { get; set; } = true;
        [Display(Name = "Age")] public int Age { get; set; }
        [Display(Name = "Gender")] public string Gender { get; set; } = "Male";
        [Display(Name = "Activity level")]
        public string ActivityLevel { get; set; } = "Moderate";
        [Display(Name = "City / Region")]
        [StringLength(120)]
        public string CityRegion { get; set; } = string.Empty;
        [Display(Name = "Profile photo URL")]
        [StringLength(500)]
        public string? ProfileImageUrl { get; set; }
    }
}