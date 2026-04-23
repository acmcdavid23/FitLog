using FitLog.Data;
using FitLog.Models;
using FitLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FitLog.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IWebHostEnvironment _env;
        private readonly ImageProcessService _images;

        public SettingsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IWebHostEnvironment env, ImageProcessService images)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
            _images = images;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId)
                ?? new UserSettings { UserId = userId ?? string.Empty };
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(UserSettings settings)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            settings.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");

            // Check username uniqueness
            if (!string.IsNullOrEmpty(settings.Username))
            {
                var taken = _context.UserSettings.Any(s => s.Username == settings.Username && s.UserId != userId);
                if (taken)
                {
                    TempData["Error"] = "That username is already taken. Please choose another.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (ModelState.IsValid)
            {
                var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
                if (existing != null)
                {
                    existing.CalorieGoal = settings.CalorieGoal;
                    existing.ProteinGoal = settings.ProteinGoal;
                    existing.CarbGoal = settings.CarbGoal;
                    existing.FatGoal = settings.FatGoal;
                    existing.WaterGoal = settings.WaterGoal;
                    existing.DisplayName = settings.DisplayName;
                    existing.Username = settings.Username;
                    existing.WeightUnit = settings.WeightUnit;
                    existing.FitnessGoal = settings.FitnessGoal;
                    existing.BodyGoal = settings.BodyGoal;
                    existing.ShowOnLeaderboard = settings.ShowOnLeaderboard;
                    existing.Age = settings.Age;
                    existing.Gender = settings.Gender;
                    existing.CurrentWeight = settings.CurrentWeight;
                    existing.GoalWeight = settings.GoalWeight;
                    existing.HeightInches = settings.HeightInches;
                    existing.ActivityLevel = string.IsNullOrWhiteSpace(settings.ActivityLevel)
                        ? "Moderate"
                        : settings.ActivityLevel;
                    existing.CityRegion = settings.CityRegion ?? string.Empty;
                    existing.ProfileImageUrl = string.IsNullOrWhiteSpace(settings.ProfileImageUrl)
                        ? existing.ProfileImageUrl
                        : settings.ProfileImageUrl;
                }
                else { _context.UserSettings.Add(settings); }
                _context.SaveChanges();
                TempData["Success"] = "Settings saved successfully!";
            }
            else
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                TempData["Error"] = string.IsNullOrEmpty(err) ? "Could not save settings. Please check the form." : err;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult CalculateMacros([FromBody] MacroCalcRequest request)
        {
            try
            {
                if (request == null || request.HeightInches <= 0 || request.CurrentWeight <= 0 || request.Age <= 0)
                    return Json(new { ok = false, error = "Please fill in your age, height, and current weight in Body Stats first." });

                // Convert to metric for Mifflin-St Jeor
                double weightKg = request.WeightUnit?.ToLower() == "kg"
                    ? (double)request.CurrentWeight
                    : (double)request.CurrentWeight * 0.453592;
                double heightCm = (double)request.HeightInches * 2.54;

                // Mifflin-St Jeor BMR
                double bmr = request.Gender?.ToLower() == "female"
                    ? 10 * weightKg + 6.25 * heightCm - 5 * request.Age - 161
                    : 10 * weightKg + 6.25 * heightCm - 5 * request.Age + 5;

                // Activity multiplier
                double multiplier = request.ActivityLevel switch
                {
                    "Sedentary" => 1.2,
                    "Light" => 1.375,
                    "Active" => 1.725,
                    "VeryActive" => 1.9,
                    _ => 1.55 // Moderate default
                };

                double tdee = bmr * multiplier;

                // Adjust for body goal
                double targetCalories = request.BodyGoal switch
                {
                    "Bulk" => tdee + 300,
                    "Cut" => tdee - 400,
                    _ => tdee // Maintain
                };

                targetCalories = Math.Round(targetCalories / 50) * 50;

                // Macro split based on fitness goal
                double proteinPct, carbPct, fatPct;
                switch (request.FitnessGoal)
                {
                    case "Weight Loss":
                        proteinPct = 0.40; carbPct = 0.35; fatPct = 0.25; break;
                    case "Strength":
                        proteinPct = 0.25; carbPct = 0.55; fatPct = 0.20; break;
                    case "Hypertrophy":
                        proteinPct = 0.30; carbPct = 0.50; fatPct = 0.20; break;
                    default:
                        proteinPct = 0.30; carbPct = 0.45; fatPct = 0.25; break;
                }

                int protein = (int)Math.Round(targetCalories * proteinPct / 4);
                int carbs = (int)Math.Round(targetCalories * carbPct / 4);
                int fat = (int)Math.Round(targetCalories * fatPct / 9);

                return Json(new
                {
                    ok = true,
                    data = new
                    {
                        calories = (int)targetCalories,
                        protein,
                        carbs,
                        fat,
                        bmr = (int)Math.Round(bmr),
                        tdee = (int)Math.Round(tdee)
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetUsername(string username)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            username = (username ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 50)
            {
                TempData["UsernameModalError"] = "Username must be 3–50 characters.";
                return RedirectToAction("Index", "Home");
            }
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            {
                TempData["UsernameModalError"] = "Username can only contain letters and numbers.";
                return RedirectToAction("Index", "Home");
            }
            var taken = _context.UserSettings.Any(s => s.Username == username && s.UserId != userId);
            if (taken)
            {
                TempData["UsernameModalError"] = "That username is already taken.";
                return RedirectToAction("Index", "Home");
            }
            var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (existing == null)
            {
                _context.UserSettings.Add(new UserSettings
                {
                    UserId = userId ?? string.Empty,
                    Username = username,
                    DisplayName = "Athlete"
                });
            }
            else
                existing.Username = username;
            _context.SaveChanges();
            TempData["Success"] = "Username saved!";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile photo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (photo == null || photo.Length == 0)
            {
                TempData["Error"] = "Please choose a photo.";
                return RedirectToAction(nameof(Index));
            }
            if (!ImageProcessService.IsAllowedImage(photo))
            {
                TempData["Error"] = "Use JPG, PNG, WebP, GIF, or BMP up to 15 MB.";
                return RedirectToAction(nameof(Index));
            }
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings == null)
            {
                TempData["Error"] = "Save your settings once before uploading a photo.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, userId + ".jpg");
                await _images.SaveSquareJpegAsync(photo, path);
                // Cache-bust so browsers always load the newly written file (same path as before).
                settings.ProfileImageUrl = $"/uploads/profiles/{userId}.jpg?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                _context.SaveChanges();
                TempData["Success"] = "Profile photo updated.";
            }
            catch
            {
                TempData["Error"] = "Could not process that image. Try a different file.";
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult DeleteAccount() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccountConfirmed(string confirmText)
        {
            if (confirmText != "DELETE")
            {
                TempData["Error"] = "Please type DELETE to confirm.";
                return RedirectToAction(nameof(DeleteAccount));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            _context.WeightLogs.RemoveRange(_context.WeightLogs.Where(w => w.UserId == userId));
            _context.NutritionLogs.RemoveRange(_context.NutritionLogs.Where(n => n.UserId == userId));
            _context.WaterLogs.RemoveRange(_context.WaterLogs.Where(w => w.UserId == userId));
            _context.SupplementLogs.RemoveRange(_context.SupplementLogs.Where(s => s.UserId == userId));
            _context.Supplements.RemoveRange(_context.Supplements.Where(s => s.UserId == userId));
            _context.WorkoutEntries.RemoveRange(_context.WorkoutEntries.Where(w => w.UserId == userId));
            _context.WorkoutSessions.RemoveRange(_context.WorkoutSessions.Where(w => w.UserId == userId));
            _context.FriendRequests.RemoveRange(_context.FriendRequests.Where(f => f.SenderId == userId || f.ReceiverId == userId));
            _context.GroupMembers.RemoveRange(_context.GroupMembers.Where(m => m.UserId == userId));
            _context.UserSettings.RemoveRange(_context.UserSettings.Where(s => s.UserId == userId));
            _context.SaveChanges();

            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);
            return RedirectToAction("Index", "Home");
        }
    }

    public class MacroCalcRequest
    {
        public int Age { get; set; }
        public string Gender { get; set; } = "Male";
        public decimal HeightInches { get; set; }
        public decimal CurrentWeight { get; set; }
        public string WeightUnit { get; set; } = "lbs";
        public string ActivityLevel { get; set; } = "Moderate";
        public string BodyGoal { get; set; } = "Maintain";
        public string FitnessGoal { get; set; } = "General Fitness";
    }
}