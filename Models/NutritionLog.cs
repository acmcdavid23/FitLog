using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class NutritionLog
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime LogDate { get; set; }

        [Required]
        [Display(Name = "Meal Name")]
        public string MealName { get; set; } = string.Empty; // Breakfast, Lunch, Dinner, Snack

        [Required]
        [Display(Name = "Food Item")]
        public string FoodItem { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Calories")]
        public int Calories { get; set; }

        [Display(Name = "Protein (g)")]
        public decimal Protein { get; set; }

        [Display(Name = "Carbs (g)")]
        public decimal Carbs { get; set; }

        [Display(Name = "Fat (g)")]
        public decimal Fat { get; set; }

        [Display(Name = "Serving Size")]
        public string ServingSize { get; set; } = string.Empty;
    }
}