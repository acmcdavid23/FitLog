using FitLog.Data;
using FitLog.Models;
using FitLog.ViewModels;
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
                .ThenByDescending(w => w.Id)
                .Take(90)
                .ToList();

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var vm = new WeightLogPageViewModel
            {
                Logs = logs.Select(WeightLogRowViewModel.FromEntity).ToList(),
                WeightUnit = settings?.WeightUnit ?? "lbs",
                GoalWeight = settings?.GoalWeight ?? 0,
                SettingsCurrentWeight = settings?.CurrentWeight ?? 0
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Log(WeightLogDayEntryViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var weightLbs = model.WeightLbs;
            var notes = model.Notes;
            var weightUnit = model.WeightUnit ?? "lbs";

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
            TempData["Success"] = "Weight entry saved";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> LogAjax([FromBody] WeightLogAjaxRequest model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Json(new { success = false, message = "User not found." });
            if (model == null || model.WeightLbs <= 0)
                return Json(new { success = false, message = "Invalid weight entry." });

            var weightLbs = model.WeightLbs;
            var notes = model.Notes;
            var logDate = model.LogDate?.Date ?? DateTime.Today;

            var existing = _context.WeightLogs
                .FirstOrDefault(w => w.UserId == userId && w.LogDate == logDate);

            if (existing != null)
            {
                existing.WeightLbs = Math.Round(weightLbs, 2);
                existing.Notes = notes ?? string.Empty;
            }
            else
            {
                _context.WeightLogs.Add(new WeightLog
                {
                    UserId = userId,
                    LogDate = logDate,
                    WeightLbs = Math.Round(weightLbs, 2),
                    Notes = notes ?? string.Empty
                });
            }

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings != null)
                settings.CurrentWeight = Math.Round(weightLbs, 2);

            await _context.SaveChangesAsync();

            var entry = _context.WeightLogs
                .First(w => w.UserId == userId && w.LogDate == logDate);
            var count = _context.WeightLogs.Count(w => w.UserId == userId);

            return Json(new
            {
                success = true,
                message = "Weight entry saved",
                data = new { id = entry.Id, logDate = entry.LogDate, weightLbs = entry.WeightLbs, notes = entry.Notes ?? string.Empty },
                entriesCount = count
            });
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
                SyncCurrentWeightFromLogs(userId!);
                _context.SaveChanges();
                TempData["Success"] = "Weight entry removed";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAjax(WeightLogEditEntryViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.WeightLogs.FirstOrDefault(w => w.Id == model.Id && w.UserId == userId);
            if (log == null)
                return Json(new { success = false, error = "Entry not found." });
            var weightLbs = model.WeightLbs;
            var weightUnit = model.WeightUnit ?? "lbs";
            if (weightUnit == "kg")
                weightLbs = weightLbs * 2.205m;
            log.WeightLbs = Math.Round(weightLbs, 2);
            log.Notes = model.Notes ?? string.Empty;
            _context.SaveChanges();
            SyncCurrentWeightFromLogs(userId!);
            _context.SaveChanges();
            return Json(new
            {
                success = true,
                message = "Weight entry saved",
                data = new { id = log.Id, logDate = log.LogDate, weightLbs = log.WeightLbs, notes = log.Notes ?? string.Empty }
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.WeightLogs.FirstOrDefault(w => w.Id == id && w.UserId == userId);
            if (log == null)
                return Json(new { success = false });

            _context.WeightLogs.Remove(log);
            _context.SaveChanges();
            SyncCurrentWeightFromLogs(userId!);
            _context.SaveChanges();
            var entriesCount = _context.WeightLogs.Count(w => w.UserId == userId);
            return Json(new { success = true, message = "Weight entry removed", entriesCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(WeightLogEditEntryViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.WeightLogs.FirstOrDefault(w => w.Id == model.Id && w.UserId == userId);
            if (log != null)
            {
                var weightLbs = model.WeightLbs;
                var weightUnit = model.WeightUnit ?? "lbs";
                if (weightUnit == "kg")
                    weightLbs = weightLbs * 2.205m;
                log.WeightLbs = Math.Round(weightLbs, 2);
                log.Notes = model.Notes ?? string.Empty;
                _context.SaveChanges();
                SyncCurrentWeightFromLogs(userId!);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }

        private void SyncCurrentWeightFromLogs(string userId)
        {
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings == null) return;
            var latest = _context.WeightLogs
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.LogDate)
                .ThenByDescending(w => w.Id)
                .FirstOrDefault();
            settings.CurrentWeight = latest?.WeightLbs ?? 0;
        }
    }

    public class WeightLogAjaxRequest
    {
        public decimal WeightLbs { get; set; }
        public DateTime? LogDate { get; set; }
        public string? Notes { get; set; }
    }
}