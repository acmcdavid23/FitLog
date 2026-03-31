using FitLog.Data;
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
                userRolesDict[user.Id] = await _userManager.GetRolesAsync(user);

            // Build a dictionary of userId -> display name
            var settings = await _context.UserSettings
                .ToDictionaryAsync(s => s.UserId, s => s.DisplayName);

            ViewBag.UserRoles = userRolesDict;
            ViewBag.DisplayNames = settings;

            return View(users);
        }

        // GET: Manage a specific user's roles
        public async Task<IActionResult> ManageRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var allRoles = _roleManager.Roles.ToList();
            var userRoles = await _userManager.GetRolesAsync(user);

            var displayName = (await _context.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId))?.DisplayName ?? string.Empty;

            ViewBag.UserId = userId;
            ViewBag.UserEmail = user.Email;
            ViewBag.DisplayName = displayName;
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;

            return View();
        }

        // POST: Save role changes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(string userId, List<string> selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Prevent admin from removing their own Admin role
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId && !selectedRoles.Contains("Admin"))
                selectedRoles.Add("Admin");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (selectedRoles != null && selectedRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, selectedRoles);

            TempData["Success"] = $"Roles updated successfully.";
            return RedirectToAction("Index");
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
    }
}