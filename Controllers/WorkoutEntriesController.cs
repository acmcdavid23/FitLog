using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FitLog.Data;
using FitLog.Models;

namespace FitLog.Controllers
{
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
            return View(await _context.WorkoutEntries.ToListAsync());
        }

        // GET: WorkoutEntries/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id);
            if (workoutEntry == null)
            {
                return NotFound();
            }

            return View(workoutEntry);
        }

        // GET: WorkoutEntries/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: WorkoutEntries/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserId,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry workoutEntry)
        {
            if (ModelState.IsValid)
            {
                _context.Add(workoutEntry);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(workoutEntry);
        }

        // GET: WorkoutEntries/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workoutEntry = await _context.WorkoutEntries.FindAsync(id);
            if (workoutEntry == null)
            {
                return NotFound();
            }
            return View(workoutEntry);
        }

        // POST: WorkoutEntries/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,ExerciseName,MuscleGroup,WorkoutDate,Sets,Reps,WeightLbs,Notes,IsCompleted")] WorkoutEntry workoutEntry)
        {
            if (id != workoutEntry.Id)
            {
                return NotFound();
            }

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
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(workoutEntry);
        }

        // GET: WorkoutEntries/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workoutEntry = await _context.WorkoutEntries
                .FirstOrDefaultAsync(m => m.Id == id);
            if (workoutEntry == null)
            {
                return NotFound();
            }

            return View(workoutEntry);
        }

        // POST: WorkoutEntries/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var workoutEntry = await _context.WorkoutEntries.FindAsync(id);
            if (workoutEntry != null)
            {
                _context.WorkoutEntries.Remove(workoutEntry);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool WorkoutEntryExists(int id)
        {
            return _context.WorkoutEntries.Any(e => e.Id == id);
        }
    }
}
