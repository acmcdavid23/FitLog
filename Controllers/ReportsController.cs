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

            // Report 1: Volume by muscle group (sets x reps x weight)
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

            // Report 2: Workouts per week
            var workoutsPerWeek = workouts
                .GroupBy(w => System.Globalization.ISOWeek.GetWeekOfYear(w.WorkoutDate))
                .Select(g => new
                {
                    Week = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Week)
                .ToList();

            // Report 3: Personal records (max weight per exercise)
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

            ViewBag.VolumeByMuscle = volumeByMuscle;
            ViewBag.WorkoutsPerWeek = workoutsPerWeek;
            ViewBag.PersonalRecords = personalRecords;
            ViewBag.TotalWorkouts = workouts.Count;
            ViewBag.TotalExercises = workouts.Select(w => w.ExerciseName).Distinct().Count();

            return View();
        }
    }
}