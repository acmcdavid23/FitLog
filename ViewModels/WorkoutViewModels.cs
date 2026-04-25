using System.ComponentModel.DataAnnotations;
using FitLog.Helpers;
using FitLog.Models;

namespace FitLog.ViewModels;

public class WorkoutSessionCreateViewModel
{
    [Required]
    [Display(Name = "Workout Name")]
    public string SessionName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime SessionDate { get; set; } = DateTime.Today;

    [Display(Name = "Notes")]
    public string Notes { get; set; } = string.Empty;

    public WorkoutSession ToEntity(string userId) => new()
    {
        UserId = userId,
        SessionName = SessionName,
        SessionDate = SessionDate,
        Notes = Notes ?? string.Empty
    };
}

public class WorkoutEntrySetRowViewModel
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public DateTime WorkoutDate { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightLbs { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }

    public static WorkoutEntrySetRowViewModel FromEntity(WorkoutEntry e) => new()
    {
        Id = e.Id,
        ExerciseName = e.ExerciseName,
        MuscleGroup = e.MuscleGroup,
        WorkoutDate = e.WorkoutDate,
        Sets = e.Sets,
        Reps = e.Reps,
        WeightLbs = e.WeightLbs,
        Notes = e.Notes,
        IsCompleted = e.IsCompleted
    };
}

/// <summary>Session header + entry rows for session detail and active workout screens.</summary>
public class WorkoutSessionDetailViewModel
{
    public int Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public List<WorkoutEntrySetRowViewModel> Entries { get; set; } = new();

    public static WorkoutSessionDetailViewModel FromEntity(WorkoutSession s) => new()
    {
        Id = s.Id,
        SessionName = s.SessionName,
        SessionDate = s.SessionDate,
        Entries = s.Entries
            .GroupBy(e => e.ExerciseName)
            .OrderBy(g => g.Min(x => x.Id))
            .SelectMany(g => g.OrderBy(x => x.Id))
            .Select(WorkoutEntrySetRowViewModel.FromEntity)
            .ToList()
    };
}

public class WorkoutEntryCreateViewModel
{
    [Required]
    [Display(Name = "Exercise Name")]
    public string ExerciseName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Muscle Group")]
    public string MuscleGroup { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    public DateTime WorkoutDate { get; set; } = DateTime.Today;

    [Required]
    [Range(1, 20)]
    public int Sets { get; set; } = 3;

    [Required]
    [Range(1, 100)]
    public int Reps { get; set; } = 10;

    [Required]
    [Range(0, 2000)]
    [Display(Name = "Weight (lbs)")]
    public decimal WeightLbs { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Completed")]
    public bool IsCompleted { get; set; }

    public WorkoutEntry ToEntity(string userId) => new()
    {
        UserId = userId,
        SessionId = null,
        ExerciseName = ExerciseName,
        MuscleGroup = MuscleGroup,
        WorkoutDate = WorkoutDate,
        Sets = Sets,
        Reps = Reps,
        WeightLbs = WeightLbs,
        Notes = Notes,
        IsCompleted = IsCompleted
    };
}

public class WorkoutEntryEditViewModel
{
    public int Id { get; set; }
    public int? SessionId { get; set; }

    [Required]
    [Display(Name = "Exercise Name")]
    public string ExerciseName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Muscle Group")]
    public string MuscleGroup { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    public DateTime WorkoutDate { get; set; }

    [Required]
    [Range(1, 20)]
    public int Sets { get; set; }

    [Required]
    [Range(1, 100)]
    public int Reps { get; set; }

    [Required]
    [Range(0, 2000)]
    [Display(Name = "Weight (lbs)")]
    public decimal WeightLbs { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Completed")]
    public bool IsCompleted { get; set; }

    public static WorkoutEntryEditViewModel FromEntity(WorkoutEntry e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        ExerciseName = e.ExerciseName,
        MuscleGroup = e.MuscleGroup,
        WorkoutDate = e.WorkoutDate,
        Sets = e.Sets,
        Reps = e.Reps,
        WeightLbs = e.WeightLbs,
        Notes = e.Notes,
        IsCompleted = e.IsCompleted
    };

    public void ApplyTo(WorkoutEntry e)
    {
        e.ExerciseName = ExerciseName;
        e.MuscleGroup = MuscleGroup;
        e.WorkoutDate = WorkoutDate;
        e.Sets = Sets;
        e.Reps = Reps;
        e.WeightLbs = WeightLbs;
        e.Notes = Notes;
        e.IsCompleted = IsCompleted;
        e.SessionId = SessionId;
    }
}

public class WorkoutEntryDeleteViewModel
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public DateTime WorkoutDate { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightLbs { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }

    public static WorkoutEntryDeleteViewModel FromEntity(WorkoutEntry e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        ExerciseName = e.ExerciseName,
        MuscleGroup = e.MuscleGroup,
        WorkoutDate = e.WorkoutDate,
        Sets = e.Sets,
        Reps = e.Reps,
        WeightLbs = e.WeightLbs,
        Notes = e.Notes,
        IsCompleted = e.IsCompleted
    };
}

public class WorkoutSessionListItemViewModel
{
    public int Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public int ExerciseKindsCount { get; set; }
    public decimal TotalVolume { get; set; }
    public List<string> MuscleGroups { get; set; } = new();

    public static WorkoutSessionListItemViewModel FromSession(WorkoutSession s)
    {
        var entries = s.Entries ?? new List<WorkoutEntry>();
        var muscleGroups = entries
            .Where(e => !ExerciseDisplay.IsPending(e.ExerciseName))
            .Select(e => e.MuscleGroup)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .OrderBy(m => m)
            .ToList();
        var volume = entries.Sum(e => e.Sets * e.Reps * e.WeightLbs);
        var kinds = entries
            .Where(e => !ExerciseDisplay.IsPending(e.ExerciseName))
            .Select(e => e.ExerciseName)
            .Distinct()
            .Count();

        return new WorkoutSessionListItemViewModel
        {
            Id = s.Id,
            SessionName = s.SessionName,
            SessionDate = s.SessionDate,
            ExerciseKindsCount = kinds,
            TotalVolume = volume,
            MuscleGroups = muscleGroups
        };
    }
}

public class WorkoutEntryLegacyRowViewModel
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public DateTime WorkoutDate { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightLbs { get; set; }

    public static WorkoutEntryLegacyRowViewModel FromEntity(WorkoutEntry e) => new()
    {
        Id = e.Id,
        ExerciseName = e.ExerciseName,
        MuscleGroup = e.MuscleGroup,
        WorkoutDate = e.WorkoutDate,
        Sets = e.Sets,
        Reps = e.Reps,
        WeightLbs = e.WeightLbs
    };
}

public class WorkoutEntriesIndexPageViewModel
{
    public List<WorkoutSessionListItemViewModel> Sessions { get; set; } = new();
    public List<WorkoutEntryLegacyRowViewModel> UnsessionedEntries { get; set; } = new();
    public Dictionary<string, decimal> PersonalRecords { get; set; } = new();
    public List<string> MuscleGroups { get; set; } = new();
    public string? Search { get; set; }
    public string? MuscleGroup { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public bool NoSessionsAtAll { get; set; }
}

public class WorkoutSessionSummaryViewModel
{
    public int Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }

    public static WorkoutSessionSummaryViewModel FromEntity(WorkoutSession s) => new()
    {
        Id = s.Id,
        SessionName = s.SessionName,
        SessionDate = s.SessionDate
    };
}

public class StartWorkoutPageViewModel
{
    public List<WorkoutSessionSummaryViewModel> RecentSessions { get; set; } = new();
}

public class StartNewWorkoutFormViewModel
{
    [Required]
    [Display(Name = "Workout name")]
    public string SessionName { get; set; } = string.Empty;
}

public class WorkoutHistoryRowViewModel
{
    public DateTime WorkoutDate { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightLbs { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    public static WorkoutHistoryRowViewModel FromEntity(WorkoutEntry e) => new()
    {
        WorkoutDate = e.WorkoutDate,
        Sets = e.Sets,
        Reps = e.Reps,
        WeightLbs = e.WeightLbs,
        Notes = e.Notes ?? string.Empty,
        IsCompleted = e.IsCompleted
    };
}

public class ExerciseHistoryPageViewModel
{
    public string ExerciseName { get; set; } = string.Empty;
    public decimal PersonalRecordWeight { get; set; }
    public List<WorkoutHistoryRowViewModel> History { get; set; } = new();
}
