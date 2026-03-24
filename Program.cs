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
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

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

                // Seed exercise library
                if (!context.Exercises.Any())
                {
                    var exercises = new List<Exercise>
    {
        // CHEST
        new Exercise { Name = "Bench Press", MuscleGroup = "Chest", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The bench press is the king of upper body strength exercises. It targets the pectorals, anterior deltoids, and triceps.", Tips = "Keep your shoulder blades retracted and depressed throughout the lift.\nMaintain a slight arch in your lower back.\nGrip the bar just outside shoulder width.\nLower the bar to your mid-chest under control.\nDrive your feet into the floor for leg drive.", IsSystemExercise = true },
        new Exercise { Name = "Incline Dumbbell Press", MuscleGroup = "Chest", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the upper chest with a greater range of motion than barbell variations.", Tips = "Set the bench to 30-45 degrees.\nControl the descent, pause briefly at the bottom.\nPress the dumbbells together at the top to maximize chest contraction.\nKeep elbows at roughly 75 degrees from your torso.", IsSystemExercise = true },
        new Exercise { Name = "Cable Fly", MuscleGroup = "Chest", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "An isolation exercise that keeps constant tension on the chest throughout the movement.", Tips = "Maintain a slight bend in your elbows throughout.\nFocus on squeezing the chest at the point of peak contraction.\nControl the weight on the way back — don't let it pull your arms too far back.\nVary the cable height to target different areas of the chest.", IsSystemExercise = true },
        new Exercise { Name = "Push Up", MuscleGroup = "Chest", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A fundamental bodyweight exercise that builds chest, shoulder, and tricep endurance.", Tips = "Keep your body in a straight line from head to heels.\nPlace hands slightly wider than shoulder width.\nLower your chest to just above the floor.\nEngage your core throughout the movement.", IsSystemExercise = true },

        // BACK
        new Exercise { Name = "Deadlift", MuscleGroup = "Back", Category = "Strength", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "3-5", Description = "The deadlift is the ultimate full-body strength exercise, primarily targeting the posterior chain.", Tips = "Keep the bar close to your body throughout the lift.\nHinge at the hips, not the waist.\nKeep your chest up and spine neutral.\nDrive through the floor with your legs.\nLock out by squeezing your glutes at the top.", IsSystemExercise = true },
        new Exercise { Name = "Barbell Row", MuscleGroup = "Back", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "A compound pulling movement that builds thickness in the upper and mid back.", Tips = "Hinge forward to about 45 degrees.\nPull the bar to your lower chest or upper abdomen.\nKeep your elbows close to your body.\nSqueeze your shoulder blades together at the top.\nControl the descent — don't let the bar drop.", IsSystemExercise = true },
        new Exercise { Name = "Pull Up", MuscleGroup = "Back", Category = "Strength", Equipment = "Pull Up Bar", RecommendedSets = 4, RecommendedReps = "5-8", Description = "One of the best bodyweight exercises for building back width and relative strength.", Tips = "Start from a dead hang with arms fully extended.\nPull your elbows down and back.\nAim to get your chin over the bar.\nAvoid swinging or using momentum.\nLower yourself under full control.", IsSystemExercise = true },
        new Exercise { Name = "Lat Pulldown", MuscleGroup = "Back", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the latissimus dorsi for back width. Great for those working toward pull ups.", Tips = "Lean back slightly and pull the bar to your upper chest.\nFocus on pulling with your elbows, not your hands.\nStretch fully at the top of each rep.\nAvoid using momentum to swing the weight.", IsSystemExercise = true },
        new Exercise { Name = "Seated Cable Row", MuscleGroup = "Back", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Builds mid-back thickness and improves posture.", Tips = "Keep your torso upright and avoid excessive leaning.\nPull the handle to your lower chest.\nSqueeze your shoulder blades at full contraction.\nControl the weight on the way forward — get a full stretch.", IsSystemExercise = true },
        new Exercise { Name = "Face Pull", MuscleGroup = "Back", Category = "Conditioning", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "Targets the rear deltoids and external rotators. Essential for shoulder health.", Tips = "Set the cable at face height or slightly above.\nPull to your face, keeping elbows high.\nFinish with your hands beside your ears and elbows flared out.\nFocus on squeezing the rear delts and external rotators.", IsSystemExercise = true },

        // SHOULDERS
        new Exercise { Name = "Overhead Press", MuscleGroup = "Shoulders", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-6", Description = "The standing overhead press builds total shoulder strength and upper body stability.", Tips = "Brace your core and squeeze your glutes throughout.\nPress the bar in a straight line, moving your head back slightly as the bar passes.\nLock out your elbows fully at the top.\nLower the bar under control to your clavicles.", IsSystemExercise = true },
        new Exercise { Name = "Dumbbell Lateral Raise", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the lateral head of the deltoid for shoulder width.", Tips = "Lead with your elbows, not your hands.\nRaise to just above shoulder height.\nControl the descent — the negative is where growth happens.\nAvoid swinging or using momentum.", IsSystemExercise = true },
        new Exercise { Name = "Arnold Press", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "A rotational shoulder press that hits all three heads of the deltoid.", Tips = "Start with palms facing you at chin height.\nRotate your wrists as you press up so palms face forward at the top.\nControl the rotation on the way down.\nKeep your core engaged throughout.", IsSystemExercise = true },
        new Exercise { Name = "Cable Reverse Fly", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the rear deltoids for balanced shoulder development.", Tips = "Set cables at face height and cross the handles.\nKeep a slight bend in your elbows.\nPull your arms back and out to your sides.\nSqueeze the rear delts at full extension.", IsSystemExercise = true },

        // BICEPS
        new Exercise { Name = "Barbell Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "The foundational bicep exercise for building arm size and strength.", Tips = "Keep your elbows pinned to your sides throughout.\nFully extend at the bottom for a complete stretch.\nSqueeze hard at the top.\nAvoid swinging your body to lift the weight.", IsSystemExercise = true },
        new Exercise { Name = "Dumbbell Hammer Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the brachialis and brachioradialis in addition to the biceps for overall arm thickness.", Tips = "Keep palms facing each other throughout the movement.\nControl the weight — don't swing.\nCurl to shoulder height and squeeze at the top.\nAlternate arms or curl simultaneously.", IsSystemExercise = true },
        new Exercise { Name = "Incline Dumbbell Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "The incline position provides a greater stretch on the long head of the bicep.", Tips = "Set the bench to 45-60 degrees.\nLet your arms hang fully at the bottom.\nCurl without letting your elbows move forward.\nThis is a stretch-focused exercise — feel the stretch at the bottom.", IsSystemExercise = true },

        // TRICEPS
        new Exercise { Name = "Close Grip Bench Press", MuscleGroup = "Triceps", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "The most effective tricep mass builder, also building strength that transfers to the regular bench press.", Tips = "Use a shoulder-width grip — not too narrow.\nKeep your elbows close to your body.\nLower the bar to your lower chest.\nFocus on pushing through your triceps, not your chest.", IsSystemExercise = true },
        new Exercise { Name = "Cable Pushdown", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "An isolation exercise targeting the lateral head of the tricep.", Tips = "Keep your elbows pinned at your sides throughout.\nFully extend your arms at the bottom.\nControl the weight on the way up.\nUse a rope attachment for greater range of motion.", IsSystemExercise = true },
        new Exercise { Name = "Overhead Tricep Extension", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the long head of the tricep which makes up the majority of tricep mass.", Tips = "Keep your elbows pointed forward throughout.\nGet a full stretch at the bottom.\nExtend fully at the top.\nAvoid flaring your elbows out.", IsSystemExercise = true },
        new Exercise { Name = "Skull Crusher", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "10-12", Description = "A lying tricep extension that heavily loads the long head of the tricep.", Tips = "Lower the bar to your forehead or just behind your head.\nKeep your upper arms perpendicular to the floor.\nExtend fully at the top.\nUse an EZ bar to reduce wrist strain.", IsSystemExercise = true },

        // LEGS
        new Exercise { Name = "Squat", MuscleGroup = "Legs", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The squat is the king of lower body exercises, building total leg strength and size.", Tips = "Keep your chest up and core braced throughout.\nPush your knees out in line with your toes.\nDescend until your hips are at or below parallel.\nDrive through your heels to stand.\nKeep the bar in contact with your upper traps.", IsSystemExercise = true },
        new Exercise { Name = "Romanian Deadlift", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the hamstrings and glutes with a hip hinge pattern.", Tips = "Keep a slight bend in your knees throughout.\nHinge at the hips and push them back.\nFeel a deep stretch in your hamstrings at the bottom.\nKeep the bar close to your legs the entire time.\nSqueezeoglutes at the top.", IsSystemExercise = true },
        new Exercise { Name = "Leg Press", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "10-12", Description = "A machine-based quad dominant movement allowing heavy loading without spinal compression.", Tips = "Place feet shoulder width on the platform.\nDo not lock out your knees at the top.\nLower the platform until your knees reach 90 degrees.\nKeep your lower back pressed into the pad.", IsSystemExercise = true },
        new Exercise { Name = "Walking Lunge", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12 each leg", Description = "Builds unilateral leg strength and stability.", Tips = "Take a long stride forward.\nLower your back knee toward the floor without touching it.\nKeep your front knee over your ankle.\nStay upright — avoid leaning forward.", IsSystemExercise = true },
        new Exercise { Name = "Leg Curl", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the hamstrings for targeted development.", Tips = "Curl fully to your glutes and squeeze at the top.\nControl the negative slowly.\nAvoid letting your hips rise off the pad.\nPoint toes slightly to change muscle emphasis.", IsSystemExercise = true },
        new Exercise { Name = "Calf Raise", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "15-20", Description = "Targets the gastrocnemius and soleus for lower leg development.", Tips = "Get a full stretch at the bottom of every rep.\nHold the contraction at the top for a beat.\nCalves respond well to high volume and slow tempos.\nVary foot position to target different areas.", IsSystemExercise = true },

        // CORE
        new Exercise { Name = "Plank", MuscleGroup = "Core", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "30-60 sec", Description = "The foundational core stability exercise.", Tips = "Keep your body in a straight line from head to heels.\nEngage your glutes and brace your abs hard.\nDo not let your hips sag or pike up.\nBreath normally throughout.", IsSystemExercise = true },
        new Exercise { Name = "Cable Crunch", MuscleGroup = "Core", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A loaded ab exercise that allows progressive overload unlike bodyweight variations.", Tips = "Kneel facing the cable machine.\nCrunch down by flexing your spine, not pulling with your arms.\nTouch your elbows to your knees at the bottom.\nControl the eccentric back up.", IsSystemExercise = true },
        new Exercise { Name = "Hanging Leg Raise", MuscleGroup = "Core", Category = "Conditioning", Equipment = "Pull Up Bar", RecommendedSets = 3, RecommendedReps = "10-15", Description = "Targets the lower abs and hip flexors.", Tips = "Hang from a bar with a shoulder width grip.\nRaise your legs by flexing your hips and curling your pelvis.\nAvoid swinging — use a slow controlled movement.\nFor progression, raise legs to 90 degrees then eventually vertical.", IsSystemExercise = true },
        new Exercise { Name = "Ab Wheel Rollout", MuscleGroup = "Core", Category = "Strength", Equipment = "Ab Wheel", RecommendedSets = 3, RecommendedReps = "8-12", Description = "One of the most effective core exercises for building anti-extension strength.", Tips = "Start on your knees with the wheel directly below your shoulders.\nRoll out slowly, keeping your core braced.\nGo as far as you can without your hips sagging.\nPull back to the start by contracting your abs.", IsSystemExercise = true },

        // CARDIO
        new Exercise { Name = "Treadmill Run", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Treadmill", RecommendedSets = 1, RecommendedReps = "20-30 min", Description = "Steady state cardio for cardiovascular health and endurance.", Tips = "Maintain an upright posture.\nLand with your foot under your center of mass.\nStart at a comfortable pace and gradually increase.\nAim for a pace where you can still hold a conversation.", IsSystemExercise = true },
        new Exercise { Name = "Rowing Machine", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Rowing Machine", RecommendedSets = 1, RecommendedReps = "15-20 min", Description = "Full body cardio that also works the back and arms.", Tips = "Drive with your legs first, then lean back, then pull with your arms.\nReverse the sequence on the return.\nMaintain a strong posture throughout.\nAim for a consistent stroke rate.", IsSystemExercise = true },
        new Exercise { Name = "Jump Rope", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Jump Rope", RecommendedSets = 3, RecommendedReps = "3-5 min", Description = "High intensity cardio that also improves coordination and footwork.", Tips = "Jump on the balls of your feet.\nKeep your elbows close to your sides.\nUse your wrists to turn the rope, not your arms.\nStart with basic jumps before attempting double unders.", IsSystemExercise = true },

        // FULL BODY
        new Exercise { Name = "Clean and Press", MuscleGroup = "Full Body", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "A total body power movement combining a clean and overhead press.", Tips = "Start with the bar at mid-shin.\nExplosively extend your hips and shrug to pull the bar up.\nDrop under the bar and catch it at shoulder height.\nStand up then press the bar overhead.", IsSystemExercise = true },
        new Exercise { Name = "Kettlebell Swing", MuscleGroup = "Full Body", Category = "Conditioning", Equipment = "Kettlebell", RecommendedSets = 4, RecommendedReps = "15-20", Description = "A ballistic hip hinge that builds posterior chain power and cardiovascular fitness.", Tips = "Hinge at the hips, not the knees.\nDrive your hips forward explosively to swing the bell.\nKeep your core braced throughout.\nThe swing is powered by your hips, not your arms.", IsSystemExercise = true },
        new Exercise { Name = "Burpee", MuscleGroup = "Full Body", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "10-15", Description = "A high intensity full body exercise that builds endurance and burns calories.", Tips = "Move at a consistent pace rather than sprinting then stopping.\nKeep your core engaged during the plank position.\nJump your feet outside your hands to stand up faster.\nScale by stepping instead of jumping if needed.", IsSystemExercise = true },
    };

                    context.Exercises.AddRange(exercises);
                    await context.SaveChangesAsync();
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