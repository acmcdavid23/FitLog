using FitLog.Data;
using FitLog.Helpers;
using FitLog.Models;
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

        // Step 1 - Welcome + Display Name
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (existing != null)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step1(string displayName)
        {
            if (!string.IsNullOrEmpty(displayName) && !ProfanityHelper.IsValidDisplayName(displayName))
            {
                TempData["Error"] = "Display name contains inappropriate language or is invalid.";
                return RedirectToAction(nameof(Index));
            }

            TempData["DisplayName"] = displayName;
            return RedirectToAction(nameof(Step2));
        }

        // Step 2 - Body Stats
        public IActionResult Step2()
        {
            if (TempData.Peek("DisplayName") == null)
                return RedirectToAction(nameof(Index));
            TempData.Keep();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step2Post(decimal currentWeight, decimal goalWeight,
            decimal heightFeet, decimal heightInches, int goalTimeframeWeeks, string weightUnit)
        {
            decimal totalInches = (heightFeet * 12) + heightInches;
            TempData["CurrentWeight"] = currentWeight.ToString();
            TempData["GoalWeight"] = goalWeight.ToString();
            TempData["HeightInches"] = totalInches.ToString();
            TempData["GoalTimeframeWeeks"] = goalTimeframeWeeks.ToString();
            TempData["WeightUnit"] = weightUnit;
            TempData["DisplayName"] = TempData.Peek("DisplayName");
            return RedirectToAction(nameof(Step3));
        }

        // Step 3 - Fitness Goal
        public IActionResult Step3()
        {
            if (TempData.Peek("CurrentWeight") == null)
                return RedirectToAction(nameof(Step2));
            TempData.Keep();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Step3Post(string fitnessGoal, string bodyGoal)
        {
            TempData["FitnessGoal"] = fitnessGoal;
            TempData["BodyGoal"] = bodyGoal;
            TempData.Keep();
            return RedirectToAction(nameof(Step4));
        }

        // Step 4 - Auto-calculated targets
        public IActionResult Step4()
        {
            if (TempData.Peek("FitnessGoal") == null)
                return RedirectToAction(nameof(Step3));

            TempData.Keep();

            decimal currentWeight = decimal.TryParse(TempData.Peek("CurrentWeight")?.ToString(), out var cw) ? cw : 170;
            decimal goalWeight = decimal.TryParse(TempData.Peek("GoalWeight")?.ToString(), out var gw) ? gw : 170;
            decimal heightInches = decimal.TryParse(TempData.Peek("HeightInches")?.ToString(), out var hi) ? hi : 70;
            int weeks = int.TryParse(TempData.Peek("GoalTimeframeWeeks")?.ToString(), out var wk) ? wk : 12;
            string bodyGoal = TempData.Peek("BodyGoal")?.ToString() ?? "Maintain";
            string fitnessGoal = TempData.Peek("FitnessGoal")?.ToString() ?? "Hypertrophy";
            string weightUnit = TempData.Peek("WeightUnit")?.ToString() ?? "lbs";

            if (weightUnit == "kg")
            {
                currentWeight *= 2.205m;
                goalWeight *= 2.205m;
            }

            decimal bmr = (10 * (currentWeight / 2.205m)) + (6.25m * (heightInches * 2.54m)) - 500;
            decimal tdee = bmr * 1.55m;

            decimal weightDiff = goalWeight - currentWeight;
            decimal weeklyCalorieAdjustment = (weightDiff * 3500) / weeks;
            decimal dailyAdjustment = weeklyCalorieAdjustment / 7;
            dailyAdjustment = Math.Max(-750, Math.Min(500, dailyAdjustment));

            int calories = (int)(tdee + dailyAdjustment);
            calories = Math.Max(1200, Math.Min(4000, calories));

            decimal proteinMultiplier = fitnessGoal switch
            {
                "Strength" => 1.0m,
                "Hypertrophy" => 0.9m,
                "Weight Loss" => 1.1m,
                _ => 0.8m
            };
            int protein = (int)(currentWeight * proteinMultiplier);
            int fat = (int)(calories * 0.25m / 9);
            int carbCalories = calories - (protein * 4) - (fat * 9);
            int carbs = Math.Max(50, carbCalories / 4);

            ViewBag.SuggestedCalories = calories;
            ViewBag.SuggestedProtein = protein;
            ViewBag.SuggestedCarbs = carbs;
            ViewBag.SuggestedFat = fat;
            ViewBag.BodyGoal = bodyGoal;
            ViewBag.FitnessGoal = fitnessGoal;
            ViewBag.CurrentWeight = currentWeight;
            ViewBag.GoalWeight = goalWeight;
            ViewBag.WeeklyChange = Math.Round(Math.Abs(weightDiff) / weeks, 1);
            ViewBag.Weeks = weeks;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Complete(int calorieGoal, int proteinGoal, int carbGoal, int fatGoal, int waterGoal)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            decimal currentWeight = decimal.TryParse(TempData["CurrentWeight"]?.ToString(), out var cw) ? cw : 0;
            decimal goalWeight = decimal.TryParse(TempData["GoalWeight"]?.ToString(), out var gw) ? gw : 0;
            decimal heightInches = decimal.TryParse(TempData["HeightInches"]?.ToString(), out var hi) ? hi : 0;
            int weeks = int.TryParse(TempData["GoalTimeframeWeeks"]?.ToString(), out var wk) ? wk : 12;

            var settings = new UserSettings
            {
                UserId = userId ?? string.Empty,
                DisplayName = TempData["DisplayName"]?.ToString() ?? string.Empty,
                FitnessGoal = TempData["FitnessGoal"]?.ToString() ?? "Hypertrophy",
                BodyGoal = TempData["BodyGoal"]?.ToString() ?? "Maintain",
                WeightUnit = TempData["WeightUnit"]?.ToString() ?? "lbs",
                CalorieGoal = calorieGoal,
                ProteinGoal = proteinGoal,
                CarbGoal = carbGoal,
                FatGoal = fatGoal,
                WaterGoal = waterGoal,
                CurrentWeight = currentWeight,
                GoalWeight = goalWeight,
                HeightInches = heightInches,
                GoalTimeframeWeeks = weeks,
                ShowOnLeaderboard = true
            };

            _context.UserSettings.Add(settings);
            _context.SaveChanges();

            return RedirectToAction("Index", "Dashboard");
        }
    }
}