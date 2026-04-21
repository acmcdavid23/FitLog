using FitLog.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitLog.ViewComponents
{
    public class UsernameRequiredViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public UsernameRequiredViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var row = await _context.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (row != null && !string.IsNullOrWhiteSpace(row.Username))
                return Content(string.Empty);

            if (TempData.ContainsKey("UsernameModalError"))
                ViewData["UsernameModalError"] = TempData["UsernameModalError"];

            return View();
        }
    }
}
