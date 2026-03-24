using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
}