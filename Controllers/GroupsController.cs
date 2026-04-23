using FitLog.Data;
using FitLog.Models;
using FitLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;
        private readonly ImageProcessService _images;

        public GroupsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IEmailService emailService, IWebHostEnvironment env, ImageProcessService images)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _env = env;
            _images = images;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var groups = await _context.Groups.Include(g => g.Members).Where(g => g.Members.Any(m => m.UserId == userId)).ToListAsync();
            var mySettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);

            ViewBag.Groups = groups;
            ViewBag.UserId = userId;
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";

            var openGroups = await _context.Groups
                .Include(g => g.Members)
                .Where(g => !g.Members.Any(m => m.UserId == userId))
                .Where(g => !g.IsPrivate)
                .Where(g => g.InviteCode != null && g.InviteCode != "")
                .ToListAsync();

            var userCity = (mySettings?.CityRegion ?? "").Trim();
            var userGender = (mySettings?.Gender ?? "").Trim();
            var fitness = (mySettings?.FitnessGoal ?? "").Trim();
            var cityTokens = userCity.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 1).ToList();

            var recommended = openGroups
                .Select(g =>
                {
                    int score = Math.Min(8, g.Members.Count);
                    var loc = (g.Location ?? "").Trim();
                    if (!string.IsNullOrEmpty(loc) && cityTokens.Any(t => loc.Contains(t, StringComparison.OrdinalIgnoreCase)))
                        score += 6;
                    else if (!string.IsNullOrEmpty(userCity) && !string.IsNullOrEmpty(loc) && loc.Contains(userCity, StringComparison.OrdinalIgnoreCase))
                        score += 5;
                    if (!string.IsNullOrEmpty(userGender) && (g.Description ?? "").Contains(userGender, StringComparison.OrdinalIgnoreCase))
                        score += 2;
                    if (!string.IsNullOrEmpty(fitness) && (g.Description ?? "").Contains(fitness, StringComparison.OrdinalIgnoreCase))
                        score += 2;
                    if (!string.IsNullOrEmpty(loc) && !string.IsNullOrEmpty(userCity) && string.Equals(loc, userCity, StringComparison.OrdinalIgnoreCase))
                        score += 3;
                    return (Group: g, Score: score);
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Group.Members.Count)
                .Take(10)
                .Select(x => x.Group)
                .ToList();
            if (!recommended.Any())
            {
                recommended = openGroups
                    .OrderByDescending(g => g.Members.Count)
                    .Take(10)
                    .ToList();
            }
            ViewBag.RecommendedGroups = recommended;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(string groupName, string description, string location, string password, IFormFile? groupImage)
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
                CreatedAt = DateTime.Now,
                InviteCode = Guid.NewGuid().ToString("N")
            };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            _context.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId ?? string.Empty, Role = "Admin", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            if (groupImage != null && groupImage.Length > 0)
            {
                if (!ImageProcessService.IsAllowedImage(groupImage))
                    TempData["Error"] = "Group image must be 15 MB or less (JPG, PNG, WebP, GIF, or BMP).";
                else
                {
                    try
                    {
                        var v = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var rel = $"/uploads/groups/g{group.Id}.jpg?v={v}";
                        var path = Path.Combine(_env.WebRootPath, "uploads", "groups", $"g{group.Id}.jpg");
                        await _images.SaveSquareJpegAsync(groupImage, path);
                        group.ImageUrl = rel;
                        await _context.SaveChangesAsync();
                    }
                    catch
                    {
                        TempData["Error"] = "Could not process that image. Try JPG or PNG.";
                    }
                }
            }

            TempData["Success"] = $"Group '{groupName}' created!";
            return RedirectToAction(nameof(Detail), new { id = group.Id });
        }

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
            return RedirectToAction(nameof(Detail), new { id = group.Id });
        }

        [HttpGet]
        public async Task<IActionResult> JoinByInvite(string code)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["Error"] = "Invalid invite link.";
                return RedirectToAction(nameof(Index));
            }
            var group = await _context.Groups.Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.InviteCode == code.Trim());
            if (group == null)
            {
                TempData["Error"] = "Group not found.";
                return RedirectToAction(nameof(Index));
            }
            if (group.IsPrivate)
            {
                TempData["Error"] = "This group is private. Join with the exact name and password below.";
                return RedirectToAction(nameof(Index));
            }
            if (group.Members.Any(m => m.UserId == userId))
            {
                TempData["Error"] = "You are already in this group.";
                return RedirectToAction(nameof(Detail), new { id = group.Id });
            }
            _context.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId ?? string.Empty, Role = "Member", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Joined '{group.Name}'!";
            return RedirectToAction(nameof(Detail), new { id = group.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadGroupPhoto(int groupId, IFormFile photo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (!group.Members.Any(m => m.UserId == userId && m.Role == "Admin"))
            {
                TempData["Error"] = "Only group admins can change the photo.";
                return RedirectToAction(nameof(Detail), new { id = groupId });
            }
            if (photo == null || photo.Length == 0)
            {
                TempData["Error"] = "Please choose an image.";
                return RedirectToAction(nameof(Detail), new { id = groupId });
            }
            if (!ImageProcessService.IsAllowedImage(photo))
            {
                TempData["Error"] = "Use JPG, PNG, WebP, GIF, or BMP up to 15 MB.";
                return RedirectToAction(nameof(Detail), new { id = groupId });
            }
            try
            {
                var path = Path.Combine(_env.WebRootPath, "uploads", "groups", $"g{groupId}.jpg");
                await _images.SaveSquareJpegAsync(photo, path);
                group.ImageUrl = $"/uploads/groups/g{groupId}.jpg?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                await _context.SaveChangesAsync();
                TempData["Success"] = "Group photo updated.";
            }
            catch
            {
                TempData["Error"] = "Could not process that image.";
            }
            return RedirectToAction(nameof(Detail), new { id = groupId });
        }

        public async Task<IActionResult> Detail(int id, string? exercise)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id && g.Members.Any(m => m.UserId == userId));
            if (group == null) return NotFound();

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var members = await _userManager.Users.Where(u => memberIds.Contains(u.Id)).ToListAsync();
            var memberSettings = _context.UserSettings.Where(s => memberIds.Contains(s.UserId)).ToDictionary(s => s.UserId, s => s.DisplayName);
            var memberProfileUrls = _context.UserSettings.Where(s => memberIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId, s => s.ProfileImageUrl ?? string.Empty);

            var accepted = await _context.FriendRequests
                .Where(f => (f.SenderId == userId || f.ReceiverId == userId) && f.Status == "Accepted").ToListAsync();
            var friendIds = accepted.Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId).Where(fid => !memberIds.Contains(fid)).ToList();
            var friendsNotInGroup = await _userManager.Users.Where(u => friendIds.Contains(u.Id)).ToListAsync();
            var friendSettings = _context.UserSettings.Where(s => friendIds.Contains(s.UserId)).ToDictionary(s => s.UserId, s => s.DisplayName);

            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            string DisplayName(string uid) => memberSettings.ContainsKey(uid) && !string.IsNullOrEmpty(memberSettings[uid])
                ? memberSettings[uid] : members.FirstOrDefault(m => m.Id == uid)?.Email?.Split('@')[0] ?? "Unknown";

            var volumeLeaderboard = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.WorkoutDate >= weekStart).ToList()
                .GroupBy(w => w.UserId).Select(g => new { UserId = g.Key, DisplayName = DisplayName(g.Key), ProfileImageUrl = memberProfileUrls.GetValueOrDefault(g.Key), TotalVolume = g.Sum(w => w.Sets * w.Reps * w.WeightLbs), IsCurrentUser = g.Key == userId })
                .OrderByDescending(x => x.TotalVolume).ToList();

            var streakLeaderboard = memberIds.Select(mid => {
                var dates = _context.WorkoutEntries.Where(w => w.UserId == mid).Select(w => w.WorkoutDate.Date).Distinct().OrderByDescending(d => d).ToList();
                int streak = 0; var checkDate = DateTime.Today;
                foreach (var date in dates) { if (date == checkDate || date == checkDate.AddDays(-1)) { streak++; checkDate = date; } else break; }
                return new { UserId = mid, DisplayName = DisplayName(mid), ProfileImageUrl = memberProfileUrls.GetValueOrDefault(mid), Streak = streak, IsCurrentUser = mid == userId };
            }).Where(x => x.Streak > 0).OrderByDescending(x => x.Streak).ToList();

            var exercises = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.WeightLbs > 0).Select(w => w.ExerciseName).Distinct().OrderBy(e => e).ToList();
            var selectedExercise = exercise ?? exercises.FirstOrDefault() ?? "";
            var prLeaderboard = _context.WorkoutEntries.Where(w => memberIds.Contains(w.UserId) && w.ExerciseName == selectedExercise && w.WeightLbs > 0).ToList()
                .GroupBy(w => w.UserId).Select(g => new { UserId = g.Key, DisplayName = DisplayName(g.Key), ProfileImageUrl = memberProfileUrls.GetValueOrDefault(g.Key), MaxWeight = g.Max(w => w.WeightLbs), IsCurrentUser = g.Key == userId })
                .OrderByDescending(x => x.MaxWeight).ToList();

            ViewBag.Group = group; ViewBag.Members = members; ViewBag.MemberSettings = memberSettings; ViewBag.MemberProfileUrls = memberProfileUrls;
            ViewBag.FriendsNotInGroup = friendsNotInGroup; ViewBag.FriendSettings = friendSettings;
            ViewBag.VolumeLeaderboard = volumeLeaderboard; ViewBag.StreakLeaderboard = streakLeaderboard;
            ViewBag.PRLeaderboard = prLeaderboard; ViewBag.Exercises = exercises;
            ViewBag.SelectedExercise = selectedExercise; ViewBag.WeekStart = weekStart;
            ViewBag.IsAdmin = group.Members.Any(m => m.UserId == userId && m.Role == "Admin");
            ViewBag.UserId = userId;
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteToGroup(int groupId, string inviteUserId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (!group.Members.Any(m => m.UserId == userId && m.Role == "Admin")) { TempData["Error"] = "Only admins can invite."; return RedirectToAction(nameof(Detail), new { id = groupId }); }
            if (group.Members.Any(m => m.UserId == inviteUserId)) { TempData["Error"] = "Already in group."; return RedirectToAction(nameof(Detail), new { id = groupId }); }

            _context.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = inviteUserId, Role = "Member", JoinedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            var invitedUser = await _userManager.FindByIdAsync(inviteUserId);
            var inviterSettings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            if (invitedUser?.Email != null)
                await _emailService.SendEmailAsync(invitedUser.Email, $"You've been added to {group.Name} on FitLog",
                    $"<h2>Group Invite</h2><p><strong>{inviterSettings?.DisplayName ?? "Someone"}</strong> added you to <strong>{group.Name}</strong> on FitLog.</p>");

            TempData["Success"] = "Member added!";
            return RedirectToAction(nameof(Detail), new { id = groupId });
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
