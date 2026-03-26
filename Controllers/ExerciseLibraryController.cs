using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public IActionResult Index(string? search, string? muscleGroup, string? category)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var exercises = _context.Exercises.AsQueryable().Where(e => e.IsSystemExercise);

            if (!string.IsNullOrEmpty(search))
                exercises = exercises.Where(e => e.Name.Contains(search) || e.Description.Contains(search));

            if (!string.IsNullOrEmpty(muscleGroup))
                exercises = exercises.Where(e => e.MuscleGroup == muscleGroup);

            if (!string.IsNullOrEmpty(category))
                exercises = exercises.Where(e => e.Category == category);

            ViewBag.Search = search;
            ViewBag.MuscleGroup = muscleGroup;
            ViewBag.Category = category;
            ViewBag.MuscleGroups = _context.Exercises.Where(e => e.IsSystemExercise).Select(e => e.MuscleGroup).Distinct().OrderBy(m => m).ToList();
            ViewBag.Categories = new List<string> { "Strength", "Hypertrophy", "Conditioning" };

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
                    .OrderBy(e => e.Name)
                    .ToList();
                ViewBag.PersonalExercises = personal;
            }

            return View(grouped);
        }

        public IActionResult Details(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null) return NotFound();
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
            ModelState.Remove("CreatedByUserId");

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