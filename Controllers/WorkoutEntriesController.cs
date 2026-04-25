using FitLog.Configuration;
using FitLog.Data;
using FitLog.Helpers;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FitLog.Controllers
{
    [Authorize]
    public class WorkoutEntriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WorkoutEntriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: WorkoutEntries
        public async Task<IActionResult> Index(string? search, string? muscleGroup, string? dateFrom, string? dateTo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (sessions, unsessionedEntries, muscleGroups, prs) = await LoadWorkoutEntriesIndexDataAsync(userId!, search, muscleGroup, dateFrom, dateTo);
            var noSessionsAtAll = !await _context.WorkoutSessions.AnyAsync(s => s.UserId == userId);

            var vm = new WorkoutEntriesIndexPageViewModel
            {
                Sessions = sessions.Select(WorkoutSessionListItemViewModel.FromSession).ToList(),
                UnsessionedEntries = unsessionedEntries.Select(WorkoutEntryLegacyRowViewModel.FromEntity).ToList(),
                PersonalRecords = prs,
                MuscleGroups = muscleGroups.Where(m => !string.IsNullOrEmpty(m)).ToList()!,
                Search = search,
                MuscleGroup = muscleGroup,
                DateFrom = dateFrom,
                DateTo = dateTo,
                NoSessionsAtAll = noSessionsAtAll
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SessionsList(string? search, string? muscleGroup, string? dateFrom, string? dateTo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (sessions, unsessionedEntries, muscleGroups, prs) = await LoadWorkoutEntriesIndexDataAsync(userId!, search, muscleGroup, dateFrom, dateTo);
            var noSessionsAtAll = !await _context.WorkoutSessions.AnyAsync(s => s.UserId == userId);

            var vm = new WorkoutEntriesIndexPageViewModel
            {
                Sessions = sessions.Select(WorkoutSessionListItemViewModel.FromSession).ToList(),
                UnsessionedEntries = unsessionedEntries.Select(WorkoutEntryLegacyRowViewModel.FromEntity).ToList(),
                PersonalRecords = prs,
                MuscleGroups = muscleGroups.Where(m => !string.IsNullOrEmpty(m)).ToList()!,
                Search = search,
                MuscleGroup = muscleGroup,
                DateFrom = dateFrom,
                DateTo = dateTo,
                NoSessionsAtAll = noSessionsAtAll
            };

            return PartialView("_IndexWorkoutList", vm);
        }

        private async Task<(List<WorkoutSession> Sessions, List<WorkoutEntry> Unsessioned, List<string> MuscleGroups, Dictionary<string, decimal> Prs)> LoadWorkoutEntriesIndexDataAsync(
            string userId, string? search, string? muscleGroup, string? dateFrom, string? dateTo)
        {
            var sessions = await _context.WorkoutSessions
                .Where(s => s.UserId == userId)
                .Include(s => s.Entries)
                .OrderByDescending(s => s.SessionDate)
                .ToListAsync();

            if (!string.IsNullOrEmpty(search))
                sessions = sessions.Where(s => s.SessionName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(muscleGroup))
                sessions = sessions.Where(s => s.Entries.Any(e => e.MuscleGroup == muscleGroup)).ToList();

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                sessions = sessions.Where(s => s.SessionDate >= from).ToList();

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
                sessions = sessions.Where(s => s.SessionDate <= to).ToList();

            var unsessionedEntries = await _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.SessionId == null)
                .OrderByDescending(w => w.WorkoutDate)
                .ToListAsync();

            var allEntries = await _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .ToListAsync();

            var prs = allEntries
                .Where(w => w.WeightLbs > 0)
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            var muscleGroups = allEntries
                .Select(w => w.MuscleGroup)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            return (sessions, unsessionedEntries, muscleGroups, prs);
        }

        // GET: Start workout - choose or create
        public async Task<IActionResult> StartWorkout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var recentSessions = await _context.WorkoutSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .Take(5)
                .ToListAsync();

            var vm = new StartWorkoutPageViewModel
            {
                RecentSessions = recentSessions.Select(WorkoutSessionSummaryViewModel.FromEntity).ToList()
            };
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(WorkoutEntrySetRowViewModel.FromEntity(workoutEntry));
        }

        // POST: Start new active workout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartNewWorkout(StartNewWorkoutFormViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(StartWorkout));

            var session = new WorkoutSession
            {
                UserId = userId ?? string.Empty,
                SessionName = model.SessionName,
                SessionDate = DateTime.Today,
                Notes = ""
            };

            _context.WorkoutSessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ActiveWorkout), new { id = session.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartNewWorkoutAjax(StartNewWorkoutFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, error = "Enter a workout name." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = new WorkoutSession
            {
                UserId = userId ?? string.Empty,
                SessionName = model.SessionName,
                SessionDate = DateTime.Today,
                Notes = ""
            };
            _context.WorkoutSessions.Add(session);
            await _context.SaveChangesAsync();
            return Json(new { success = true, redirectUrl = Url.Action(nameof(ActiveWorkout), new { id = session.Id }) });
        }

        // GET: Active workout mode
        public async Task<IActionResult> ActiveWorkout(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();

            var today = DateTime.Today;
            var musclesHitToday = _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.WorkoutDate.Date == today)
                .Select(w => w.MuscleGroup)
                .ToList()
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.ExerciseList = exercises;
            ViewBag.MusclesHitTodayJson = JsonSerializer.Serialize(musclesHitToday);
            ViewBag.ExerciseLibraryJson = JsonSerializer.Serialize(
                exercises.Select(e => new { name = e.Name, muscleGroup = e.MuscleGroup, description = e.Description ?? "", tips = e.Tips ?? "" }).ToList(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return View(WorkoutSessionDetailViewModel.FromEntity(session));
        }

        // POST: Add set during active workout (AJAX)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddActiveSet([FromBody] ActiveSetRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var entry = new WorkoutEntry
            {
                UserId = userId ?? string.Empty,
                SessionId = request.SessionId,
                ExerciseName = request.ExerciseName,
                MuscleGroup = string.IsNullOrEmpty(request.MuscleGroup) ? "General" : request.MuscleGroup,
                Sets = 1,
                Reps = request.Reps,
                WeightLbs = request.Weight,
                WorkoutDate = DateTime.Today,
                IsCompleted = true,
                Notes = ""
            };

            _context.WorkoutEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Json(new { success = true, entryId = entry.Id });
        }

        // POST: Delete set during active workout (AJAX)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteActiveSet([FromBody] DeleteSetRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var entry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(e => e.Id == request.EntryId && e.UserId == userId);

            if (entry != null)
            {
                _context.WorkoutEntries.Remove(entry);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Deleted successfully." });
        }

        // POST: Rename / change exercise for an active-session entry (library pick)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateActiveEntryExercise([FromBody] UpdateActiveEntryExerciseRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var entry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(e => e.Id == request.EntryId && e.UserId == userId);

            if (entry == null || entry.SessionId == null)
                return Json(new { success = false });

            entry.ExerciseName = (request.ExerciseName ?? string.Empty).Trim();
            entry.MuscleGroup = string.IsNullOrWhiteSpace(request.MuscleGroup) ? "General" : request.MuscleGroup.Trim();
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Saved successfully." });
        }

        // POST: Update reps/weight for an existing active-session set (row)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateActiveEntryValues([FromBody] UpdateActiveEntryValuesRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var entry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(e => e.Id == request.EntryId && e.UserId == userId);

            if (entry == null || entry.SessionId == null)
                return Json(new { success = false });

            entry.Reps = request.Reps;
            entry.WeightLbs = request.Weight;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Saved successfully." });
        }

        // POST: End workout - save summary info
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> EndWorkout([FromBody] EndWorkoutRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId);

            if (session == null) return NotFound();

            session.SessionName = request.SessionName;
            session.SessionDate = request.SessionDate;
            session.Notes = request.Notes ?? "";

            var pendingRows = session.Entries
                .Where(e => ExerciseDisplay.IsPending(e.ExerciseName))
                .ToList();
            if (pendingRows.Count > 0)
            {
                _context.WorkoutEntries.RemoveRange(pendingRows);
            }

            await _context.SaveChangesAsync();

            var sessionForSummary = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstAsync(s => s.Id == request.SessionId && s.UserId == userId);

            var summary = await GenerateWorkoutSummary(sessionForSummary);

            return Json(new { success = true, summary });
        }

        private async Task<string> GenerateWorkoutSummary(WorkoutSession session)
        {
            try
            {
                var apiKey = OpenAiApiKeyResolver.Resolve();
                if (string.IsNullOrEmpty(apiKey)) return "Workout saved successfully!";

                var sb = new StringBuilder();
                sb.AppendLine($"You are an expert personal trainer. Analyze this workout in detail and provide specific, data-driven feedback.");
                sb.AppendLine($"Workout: {session.SessionName}");
                sb.AppendLine($"Date: {session.SessionDate:MMMM d, yyyy}");
                sb.AppendLine("\nExercises performed:");
                foreach (var e in session.Entries)
                    sb.AppendLine($"- {ExerciseDisplay.Format(e.ExerciseName)} ({e.MuscleGroup}): {e.Sets} sets x {e.Reps} reps @ {e.WeightLbs}lbs (total volume: {e.Sets * e.Reps * e.WeightLbs}lbs)");

                sb.AppendLine(@"
Provide a structured analysis with these exact sections:

OVERALL PERFORMANCE
One paragraph assessing the total workout volume, intensity, and balance across muscle groups with specific numbers from the data.

WHERE TO PUSH MORE
List 1-3 specific exercises where the user should increase weight or volume next session, with specific target numbers (e.g. 'Bench Press: increase from 185lbs to 190-195lbs next session because your rep count of 8 suggests you have more in the tank').

WHERE TO EASE UP
List any exercises where the volume or weight may be excessive or where form could be compromised, with specific reasoning.

WHAT TO DO DIFFERENTLY
List 1-3 specific changes for next session with reasoning (e.g. 'Add Romanian Deadlifts — your hamstrings were not trained today which creates an imbalance with your quad-dominant leg work').

NEXT SESSION RECOMMENDATION
One specific recommendation for what to train next based on what was done today.");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                new { role = "system", content = "You are an expert personal trainer. Give detailed, data-driven, specific workout feedback. Always reference actual numbers from the workout data provided. Never give vague advice." },
                new { role = "user", content = sb.ToString() }
            },
                    max_tokens = 900
                };

                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return "Workout saved successfully!";
                using var jsonDoc = JsonDocument.Parse(responseContent);
                if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    return "Workout saved successfully!";
                return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "Workout saved!";
            }
            catch
            {
                return "Workout saved successfully!";
            }
        }

        // GET: Create a new workout session
        public IActionResult CreateSession()
        {
            return View(new WorkoutSessionCreateViewModel { SessionDate = DateTime.Today });
        }

        // POST: Create a new workout session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(WorkoutSessionCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
                return View(model);

            var session = model.ToEntity(userId);
            _context.WorkoutSessions.Add(session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(SessionDetail), new { id = session.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSessionAjax(WorkoutSessionCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Check workout name and try again." : err });
            }

            var session = model.ToEntity(userId);
            _context.WorkoutSessions.Add(session);
            await _context.SaveChangesAsync();
            return Json(new { success = true, redirectUrl = Url.Action(nameof(SessionDetail), new { id = session.Id }) });
        }

        // GET: Session detail
        public async Task<IActionResult> SessionDetail(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();

            var today = DateTime.Today;
            var musclesHitToday = _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.WorkoutDate.Date == today)
                .Select(w => w.MuscleGroup)
                .ToList()
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prs = _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.WeightLbs > 0)
                .ToList()
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            ViewBag.Exercises = exercises;
            ViewBag.PersonalRecords = prs;
            ViewBag.MusclesHitTodayJson = JsonSerializer.Serialize(musclesHitToday);
            ViewBag.ExerciseLibraryJson = JsonSerializer.Serialize(
                exercises.Select(e => new { name = e.Name, muscleGroup = e.MuscleGroup, description = e.Description ?? "", tips = e.Tips ?? "" }).ToList(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            ViewBag.SessionNameJson = JsonSerializer.Serialize(session.SessionName ?? "");
            ViewBag.LoggedExerciseNamesJson = JsonSerializer.Serialize(
                session.Entries
                    .Select(e => e.ExerciseName)
                    .Where(n => !string.IsNullOrWhiteSpace(n) && !ExerciseDisplay.IsPending(n))
                    .Distinct()
                    .ToList());

            return View(WorkoutSessionDetailViewModel.FromEntity(session));
        }

        // POST: Add exercise to session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExerciseToSession([Bind("SessionId,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry entry)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            entry.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");
            ModelState.Remove("Session");

            if (entry.WorkoutDate == default)
            {
                var session = await _context.WorkoutSessions.FindAsync(entry.SessionId);
                entry.WorkoutDate = session?.SessionDate ?? DateTime.Today;
            }

            if (ModelState.IsValid)
            {
                _context.WorkoutEntries.Add(entry);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SessionDetail), new { id = entry.SessionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExerciseToSessionAjax([Bind("SessionId,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry entry)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            entry.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");
            ModelState.Remove("Session");

            if (entry.WorkoutDate == default)
            {
                var session = await _context.WorkoutSessions.FindAsync(entry.SessionId);
                entry.WorkoutDate = session?.SessionDate ?? DateTime.Today;
            }

            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid exercise entry." : err });
            }

            _context.WorkoutEntries.Add(entry);
            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                message = "Exercise added.",
                entryId = entry.Id,
                redirectUrl = Url.Action(nameof(SessionDetail), new { id = entry.SessionId })
            });
        }

        // POST: Rename session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameSession(int sessionId, string sessionName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session != null)
            {
                session.SessionName = sessionName;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SessionDetail), new { id = sessionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameSessionAjax(int sessionId, string sessionName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return Json(new { success = false, error = "Workout not found." });

            session.SessionName = sessionName ?? string.Empty;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Name saved.", sessionName = session.SessionName });
        }

        // POST: Remove all sets for one exercise name from a session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExerciseFromSession(int sessionId, string exerciseName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound();

            var toRemove = session.Entries
                .Where(e => string.Equals(e.ExerciseName, exerciseName, StringComparison.Ordinal))
                .ToList();
            if (toRemove.Count > 0)
            {
                _context.WorkoutEntries.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SessionDetail), new { id = sessionId });
        }

        // POST: Delete session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session != null)
            {
                _context.WorkoutEntries.RemoveRange(session.Entries);
                _context.WorkoutSessions.Remove(session);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteSessionAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null)
                return Json(new { success = false });

            _context.WorkoutEntries.RemoveRange(session.Entries);
            _context.WorkoutSessions.Remove(session);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Deleted successfully." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteExerciseFromSessionAjax([FromBody] DeleteExerciseFromSessionJsonRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.WorkoutSessions
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId);

            if (session == null)
                return Json(new { success = false });

            var toRemove = session.Entries
                .Where(e => string.Equals(e.ExerciseName, request.ExerciseName ?? string.Empty, StringComparison.Ordinal))
                .ToList();
            if (toRemove.Count > 0)
            {
                _context.WorkoutEntries.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Deleted successfully." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteWorkoutEntryAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null)
                return Json(new { success = false });

            var sessionId = workoutEntry.SessionId;
            _context.WorkoutEntries.Remove(workoutEntry);
            await _context.SaveChangesAsync();
            return Json(new { success = true, sessionId, message = "Deleted successfully." });
        }

        // GET: Create standalone entry
        public IActionResult Create(string? exerciseName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = new WorkoutEntryCreateViewModel { WorkoutDate = DateTime.Today, Sets = 3, Reps = 10 };

            if (!string.IsNullOrEmpty(exerciseName))
            {
                var lastSession = _context.WorkoutEntries
                    .Where(w => w.UserId == userId && w.ExerciseName == exerciseName)
                    .OrderByDescending(w => w.WorkoutDate)
                    .FirstOrDefault();

                ViewBag.LastSession = lastSession;
                ViewBag.ExerciseName = exerciseName;
                model.ExerciseName = exerciseName;
                var lib = _context.Exercises.FirstOrDefault(e => e.Name == exerciseName);
                if (lib != null)
                    model.MuscleGroup = lib.MuscleGroup;
            }

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();

            ViewBag.Exercises = exercises;
            return View(model);
        }

        // POST: Create standalone entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkoutEntryCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (ModelState.IsValid)
            {
                _context.Add(model.ToEntity(userId));
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();
            ViewBag.Exercises = exercises;
            return View(model);
        }

        // GET: Edit entry
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(WorkoutEntryEditViewModel.FromEntity(workoutEntry));
        }

        // POST: Edit entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WorkoutEntryEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (workoutEntry == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    model.ApplyTo(workoutEntry);
                    workoutEntry.UserId = userId;
                    _context.Update(workoutEntry);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WorkoutEntryExists(workoutEntry.Id))
                        return NotFound();
                    throw;
                }

                if (workoutEntry.SessionId.HasValue)
                    return RedirectToAction(nameof(SessionDetail), new { id = workoutEntry.SessionId });

                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Delete entry
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(WorkoutEntryDeleteViewModel.FromEntity(workoutEntry));
        }

        // POST: Delete entry
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            var sessionId = workoutEntry?.SessionId;

            if (workoutEntry != null)
                _context.WorkoutEntries.Remove(workoutEntry);

            await _context.SaveChangesAsync();

            if (sessionId.HasValue)
                return RedirectToAction(nameof(SessionDetail), new { id = sessionId });

            return RedirectToAction(nameof(Index));
        }

        // GET: Exercise history
        public IActionResult ExerciseHistory(string exerciseName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = _context.WorkoutEntries
                .Where(w => w.UserId == userId && w.ExerciseName == exerciseName)
                .OrderByDescending(w => w.WorkoutDate)
                .ToList();

            var pr = history.Any() ? history.Max(w => w.WeightLbs) : 0;

            var vm = new ExerciseHistoryPageViewModel
            {
                ExerciseName = exerciseName ?? string.Empty,
                PersonalRecordWeight = pr,
                History = history.Select(WorkoutHistoryRowViewModel.FromEntity).ToList()
            };
            return View(vm);
        }

        private bool WorkoutEntryExists(int id)
        {
            return _context.WorkoutEntries.Any(e => e.Id == id);
        }
    }

    public class ActiveSetRequest
    {
        public int SessionId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public int Reps { get; set; }
        public decimal Weight { get; set; }
    }

    public class DeleteSetRequest
    {
        public int EntryId { get; set; }
    }

    public class UpdateActiveEntryExerciseRequest
    {
        public int EntryId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = "General";
    }

    public class UpdateActiveEntryValuesRequest
    {
        public int EntryId { get; set; }
        public int Reps { get; set; }
        public decimal Weight { get; set; }
    }

    public class EndWorkoutRequest
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string? Notes { get; set; }
    }

    public class DeleteExerciseFromSessionJsonRequest
    {
        public int SessionId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
    }
}