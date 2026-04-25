using FitLog.Data;
using FitLog.Models;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
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
            ViewBag.PersonalExerciseEquipment = StandardEquipment.ToList();
        }

        private ExerciseLibraryIndexPageViewModel BuildGroupedLibrary(string? userId, string? search, string? muscleGroup, string? equipment)
        {
            var exercises = _context.Exercises.AsQueryable().Where(e => e.IsSystemExercise);

            if (!string.IsNullOrEmpty(search))
                exercises = exercises.Where(e => e.Name.Contains(search) || (e.Description != null && e.Description.Contains(search)));

            if (!string.IsNullOrEmpty(muscleGroup))
                exercises = exercises.Where(e => e.MuscleGroup == muscleGroup);

            if (!string.IsNullOrEmpty(equipment))
                exercises = exercises.Where(e => e.Equipment != null && e.Equipment.Contains(equipment, StringComparison.OrdinalIgnoreCase));

            var groupedDict = exercises
                .OrderBy(e => e.MuscleGroup)
                .ThenBy(e => e.Name)
                .ToList()
                .GroupBy(e => e.MuscleGroup)
                .ToDictionary(g => g.Key, g => g.ToList());

            var systemGrouped = groupedDict
                .OrderBy(kv => kv.Key)
                .Select(kv => new ExerciseLibraryGroupedSectionViewModel
                {
                    MuscleGroup = kv.Key,
                    Exercises = kv.Value.Select(ExerciseLibraryCardViewModel.FromEntity).ToList()
                })
                .ToList();

            List<ExerciseLibraryCardViewModel>? personalVm = null;
            if (userId != null)
            {
                var personal = _context.Exercises
                    .Where(e => !e.IsSystemExercise && e.CreatedByUserId == userId)
                    .AsQueryable();
                if (!string.IsNullOrEmpty(equipment))
                    personal = personal.Where(e => e.Equipment != null && e.Equipment.Contains(equipment, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(search))
                    personal = personal.Where(e => e.Name.Contains(search) || (e.Description != null && e.Description.Contains(search)));
                personalVm = personal.OrderBy(e => e.Name).Select(ExerciseLibraryCardViewModel.FromEntity).ToList();
            }

            return new ExerciseLibraryIndexPageViewModel
            {
                SystemGrouped = systemGrouped,
                PersonalExercises = personalVm
            };
        }

        [AllowAnonymous]
        public IActionResult Index(string? search, string? muscleGroup, string? equipment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetExerciseLibraryViewBag(search, muscleGroup, equipment);
            var page = BuildGroupedLibrary(userId, search, muscleGroup, equipment);
            return View(page);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult LibraryMainPartial(string? search, string? muscleGroup, string? equipment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            SetExerciseLibraryViewBag(search, muscleGroup, equipment);
            var page = BuildGroupedLibrary(userId, search, muscleGroup, equipment);
            return PartialView("_ExerciseLibraryMain", page);
        }

        [HttpGet]
        [AllowAnonymous]
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

            return PartialView("_ExerciseModalBody", ExerciseModalViewModel.FromEntity(exercise));
        }

        [AllowAnonymous]
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

            return View(ExerciseModalViewModel.FromEntity(exercise));
        }

        public IActionResult CreatePersonal()
        {
            ViewBag.PersonalExerciseEquipment = StandardEquipment.ToList();
            return View(new ExercisePersonalCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePersonal(ExercisePersonalCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
            {
                ViewBag.PersonalExerciseEquipment = StandardEquipment.ToList();
                return View(model);
            }

            _context.Exercises.Add(model.ToEntity(userId));
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new ExerciseFormViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ExerciseFormViewModel model)
        {
            var exercise = model.ToExercise(createdByUserId: null);
            if (ModelState.IsValid)
            {
                _context.Exercises.Add(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null) return NotFound();
            return View(ExerciseFormViewModel.FromEntity(exercise));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ExerciseFormViewModel model)
        {
            if (id != model.Id) return NotFound();
            var exercise = model.ToExercise(createdByUserId: null);
            if (ModelState.IsValid)
            {
                _context.Exercises.Update(exercise);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAjax(ExerciseFormViewModel model)
        {
            var exercise = model.ToExercise(createdByUserId: null);
            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid exercise." : err });
            }
            _context.Exercises.Add(exercise);
            _context.SaveChanges();
            return Json(new { success = true, message = "Exercise added to library", redirectUrl = Url.Action(nameof(Index)) });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult EditAjax(int id, ExerciseFormViewModel model)
        {
            if (id != model.Id)
                return Json(new { success = false, error = "Invalid exercise." });
            var exercise = model.ToExercise(createdByUserId: null);
            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid exercise." : err });
            }
            _context.Exercises.Update(exercise);
            _context.SaveChanges();
            return Json(new { success = true, message = "Exercise updated.", redirectUrl = Url.Action(nameof(Index)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePersonalAjax(ExercisePersonalCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return Json(new { success = false, error = string.IsNullOrEmpty(err) ? "Invalid exercise." : err });
            }

            _context.Exercises.Add(model.ToEntity(userId));
            _context.SaveChanges();
            return Json(new { success = true, message = "Exercise added to library" });
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
                TempData["Success"] = "Deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var exercise = _context.Exercises.FirstOrDefault(e => e.Id == id);
            if (exercise == null)
                return Json(new { success = false, message = "Exercise not found." });
            if (exercise.IsSystemExercise && !User.IsInRole("Admin"))
                return Json(new { success = false, message = "You cannot delete this exercise." });

            var deletedId = exercise.Id;
            _context.Exercises.Remove(exercise);
            _context.SaveChanges();
            return Json(new { success = true, message = "Exercise removed", deletedId });
        }
    }
}
