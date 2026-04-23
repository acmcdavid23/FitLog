using FitLog.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LeaderboardController(ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? exercise, string? tab, int? groupId)
        {
            await PopulateLeaderboardViewBagsAsync(exercise, tab, groupId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> BoardsPartial(string? exercise, string? tab, int? groupId)
        {
            await PopulateLeaderboardViewBagsAsync(exercise, tab, groupId);
            return PartialView("_LeaderboardBoards");
        }

        /// <summary>Header + tab dropdown + leaderboard boards (for in-page tab switches).</summary>
        [HttpGet]
        public async Task<IActionResult> ShellPartial(string? exercise, string? tab, int? groupId)
        {
            await PopulateLeaderboardViewBagsAsync(exercise, tab, groupId);
            return PartialView("_LeaderboardShell");
        }

        private async Task PopulateLeaderboardViewBagsAsync(string? exercise, string? tabParam, int? groupId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tab = tabParam ?? "global";
            if (tab == "groups")
                tab = "group";

            var optedInUserIds = _context.UserSettings
                .Where(s => s.ShowOnLeaderboard)
                .Select(s => s.UserId)
                .ToList();

            var userSettings = _context.UserSettings
                .Where(s => s.ShowOnLeaderboard)
                .ToDictionary(s => s.UserId, s => string.IsNullOrEmpty(s.DisplayName) ? "Anonymous" : s.DisplayName);

            var profileUrls = _context.UserSettings
                .Where(s => optedInUserIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.ProfileImageUrl ?? string.Empty);

            var allUsers = _userManager.Users.ToList();
            var userEmails = allUsers.ToDictionary(u => u.Id, u => u.Email ?? "Unknown");

            string GetDisplayName(string userId)
            {
                if (userSettings.ContainsKey(userId) && userSettings[userId] != "Anonymous")
                    return userSettings[userId];
                if (userEmails.ContainsKey(userId))
                    return userEmails[userId].Split('@')[0];
                return "Anonymous";
            }

            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            List<string> friendIds = new();
            if (currentUserId != null)
            {
                var accepted = await _context.FriendRequests
                    .Where(f => (f.SenderId == currentUserId || f.ReceiverId == currentUserId) && f.Status == "Accepted")
                    .ToListAsync();
                friendIds = accepted
                    .Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId)
                    .ToList();
            }
            var friendAndSelfIds = friendIds.Concat(new[] { currentUserId ?? "" }).Where(x => !string.IsNullOrEmpty(x)).ToList();

            List<string> groupMemberIds = new();
            List<FitLog.Models.FitLogGroup> userGroups = new();
            FitLog.Models.FitLogGroup? activeGroup = null;
            if (currentUserId != null)
            {
                userGroups = await _context.Groups
                    .Include(g => g.Members)
                    .Where(g => g.Members.Any(m => m.UserId == currentUserId))
                    .ToListAsync();
            }

            if (tab == "group")
            {
                if (userGroups.Count == 0)
                    tab = "global";
                else
                {
                    activeGroup = groupId.HasValue
                        ? userGroups.FirstOrDefault(g => g.Id == groupId.Value) ?? userGroups[0]
                        : userGroups[0];
                    groupMemberIds = activeGroup.Members.Select(m => m.UserId).ToList();
                }
            }

            var targetIds = tab switch
            {
                "friends" => friendAndSelfIds.Where(id => optedInUserIds.Contains(id)).ToList(),
                "group" => groupMemberIds.Where(id => optedInUserIds.Contains(id)).ToList(),
                _ => optedInUserIds
            };

            var volumeLeaderboard = _context.WorkoutEntries
                .Where(w => targetIds.Contains(w.UserId) && w.WorkoutDate >= weekStart)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = GetDisplayName(g.Key),
                    ProfileImageUrl = profileUrls.GetValueOrDefault(g.Key),
                    TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs),
                    WorkoutCount = g.Select(w => w.WorkoutDate.Date).Distinct().Count(),
                    IsCurrentUser = g.Key == currentUserId
                })
                .OrderByDescending(x => x.TotalVolume)
                .Take(10)
                .ToList();

            var streakLeaderboard = targetIds
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
                        if (date == checkDate || date == checkDate.AddDays(-1)) { streak++; checkDate = date; }
                        else break;
                    }

                    return new
                    {
                        UserId = userId,
                        DisplayName = GetDisplayName(userId),
                        ProfileImageUrl = profileUrls.GetValueOrDefault(userId),
                        Streak = streak,
                        IsCurrentUser = userId == currentUserId
                    };
                })
                .Where(x => x.Streak > 0)
                .OrderByDescending(x => x.Streak)
                .Take(10)
                .ToList();

            var exercises = _context.WorkoutEntries
                .Where(w => targetIds.Contains(w.UserId) && w.WeightLbs > 0)
                .Select(w => w.ExerciseName)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            var selectedExercise = exercise ?? exercises.FirstOrDefault() ?? "";

            var prLeaderboard = _context.WorkoutEntries
                .Where(w => targetIds.Contains(w.UserId) && w.ExerciseName == selectedExercise && w.WeightLbs > 0)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = GetDisplayName(g.Key),
                    ProfileImageUrl = profileUrls.GetValueOrDefault(g.Key),
                    MaxWeight = g.Max(w => w.WeightLbs),
                    Date = g.OrderByDescending(w => w.WeightLbs).First().WorkoutDate,
                    IsCurrentUser = g.Key == currentUserId
                })
                .OrderByDescending(x => x.MaxWeight)
                .Take(10)
                .ToList();

            var currentUserSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == currentUserId);

            ViewBag.UserOptedIn = currentUserSettings?.ShowOnLeaderboard ?? true;
            ViewBag.VolumeLeaderboard = volumeLeaderboard;
            ViewBag.StreakLeaderboard = streakLeaderboard;
            ViewBag.PRLeaderboard = prLeaderboard;
            ViewBag.Exercises = exercises;
            ViewBag.SelectedExercise = selectedExercise;
            ViewBag.WeekStart = weekStart;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.ActiveTab = tab;
            ViewBag.HasFriends = friendIds.Any();
            ViewBag.UserGroups = userGroups;
            ViewBag.ActiveGroupId = activeGroup?.Id;
            ViewBag.ActiveGroupName = activeGroup?.Name ?? "";
        }
    }
}
