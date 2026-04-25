using FitLog.Data;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitLog.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // List all users with their roles and display names
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();

            // Build a dictionary of userId -> roles
            var userRolesDict = new Dictionary<string, IList<string>>();
            foreach (var user in users)
            {
                var rolesList = (await _userManager.GetRolesAsync(user)).ToList();
                if (rolesList.Count == 0)
                {
                    await _userManager.AddToRoleAsync(user, "User");
                    rolesList = (await _userManager.GetRolesAsync(user)).ToList();
                }

                userRolesDict[user.Id] = rolesList;
            }

            // Build a dictionary of userId -> display name
            var settings = await _context.UserSettings
                .ToDictionaryAsync(s => s.UserId, s => s.DisplayName);

            ViewBag.UserRoles = userRolesDict;
            ViewBag.DisplayNames = settings;

            return View(users);
        }

        // GET: Manage a specific user's roles
        [HttpGet]
        public async Task<IActionResult> ManageRoles(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Select a user to manage roles.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var allRoles = _roleManager.Roles.ToList();
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Any())
            {
                // Ensure every account has at least the baseline role.
                await _userManager.AddToRoleAsync(user, "User");
                userRoles = await _userManager.GetRolesAsync(user);
            }

            var displayName = (await _context.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId))?.DisplayName ?? string.Empty;

            var vm = new ManageRolesPageViewModel
            {
                UserId = userId,
                UserEmail = user.Email ?? string.Empty,
                DisplayName = displayName,
                Roles = allRoles
                    .Select(r => new ManageRolesRoleRowViewModel
                    {
                        RoleName = r.Name ?? string.Empty,
                        IsAssigned = userRoles.Contains(r.Name ?? string.Empty)
                    })
                    .ToList()
            };

            return View(vm);
        }

        // POST: Save role changes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(string userId, List<string> selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            selectedRoles ??= new List<string>();

            // Prevent admin from removing their own Admin role
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId && !selectedRoles.Contains("Admin"))
                selectedRoles.Add("Admin");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (selectedRoles != null && selectedRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, selectedRoles);
            else
                await _userManager.AddToRoleAsync(user, "User");

            TempData["Success"] = $"Role updated for {user.Email ?? user.UserName}";
            return RedirectToAction(nameof(ManageRoles), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRolesAjax(string userId, List<string>? selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Json(new { success = false, error = "User not found." });

            var currentUserId = _userManager.GetUserId(User);
            selectedRoles ??= new List<string>();
            if (userId == currentUserId && !selectedRoles.Contains("Admin"))
                selectedRoles.Add("Admin");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (selectedRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, selectedRoles);
            else
                await _userManager.AddToRoleAsync(user, "User");

            var email = user.Email ?? user.UserName ?? user.Id;
            return Json(new { success = true, message = $"Role updated for {email}" });
        }

        /// <summary>Platform-wide aggregates (all users).</summary>
        public async Task<IActionResult> PlatformReports()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var workoutRows = await _context.WorkoutEntries.CountAsync();
            var sessions = await _context.WorkoutSessions.CountAsync();
            var nutritionRows = await _context.NutritionLogs.CountAsync();
            var distinctExercises = await _context.WorkoutEntries.Select(w => w.ExerciseName).Distinct().CountAsync();
            var volumeByMuscle = await _context.WorkoutEntries
                .GroupBy(w => w.MuscleGroup ?? "Unknown")
                .Select(g => new
                {
                    MuscleGroup = g.Key,
                    TotalVolume = g.Sum(x => (decimal)x.Sets * x.Reps * x.WeightLbs),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.TotalVolume)
                .Take(15)
                .ToListAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.WorkoutRows = workoutRows;
            ViewBag.Sessions = sessions;
            ViewBag.NutritionRows = nutritionRows;
            ViewBag.DistinctExercises = distinctExercises;
            ViewBag.VolumeByMuscle = volumeByMuscle;
            return View();
        }

        // POST: Delete a user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            // Prevent admin from deleting themselves
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
                await _userManager.DeleteAsync(user);

            TempData["Success"] = "User deleted.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserAjax(string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
                return Json(new { success = false, error = "You cannot delete your own account." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
                await _userManager.DeleteAsync(user);

            return Json(new { success = true, message = "User deleted.", deletedUserId = userId });
        }
    }
}