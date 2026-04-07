using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class WeightLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WeightLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var logs = _context.WeightLogs
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.LogDate)
                .Take(90)
                .ToList();

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            ViewBag.WeightUnit = settings?.WeightUnit ?? "lbs";
            ViewBag.GoalWeight = settings?.GoalWeight ?? 0;

            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Log(decimal weightLbs, string notes, string weightUnit)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Convert to lbs if user entered kg
            if (weightUnit == "kg")
                weightLbs = weightLbs * 2.205m;

            // Upsert — one entry per day
            var existing = _context.WeightLogs
                .FirstOrDefault(w => w.UserId == userId && w.LogDate == DateTime.Today);

            if (existing != null)
            {
                existing.WeightLbs = Math.Round(weightLbs, 2);
                existing.Notes = notes ?? string.Empty;
            }
            else
            {
                _context.WeightLogs.Add(new WeightLog
                {
                    UserId = userId ?? string.Empty,
                    LogDate = DateTime.Today,
                    WeightLbs = Math.Round(weightLbs, 2),
                    Notes = notes ?? string.Empty
                });
            }

            // Also update UserSettings.CurrentWeight
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings != null)
                settings.CurrentWeight = Math.Round(weightLbs, 2);

            _context.SaveChanges();
            TempData["Success"] = "Weight logged!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.WeightLogs.FirstOrDefault(w => w.Id == id && w.UserId == userId);
            if (log != null)
            {
                _context.WeightLogs.Remove(log);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, decimal weightLbs, string notes, string weightUnit)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.WeightLogs.FirstOrDefault(w => w.Id == id && w.UserId == userId);
            if (log != null)
            {
                if (weightUnit == "kg")
                    weightLbs = weightLbs * 2.205m;
                log.WeightLbs = Math.Round(weightLbs, 2);
                log.Notes = notes ?? string.Empty;
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}