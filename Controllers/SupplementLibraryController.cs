using FitLog.Configuration;
using FitLog.Data;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FitLog.Controllers
{
    [Authorize]
    public class SupplementLibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SupplementLibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        private SupplementLibraryIndexPageViewModel BuildSupplementLibraryGrouped(string? userId, string? search, string? category, string? evidenceLevel)
        {
            var supplements = _context.SupplementLibraryItems.AsQueryable().Where(s => s.IsSystemItem);

            if (!string.IsNullOrEmpty(search))
                supplements = supplements.Where(s => s.Name.Contains(search) || (s.Description != null && s.Description.Contains(search)));

            if (!string.IsNullOrEmpty(category))
                supplements = supplements.Where(s => s.Category == category);

            if (!string.IsNullOrEmpty(evidenceLevel))
                supplements = supplements.Where(s => s.EvidenceLevel == evidenceLevel);

            var groupedDict = supplements
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList()
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            var systemGrouped = groupedDict
                .OrderBy(kv => kv.Key)
                .Select(kv => new SupplementLibraryGroupedSectionViewModel
                {
                    Category = kv.Key,
                    Items = kv.Value.Select(SupplementLibraryCardViewModel.FromEntity).ToList()
                })
                .ToList();

            List<SupplementLibraryCardViewModel>? personalVm = null;
            if (userId != null)
            {
                personalVm = _context.SupplementLibraryItems
                    .Where(s => !s.IsSystemItem && s.CreatedByUserId == userId)
                    .OrderBy(s => s.Name)
                    .ToList()
                    .Select(SupplementLibraryCardViewModel.FromEntity)
                    .ToList();
            }

            return new SupplementLibraryIndexPageViewModel
            {
                SystemGrouped = systemGrouped,
                PersonalSupplements = personalVm
            };
        }

        private void SetSupplementLibraryFilterViewBag(string? search, string? category, string? evidenceLevel)
        {
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.EvidenceLevel = evidenceLevel;
            ViewBag.Categories = new List<string> { "Performance", "Recovery", "Health", "Weight Management", "Vitamins & Minerals" };
            ViewBag.EvidenceLevels = new List<string> { "Strong", "Moderate", "Limited" };
        }

        [AllowAnonymous]
        public IActionResult Index(string? search, string? category, string? evidenceLevel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetSupplementLibraryFilterViewBag(search, category, evidenceLevel);
            var page = BuildSupplementLibraryGrouped(userId, search, category, evidenceLevel);
            return View(page);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult LibraryMainPartial(string? search, string? category, string? evidenceLevel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetSupplementLibraryFilterViewBag(search, category, evidenceLevel);
            var page = BuildSupplementLibraryGrouped(userId, search, category, evidenceLevel);
            return PartialView("_SupplementLibraryMain", page);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var supplement = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (supplement == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string personalizedDosing = "";

            if (userId != null)
            {
                var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
                if (settings != null && settings.CurrentWeight > 0)
                {
                    personalizedDosing = await GetPersonalizedDosing(supplement, settings);
                }
            }

            var vm = SupplementLibraryItemDetailsViewModel.FromEntity(supplement, personalizedDosing);
            return View(vm);
        }

        private async Task<string> GetPersonalizedDosing(SupplementLibraryItem supplement, UserSettings settings)
        {
            try
            {
                var apiKey = OpenAiApiKeyResolver.Resolve();
                if (string.IsNullOrEmpty(apiKey)) return "";

                var prompt = $@"Given a person with these stats:
- Weight: {settings.CurrentWeight} {settings.WeightUnit}
- Goal: {settings.FitnessGoal}, {settings.BodyGoal}
- Calorie Goal: {settings.CalorieGoal} kcal

What is the personalized recommended dosage for {supplement.Name}?
Standard dosage is: {supplement.RecommendedDosage}
When to take: {supplement.WhenToTake}

Provide a 2-3 sentence personalized dosing recommendation with specific amounts based on their weight and goals. Be concise and practical.";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a sports nutritionist. Give brief, specific, personalized supplement dosing advice." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 150
                };

                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                return jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch { return ""; }
        }

        public IActionResult CreatePersonal()
        {
            return View(new SupplementLibraryItemCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePersonal(SupplementLibraryItemCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
                return View(model);

            var item = model.ToEntity(isSystemItem: false, createdByUserId: userId);
            _context.SupplementLibraryItems.Add(item);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult EditPersonal(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id && s.CreatedByUserId == userId);
            if (item == null) return NotFound();
            ViewBag.FormAction = nameof(EditPersonal);
            return View("Edit", SupplementLibraryItemEditViewModel.FromEntity(item));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditPersonal(int id, SupplementLibraryItemEditViewModel model)
        {
            if (id != model.Id) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id && s.CreatedByUserId == userId);
            if (item == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.FormAction = nameof(EditPersonal);
                return View("Edit", model);
            }

            model.ApplyTo(item);
            _context.SupplementLibraryItems.Update(item);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePersonal(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id && s.CreatedByUserId == userId);
            if (item != null)
            {
                _context.SupplementLibraryItems.Remove(item);
                _context.SaveChanges();
                TempData["Success"] = "Deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePersonalAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id && s.CreatedByUserId == userId);
            if (item != null)
            {
                _context.SupplementLibraryItems.Remove(item);
                _context.SaveChanges();
            }
            return Json(new { success = true, message = "Removed." });
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new SupplementLibraryItemCreateViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SupplementLibraryItemCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.SupplementLibraryItems.Add(model.ToEntity(isSystemItem: true, createdByUserId: null));
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (item == null) return NotFound();
            ViewBag.FormAction = nameof(Edit);
            return View(SupplementLibraryItemEditViewModel.FromEntity(item));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, SupplementLibraryItemEditViewModel model)
        {
            if (id != model.Id) return NotFound();
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (item == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.FormAction = nameof(Edit);
                return View(model);
            }

            model.ApplyTo(item);
            _context.SupplementLibraryItems.Update(item);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
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

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                _context.SupplementLibraryItems.Remove(item);
                _context.SaveChanges();
            }
            return Json(new { success = true, message = "Deleted." });
        }
    }
}