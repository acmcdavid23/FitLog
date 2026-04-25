using FitLog.Data;
using FitLog.Helpers;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class OnboardingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OnboardingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Step 1 - Welcome (display name captured at registration)
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            // Fully completed onboarding � go to dashboard
            if (existing != null && existing.HeightInches > 0)
                return RedirectToAction("Index", "Dashboard");

            string displayName = existing?.DisplayName ?? string.Empty;
            TempData["DisplayName"] = displayName;

            return View(new OnboardingStep1ViewModel { DisplayName = displayName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step1(OnboardingStep1ViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            TempData["DisplayName"] = existing?.DisplayName ?? model.DisplayName ?? string.Empty;
            return RedirectToAction(nameof(Step2));
        }

        // Step 2 - Body Stats + Age + Gender
        public IActionResult Step2()
        {
            if (TempData.Peek("DisplayName") == null)
                return RedirectToAction(nameof(Index));
            TempData.Keep();
            return View(new OnboardingBodyStatsViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step2Post(OnboardingBodyStatsViewModel model)
        {
            decimal totalInches = (model.HeightFeet * 12) + model.HeightInches;
            TempData["CurrentWeight"] = model.CurrentWeight.ToString();
            TempData["GoalWeight"] = model.GoalWeight.ToString();
            TempData["HeightInches"] = totalInches.ToString();
            TempData["GoalTimeframeWeeks"] = model.GoalTimeframeWeeks.ToString();
            TempData["WeightUnit"] = model.WeightUnit;
            TempData["Age"] = model.Age.ToString();
            TempData["Gender"] = model.Gender;
            TempData["DisplayName"] = TempData.Peek("DisplayName");
            return RedirectToAction(nameof(Step3));
        }

        // Step 3 - Fitness Goal
        public IActionResult Step3()
        {
            if (TempData.Peek("CurrentWeight") == null)
                return RedirectToAction(nameof(Step2));
            TempData.Keep();
            return View(new OnboardingGoalsViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step3Post(OnboardingGoalsViewModel model)
        {
            TempData["FitnessGoal"] = model.FitnessGoal;
            TempData["BodyGoal"] = model.BodyGoal;
            TempData.Keep();
            return RedirectToAction(nameof(Step4));
        }

        // Step 4 - Auto-calculated targets using proper Mifflin-St Jeor
        public IActionResult Step4()
        {
            if (TempData.Peek("FitnessGoal") == null)
                return RedirectToAction(nameof(Step3));

            TempData.Keep();

            decimal currentWeight = decimal.TryParse(TempData.Peek("CurrentWeight")?.ToString(), out var cw) ? cw : 170;
            decimal goalWeight = decimal.TryParse(TempData.Peek("GoalWeight")?.ToString(), out var gw) ? gw : 170;
            decimal heightInches = decimal.TryParse(TempData.Peek("HeightInches")?.ToString(), out var hi) ? hi : 70;
            int weeks = int.TryParse(TempData.Peek("GoalTimeframeWeeks")?.ToString(), out var wk) ? wk : 12;
            int age = int.TryParse(TempData.Peek("Age")?.ToString(), out var ag) ? ag : 25;
            string gender = TempData.Peek("Gender")?.ToString() ?? "Male";
            string bodyGoal = TempData.Peek("BodyGoal")?.ToString() ?? "Maintain";
            string fitnessGoal = TempData.Peek("FitnessGoal")?.ToString() ?? "General Fitness";
            string weightUnit = TempData.Peek("WeightUnit")?.ToString() ?? "lbs";

            // Convert to lbs if needed for display consistency
            if (weightUnit == "kg")
            {
                currentWeight *= 2.205m;
                goalWeight *= 2.205m;
            }

            // Convert to metric for Mifflin-St Jeor
            decimal weightKg = currentWeight / 2.205m;
            decimal heightCm = heightInches * 2.54m;

            // Mifflin-St Jeor BMR
            decimal bmr = gender == "Female"
                ? (10 * weightKg) + (6.25m * heightCm) - (5 * age) - 161
                : (10 * weightKg) + (6.25m * heightCm) - (5 * age) + 5;

            // Moderate activity TDEE
            decimal tdee = bmr * 1.55m;

            // Calorie adjustment toward goal weight
            decimal weightDiff = goalWeight - currentWeight;
            decimal weeklyCalAdjustment = (weightDiff * 3500) / weeks;
            decimal dailyAdjustment = weeklyCalAdjustment / 7;
            dailyAdjustment = Math.Max(-750, Math.Min(500, dailyAdjustment));

            int calories = (int)(tdee + dailyAdjustment);
            calories = Math.Max(1200, Math.Min(4000, calories));

            decimal proteinMultiplier = fitnessGoal switch
            {
                "Build Muscle" => 1.0m,
                "General Fitness" => 0.9m,
                "Weight Loss" => 1.1m,
                "Improve Endurance" => 0.85m,
                _ => 0.8m
            };
            int protein = (int)(currentWeight * proteinMultiplier);
            int fat = (int)(calories * 0.25m / 9);
            int carbCalories = calories - (protein * 4) - (fat * 9);
            int carbs = Math.Max(50, carbCalories / 4);

            var vm = new OnboardingStep4PageViewModel
            {
                BodyGoal = bodyGoal,
                FitnessGoal = fitnessGoal,
                CurrentWeight = currentWeight,
                GoalWeight = goalWeight,
                WeeklyChange = Math.Round(Math.Abs(weightDiff) / weeks, 1),
                Weeks = weeks,
                CalorieGoal = calories,
                ProteinGoal = protein,
                CarbGoal = carbs,
                FatGoal = fat,
                WaterGoal = 128
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Complete(OnboardingStep4PageViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            decimal currentWeight = decimal.TryParse(TempData["CurrentWeight"]?.ToString(), out var cw) ? cw : 0;
            decimal goalWeight = decimal.TryParse(TempData["GoalWeight"]?.ToString(), out var gw) ? gw : 0;
            decimal heightInches = decimal.TryParse(TempData["HeightInches"]?.ToString(), out var hi) ? hi : 0;
            int weeks = int.TryParse(TempData["GoalTimeframeWeeks"]?.ToString(), out var wk) ? wk : 12;
            int age = int.TryParse(TempData["Age"]?.ToString(), out var ag) ? ag : 25;
            string gender = TempData["Gender"]?.ToString() ?? "Male";

            // Update the existing row created at registration � never insert a second one
            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings == null)
            {
                // Fallback safety net � shouldn't happen with normal registration flow
                settings = new UserSettings { UserId = userId ?? string.Empty };
                _context.UserSettings.Add(settings);
            }

            settings.DisplayName = TempData["DisplayName"]?.ToString() ?? settings.DisplayName;
            settings.FitnessGoal = TempData["FitnessGoal"]?.ToString() ?? "General Fitness";
            settings.BodyGoal = TempData["BodyGoal"]?.ToString() ?? "Maintain";
            settings.WeightUnit = TempData["WeightUnit"]?.ToString() ?? "lbs";
            settings.CalorieGoal = model.CalorieGoal;
            settings.ProteinGoal = model.ProteinGoal;
            settings.CarbGoal = model.CarbGoal;
            settings.FatGoal = model.FatGoal;
            settings.WaterGoal = model.WaterGoal;
            settings.CurrentWeight = currentWeight;
            settings.GoalWeight = goalWeight;
            settings.HeightInches = heightInches;
            settings.GoalTimeframeWeeks = weeks;
            settings.Age = age;
            settings.Gender = gender;
            settings.ShowOnLeaderboard = true;

            _context.SaveChanges();

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step1Ajax(OnboardingStep1ViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            TempData["DisplayName"] = existing?.DisplayName ?? model.DisplayName ?? string.Empty;
            return Json(new { success = true, redirectUrl = Url.Action(nameof(Step2)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step2PostAjax(OnboardingBodyStatsViewModel model)
        {
            decimal totalInches = (model.HeightFeet * 12) + model.HeightInches;
            TempData["CurrentWeight"] = model.CurrentWeight.ToString();
            TempData["GoalWeight"] = model.GoalWeight.ToString();
            TempData["HeightInches"] = totalInches.ToString();
            TempData["GoalTimeframeWeeks"] = model.GoalTimeframeWeeks.ToString();
            TempData["WeightUnit"] = model.WeightUnit;
            TempData["Age"] = model.Age.ToString();
            TempData["Gender"] = model.Gender;
            TempData["DisplayName"] = TempData.Peek("DisplayName");
            return Json(new { success = true, redirectUrl = Url.Action(nameof(Step3)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step3PostAjax(OnboardingGoalsViewModel model)
        {
            TempData["FitnessGoal"] = model.FitnessGoal;
            TempData["BodyGoal"] = model.BodyGoal;
            TempData.Keep();
            return Json(new { success = true, redirectUrl = Url.Action(nameof(Step4)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteAjax(OnboardingStep4PageViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            decimal currentWeight = decimal.TryParse(TempData["CurrentWeight"]?.ToString(), out var cw) ? cw : 0;
            decimal goalWeight = decimal.TryParse(TempData["GoalWeight"]?.ToString(), out var gw) ? gw : 0;
            decimal heightInches = decimal.TryParse(TempData["HeightInches"]?.ToString(), out var hi) ? hi : 0;
            int weeks = int.TryParse(TempData["GoalTimeframeWeeks"]?.ToString(), out var wk) ? wk : 12;
            int age = int.TryParse(TempData["Age"]?.ToString(), out var ag) ? ag : 25;
            string gender = TempData["Gender"]?.ToString() ?? "Male";

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (settings == null)
            {
                settings = new UserSettings { UserId = userId ?? string.Empty };
                _context.UserSettings.Add(settings);
            }

            settings.DisplayName = TempData["DisplayName"]?.ToString() ?? settings.DisplayName;
            settings.FitnessGoal = TempData["FitnessGoal"]?.ToString() ?? "General Fitness";
            settings.BodyGoal = TempData["BodyGoal"]?.ToString() ?? "Maintain";
            settings.WeightUnit = TempData["WeightUnit"]?.ToString() ?? "lbs";
            settings.CalorieGoal = model.CalorieGoal;
            settings.ProteinGoal = model.ProteinGoal;
            settings.CarbGoal = model.CarbGoal;
            settings.FatGoal = model.FatGoal;
            settings.WaterGoal = model.WaterGoal;
            settings.CurrentWeight = currentWeight;
            settings.GoalWeight = goalWeight;
            settings.HeightInches = heightInches;
            settings.GoalTimeframeWeeks = weeks;
            settings.Age = age;
            settings.Gender = gender;
            settings.ShowOnLeaderboard = true;

            _context.SaveChanges();

            return Json(new { success = true, redirectUrl = Url.Action("Index", "Dashboard") });
        }
    }
}