using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FitLog.Controllers
{
    [Authorize]
    public class NutritionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NutritionController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private UserSettings GetUserSettings(string userId)
        {
            return _context.UserSettings.FirstOrDefault(s => s.UserId == userId)
                ?? new UserSettings { UserId = userId };
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;
            var settings = GetUserSettings(userId!);

            var todayLogs = _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate == today)
                .OrderBy(n => n.MealName)
                .ToList();

            ViewBag.TotalCalories = todayLogs.Sum(n => n.Calories);
            ViewBag.TotalProtein = Math.Round(todayLogs.Sum(n => n.Protein), 1);
            ViewBag.TotalCarbs = Math.Round(todayLogs.Sum(n => n.Carbs), 1);
            ViewBag.TotalFat = Math.Round(todayLogs.Sum(n => n.Fat), 1);
            ViewBag.Grouped = todayLogs.GroupBy(n => n.MealName).ToDictionary(g => g.Key, g => g.ToList());
            ViewBag.Today = today;
            ViewBag.CalorieGoal = settings.CalorieGoal;
            ViewBag.ProteinGoal = settings.ProteinGoal;
            ViewBag.CarbGoal = settings.CarbGoal;
            ViewBag.FatGoal = settings.FatGoal;

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
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> EstimateMacros([FromBody] MacroEstimateRequest request)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return Json(new { calories = 0, protein = 0, carbs = 0, fat = 0, note = "AI unavailable" });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

                // Build prompt with serving size/weight context if provided
                var prompt = new StringBuilder();
                prompt.AppendLine($"Estimate the macros for: \"{request.FoodDescription}\"");
                if (!string.IsNullOrEmpty(request.ServingSize))
                    prompt.AppendLine($"Serving size: {request.ServingSize}");
                if (settings != null)
                    prompt.AppendLine($"User context: {settings.FitnessGoal} goal, {settings.CurrentWeight} {settings.WeightUnit}");
                prompt.AppendLine("Reply ONLY with JSON in this exact format, no other text:");
                prompt.AppendLine("{\"calories\": 500, \"protein\": 35.5, \"carbs\": 45.0, \"fat\": 12.5, \"note\": \"Estimated for standard serving.\"}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a nutritionist. Always respond with ONLY valid JSON for macro estimates. No extra text, no markdown." },
                        new { role = "user", content = prompt.ToString() }
                    },
                    max_tokens = 150
                };

                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                var content = jsonDoc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "{}";

                var clean = content.Replace("```json", "").Replace("```", "").Trim();
                var result = JsonDocument.Parse(clean);

                return Json(new
                {
                    calories = result.RootElement.GetProperty("calories").GetInt32(),
                    protein = result.RootElement.GetProperty("protein").GetDouble(),
                    carbs = result.RootElement.GetProperty("carbs").GetDouble(),
                    fat = result.RootElement.GetProperty("fat").GetDouble(),
                    note = result.RootElement.TryGetProperty("note", out var noteEl) ? noteEl.GetString() : ""
                });
            }
            catch
            {
                return Json(new { calories = 0, protein = 0, carbs = 0, fat = 0, note = "Could not estimate. Please enter manually." });
            }
        }

        // Returns AI food suggestions for autocomplete
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SuggestFoods([FromBody] FoodSuggestionRequest request)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return Json(new { suggestions = new string[] { } });

                using var client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = "You suggest common food items for a fitness tracker. Reply ONLY with a JSON array of 5 food name strings. Example: [\"Grilled Chicken Breast (4oz)\", \"Brown Rice (1 cup)\"]" },
                        new { role = "user", content = $"Suggest 5 common foods matching: \"{request.Query}\". Include typical serving sizes in parentheses." }
                    },
                    max_tokens = 100
                };

                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                var content = jsonDoc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "[]";

                var clean = content.Replace("```json", "").Replace("```", "").Trim();
                var suggestions = JsonDocument.Parse(clean);
                return Json(new { suggestions = suggestions.RootElement });
            }
            catch
            {
                return Json(new { suggestions = new string[] { } });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult AddAI([FromBody] AIFoodLogRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var log = new NutritionLog
            {
                UserId = userId ?? string.Empty,
                LogDate = DateTime.Today,
                MealName = request.MealName,
                FoodItem = request.FoodItem,
                Calories = request.Calories,
                Protein = (decimal)request.Protein,
                Carbs = (decimal)request.Carbs,
                Fat = (decimal)request.Fat,
                ServingSize = request.ServingSize
            };

            _context.NutritionLogs.Add(log);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.NutritionLogs.FirstOrDefault(n => n.Id == id && n.UserId == userId);
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
            var history = _context.NutritionLogs
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

            ViewBag.History = history;
            return View();
        }
    }

    public class MacroEstimateRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
        public string ServingSize { get; set; } = string.Empty;
    }

    public class FoodSuggestionRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class AIFoodLogRequest
    {
        public string MealName { get; set; } = string.Empty;
        public string FoodItem { get; set; } = string.Empty;
        public int Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public string ServingSize { get; set; } = string.Empty;
    }
}