using FitLog.Data;
using FitLog.Models;
using FitLog.Services;
using FitLog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitLog.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ILogger<AccountController> logger,
        ApplicationDbContext context,
        IEmailService emailService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _context = context;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new AccountLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLoginViewModel model, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            return LocalRedirect(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");
        return View(new AccountRegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(AccountRegisterViewModel model, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;

        if (_context.UserSettings.Any(s => s.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

        if (!ModelState.IsValid)
            return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "User");
        var userId = await _userManager.GetUserIdAsync(user);

        _context.UserSettings.Add(new UserSettings
        {
            UserId = userId,
            DisplayName = model.DisplayName,
            Username = model.Username
        });
        await _context.SaveChangesAsync();

        await _emailService.SendEmailAsync(model.Email, "Welcome to FitLog!",
            $"<h2>Welcome to FitLog, {model.DisplayName}!</h2><p>Your account has been created. Start tracking your fitness journey today.</p>");

        await _signInManager.SignInAsync(user, isPersistent: true);
        return RedirectToAction("Index", "Onboarding");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult Denied() => RedirectToAction(nameof(AccessDenied));
}
