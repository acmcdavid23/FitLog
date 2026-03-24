using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitLog.Controllers
{
    public class ExerciseLibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExerciseLibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public - anyone can browse
        public IActionResult Index(string? search, string? muscleGroup, string? category)
        {
            var exercises = _context.Exercises.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                exercises = exercises.Where(e => e.Name.Contains(search) || e.MuscleGroup.Contains(search));

            if (!string.IsNullOrEmpty(muscleGroup))
                exercises = exercises.Where(e => e.MuscleGroup == muscleGroup);

            if (!string.IsNullOrEmpty(category))
                exercises = exercises.Where(e => e.Category == category);

            ViewBag.Search = search;
            ViewBag.MuscleGroup = muscleGroup;
            ViewBag.Category = category;
            ViewBag.MuscleGroups = _context.Exercises
                .Select(e => e.MuscleGroup)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            ViewBag.Categories = new List<string> { "Strength", "Hypertrophy", "Conditioning" };

            var grouped = exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList()
                .GroupBy(e => e.MuscleGroup)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(grouped);
        }

        // Public - exercise detail
        public IActionResult Details(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null) return NotFound();
            return View(exercise);
        }

        // Admin only - Create
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
            if (ModelState.IsValid)
            {
                _context.Exercises.Add(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(exercise);
        }

        // Admin only - Edit
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

        // Admin only - Delete
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise != null)
            {
                _context.Exercises.Remove(exercise);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}