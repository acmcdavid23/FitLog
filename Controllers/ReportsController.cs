using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var workouts = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .ToList();

            var volumeByMuscle = workouts
                .GroupBy(w => w.MuscleGroup)
                .Select(g => new
                {
                    MuscleGroup = g.Key,
                    TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs),
                    TotalSessions = g.Count()
                })
                .OrderByDescending(x => x.TotalVolume)
                .ToList();

            var workoutsPerWeek = workouts
                .GroupBy(w => System.Globalization.ISOWeek.GetWeekOfYear(w.WorkoutDate))
                .Select(g => new
                {
                    Week = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Week)
                .ToList();

            var personalRecords = workouts
                .GroupBy(w => w.ExerciseName)
                .Select(g => new
                {
                    Exercise = g.Key,
                    MaxWeight = g.Max(w => w.WeightLbs),
                    BestDate = g.OrderByDescending(w => w.WeightLbs).First().WorkoutDate
                })
                .OrderByDescending(x => x.MaxWeight)
                .ToList();

            // Nutrition last 7 days
            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            var nutritionHistory = _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate >= sevenDaysAgo)
                .ToList()
                .GroupBy(n => n.LogDate)
                .Select(g => new
                {
                    Date = g.Key,
                    Calories = g.Sum(n => n.Calories),
                    Protein = Math.Round(g.Sum(n => n.Protein), 1),
                    Carbs = Math.Round(g.Sum(n => n.Carbs), 1),
                    Fat = Math.Round(g.Sum(n => n.Fat), 1)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Weight history last 30 entries
            var weightHistory = _context.WeightLogs
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.LogDate)
                .Take(30)
                .ToList()
                .OrderBy(w => w.LogDate)
                .ToList();

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            ViewBag.VolumeByMuscle = volumeByMuscle;
            ViewBag.WorkoutsPerWeek = workoutsPerWeek;
            ViewBag.PersonalRecords = personalRecords;
            ViewBag.TotalWorkouts = workouts.Count;
            ViewBag.TotalExercises = workouts.Select(w => w.ExerciseName).Distinct().Count();
            ViewBag.NutritionHistory = nutritionHistory;
            ViewBag.WeightHistory = weightHistory;
            ViewBag.WeightUnit = settings?.WeightUnit ?? "lbs";
            ViewBag.CalorieGoal = settings?.CalorieGoal ?? 2500;

            return View();
        }
    }
}