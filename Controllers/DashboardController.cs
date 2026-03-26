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
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            if (settings == null)
                return RedirectToAction("Index", "Onboarding");

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-(int)today.DayOfWeek);

            var calorieGoal = settings.CalorieGoal;
            var proteinGoal = settings.ProteinGoal;
            var carbGoal = settings.CarbGoal;
            var fatGoal = settings.FatGoal;
            var waterGoal = settings.WaterGoal;

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

            var allEntries = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .ToList();

            var workoutsThisMonth = allEntries
                .Where(w => w.WorkoutDate >= monthStart)
                .Select(w => w.WorkoutDate.Date)
                .Distinct()
                .Count();

            var workoutsThisWeek = allEntries
                .Where(w => w.WorkoutDate >= weekStart)
                .Select(w => w.WorkoutDate.Date)
                .Distinct()
                .Count();

            var monthlyVolume = allEntries
                .Where(w => w.WorkoutDate >= monthStart)
                .Sum(w => w.Sets * w.Reps * w.WeightLbs);

            var allWorkoutDates = allEntries
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

            var recentWorkouts = allEntries
                .OrderByDescending(w => w.WorkoutDate)
                .Take(8)
                .ToList();

            var prs = allEntries
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            // Visible panels from cookie
            var panelCookie = Request.Cookies["dashboardPanels"];
            var visiblePanels = panelCookie != null
                ? panelCookie.Split(',').ToList()
                : new List<string> { "calories", "workouts", "water", "supplements", "streak", "recent" };

            ViewBag.CaloriesToday = nutritionToday.Sum(n => n.Calories);
            ViewBag.ProteinToday = Math.Round(nutritionToday.Sum(n => n.Protein), 1);
            ViewBag.CarbsToday = Math.Round(nutritionToday.Sum(n => n.Carbs), 1);
            ViewBag.FatToday = Math.Round(nutritionToday.Sum(n => n.Fat), 1);
            ViewBag.WaterToday = waterToday;
            ViewBag.WaterPercentage = Math.Min((double)(waterToday / (decimal)waterGoal * 100), 100);
            ViewBag.SupplementsTaken = supplementsTaken;
            ViewBag.SupplementsTotal = supplementsTotal;
            ViewBag.Streak = streak;
            ViewBag.RecentWorkouts = recentWorkouts;
            ViewBag.PersonalRecords = prs;
            ViewBag.Today = today;
            ViewBag.CalorieGoal = calorieGoal;
            ViewBag.ProteinGoal = proteinGoal;
            ViewBag.CarbGoal = carbGoal;
            ViewBag.FatGoal = fatGoal;
            ViewBag.WaterGoal = waterGoal;
            ViewBag.WorkoutsThisMonth = workoutsThisMonth;
            ViewBag.WorkoutsThisWeek = workoutsThisWeek;
            ViewBag.MonthlyVolume = monthlyVolume;
            ViewBag.DisplayName = string.IsNullOrEmpty(settings.DisplayName)
                ? User.Identity?.Name?.Split('@')[0]
                : settings.DisplayName;
            ViewBag.VisiblePanels = visiblePanels;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePanels(List<string> panels)
        {
            var panelString = string.Join(",", panels ?? new List<string>());
            Response.Cookies.Append("dashboardPanels", panelString, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(365)
            });
            return RedirectToAction(nameof(Index));
        }
    }
}