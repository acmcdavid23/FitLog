using FitLog.Data;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class SupplementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAntiforgery _antiforgery;

        public SupplementController(ApplicationDbContext context, IAntiforgery antiforgery)
        {
            _context = context;
            _antiforgery = antiforgery;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var supplements = _context.Supplements
                .Where(s => s.UserId == userId && s.IsActive)
                .ToList();

            var todayLogs = _context.SupplementLogs
                .Where(l => l.UserId == userId && l.LogDate == today)
                .ToList();

            var library = _context.SupplementLibraryItems
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList();

            var takenIds = todayLogs.Where(l => l.IsTaken).Select(l => l.SupplementId).ToHashSet();
            var libraryByCategory = library
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Select(SupplementLibraryBrowseCardViewModel.FromEntity).ToList()))
                .ToList();

            var vm = new SupplementJournalPageViewModel
            {
                ActiveSupplements = supplements.Select(SupplementJournalRowViewModel.FromEntity).ToList(),
                TakenSupplementIdsToday = takenIds,
                Today = today,
                LibraryByCategory = libraryByCategory
            };

            return View(vm);
        }

        public IActionResult Manage()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var supplements = _context.Supplements
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderBy(s => s.Name)
                .ToList()
                .Select(SupplementJournalRowViewModel.FromEntity)
                .ToList();

            return View(new SupplementManagePageViewModel { Supplements = supplements });
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Json(new { token = tokens.RequestToken });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAjax(int supplementId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var existing = _context.SupplementLogs
                .FirstOrDefault(l => l.UserId == userId && l.SupplementId == supplementId && l.LogDate == today);

            bool isTaken;
            if (existing != null)
            {
                _context.SupplementLogs.Remove(existing);
                isTaken = false;
            }
            else
            {
                _context.SupplementLogs.Add(new SupplementLog
                {
                    UserId = userId ?? string.Empty,
                    SupplementId = supplementId,
                    LogDate = today,
                    IsTaken = true,
                    TimeTaken = DateTime.Now
                });
                isTaken = true;
            }

            _context.SaveChanges();

            var total = _context.Supplements.Count(s => s.UserId == userId && s.IsActive);
            var taken = _context.SupplementLogs.Count(l => l.UserId == userId && l.LogDate == today && l.IsTaken);

            return Json(new { isTaken, supplementId, taken, total });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(UserSupplementCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var supplement = model.ToEntity(userId);
            _context.Supplements.Add(supplement);
            _context.SaveChanges();
            TempData["Success"] = $"{supplement.Name} added to your supplements!";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAjax(UserSupplementCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid supplement." : err });
            }

            var supplement = model.ToEntity(userId);
            _context.Supplements.Add(supplement);
            _context.SaveChanges();
            return Json(new
            {
                success = true,
                message = "Supplement added",
                supplement = new
                {
                    supplement.Id,
                    supplement.Name,
                    supplement.Dosage,
                    supplement.TimeToTake,
                    notes = supplement.Notes ?? string.Empty
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var supplement = _context.Supplements
                .FirstOrDefault(s => s.Id == id && s.UserId == userId);

            if (supplement != null)
            {
                _context.Supplements.Remove(supplement);
                _context.SaveChanges();
                TempData["Success"] = "Deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var supplement = _context.Supplements
                .FirstOrDefault(s => s.Id == id && s.UserId == userId);

            if (supplement == null)
                return Json(new { success = false, message = "Not found." });

            var deletedId = supplement.Id;
            _context.Supplements.Remove(supplement);
            _context.SaveChanges();

            return Json(new { success = true, message = "Supplement removed", deletedId });
        }
    }
}