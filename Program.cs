using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitLog
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Create roles
                string[] roles = { "Admin", "User" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }

                // Create default admin
                string adminEmail = "admin@fitlog.com";
                string adminPassword = "Admin123!";
                IdentityUser? adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new IdentityUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };
                    await userManager.CreateAsync(adminUser, adminPassword);
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }

                // Create default test user
                string testEmail = "user@fitlog.com";
                string testPassword = "User123!";
                IdentityUser? testUser = await userManager.FindByEmailAsync(testEmail);
                if (testUser == null)
                {
                    testUser = new IdentityUser
                    {
                        UserName = testEmail,
                        Email = testEmail,
                        EmailConfirmed = true
                    };
                    await userManager.CreateAsync(testUser, testPassword);
                    await userManager.AddToRoleAsync(testUser, "User");
                }

                // Seed workout entries for test user
                if (!context.WorkoutEntries.Any())
                {
                    var entries = new List<WorkoutEntry>
                    {
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Bench Press", MuscleGroup = "Chest", WorkoutDate = DateTime.Now.AddDays(-21), Sets = 4, Reps = 8, WeightLbs = 185, Notes = "Felt strong today", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Bench Press", MuscleGroup = "Chest", WorkoutDate = DateTime.Now.AddDays(-14), Sets = 4, Reps = 8, WeightLbs = 195, Notes = "New PR", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Bench Press", MuscleGroup = "Chest", WorkoutDate = DateTime.Now.AddDays(-7), Sets = 4, Reps = 8, WeightLbs = 200, Notes = "Pushed hard", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Squat", MuscleGroup = "Legs", WorkoutDate = DateTime.Now.AddDays(-20), Sets = 5, Reps = 5, WeightLbs = 225, Notes = "Legs felt heavy", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Squat", MuscleGroup = "Legs", WorkoutDate = DateTime.Now.AddDays(-13), Sets = 5, Reps = 5, WeightLbs = 235, Notes = "Better depth", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Squat", MuscleGroup = "Legs", WorkoutDate = DateTime.Now.AddDays(-6), Sets = 5, Reps = 5, WeightLbs = 245, Notes = "Solid session", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Deadlift", MuscleGroup = "Back", WorkoutDate = DateTime.Now.AddDays(-19), Sets = 3, Reps = 5, WeightLbs = 275, Notes = "Form was good", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Deadlift", MuscleGroup = "Back", WorkoutDate = DateTime.Now.AddDays(-12), Sets = 3, Reps = 5, WeightLbs = 285, Notes = "Grip was the limiter", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Deadlift", MuscleGroup = "Back", WorkoutDate = DateTime.Now.AddDays(-5), Sets = 3, Reps = 5, WeightLbs = 295, Notes = "New PR", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Overhead Press", MuscleGroup = "Shoulders", WorkoutDate = DateTime.Now.AddDays(-18), Sets = 4, Reps = 6, WeightLbs = 115, Notes = "Shoulder felt tight", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Overhead Press", MuscleGroup = "Shoulders", WorkoutDate = DateTime.Now.AddDays(-11), Sets = 4, Reps = 6, WeightLbs = 120, Notes = "Much better", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Pull Ups", MuscleGroup = "Back", WorkoutDate = DateTime.Now.AddDays(-17), Sets = 4, Reps = 8, WeightLbs = 0, Notes = "Bodyweight", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Cable Pushdown", MuscleGroup = "Triceps", WorkoutDate = DateTime.Now.AddDays(-16), Sets = 3, Reps = 12, WeightLbs = 50, Notes = "Good pump", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Face Pull", MuscleGroup = "Shoulders", WorkoutDate = DateTime.Now.AddDays(-15), Sets = 3, Reps = 15, WeightLbs = 40, Notes = "Rear delt focus", IsCompleted = true },
                        new WorkoutEntry { UserId = testUser.Id, ExerciseName = "Leg Press", MuscleGroup = "Legs", WorkoutDate = DateTime.Now.AddDays(-4), Sets = 4, Reps = 10, WeightLbs = 360, Notes = "High volume day", IsCompleted = true },
                    };

                    context.WorkoutEntries.AddRange(entries);
                    await context.SaveChangesAsync();
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();
            app.Run();
        }
    }
}