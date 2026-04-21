#nullable disable
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using FitLog.Data;
using FitLog.Models;
using FitLog.Services;

namespace FitLog.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public RegisterModel(UserManager<IdentityUser> userManager, IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager, ILogger<RegisterModel> logger,
            IEmailSender emailSender, ApplicationDbContext context, IEmailService emailService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _emailService = emailService;
        }

        [BindProperty] public InputModel Input { get; set; }
        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(50, ErrorMessage = "Display name can't be longer than 50 characters.")]
            [Display(Name = "Display Name")]
            public string DisplayName { get; set; }

            [Required]
            [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Username can only contain letters and numbers.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3-50 characters.")]
            [Display(Name = "Username")]
            public string Username { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // Check username uniqueness
            if (_context.UserSettings.Any(s => s.Username == Input.Username))
            {
                ModelState.AddModelError("Input.Username", "That username is already taken.");
                return Page();
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");
                    var userId = await _userManager.GetUserIdAsync(user);

                    _context.UserSettings.Add(new UserSettings
                    {
                        UserId = userId,
                        DisplayName = Input.DisplayName,
                        Username = Input.Username
                    });
                    await _context.SaveChangesAsync();

                    await _emailService.SendEmailAsync(Input.Email, "Welcome to FitLog!",
                        $"<h2>Welcome to FitLog, {Input.DisplayName}!</h2><p>Your account has been created. Start tracking your fitness journey today.</p>");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });

                    await _signInManager.SignInAsync(user, isPersistent: true);
                    return LocalRedirect("/Onboarding");
                }
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        private IdentityUser CreateUser()
        {
            try { return Activator.CreateInstance<IdentityUser>(); }
            catch { throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'."); }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("The default UI requires a user store with email support.");
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}