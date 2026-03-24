using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitLog.Controllers
{
    public class SupplementLibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SupplementLibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? search, string? category, string? evidenceLevel)
        {
            var supplements = _context.SupplementLibraryItems.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                supplements = supplements.Where(s => s.Name.Contains(search) || s.Description.Contains(search));

            if (!string.IsNullOrEmpty(category))
                supplements = supplements.Where(s => s.Category == category);

            if (!string.IsNullOrEmpty(evidenceLevel))
                supplements = supplements.Where(s => s.EvidenceLevel == evidenceLevel);

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.EvidenceLevel = evidenceLevel;
            ViewBag.Categories = new List<string> { "Performance", "Recovery", "Health", "Weight Management", "Vitamins & Minerals" };
            ViewBag.EvidenceLevels = new List<string> { "Strong", "Moderate", "Limited" };

            var grouped = supplements
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList()
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(grouped);
        }

        public IActionResult Details(int id)
        {
            var supplement = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (supplement == null) return NotFound();
            return View(supplement);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SupplementLibraryItem item)
        {
            if (ModelState.IsValid)
            {
                _context.SupplementLibraryItems.Add(item);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, SupplementLibraryItem item)
        {
            if (id != item.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.SupplementLibraryItems.Update(item);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                _context.SupplementLibraryItems.Remove(item);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}