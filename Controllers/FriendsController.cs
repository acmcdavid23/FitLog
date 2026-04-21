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

            var pending = await _context.FriendRequests.Where(f => f.ReceiverId == userId && f.Status == "Pending").ToListAsync();
            var pendingSenders = await _userManager.Users.Where(u => pending.Select(p => p.SenderId).Contains(u.Id)).ToListAsync();
            var groups = await _context.Groups.Include(g => g.Members).Where(g => g.Members.Any(m => m.UserId == userId)).ToListAsync();

            var mySettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            ViewBag.Friends = friends;
            ViewBag.FriendDisplayNames = friendDisplayNames;
            ViewBag.PendingRequests = pending;
            ViewBag.PendingSenders = pendingSenders;
            ViewBag.Groups = groups;
            ViewBag.UserId = userId;
            ViewBag.MyUsername = mySettings?.Username ?? "";
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";

            return View();
        }

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
        public async Task<IActionResult> CreateGroup(string groupName, string description, string location, string password)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(groupName)) { TempData["Error"] = "Please enter a group name."; return RedirectToAction(nameof(Index)); }

            var group = new FitLogGroup
            {
                Name = groupName,
                Description = description ?? string.Empty,
                Location = location ?? string.Empty,
                Password = password ?? string.Empty,
                IsPrivate = !string.IsNullOrEmpty(password),
                CreatedByUserId = userId ?? string.Empty,
                CreatedAt = DateTime.Now
            };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            _context.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId ?? string.Empty, Role = "Admin", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Group '{groupName}' created!";
            return RedirectToAction(nameof(GroupDetail), new { id = group.Id });
        }

        // Join a group by name + password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinGroup(string groupName, string groupPassword)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Name == groupName);

            if (group == null) { TempData["Error"] = "Group not found."; return RedirectToAction(nameof(Index)); }
            if (group.IsPrivate && group.Password != groupPassword) { TempData["Error"] = "Incorrect group password."; return RedirectToAction(nameof(Index)); }
            if (group.Members.Any(m => m.UserId == userId)) { TempData["Error"] = "You are already in this group."; return RedirectToAction(nameof(Index)); }

            _context.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId ?? string.Empty, Role = "Member", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Joined '{group.Name}'!";
            return RedirectToAction(nameof(GroupDetail), new { id = group.Id });
        }

        public async Task<IActionResult> GroupDetail(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id && g.Members.Any(m => m.UserId == userId));
            if (group == null) return NotFound();

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var members = await _userManager.Users.Where(u => memberIds.Contains(u.Id)).ToListAsync();
            var memberSettings = _context.UserSettings.Where(s => memberIds.Contains(s.UserId)).ToDictionary(s => s.UserId, s => s.DisplayName);

            var accepted = await _context.FriendRequests
                .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == "Accepted").ToListAsync();
            var friendIds = accepted.Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId).Where(fid => !memberIds.Contains(fid)).ToList();
            var friendsNotInGroup = await _userManager.Users.Where(u => friendIds.Contains(u.Id)).ToListAsync();
            var friendSettings = _context.UserSettings.Where(s => friendIds.Contains(s.UserId)).ToDictionary(s => s.UserId, s => s.DisplayName);

            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            string DisplayName(string uid) => memberSettings.ContainsKey(uid) && !string.IsNullOrEmpty(memberSettings[uid])
                ? memberSettings[uid] : members.FirstOrDefault(m => m.Id == uid)?.Email?.Split('@')[0] ?? "Unknown";

            var volumeLeaderboard = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.WorkoutDate >= weekStart).ToList()
                .GroupBy(w => w.UserId).Select(g => new { UserId = g.Key, DisplayName = DisplayName(g.Key), TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs), IsCurrentUser = g.Key == userId })
                .OrderByDescending(x => x.TotalVolume).ToList();

            var streakLeaderboard = memberIds.Select(mid => {
                var dates = _context.WorkoutEntries.Where(w => w.UserId == mid).Select(w => w.WorkoutDate.Date).Distinct().OrderByDescending(d => d).ToList();
                int streak = 0; var checkDate = DateTime.Today;
                foreach (var date in dates) { if (date == checkDate || date == checkDate.AddDays(-1)) { streak++; checkDate = date; } else break; }
                return new { UserId = mid, DisplayName = DisplayName(mid), Streak = streak, IsCurrentUser = mid == userId };
            }).Where(x => x.Streak > 0).OrderByDescending(x => x.Streak).ToList();

            var exercises = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.WeightLbs > 0).Select(w => w.ExerciseName).Distinct().OrderBy(e => e).ToList();
            var selectedExercise = exercises.FirstOrDefault() ?? "";
            var prLeaderboard = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.ExerciseName == selectedExercise && w.WeightLbs > 0).ToList()
                .GroupBy(w => w.UserId).Select(g => new { UserId = g.Key, DisplayName = DisplayName(g.Key), MaxWeight = g.Max(w => w.WeightLbs), IsCurrentUser = g.Key == userId })
                .OrderByDescending(x => x.MaxWeight).ToList();

            ViewBag.Group = group; ViewBag.Members = members; ViewBag.MemberSettings = memberSettings;
            ViewBag.FriendsNotInGroup = friendsNotInGroup; ViewBag.FriendSettings = friendSettings;
            ViewBag.VolumeLeaderboard = volumeLeaderboard; ViewBag.StreakLeaderboard = streakLeaderboard;
            ViewBag.PRLeaderboard = prLeaderboard; ViewBag.Exercises = exercises;
            ViewBag.SelectedExercise = selectedExercise; ViewBag.WeekStart = weekStart;
            ViewBag.IsAdmin = group.Members.Any(m => m.UserId == userId && m.Role == "Admin");
            ViewBag.UserId = userId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteToGroup(int groupId, string inviteUserId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (!group.Members.Any(m => m.UserId == userId && m.Role == "Admin")) { TempData["Error"] = "Only admins can invite."; return RedirectToAction(nameof(GroupDetail), new { id = groupId }); }
            if (group.Members.Any(m => m.UserId == inviteUserId)) { TempData["Error"] = "Already in group."; return RedirectToAction(nameof(GroupDetail), new { id = groupId }); }

            _context.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = inviteUserId, Role = "Member", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            var invitedUser = await _userManager.FindByIdAsync(inviteUserId);
            var inviterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (invitedUser?.Email != null)
                await _emailService.SendEmailAsync(invitedUser.Email, $"You've been added to {group.Name} on FitLog",
                    $"<h2>Group Invite</h2><p><strong>{inviterSettings?.DisplayName ?? "Someone"}</strong> added you to <strong>{group.Name}</strong> on FitLog.</p>");

            TempData["Success"] = "Member added!";
            return RedirectToAction(nameof(GroupDetail), new { id = groupId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveGroup(int groupId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var member = await _context.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (member != null) { _context.GroupMembers.Remove(member); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId && g.CreatedByUserId == userId);
            if (group != null) { _context.GroupMembers.RemoveRange(group.Members); _context.Groups.Remove(group); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}