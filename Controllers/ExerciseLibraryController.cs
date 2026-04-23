using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.Controllers
{
    public class ExerciseLibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExerciseLibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static readonly string[] StandardEquipment =
        {
            "Barbell", "Dumbbell", "Cable", "Machine", "Bodyweight", "Kettlebell", "Bands", "Other"
        };

        private void SetExerciseLibraryViewBag(string? search, string? muscleGroup, string? equipment)
        {
            ViewBag.Search = search;
            ViewBag.MuscleGroup = muscleGroup;
            ViewBag.Equipment = equipment;
            ViewBag.MuscleGroups = _context.Exercises.Where(e => e.IsSystemExercise).Select(e => e.MuscleGroup).Distinct().OrderBy(m => m).ToList();
            var fromDb = _context.Exercises
                .Where(e => e.IsSystemExercise && !string.IsNullOrEmpty(e.Equipment))
                .Select(e => e.Equipment!)
                .AsEnumerable()
                .SelectMany(eq => eq.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            ViewBag.EquipmentOptions = StandardEquipment
                .Concat(fromDb)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        private Dictionary<string, List<Exercise>> BuildGroupedLibrary(string? userId, string? search, string? muscleGroup, string? equipment)
        {
            if (userId == null)
                ViewBag.PersonalExercises = null;
            var exercises = _context.Exercises.AsQueryable().Where(e => e.IsSystemExercise);

            if (!string.IsNullOrEmpty(search))
                exercises = exercises.Where(e => e.Name.Contains(search) || (e.Description != null && e.Description.Contains(search)));

            if (!string.IsNullOrEmpty(muscleGroup))
                exercises = exercises.Where(e => e.MuscleGroup == muscleGroup);

            if (!string.IsNullOrEmpty(equipment))
                exercises = exercises.Where(e => e.Equipment != null && e.Equipment.Contains(equipment, StringComparison.OrdinalIgnoreCase));

            var grouped = exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList()
                .GroupBy(e => e.MuscleGroup)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (userId != null)
            {
                var personal = _context.Exercises
                    .Where(e => !e.IsSystemExercise && e.CreatedByUserId == userId)
                    .AsQueryable();
                if (!string.IsNullOrEmpty(equipment))
                    personal = personal.Where(e => e.Equipment != null && e.Equipment.Contains(equipment, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(search))
                    personal = personal.Where(e => e.Name.Contains(search) || (e.Description != null && e.Description.Contains(search)));
                ViewBag.PersonalExercises = personal.OrderBy(e => e.Name).ToList();
            }

            return grouped;
        }

        public IActionResult Index(string? search, string? muscleGroup, string? equipment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetExerciseLibraryViewBag(search, muscleGroup, equipment);
            var grouped = BuildGroupedLibrary(userId, search, muscleGroup, equipment);
            return View(grouped);
        }

        [HttpGet]
        public IActionResult LibraryMainPartial(string? search, string? muscleGroup, string? equipment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetExerciseLibraryViewBag(search, muscleGroup, equipment);
            var grouped = BuildGroupedLibrary(userId, search, muscleGroup, equipment);
            return PartialView("_ExerciseLibraryMain", grouped);
        }

        [HttpGet]
        public IActionResult ExerciseModalBody(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                ViewBag.UserSessions = _context.WorkoutSessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SessionDate)
                    .ThenByDescending(s => s.Id)
                    .Take(30)
                    .ToList();
            }

            return PartialView("_ExerciseModalBody", exercise);
        }

        public async Task<IActionResult> Details(int id)
        {
            var exercise = await _context.Exercises.FirstOrDefaultAsync(e => e.Id == id);
            if (exercise == null) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                ViewBag.UserSessions = await _context.WorkoutSessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SessionDate)
                    .ThenByDescending(s => s.Id)
                    .Take(30)
                    .ToListAsync();
            }

            return View(exercise);
        }

        [Authorize]
        public IActionResult CreatePersonal()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePersonal(Exercise exercise)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            exercise.IsSystemExercise = false;
            exercise.CreatedByUserId = userId ?? string.Empty;
            exercise.Category = string.Empty;
            exercise.RecommendedSets = 0;
            if (string.IsNullOrEmpty(exercise.RecommendedReps))
                exercise.RecommendedReps = string.Empty;
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("Category");
            ModelState.Remove("RecommendedSets");
            ModelState.Remove("RecommendedReps");

            if (ModelState.IsValid)
            {
                _context.Exercises.Add(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(exercise);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Exercise exercise)
        {
            exercise.IsSystemExercise = true;
            if (ModelState.IsValid)
            {
                _context.Exercises.Add(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(exercise);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null) return NotFound();
            return View(exercise);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Exercise exercise)
        {
            if (id != exercise.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Exercises.Update(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(exercise);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise != null && (!exercise.IsSystemExercise || User.IsInRole("Admin")))
            {
                _context.Exercises.Remove(exercise);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
