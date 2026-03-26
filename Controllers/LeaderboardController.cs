using FitLog.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Microsoft.AspNetCore.Identity.IdentityUser> _userManager;

        public LeaderboardController(ApplicationDbContext context, UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index(string? exercise)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var optedInUserIds = _context.UserSettings
                .Where(s => s.ShowOnLeaderboard)
                .Select(s => s.UserId)
                .ToList();

            var userSettings = _context.UserSettings
                .Where(s => s.ShowOnLeaderboard)
                .ToDictionary(s => s.UserId, s => string.IsNullOrEmpty(s.DisplayName) ? "Anonymous" : s.DisplayName);

            var allUsers = _userManager.Users.ToList();
            var userEmails = allUsers.ToDictionary(u => u.Id, u => u.Email ?? "Unknown");

            string GetDisplayName(string userId)
            {
                if (userSettings.ContainsKey(userId) && !string.IsNullOrEmpty(userSettings[userId]) && userSettings[userId] != "Anonymous")
                    return userSettings[userId];
                if (userEmails.ContainsKey(userId))
                    return userEmails[userId].Split('@')[0];
                return "Anonymous";
            }

            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            var volumeLeaderboard = _context.WorkoutEntries
                .Where(w => optedInUserIds.Contains(w.UserId) && w.WorkoutDate >= weekStart)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = GetDisplayName(g.Key),
                    TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs),
                    WorkoutCount = g.Select(w => w.WorkoutDate.Date).Distinct().Count(),
                    IsCurrentUser = g.Key == currentUserId
                })
                .OrderByDescending(x => x.TotalVolume)
                .Take(10)
                .ToList();

            var streakLeaderboard = optedInUserIds
                .Select(userId =>
                {
                    var dates = _context.WorkoutEntries
                        .Where(w => w.UserId == userId)
                        .Select(w => w.WorkoutDate.Date)
                        .Distinct()
                        .OrderByDescending(d => d)
                        .ToList();

                    int streak = 0;
                    var checkDate = DateTime.Today;
                    foreach (var date in dates)
                    {
                        if (date == checkDate || date == checkDate.AddDays(-1))
                        {
                            streak++;
                            checkDate = date;
                        }
                        else break;
                    }

                    return new
                    {
                        UserId = userId,
                        DisplayName = GetDisplayName(userId),
                        Streak = streak,
                        IsCurrentUser = userId == currentUserId
                    };
                })
                .Where(x => x.Streak > 0)
                .OrderByDescending(x => x.Streak)
                .Take(10)
                .ToList();

            var exercises = _context.WorkoutEntries
                .Where(w => optedInUserIds.Contains(w.UserId))
                .Select(w => w.ExerciseName)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            var selectedExercise = exercise ?? exercises.FirstOrDefault() ?? "";

            var prLeaderboard = _context.WorkoutEntries
                .Where(w => optedInUserIds.Contains(w.UserId) && w.ExerciseName == selectedExercise && w.WeightLbs > 0)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = GetDisplayName(g.Key),
                    MaxWeight = g.Max(w => w.WeightLbs),
                    Date = g.OrderByDescending(w => w.WeightLbs).First().WorkoutDate,
                    IsCurrentUser = g.Key == currentUserId
                })
                .OrderByDescending(x => x.MaxWeight)
                .Take(10)
                .ToList();

            // Check if current user is opted in
            var currentUserSettings = _context.UserSettings
                .FirstOrDefault(s => s.UserId == currentUserId);
            ViewBag.UserOptedIn = currentUserSettings?.ShowOnLeaderboard ?? true;

            ViewBag.VolumeLeaderboard = volumeLeaderboard;
            ViewBag.StreakLeaderboard = streakLeaderboard;
            ViewBag.PRLeaderboard = prLeaderboard;
            ViewBag.Exercises = exercises;
            ViewBag.SelectedExercise = selectedExercise;
            ViewBag.WeekStart = weekStart;
            ViewBag.CurrentUserId = currentUserId;

            return View();
        }
    }
}