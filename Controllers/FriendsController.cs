using FitLog.Data;
using FitLog.Models;
using FitLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public FriendsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var accepted = await _context.FriendRequests
                .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == "Accepted")
                .ToListAsync();

            var friendIds = accepted.Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId).ToList();
            var friends = await _userManager.Users.Where(u => friendIds.Contains(u.Id)).ToListAsync();
            var friendDisplayNames = _context.UserSettings.Where(s => friendIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => string.IsNullOrEmpty(s.DisplayName) ? "" : s.DisplayName);
            var friendProfileUrls = _context.UserSettings.Where(s => friendIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.ProfileImageUrl ?? string.Empty);

            var pending = await _context.FriendRequests.Where(f => f.ReceiverId == userId && f.Status == "Pending").ToListAsync();
            var pendingSenders = await _userManager.Users.Where(u => pending.Select(p => p.SenderId).Contains(u.Id)).ToListAsync();

            var mySettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            ViewBag.Friends = friends;
            ViewBag.FriendDisplayNames = friendDisplayNames;
            ViewBag.FriendProfileUrls = friendProfileUrls;
            ViewBag.PendingRequests = pending;
            ViewBag.PendingSenders = pendingSenders;
            ViewBag.UserId = userId;
            ViewBag.MyUsername = mySettings?.Username ?? "";
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";

            return View();
        }

        [HttpGet]
        public IActionResult GroupDetail(int id) => RedirectToAction("Detail", "Groups", new { id });

        [HttpGet]
        public IActionResult JoinByInvite(string code) => RedirectToAction("JoinByInvite", "Groups", new { code });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string searchQuery)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(searchQuery))
            {
                TempData["Error"] = "Please enter a username or email.";
                return RedirectToAction(nameof(Index));
            }

            // Search by username first, then email
            IdentityUser? target = null;
            var byUsername = _context.UserSettings.FirstOrDefault(s => s.Username == searchQuery);
            if (byUsername != null)
                target = await _userManager.FindByIdAsync(byUsername.UserId);
            target ??= await _userManager.FindByEmailAsync(searchQuery);

            if (target == null) { TempData["Error"] = "No user found with that username or email."; return RedirectToAction(nameof(Index)); }
            if (target.Id == userId) { TempData["Error"] = "You cannot send a friend request to yourself."; return RedirectToAction(nameof(Index)); }

            var existing = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == target.Id) ||
                (f.SenderId == target.Id && f.ReceiverId == userId));

            if (existing != null)
            {
                TempData["Error"] = existing.Status == "Accepted" ? "You are already friends." : "A request already exists.";
                return RedirectToAction(nameof(Index));
            }

            _context.FriendRequests.Add(new FriendRequest { SenderId = userId ?? string.Empty, ReceiverId = target.Id, Status = "Pending", CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            var senderSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var senderName = senderSettings?.DisplayName ?? "Someone";
            if (!string.IsNullOrEmpty(target.Email))
                await _emailService.SendEmailAsync(target.Email, "New Friend Request on FitLog",
                    $"<h2>Friend Request</h2><p><strong>{senderName}</strong> sent you a friend request on FitLog.</p><p><a href='https://fitlog-f2emavbccfbpg9de.canadacentral-01.azurewebsites.net/Friends'>View Request</a></p>");

            TempData["Success"] = "Friend request sent!";
            return RedirectToAction(nameof(Index));
        }

        // QR code auto-add endpoint — scanning QR sends request instantly
        [HttpGet]
        public async Task<IActionResult> AddViaQR(string username)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var targetSettings = _context.UserSettings.FirstOrDefault(s => s.Username == username);
            if (targetSettings == null) { TempData["Error"] = "User not found."; return RedirectToAction(nameof(Index)); }
            if (targetSettings.UserId == userId) { TempData["Error"] = "That's your own QR code."; return RedirectToAction(nameof(Index)); }

            var existing = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == targetSettings.UserId) ||
                (f.SenderId == targetSettings.UserId && f.ReceiverId == userId));

            if (existing != null)
            {
                TempData["Error"] = existing.Status == "Accepted" ? "Already friends." : "Request already pending.";
                return RedirectToAction(nameof(Index));
            }

            _context.FriendRequests.Add(new FriendRequest { SenderId = userId ?? string.Empty, ReceiverId = targetSettings.UserId, Status = "Pending", CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Friend request sent via QR!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);
            if (request != null)
            {
                request.Status = "Accepted";
                await _context.SaveChangesAsync();
                var sender = await _userManager.FindByIdAsync(request.SenderId);
                var accepterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
                var accepterName = accepterSettings?.DisplayName ?? "Someone";
                if (sender?.Email != null)
                    await _emailService.SendEmailAsync(sender.Email, "Friend Request Accepted on FitLog",
                        $"<h2>Friend Request Accepted</h2><p><strong>{accepterName}</strong> accepted your friend request on FitLog.</p>");
                TempData["Success"] = "Friend request accepted!";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);
            if (request != null) { _context.FriendRequests.Remove(request); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFriend(string friendId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == friendId) ||
                (f.SenderId == friendId && f.ReceiverId == userId));
            if (request != null) { _context.FriendRequests.Remove(request); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequestAjax(string searchQuery)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(searchQuery))
                return Json(new { success = false, error = "Please enter a username or email." });

            IdentityUser? target = null;
            var byUsername = _context.UserSettings.FirstOrDefault(s => s.Username == searchQuery);
            if (byUsername != null)
                target = await _userManager.FindByIdAsync(byUsername.UserId);
            target ??= await _userManager.FindByEmailAsync(searchQuery);

            if (target == null) return Json(new { success = false, error = "No user found with that username or email." });
            if (target.Id == userId) return Json(new { success = false, error = "You cannot send a friend request to yourself." });

            var existing = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == target.Id) ||
                (f.SenderId == target.Id && f.ReceiverId == userId));

            if (existing != null)
                return Json(new { success = false, error = existing.Status == "Accepted" ? "You are already friends." : "A request already exists." });

            _context.FriendRequests.Add(new FriendRequest { SenderId = userId ?? string.Empty, ReceiverId = target.Id, Status = "Pending", CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            var senderSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var senderName = senderSettings?.DisplayName ?? "Someone";
            if (!string.IsNullOrEmpty(target.Email))
                await _emailService.SendEmailAsync(target.Email, "New Friend Request on FitLog",
                    $"<h2>Friend Request</h2><p><strong>{senderName}</strong> sent you a friend request on FitLog.</p><p><a href='https://fitlog-f2emavbccfbpg9de.canadacentral-01.azurewebsites.net/Friends'>View Request</a></p>");

            return Json(new { success = true, message = "Friend request sent!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequestAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);
            if (request == null)
                return Json(new { success = false, error = "Request not found." });

            request.Status = "Accepted";
            await _context.SaveChangesAsync();
            var sender = await _userManager.FindByIdAsync(request.SenderId);
            var accepterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var accepterName = accepterSettings?.DisplayName ?? "Someone";
            if (sender?.Email != null)
                await _emailService.SendEmailAsync(sender.Email, "Friend Request Accepted on FitLog",
                    $"<h2>Friend Request Accepted</h2><p><strong>{accepterName}</strong> accepted your friend request on FitLog.</p>");

            var senderDisplay = _context.UserSettings.FirstOrDefault(s => s.UserId == request.SenderId)?.DisplayName ?? "";
            return Json(new
            {
                success = true,
                message = "Friend request accepted!",
                requestId = id,
                friendId = request.SenderId,
                friendUserName = sender?.UserName ?? "",
                friendDisplayName = string.IsNullOrWhiteSpace(senderDisplay) ? (sender?.UserName ?? "") : senderDisplay
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequestAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);
            if (request != null) { _context.FriendRequests.Remove(request); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Declined.", requestId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFriendAjax(string friendId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == friendId) ||
                (f.SenderId == friendId && f.ReceiverId == userId));
            if (request != null) { _context.FriendRequests.Remove(request); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Removed.", friendId });
        }

    }
}