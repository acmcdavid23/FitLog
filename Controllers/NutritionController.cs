using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class NutritionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NutritionController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var todayLogs = _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate == today)
                .OrderBy(n => n.MealName)
                .ToList();

            var totalCalories = todayLogs.Sum(n => n.Calories);
            var totalProtein = todayLogs.Sum(n => n.Protein);
            var totalCarbs = todayLogs.Sum(n => n.Carbs);
            var totalFat = todayLogs.Sum(n => n.Fat);

            var grouped = todayLogs
                .GroupBy(n => n.MealName)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.TotalCalories = totalCalories;
            ViewBag.TotalProtein = totalProtein;
            ViewBag.TotalCarbs = totalCarbs;
            ViewBag.TotalFat = totalFat;
            ViewBag.Grouped = grouped;
            ViewBag.Today = today;

            // Goals (default values - can be customized later)
            ViewBag.CalorieGoal = 2500;
            ViewBag.ProteinGoal = 180;
            ViewBag.CarbGoal = 300;
            ViewBag.FatGoal = 80;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(NutritionLog log)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            log.UserId = userId ?? string.Empty;
            log.LogDate = DateTime.Today;
            ModelState.Remove("UserId");

            if (ModelState.IsValid)
            {
                _context.NutritionLogs.Add(log);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.NutritionLogs
                .FirstOrDefault(n => n.Id == id && n.UserId == userId);

            if (log != null)
            {
                _context.NutritionLogs.Remove(log);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var last7Days = _context.NutritionLogs
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.LogDate)
                .Take(200)
                .ToList()
                .GroupBy(n => n.LogDate)
                .Select(g => new
                {
                    Date = g.Key,
                    Calories = g.Sum(n => n.Calories),
                    Protein = g.Sum(n => n.Protein),
                    Carbs = g.Sum(n => n.Carbs),
                    Fat = g.Sum(n => n.Fat)
                })
                .OrderByDescending(x => x.Date)
                .Take(7)
                .ToList();

            ViewBag.History = last7Days;
            return View();
        }
    }
}