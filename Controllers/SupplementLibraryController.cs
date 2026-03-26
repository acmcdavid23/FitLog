using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FitLog.Controllers
{
    public class SupplementLibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public SupplementLibraryController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index(string? search, string? category, string? evidenceLevel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var supplements = _context.SupplementLibraryItems.AsQueryable().Where(s => s.IsSystemItem);

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

            if (userId != null)
            {
                var personal = _context.SupplementLibraryItems
                    .Where(s => !s.IsSystemItem && s.CreatedByUserId == userId)
                    .OrderBy(s => s.Name)
                    .ToList();
                ViewBag.PersonalSupplements = personal;
            }

            var grouped = supplements
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList()
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(grouped);
        }

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

            ViewBag.PersonalizedDosing = personalizedDosing;
            return View(supplement);
        }

        private async Task<string> GetPersonalizedDosing(SupplementLibraryItem supplement, UserSettings settings)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
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

        [Authorize]
        public IActionResult CreatePersonal()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePersonal(SupplementLibraryItem item)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            item.IsSystemItem = false;
            item.CreatedByUserId = userId ?? string.Empty;
            ModelState.Remove("CreatedByUserId");

            if (ModelState.IsValid)
            {
                _context.SupplementLibraryItems.Add(item);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        [Authorize]
        public IActionResult EditPersonal(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = _context.SupplementLibraryItems.FirstOrDefault(s => s.Id == id && s.CreatedByUserId == userId);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult EditPersonal(int id, SupplementLibraryItem item)
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
            }
            return RedirectToAction(nameof(Index));
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
            item.IsSystemItem = true;
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