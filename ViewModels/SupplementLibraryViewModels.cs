using System.ComponentModel.DataAnnotations;
using FitLog.Models;

namespace FitLog.ViewModels;

public class SupplementLibraryItemCreateViewModel
{
    [Required]
    [Display(Name = "Supplement Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Evidence Level")]
    public string EvidenceLevel { get; set; } = string.Empty;

    [Display(Name = "Recommended Dosage")]
    public string RecommendedDosage { get; set; } = string.Empty;

    [Display(Name = "When to Take")]
    public string WhenToTake { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Benefits")]
    public string Benefits { get; set; } = string.Empty;

    [Display(Name = "More Info URL")]
    public string InfoUrl { get; set; } = string.Empty;

    public bool IsRecommended { get; set; }

    public SupplementLibraryItem ToEntity(bool isSystemItem, string? createdByUserId) => new()
    {
        Name = Name,
        Category = Category,
        EvidenceLevel = EvidenceLevel ?? string.Empty,
        RecommendedDosage = RecommendedDosage ?? string.Empty,
        WhenToTake = WhenToTake ?? string.Empty,
        Description = Description ?? string.Empty,
        Benefits = Benefits ?? string.Empty,
        InfoUrl = InfoUrl ?? string.Empty,
        IsRecommended = IsRecommended,
        IsSystemItem = isSystemItem,
        CreatedByUserId = createdByUserId
    };
}

public class SupplementLibraryItemEditViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Supplement Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Evidence Level")]
    public string EvidenceLevel { get; set; } = string.Empty;

    [Display(Name = "Recommended Dosage")]
    public string RecommendedDosage { get; set; } = string.Empty;

    [Display(Name = "When to Take")]
    public string WhenToTake { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Benefits")]
    public string Benefits { get; set; } = string.Empty;

    [Display(Name = "More Info URL")]
    public string InfoUrl { get; set; } = string.Empty;

    public bool IsRecommended { get; set; }

    public static SupplementLibraryItemEditViewModel FromEntity(SupplementLibraryItem s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Category = s.Category,
        EvidenceLevel = s.EvidenceLevel ?? string.Empty,
        RecommendedDosage = s.RecommendedDosage ?? string.Empty,
        WhenToTake = s.WhenToTake ?? string.Empty,
        Description = s.Description ?? string.Empty,
        Benefits = s.Benefits ?? string.Empty,
        InfoUrl = s.InfoUrl ?? string.Empty,
        IsRecommended = s.IsRecommended
    };

    public void ApplyTo(SupplementLibraryItem s)
    {
        s.Name = Name;
        s.Category = Category;
        s.EvidenceLevel = EvidenceLevel ?? string.Empty;
        s.RecommendedDosage = RecommendedDosage ?? string.Empty;
        s.WhenToTake = WhenToTake ?? string.Empty;
        s.Description = Description ?? string.Empty;
        s.Benefits = Benefits ?? string.Empty;
        s.InfoUrl = InfoUrl ?? string.Empty;
        s.IsRecommended = IsRecommended;
    }
}
