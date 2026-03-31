using FitLog.Data;
using FitLog.Models;
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
        private readonly IConfiguration _configuration;

        public WorkoutEntriesController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: WorkoutEntries
        public async Task<IActionResult> Index(string? search, string? muscleGroup, string? dateFrom, string? dateTo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            var muscleGroups = allEntries
                .Select(w => w.MuscleGroup)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            ViewBag.PersonalRecords = prs;
            ViewBag.UnsessionedEntries = unsessionedEntries;
            ViewBag.MuscleGroups = muscleGroups;
            ViewBag.Search = search;
            ViewBag.MuscleGroup = muscleGroup;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return View(sessions);
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

            ViewBag.RecentSessions = recentSessions;
            return View();
        }

        // POST: Start new active workout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartNewWorkout(string sessionName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var session = new WorkoutSession
            {
                UserId = userId ?? string.Empty,
                SessionName = sessionName,
                SessionDate = DateTime.Today,
                Notes = ""
            };

            _context.WorkoutSessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ActiveWorkout), new { id = session.Id });
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

            ViewBag.ExerciseList = exercises;
            return View(session);
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

            return Json(new { success = true });
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

            await _context.SaveChangesAsync();

            // Generate AI summary
            var summary = await GenerateWorkoutSummary(session);

            return Json(new { success = true, summary });
        }

        private async Task<string> GenerateWorkoutSummary(WorkoutSession session)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) return "Workout saved successfully!";

                var sb = new StringBuilder();
                sb.AppendLine($"You are an expert personal trainer. Analyze this workout in detail and provide specific, data-driven feedback.");
                sb.AppendLine($"Workout: {session.SessionName}");
                sb.AppendLine($"Date: {session.SessionDate:MMMM d, yyyy}");
                sb.AppendLine("\nExercises performed:");
                foreach (var e in session.Entries)
                    sb.AppendLine($"- {e.ExerciseName} ({e.MuscleGroup}): {e.Sets} sets x {e.Reps} reps @ {e.WeightLbs}lbs (total volume: {e.Sets * e.Reps * e.WeightLbs}lbs)");

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
                var jsonDoc = JsonDocument.Parse(responseContent);
                return jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "Workout saved!";
            }
            catch
            {
                return "Workout saved successfully!";
            }
        }

        // GET: Create a new workout session
        public IActionResult CreateSession()
        {
            return View();
        }

        // POST: Create a new workout session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(WorkoutSession session)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            session.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");
            ModelState.Remove("Entries");

            if (ModelState.IsValid)
            {
                _context.WorkoutSessions.Add(session);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(SessionDetail), new { id = session.Id });
            }
            return View(session);
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

            var prs = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .ToList()
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            ViewBag.Exercises = exercises;
            ViewBag.PersonalRecords = prs;

            return View(session);
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

            return RedirectToAction(nameof(Index));
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

        // GET: Create standalone entry
        public IActionResult Create(string? exerciseName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(exerciseName))
            {
                var lastSession = _context.WorkoutEntries
                    .Where(w => w.UserId == userId && w.ExerciseName == exerciseName)
                    .OrderByDescending(w => w.WorkoutDate)
                    .FirstOrDefault();

                ViewBag.LastSession = lastSession;
                ViewBag.ExerciseName = exerciseName;
            }

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();

            ViewBag.Exercises = exercises;
            return View();
        }

        // POST: Create standalone entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry workoutEntry)
        {
            workoutEntry.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            ModelState.Remove("UserId");
            ModelState.Remove("Session");

            if (ModelState.IsValid)
            {
                _context.Add(workoutEntry);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();
            ViewBag.Exercises = exercises;
            return View(workoutEntry);
        }

        // GET: Edit entry
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(workoutEntry);
        }

        // POST: Edit entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted,SessionId")] WorkoutEntry workoutEntry)
        {
            if (id != workoutEntry.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            workoutEntry.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");
            ModelState.Remove("Session");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(workoutEntry);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WorkoutEntryExists(workoutEntry.Id))
                        return NotFound();
                    else
                        throw;
                }

                if (workoutEntry.SessionId.HasValue)
                    return RedirectToAction(nameof(SessionDetail), new { id = workoutEntry.SessionId });

                return RedirectToAction(nameof(Index));
            }
            return View(workoutEntry);
        }

        // GET: Delete entry
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(workoutEntry);
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

            ViewBag.ExerciseName = exerciseName;
            ViewBag.PR = pr;
            return View(history);
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

    public class EndWorkoutRequest
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string? Notes { get; set; }
    }
}