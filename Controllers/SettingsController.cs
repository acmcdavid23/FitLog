using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var settings = _context.UserSettings
                .FirstOrDefault(s => s.UserId == userId);

            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = userId ?? string.Empty
                };
            }

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(UserSettings settings)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            settings.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");

            if (ModelState.IsValid)
            {
                var existing = _context.UserSettings
                    .FirstOrDefault(s => s.UserId == userId);

                if (existing != null)
                {
                    existing.CalorieGoal = settings.CalorieGoal;
                    existing.ProteinGoal = settings.ProteinGoal;
                    existing.CarbGoal = settings.CarbGoal;
                    existing.FatGoal = settings.FatGoal;
                    existing.WaterGoal = settings.WaterGoal;
                    existing.DisplayName = settings.DisplayName;
                    existing.WeightUnit = settings.WeightUnit;
                    existing.FitnessGoal = settings.FitnessGoal;
                }
                else
                {
                    _context.UserSettings.Add(settings);
                }

                _context.SaveChanges();
                TempData["Success"] = "Settings saved successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}