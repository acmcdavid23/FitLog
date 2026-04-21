using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public SettingsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
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
                }
                else { _context.UserSettings.Add(settings); }
                _context.SaveChanges();
                TempData["Success"] = "Settings saved successfully!";
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
}