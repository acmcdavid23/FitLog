using FitLog.Data;
using FitLog.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitLog;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
                options.SignIn.RequireConfirmedAccount = false)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        builder.Services.AddTransient<IEmailService, EmailService>();
        builder.Services.AddSingleton<ImageProcessService>();
        builder.Services.AddHostedService<WarmupService>();
        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey");
        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            var openAiLogger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("FitLog.OpenAI");
            openAiLogger.LogWarning(
                "OpenAI API key is not set. Set OPENAI_API_KEY or OpenAI__ApiKey in the environment (or dotnet user-secrets) to enable GPT-backed features.");
        }

        await DbInitializer.SeedAsync(app.Services);

        if (app.Environment.IsDevelopment())
            app.UseMigrationsEndPoint();
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
