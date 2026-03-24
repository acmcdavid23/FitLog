using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLog.Controllers
{
    [Authorize]
    public class WaterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAntiforgery _antiforgery;

        public WaterController(ApplicationDbContext context, IAntiforgery antiforgery)
        {
            _context = context;
            _antiforgery = antiforgery;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var todayLogs = _context.WaterLogs
                .Where(w => w.UserId == userId && w.LogDate == today)
                .ToList();

            var totalOz = todayLogs.Sum(w => w.AmountOz);
            var goalOz = 128m;

            var history = _context.WaterLogs
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.LogDate)
                .Take(50)
                .ToList()
                .GroupBy(w => w.LogDate)
                .Select(g => new { Date = g.Key, Total = g.Sum(w => w.AmountOz) })
                .OrderByDescending(x => x.Date)
                .Take(7)
                .ToList();

            ViewBag.TotalOz = totalOz;
            ViewBag.GoalOz = goalOz;
            ViewBag.Percentage = Math.Min((double)(totalOz / goalOz * 100), 100);
            ViewBag.History = history;
            ViewBag.TodayLogs = todayLogs;

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Json(new { token = tokens.RequestToken });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LogAjax(decimal amountOz)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var log = new WaterLog
            {
                UserId = userId ?? string.Empty,
                LogDate = today,
                AmountOz = amountOz,
                DailyGoalOz = 128
            };

            _context.WaterLogs.Add(log);
            _context.SaveChanges();

            var totalOz = _context.WaterLogs
                .Where(w => w.UserId == userId && w.LogDate == today)
                .Sum(w => w.AmountOz);

            var percentage = (double)(totalOz / 128m * 100);

            return Json(new
            {
                id = log.Id,
                amountOz = log.AmountOz,
                totalOz = totalOz,
                percentage = percentage
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAjax(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;

            var log = _context.WaterLogs
                .FirstOrDefault(w => w.Id == id && w.UserId == userId);

            if (log != null)
            {
                _context.WaterLogs.Remove(log);
                _context.SaveChanges();
            }

            var totalOz = _context.WaterLogs
                .Where(w => w.UserId == userId && w.LogDate == today)
                .Sum(w => w.AmountOz);

            var percentage = (double)(totalOz / 128m * 100);

            return Json(new
            {
                totalOz = totalOz,
                percentage = percentage
            });
        }
    }
}