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
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workouts = await _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WorkoutDate)
                .ToListAsync();

            // Get personal records for PR badges
            var prs = workouts
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            ViewBag.PersonalRecords = prs;
            return View(workouts);
        }

        // GET: WorkoutEntries/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(workoutEntry);
        }

        // GET: WorkoutEntries/Create
        public IActionResult Create(string? exerciseName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get last session data if exercise name is provided
            if (!string.IsNullOrEmpty(exerciseName))
            {
                var lastSession = _context.WorkoutEntries
                    .Where(w => w.UserId == userId && w.ExerciseName == exerciseName)
                    .OrderByDescending(w => w.WorkoutDate)
                    .FirstOrDefault();

                ViewBag.LastSession = lastSession;
                ViewBag.ExerciseName = exerciseName;
            }

            // Get exercise list for dropdown
            var exercises = _context.Exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList();

            ViewBag.Exercises = exercises;
            return View();
        }

        // POST: WorkoutEntries/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry workoutEntry)
        {
            workoutEntry.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            ModelState.Remove("UserId");

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

        // GET: WorkoutEntries/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(workoutEntry);
        }

        // POST: WorkoutEntries/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry workoutEntry)
        {
            if (id != workoutEntry.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            workoutEntry.UserId = userId ?? string.Empty;
            ModelState.Remove("UserId");

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
                return RedirectToAction(nameof(Index));
            }
            return View(workoutEntry);
        }

        // GET: WorkoutEntries/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry == null) return NotFound();

            return View(workoutEntry);
        }

        // POST: WorkoutEntries/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (workoutEntry != null)
                _context.WorkoutEntries.Remove(workoutEntry);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Exercise history for a specific exercise
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