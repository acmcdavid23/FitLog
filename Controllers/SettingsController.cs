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

        public SettingsController(ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
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
                    existing.WeightUnit = settings.WeightUnit;
                    existing.FitnessGoal = settings.FitnessGoal;
                    existing.Age = settings.Age;
                    existing.Gender = settings.Gender;
                }
                else
                {
                    _context.UserSettings.Add(settings);
                }
                _context.SaveChanges();
                TempData["Success"] = "Settings saved successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: confirm delete page
        public IActionResult DeleteAccount()
        {
            return View();
        }

        // POST: actually delete
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

            // Delete all user data
            var weightLogs = _context.WeightLogs.Where(w => w.UserId == userId).ToList();
            _context.WeightLogs.RemoveRange(weightLogs);

            var nutritionLogs = _context.NutritionLogs.Where(n => n.UserId == userId).ToList();
            _context.NutritionLogs.RemoveRange(nutritionLogs);

            var waterLogs = _context.WaterLogs.Where(w => w.UserId == userId).ToList();
            _context.WaterLogs.RemoveRange(waterLogs);

            var suppLogs = _context.SupplementLogs.Where(s => s.UserId == userId).ToList();
            _context.SupplementLogs.RemoveRange(suppLogs);

            var supplements = _context.Supplements.Where(s => s.UserId == userId).ToList();
            _context.Supplements.RemoveRange(supplements);

            var workoutEntries = _context.WorkoutEntries.Where(w => w.UserId == userId).ToList();
            _context.WorkoutEntries.RemoveRange(workoutEntries);

            var sessions = _context.WorkoutSessions.Where(w => w.UserId == userId).ToList();
            _context.WorkoutSessions.RemoveRange(sessions);

            var friendRequests = _context.FriendRequests
                .Where(f => f.SenderId == userId || f.ReceiverId == userId).ToList();
            _context.FriendRequests.RemoveRange(friendRequests);

            var groupMembers = _context.GroupMembers.Where(m => m.UserId == userId).ToList();
            _context.GroupMembers.RemoveRange(groupMembers);

            var userSettings = _context.UserSettings.Where(s => s.UserId == userId).ToList();
            _context.UserSettings.RemoveRange(userSettings);

            _context.SaveChanges();

            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);

            return RedirectToAction("Index", "Home");
        }
    }
}