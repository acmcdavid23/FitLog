using FitLog.Configuration;
using FitLog.Data;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace FitLog.Controllers
{
    [Authorize]
    public class NutritionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        private static readonly HashSet<string> ValidNutritionMealNames = new(StringComparer.Ordinal)
        {
            "Breakfast", "Lunch", "Dinner", "Snack", "Pre-Workout", "Post-Workout"
        };

        private static bool IsValidNutritionMealName(string? mealName) =>
            !string.IsNullOrWhiteSpace(mealName) && ValidNutritionMealNames.Contains(mealName.Trim());

        public NutritionController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private UserSettings GetUserSettings(string userId)
        {
            return _context.UserSettings.FirstOrDefault(s => s.UserId == userId)
                ?? new UserSettings { UserId = userId };
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;
            var settings = GetUserSettings(userId!);

            var todayLogs = await _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate == today)
                .OrderBy(n => n.MealName)
                .ToListAsync();

            var groupedRows = todayLogs
                .GroupBy(n => n.MealName)
                .ToDictionary(g => g.Key, g => g.Select(NutritionLogRowViewModel.FromEntity).ToList());

            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            var logs = await _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate >= sevenDaysAgo && n.LogDate <= DateTime.Today)
                .ToListAsync();

            var grouped = logs.GroupBy(n => n.LogDate.Date)
                .ToDictionary(g => g.Key, g => new
                {
                    Protein = g.Sum(x => x.Protein),
                    Carbs = g.Sum(x => x.Carbs),
                    Fat = g.Sum(x => x.Fat)
                });

            var proteinList = new List<decimal>();
            var carbsList = new List<decimal>();
            var fatList = new List<decimal>();
            var labelsList = new List<string>();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                labelsList.Add(date.ToString("ddd"));
                if (grouped.TryGetValue(date.Date, out var dayData))
                {
                    proteinList.Add(Math.Round(dayData.Protein, 1));
                    carbsList.Add(Math.Round(dayData.Carbs, 1));
                    fatList.Add(Math.Round(dayData.Fat, 1));
                }
                else
                {
                    proteinList.Add(0);
                    carbsList.Add(0);
                    fatList.Add(0);
                }
            }

            var vm = new NutritionIndexPageViewModel
            {
                GroupedByMeal = groupedRows,
                TotalCalories = todayLogs.Sum(n => n.Calories),
                TotalProtein = Math.Round(todayLogs.Sum(n => n.Protein), 1),
                TotalCarbs = Math.Round(todayLogs.Sum(n => n.Carbs), 1),
                TotalFat = Math.Round(todayLogs.Sum(n => n.Fat), 1),
                Today = today,
                CalorieGoal = settings.CalorieGoal,
                ProteinGoal = settings.ProteinGoal,
                CarbGoal = settings.CarbGoal,
                FatGoal = settings.FatGoal,
                WeeklyProtein = proteinList,
                WeeklyCarbs = carbsList,
                WeeklyFat = fatList,
                WeeklyLabels = labelsList
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> WeeklyMacroDebug()
        {
            var userId = _userManager.GetUserId(User);
            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            var logs = await _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate >= sevenDaysAgo && n.LogDate <= DateTime.Today)
                .ToListAsync();

            var grouped = logs.GroupBy(n => n.LogDate.Date)
                              .Select(g => new {
                                  Date = g.Key.ToString("yyyy-MM-dd"),
                                  Protein = g.Sum(x => x.Protein),
                                  Carbs = g.Sum(x => x.Carbs),
                                  Fat = g.Sum(x => x.Fat),
                                  Count = g.Count()
                              })
                              .OrderBy(x => x.Date)
                              .ToList();

            return Json(new { logs = logs.Count, grouped });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(NutritionLogCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!IsValidNutritionMealName(model.MealName))
                ModelState.AddModelError(nameof(model.MealName), "Please choose a meal.");
            if (!ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstErr))
                    TempData["Error"] = firstErr;
                return RedirectToAction(nameof(Index));
            }

            var log = model.ToEntity(userId, DateTime.Today);
            _context.NutritionLogs.Add(log);
            _context.SaveChanges();
            TempData["Success"] = $"{model.FoodItem} added to {model.MealName}";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAjax(NutritionLogCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!IsValidNutritionMealName(model.MealName))
                return Json(new { success = false, error = "Please choose a meal." });

            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid food entry." : err });
            }

            var log = model.ToEntity(userId, DateTime.Today);
            _context.NutritionLogs.Add(log);
            _context.SaveChanges();
            var itemDto = new
            {
                id = log.Id,
                mealName = log.MealName,
                foodItem = log.FoodItem,
                calories = log.Calories,
                protein = log.Protein,
                carbs = log.Carbs,
                fat = log.Fat,
                servingSize = log.ServingSize ?? string.Empty
            };
            var addMsg = $"{model.FoodItem} added to {model.MealName}";
            return Json(new { success = true, message = addMsg, item = itemDto, data = new { item = itemDto } });
        }

        // Returns AI food suggestions for autocomplete
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SuggestFoods([FromBody] FoodSuggestionRequest request)
        {
            try
            {
                var apiKey = OpenAiApiKeyResolver.Resolve();
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
            if (request == null)
                return Json(new { success = false, error = "Invalid request." });
            if (!IsValidNutritionMealName(request.MealName))
                return Json(new { success = false, error = "Please choose a meal." });

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

            var itemDto = new
            {
                id = log.Id,
                mealName = log.MealName,
                foodItem = log.FoodItem,
                calories = log.Calories,
                protein = log.Protein,
                carbs = log.Carbs,
                fat = log.Fat,
                servingSize = log.ServingSize ?? string.Empty
            };
            var addMsgAi = $"{request.FoodItem} added to {request.MealName}";
            return Json(new { success = true, message = addMsgAi, item = itemDto, data = new { item = itemDto } });
        }

        [HttpGet]
        public async Task<IActionResult> LookupBarcode(string code)
        {
            try
            {
                var normalized = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
                if (normalized.Length < 8 || normalized.Length > 14)
                    return Json(new { success = false, message = "Invalid barcode." });

                using var client = new HttpClient();
                var url = $"https://world.openfoodfacts.org/api/v2/product/{normalized}.json";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return Json(new { success = false, message = "Barcode lookup failed." });

                var payload = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (!root.TryGetProperty("status", out var statusEl) || statusEl.GetInt32() != 1)
                    return Json(new { success = false, message = "Product not found." });

                if (!root.TryGetProperty("product", out var product))
                    return Json(new { success = false, message = "Product not found." });

                var primary = ParseOpenFoodFactsProduct(product, normalized);
                if (primary == null || !primary.HasAnyMacros)
                    return Json(new { success = false, message = "Nutrition data unavailable for this barcode." });

                var alternatives = new List<PackagedFoodResult>();
                var searchSeed = $"{primary.Brand} {primary.Name}".Trim();
                if (!string.IsNullOrWhiteSpace(searchSeed))
                {
                    var relatedUrl = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(searchSeed)}&search_simple=1&action=process&json=1&page_size=12";
                    var relatedRes = await client.GetAsync(relatedUrl);
                    if (relatedRes.IsSuccessStatusCode)
                    {
                        var relatedPayload = await relatedRes.Content.ReadAsStringAsync();
                        using var relatedDoc = JsonDocument.Parse(relatedPayload);
                        if (relatedDoc.RootElement.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var p in products.EnumerateArray())
                            {
                                var item = ParseOpenFoodFactsProduct(p);
                                if (item == null || !item.HasAnyMacros) continue;
                                if (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode == primary.Barcode) continue;
                                alternatives.Add(item);
                            }
                        }
                    }
                }
                alternatives = DeduplicateAndSort(alternatives).Take(8).ToList();

                return Json(new
                {
                    success = true,
                    product = primary,
                    alternatives
                });
            }
            catch
            {
                return Json(new { success = false, message = "Could not look up that barcode right now." });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchPackagedFoods(string query)
        {
            try
            {
                var q = (query ?? string.Empty).Trim();
                if (q.Length < 2)
                    return Json(new { items = Array.Empty<object>() });

                using var client = new HttpClient();
                var usdaApiKey = Environment.GetEnvironmentVariable("USDA_API_KEY") ?? "DEMO_KEY";
                var url = $"https://api.nal.usda.gov/fdc/v1/foods/search?query={Uri.EscapeDataString(q)}&pageSize=10&api_key={Uri.EscapeDataString(usdaApiKey)}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return Json(new { items = Array.Empty<object>() });

                var payload = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(payload);
                var items = new List<object>();
                var textInfo = CultureInfo.InvariantCulture.TextInfo;
                if (doc.RootElement.TryGetProperty("foods", out var foods) && foods.ValueKind == JsonValueKind.Array)
                {
                    foreach (var food in foods.EnumerateArray())
                    {
                        var rawName = (GetString(food, "description") ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(rawName))
                            continue;

                        var name = textInfo.ToTitleCase(rawName.ToLowerInvariant());
                        var brand = (GetString(food, "brandOwner") ?? GetString(food, "brandName") ?? string.Empty).Trim();

                        double calories100g = 0, protein100g = 0, carbs100g = 0, fat100g = 0;
                        if (food.TryGetProperty("foodNutrients", out var nutrients) && nutrients.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var n in nutrients.EnumerateArray())
                            {
                                var nutrientName = (GetString(n, "nutrientName") ?? string.Empty).Trim();
                                var unitName = (GetString(n, "unitName") ?? string.Empty).Trim();
                                var val = GetNumber(n, "value") ?? 0;

                                if (nutrientName.Contains("Energy", StringComparison.OrdinalIgnoreCase) &&
                                    unitName.Equals("KCAL", StringComparison.OrdinalIgnoreCase))
                                    calories100g = val;
                                else if (nutrientName.Equals("Protein", StringComparison.OrdinalIgnoreCase))
                                    protein100g = val;
                                else if (nutrientName.Contains("Carbohydrate", StringComparison.OrdinalIgnoreCase))
                                    carbs100g = val;
                                else if (nutrientName.Contains("Total lipid", StringComparison.OrdinalIgnoreCase))
                                    fat100g = val;
                            }
                        }

                        var servingSizeVal = GetNumber(food, "servingSize");
                        var servingSizeUnit = (GetString(food, "servingSizeUnit") ?? string.Empty).Trim();

                        items.Add(new
                        {
                            name,
                            brand,
                            calories100g = (int)Math.Round(calories100g),
                            protein100g = Math.Round(protein100g, 2),
                            carbs100g = Math.Round(carbs100g, 2),
                            fat100g = Math.Round(fat100g, 2),
                            servingSize = servingSizeVal,
                            servingSizeUnit
                        });
                    }
                }
                return Json(new { items });
            }
            catch
            {
                return Json(new { items = Array.Empty<object>() });
            }
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
                TempData["Success"] = "Food entry removed";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = _context.NutritionLogs.FirstOrDefault(n => n.Id == id && n.UserId == userId);
            if (log == null)
                return Json(new { success = false, message = "Entry not found." });

            _context.NutritionLogs.Remove(log);
            _context.SaveChanges();
            return Json(new { success = true, message = "Food entry removed" });
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

        private static string? GetString(JsonElement obj, string name)
        {
            if (!obj.TryGetProperty(name, out var el)) return null;
            return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
        }

        private static double? GetNumber(JsonElement obj, string name)
        {
            if (!obj.TryGetProperty(name, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n))
                return n;
            if (el.ValueKind == JsonValueKind.String &&
                double.TryParse(el.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var s))
                return s;
            return null;
        }

        private static PackagedFoodResult? ParseOpenFoodFactsProduct(JsonElement product, string? fallbackBarcode = null)
        {
            var name = (GetString(product, "product_name") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;

            var barcode = (GetString(product, "code") ?? fallbackBarcode ?? "").Trim();
            var brand = (GetString(product, "brands") ?? "").Trim();
            var servingSize = (GetString(product, "serving_size") ?? "").Trim();

            var hasNutriments = product.TryGetProperty("nutriments", out var nutriments) && nutriments.ValueKind == JsonValueKind.Object;

            double? N(string key) => hasNutriments ? GetNumber(nutriments, key) : null;

            var cServ = N("energy-kcal_serving") ?? N("energy-kcal");
            var pServ = N("proteins_serving") ?? N("proteins");
            var carbServ = N("carbohydrates_serving") ?? N("carbohydrates");
            var fServ = N("fat_serving") ?? N("fat");

            var c100 = N("energy-kcal_100g") ?? N("energy-kcal");
            var p100 = N("proteins_100g") ?? N("proteins");
            var carb100 = N("carbohydrates_100g") ?? N("carbohydrates");
            var f100 = N("fat_100g") ?? N("fat");

            return new PackagedFoodResult
            {
                Barcode = barcode,
                Name = string.IsNullOrWhiteSpace(brand) ? name : $"{brand} {name}",
                Brand = brand,
                ServingSize = string.IsNullOrWhiteSpace(servingSize) ? "1 serving" : servingSize,
                CaloriesServing = cServ.HasValue ? (int)Math.Round(cServ.Value) : 0,
                ProteinServing = pServ.HasValue ? Math.Round(pServ.Value, 1) : 0,
                CarbsServing = carbServ.HasValue ? Math.Round(carbServ.Value, 1) : 0,
                FatServing = fServ.HasValue ? Math.Round(fServ.Value, 1) : 0,
                Calories100g = c100.HasValue ? (int)Math.Round(c100.Value) : 0,
                Protein100g = p100.HasValue ? Math.Round(p100.Value, 1) : 0,
                Carbs100g = carb100.HasValue ? Math.Round(carb100.Value, 1) : 0,
                Fat100g = f100.HasValue ? Math.Round(f100.Value, 1) : 0,
                IsVerified = !string.IsNullOrWhiteSpace(barcode) &&
                             !string.IsNullOrWhiteSpace(brand) &&
                             (cServ.HasValue || c100.HasValue),
                Source = "Open Food Facts",
                QualityScore =
                    (!string.IsNullOrWhiteSpace(barcode) ? 20 : 0) +
                    (!string.IsNullOrWhiteSpace(brand) ? 10 : 0) +
                    (cServ.HasValue ? 10 : 0) +
                    (pServ.HasValue ? 5 : 0) +
                    (carbServ.HasValue ? 5 : 0) +
                    (fServ.HasValue ? 5 : 0) +
                    (c100.HasValue ? 6 : 0) +
                    (p100.HasValue ? 3 : 0) +
                    (carb100.HasValue ? 3 : 0) +
                    (f100.HasValue ? 3 : 0)
            };
        }

        private static List<PackagedFoodResult> DeduplicateAndSort(IEnumerable<PackagedFoodResult> items)
        {
            var deduped = items
                .GroupBy(i =>
                    !string.IsNullOrWhiteSpace(i.Barcode)
                        ? $"bc:{i.Barcode}"
                        : $"nm:{(i.Brand ?? "").Trim().ToLowerInvariant()}|{(i.Name ?? "").Trim().ToLowerInvariant()}")
                .Select(g => g
                    .OrderByDescending(x => x.QualityScore)
                    .ThenByDescending(x => x.IsVerified)
                    .ThenByDescending(x => x.CaloriesServing + x.ProteinServing + x.CarbsServing + x.FatServing)
                    .First())
                .OrderByDescending(x => x.QualityScore)
                .ThenByDescending(x => x.IsVerified)
                .ThenBy(x => x.Name)
                .ToList();

            return deduped;
        }
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

    public class PackagedFoodResult
    {
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string Source { get; set; } = "Community";
        public int QualityScore { get; set; }
        public string ServingSize { get; set; } = "1 serving";
        public int CaloriesServing { get; set; }
        public double ProteinServing { get; set; }
        public double CarbsServing { get; set; }
        public double FatServing { get; set; }
        public int Calories100g { get; set; }
        public double Protein100g { get; set; }
        public double Carbs100g { get; set; }
        public double Fat100g { get; set; }

        public bool HasAnyMacros =>
            CaloriesServing > 0 || ProteinServing > 0 || CarbsServing > 0 || FatServing > 0 ||
            Calories100g > 0 || Protein100g > 0 || Carbs100g > 0 || Fat100g > 0;
    }

}