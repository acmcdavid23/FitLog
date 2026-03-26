using FitLog.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Redirect to onboarding if no settings exist
            var settings = _context.UserSettings
                .FirstOrDefault(s => s.UserId == userId);

            if (settings == null)
                return RedirectToAction("Index", "Onboarding");

            var today = DateTime.Today;
            var calorieGoal = settings.CalorieGoal;
            var proteinGoal = settings.ProteinGoal;
            var waterGoal = settings.WaterGoal;

            var workoutsToday = _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.WorkoutDate == today)
                .ToList();

            var nutritionToday = _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate == today)
                .ToList();

            var waterToday = _context.WaterLogs
                .Where(w => w.UserId == userId && w.LogDate == today)
                .Sum(w => (decimal?)w.AmountOz) ?? 0;

            var supplementsTotal = _context.Supplements
                .Count(s => s.UserId == userId && s.IsActive);

            var supplementsTaken = _context.SupplementLogs
                .Count(l => l.UserId == userId && l.LogDate == today && l.IsTaken);

            var allWorkoutDates = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .Select(w => w.WorkoutDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            int streak = 0;
            var checkDate = today;
            foreach (var date in allWorkoutDates)
            {
                if (date == checkDate || date == checkDate.AddDays(-1))
                {
                    streak++;
                    checkDate = date;
                }
                else break;
            }

            var recentWorkouts = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WorkoutDate)
                .Take(5)
                .ToList();

            ViewBag.WorkoutsToday = workoutsToday.Count;
            ViewBag.CaloriesToday = nutritionToday.Sum(n => n.Calories);
            ViewBag.ProteinToday = nutritionToday.Sum(n => n.Protein);
            ViewBag.WaterToday = waterToday;
            ViewBag.WaterPercentage = Math.Min((double)(waterToday / (decimal)waterGoal * 100), 100);
            ViewBag.SupplementsTaken = supplementsTaken;
            ViewBag.SupplementsTotal = supplementsTotal;
            ViewBag.Streak = streak;
            ViewBag.RecentWorkouts = recentWorkouts;
            ViewBag.Today = today;
            ViewBag.CalorieGoal = calorieGoal;
            ViewBag.ProteinGoal = proteinGoal;
            ViewBag.WaterGoal = waterGoal;
            ViewBag.DisplayName = string.IsNullOrEmpty(settings.DisplayName)
                ? User.Identity?.Name?.Split('@')[0]
                : settings.DisplayName;

            return View();
        }
    }
}