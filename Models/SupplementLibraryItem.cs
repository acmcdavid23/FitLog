using System.ComponentModel.DataAnnotations;

namespace FitLog.Models
{
    public class SupplementLibraryItem
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Supplement Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Benefits")]
        public string Benefits { get; set; } = string.Empty;

        [Display(Name = "Recommended Dosage")]
        public string RecommendedDosage { get; set; } = string.Empty;

        [Display(Name = "When to Take")]
        public string WhenToTake { get; set; } = string.Empty;

        [Display(Name = "More Info URL")]
        public string InfoUrl { get; set; } = string.Empty;

        [Display(Name = "Evidence Level")]
        public string EvidenceLevel { get; set; } = string.Empty;

        public bool IsRecommended { get; set; } = false;

        public bool IsSystemItem { get; set; } = true;

        public string? CreatedByUserId { get; set; }
    }
}