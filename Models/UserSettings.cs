using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class UserSettings
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Daily Calorie Goal")]
        public int CalorieGoal { get; set; } = 2500;

        [Display(Name = "Daily Protein Goal (g)")]
        public int ProteinGoal { get; set; } = 180;

        [Display(Name = "Daily Carb Goal (g)")]
        public int CarbGoal { get; set; } = 300;

        [Display(Name = "Daily Fat Goal (g)")]
        public int FatGoal { get; set; } = 80;

        [Display(Name = "Daily Water Goal (oz)")]
        public int WaterGoal { get; set; } = 128;

        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "Weight Unit")]
        public string WeightUnit { get; set; } = "lbs";

        [Display(Name = "Fitness Goal")]
        public string FitnessGoal { get; set; } = "Hypertrophy";

        [Display(Name = "Show on Leaderboards")]
        public bool ShowOnLeaderboard { get; set; } = true;
    }
}