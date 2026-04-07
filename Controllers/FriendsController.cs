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

        public FriendsController(ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IEmailService emailService)
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

            var friendIds = accepted
                .Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId)
                .ToList();

            var friends = await _userManager.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            var friendDisplayNames = _context.UserSettings
                .Where(s => friendIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => string.IsNullOrEmpty(s.DisplayName) ? "" : s.DisplayName);

            var pending = await _context.FriendRequests
                .Where(f => f.ReceiverId == userId && f.Status == "Pending")
                .ToListAsync();

            var pendingSenders = await _userManager.Users
                .Where(u => pending.Select(p => p.SenderId).Contains(u.Id))
                .ToListAsync();

            var groups = await _context.Groups
                .Include(g => g.Members)
                .Where(g => g.Members.Any(m => m.UserId == userId))
                .ToListAsync();

            ViewBag.Friends = friends;
            ViewBag.FriendDisplayNames = friendDisplayNames;
            ViewBag.PendingRequests = pending;
            ViewBag.PendingSenders = pendingSenders;
            ViewBag.Groups = groups;
            ViewBag.UserId = userId;

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

            var target = await _userManager.FindByEmailAsync(searchQuery)
                ?? await _userManager.FindByNameAsync(searchQuery);

            if (target == null)
            {
                TempData["Error"] = "No user found with that email or username.";
                return RedirectToAction(nameof(Index));
            }

            if (target.Id == userId)
            {
                TempData["Error"] = "You cannot send a friend request to yourself.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == target.Id) ||
                (f.SenderId == target.Id && f.ReceiverId == userId));

            if (existing != null)
            {
                TempData["Error"] = existing.Status == "Accepted"
                    ? "You are already friends with this user."
                    : "A friend request already exists with this user.";
                return RedirectToAction(nameof(Index));
            }

            _context.FriendRequests.Add(new FriendRequest
            {
                SenderId = userId ?? string.Empty,
                ReceiverId = target.Id,
                Status = "Pending",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            // Send email notification
            var senderSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var senderName = senderSettings?.DisplayName ?? User.Identity?.Name?.Split('@')[0] ?? "Someone";
            if (!string.IsNullOrEmpty(target.Email))
            {
                await _emailService.SendEmailAsync(
                    target.Email,
                    "New Friend Request on FitLog",
                    $"<h2>Friend Request</h2><p><strong>{senderName}</strong> sent you a friend request on FitLog.</p><p><a href='https://fitlog-f2emavbccfbpg9de.canadacentral-01.azurewebsites.net/Friends'>View Request</a></p>"
                );
            }

            TempData["Success"] = "Friend request sent!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests
                .FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);

            if (request != null)
            {
                request.Status = "Accepted";
                await _context.SaveChangesAsync();

                // Notify the sender
                var sender = await _userManager.FindByIdAsync(request.SenderId);
                var accepterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
                var accepterName = accepterSettings?.DisplayName ?? User.Identity?.Name?.Split('@')[0] ?? "Someone";

                if (sender?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        sender.Email,
                        "Friend Request Accepted on FitLog",
                        $"<h2>Friend Request Accepted</h2><p><strong>{accepterName}</strong> accepted your friend request on FitLog.</p><p><a href='https://fitlog-f2emavbccfbpg9de.canadacentral-01.azurewebsites.net/Friends'>View Friends</a></p>"
                    );
                }

                TempData["Success"] = "Friend request accepted!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests
                .FirstOrDefaultAsync(f => f.Id == id && f.ReceiverId == userId);

            if (request != null)
            {
                _context.FriendRequests.Remove(request);
                await _context.SaveChangesAsync();
            }

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

            if (request != null)
            {
                _context.FriendRequests.Remove(request);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(string groupName, string description,
            string location, string password)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(groupName))
            {
                TempData["Error"] = "Please enter a group name.";
                return RedirectToAction(nameof(Index));
            }

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

            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = userId ?? string.Empty,
                Role = "Admin",
                JoinedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Group '{groupName}' created!";
            return RedirectToAction(nameof(GroupDetail), new { id = group.Id });
        }

        public async Task<IActionResult> GroupDetail(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id && g.Members.Any(m => m.UserId == userId));

            if (group == null) return NotFound();

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var members = await _userManager.Users.Where(u => memberIds.Contains(u.Id)).ToListAsync();

            var memberSettings = _context.UserSettings
                .Where(s => memberIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.DisplayName);

            var accepted = await _context.FriendRequests
                .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == "Accepted")
                .ToListAsync();

            var friendIds = accepted
                .Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId)
                .Where(fid => !memberIds.Contains(fid))
                .ToList();

            var friendsNotInGroup = await _userManager.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            var friendSettings = _context.UserSettings
                .Where(s => friendIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.DisplayName);

            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            string DisplayName(string uid) =>
                memberSettings.ContainsKey(uid) && !string.IsNullOrEmpty(memberSettings[uid])
                    ? memberSettings[uid]
                    : members.FirstOrDefault(m => m.Id == uid)?.Email?.Split('@')[0] ?? "Unknown";

            var volumeLeaderboard = _context.WorkoutEntries
                .Where(w => memberIds.Contains(w.UserId) && w.WorkoutDate >= weekStart)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = DisplayName(g.Key),
                    TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs),
                    IsCurrentUser = g.Key == userId
                })
                .OrderByDescending(x => x.TotalVolume)
                .ToList();

            var streakLeaderboard = memberIds.Select(mid =>
            {
                var dates = _context.WorkoutEntries
                    .Where(w => w.UserId == mid)
                    .Select(w => w.WorkoutDate.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                int streak = 0;
                var checkDate = DateTime.Today;
                foreach (var date in dates)
                {
                    if (date == checkDate || date == checkDate.AddDays(-1)) { streak++; checkDate = date; }
                    else break;
                }

                return new
                {
                    UserId = mid,
                    DisplayName = DisplayName(mid),
                    Streak = streak,
                    IsCurrentUser = mid == userId
                };
            })
            .Where(x => x.Streak > 0)
            .OrderByDescending(x => x.Streak)
            .ToList();

            var exercises = _context.WorkoutEntries
                .Where(w => memberIds.Contains(w.UserId) && w.WeightLbs > 0)
                .Select(w => w.ExerciseName)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            var selectedExercise = exercises.FirstOrDefault() ?? "";

            var prLeaderboard = _context.WorkoutEntries
                .Where(w => memberIds.Contains(w.UserId) && w.ExerciseName == selectedExercise && w.WeightLbs > 0)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    DisplayName = DisplayName(g.Key),
                    MaxWeight = g.Max(w => w.WeightLbs),
                    IsCurrentUser = g.Key == userId
                })
                .OrderByDescending(x => x.MaxWeight)
                .ToList();

            var isAdmin = group.Members.Any(m => m.UserId == userId && m.Role == "Admin");

            ViewBag.Group = group;
            ViewBag.Members = members;
            ViewBag.MemberSettings = memberSettings;
            ViewBag.FriendsNotInGroup = friendsNotInGroup;
            ViewBag.FriendSettings = friendSettings;
            ViewBag.VolumeLeaderboard = volumeLeaderboard;
            ViewBag.StreakLeaderboard = streakLeaderboard;
            ViewBag.PRLeaderboard = prLeaderboard;
            ViewBag.Exercises = exercises;
            ViewBag.SelectedExercise = selectedExercise;
            ViewBag.WeekStart = weekStart;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.UserId = userId;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteToGroup(int groupId, string inviteUserId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null) return NotFound();

            var isAdmin = group.Members.Any(m => m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["Error"] = "Only group admins can invite members.";
                return RedirectToAction(nameof(GroupDetail), new { id = groupId });
            }

            if (group.Members.Any(m => m.UserId == inviteUserId))
            {
                TempData["Error"] = "This user is already in the group.";
                return RedirectToAction(nameof(GroupDetail), new { id = groupId });
            }

            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                UserId = inviteUserId,
                Role = "Member",
                JoinedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            // Email the invited user
            var invitedUser = await _userManager.FindByIdAsync(inviteUserId);
            var inviterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var inviterName = inviterSettings?.DisplayName ?? "Someone";

            if (invitedUser?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    invitedUser.Email,
                    $"You've been added to {group.Name} on FitLog",
                    $"<h2>Group Invite</h2><p><strong>{inviterName}</strong> added you to the group <strong>{group.Name}</strong> on FitLog.</p><p><a href='https://fitlog-f2emavbccfbpg9de.canadacentral-01.azurewebsites.net/Friends'>View Group</a></p>"
                );
            }

            TempData["Success"] = "Member added to group!";
            return RedirectToAction(nameof(GroupDetail), new { id = groupId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveGroup(int groupId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

            if (member != null)
            {
                _context.GroupMembers.Remove(member);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == groupId && g.CreatedByUserId == userId);

            if (group != null)
            {
                _context.GroupMembers.RemoveRange(group.Members);
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Leaderboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var accepted = await _context.FriendRequests
                .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == "Accepted")
                .ToListAsync();

            var friendIds = accepted
                .Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId)
                .ToList();

            var allIds = friendIds.Concat(new[] { userId! }).ToList();
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            var settings = _context.UserSettings
                .Where(s => allIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.DisplayName);

            var users = await _userManager.Users
                .Where(u => allIds.Contains(u.Id))
                .ToListAsync();

            string DisplayName(string uid) =>
                settings.ContainsKey(uid) && !string.IsNullOrEmpty(settings[uid])
                    ? settings[uid]
                    : users.FirstOrDefault(u => u.Id == uid)?.Email?.Split('@')[0] ?? "Unknown";

            var volumeBoard = _context.WorkoutEntries
                .Where(w => allIds.Contains(w.UserId) && w.WorkoutDate >= weekStart)
                .ToList()
                .GroupBy(w => w.UserId)
                .Select(g => new {
                    DisplayName = DisplayName(g.Key),
                    TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs),
                    IsCurrentUser = g.Key == userId
                })
                .OrderByDescending(x => x.TotalVolume)
                .ToList();

            var streakBoard = allIds.Select(mid =>
            {
                var dates = _context.WorkoutEntries
                    .Where(w => w.UserId == mid)
                    .Select(w => w.WorkoutDate.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                int streak = 0;
                var check = DateTime.Today;
                foreach (var d in dates)
                {
                    if (d == check || d == check.AddDays(-1)) { streak++; check = d; }
                    else break;
                }

                return new
                {
                    DisplayName = DisplayName(mid),
                    Streak = streak,
                    IsCurrentUser = mid == userId
                };
            })
            .Where(x => x.Streak > 0)
            .OrderByDescending(x => x.Streak)
            .ToList();

            ViewBag.VolumeBoard = volumeBoard;
            ViewBag.StreakBoard = streakBoard;
            ViewBag.WeekStart = weekStart;

            return View();
        }
    }
}