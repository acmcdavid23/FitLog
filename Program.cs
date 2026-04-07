using FitLog.Data;
using FitLog.Models;
using FitLog.Services;
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

            builder.Services.AddDefaultIdentity<IdentityUser>(options =>
                options.SignIn.RequireConfirmedAccount = false)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddTransient<IEmailService, EmailService>();
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                string[] roles = { "Admin", "User" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }

                string adminEmail = "admin@fitlog.com";
                string adminPassword = "Admin123!";
                IdentityUser? adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                    await userManager.CreateAsync(adminUser, adminPassword);
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }

                string testEmail = "user@fitlog.com";
                string testPassword = "User123!";
                IdentityUser? testUser = await userManager.FindByEmailAsync(testEmail);
                if (testUser == null)
                {
                    testUser = new IdentityUser { UserName = testEmail, Email = testEmail, EmailConfirmed = true };
                    await userManager.CreateAsync(testUser, testPassword);
                    await userManager.AddToRoleAsync(testUser, "User");
                }

                if (!context.UserSettings.Any(s => s.UserId == testUser.Id))
                {
                    context.UserSettings.Add(new UserSettings
                    {
                        UserId = testUser.Id,
                        DisplayName = "FitLog User",
                        CalorieGoal = 2800,
                        ProteinGoal = 200,
                        CarbGoal = 300,
                        FatGoal = 80,
                        WaterGoal = 128,
                        WeightUnit = "lbs",
                        FitnessGoal = "Hypertrophy",
                        BodyGoal = "Bulk",
                        CurrentWeight = 185,
                        GoalWeight = 195,
                        HeightInches = 71,
                        GoalTimeframeWeeks = 12,
                        Age = 22,
                        Gender = "Male",
                        ShowOnLeaderboard = true
                    });
                    await context.SaveChangesAsync();
                }

                if (!context.UserSettings.Any(s => s.UserId == adminUser.Id))
                {
                    context.UserSettings.Add(new UserSettings
                    {
                        UserId = adminUser.Id,
                        DisplayName = "Admin",
                        CalorieGoal = 2500,
                        ProteinGoal = 180,
                        CarbGoal = 250,
                        FatGoal = 70,
                        WaterGoal = 128,
                        WeightUnit = "lbs",
                        FitnessGoal = "General Fitness",
                        BodyGoal = "Maintain",
                        CurrentWeight = 175,
                        GoalWeight = 175,
                        HeightInches = 70,
                        GoalTimeframeWeeks = 12,
                        Age = 30,
                        Gender = "Male",
                        ShowOnLeaderboard = true
                    });
                    await context.SaveChangesAsync();
                }

                if (!context.Exercises.Any())
                {
                    var exercises = new List<Exercise>
                    {
                        new Exercise { Name = "Bench Press", MuscleGroup = "Chest", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The bench press is the king of upper body strength exercises.", Tips = "Keep your shoulder blades retracted.\nMaintain a slight arch in your lower back.\nGrip the bar just outside shoulder width.\nLower the bar to your mid-chest under control.\nDrive your feet into the floor for leg drive.", IsSystemExercise = true },
                        new Exercise { Name = "Incline Dumbbell Press", MuscleGroup = "Chest", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the upper chest with greater range of motion.", Tips = "Set the bench to 30-45 degrees.\nControl the descent.\nPress the dumbbells together at the top.\nKeep elbows at roughly 75 degrees from torso.", IsSystemExercise = true },
                        new Exercise { Name = "Cable Fly", MuscleGroup = "Chest", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Keeps constant tension on the chest throughout the movement.", Tips = "Maintain a slight bend in your elbows.\nFocus on squeezing the chest at peak contraction.\nControl the weight on the way back.", IsSystemExercise = true },
                        new Exercise { Name = "Push Up", MuscleGroup = "Chest", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A fundamental bodyweight exercise for chest endurance.", Tips = "Keep your body in a straight line.\nPlace hands slightly wider than shoulder width.\nLower your chest to just above the floor.", IsSystemExercise = true },
                        new Exercise { Name = "Deadlift", MuscleGroup = "Back", Category = "Strength", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "3-5", Description = "The ultimate full-body strength exercise.", Tips = "Keep the bar close to your body.\nHinge at the hips.\nKeep your chest up and spine neutral.\nDrive through the floor with your legs.", IsSystemExercise = true },
                        new Exercise { Name = "Barbell Row", MuscleGroup = "Back", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "A compound pulling movement for upper and mid back thickness.", Tips = "Hinge forward to about 45 degrees.\nPull the bar to your lower chest.\nKeep elbows close to your body.\nSqueeze shoulder blades at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Pull Up", MuscleGroup = "Back", Category = "Strength", Equipment = "Pull Up Bar", RecommendedSets = 4, RecommendedReps = "5-8", Description = "One of the best bodyweight exercises for back width.", Tips = "Start from a dead hang.\nPull your elbows down and back.\nAim to get your chin over the bar.\nLower yourself under full control.", IsSystemExercise = true },
                        new Exercise { Name = "Lat Pulldown", MuscleGroup = "Back", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the lats for back width.", Tips = "Lean back slightly and pull bar to upper chest.\nFocus on pulling with your elbows.\nStretch fully at the top of each rep.", IsSystemExercise = true },
                        new Exercise { Name = "Seated Cable Row", MuscleGroup = "Back", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Builds mid-back thickness and improves posture.", Tips = "Keep your torso upright.\nPull the handle to your lower chest.\nSqueeze shoulder blades at full contraction.", IsSystemExercise = true },
                        new Exercise { Name = "Face Pull", MuscleGroup = "Back", Category = "Conditioning", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "Targets the rear deltoids and external rotators.", Tips = "Set cable at face height.\nPull to your face keeping elbows high.\nFinish with hands beside your ears.", IsSystemExercise = true },
                        new Exercise { Name = "Overhead Press", MuscleGroup = "Shoulders", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-6", Description = "The standing overhead press builds total shoulder strength.", Tips = "Brace your core and squeeze your glutes.\nPress the bar in a straight line.\nLock out your elbows fully at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Dumbbell Lateral Raise", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the lateral head of the deltoid for shoulder width.", Tips = "Lead with your elbows.\nRaise to just above shoulder height.\nControl the descent.", IsSystemExercise = true },
                        new Exercise { Name = "Arnold Press", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "A rotational shoulder press hitting all three deltoid heads.", Tips = "Start with palms facing you at chin height.\nRotate your wrists as you press up.\nControl the rotation on the way down.", IsSystemExercise = true },
                        new Exercise { Name = "Cable Reverse Fly", MuscleGroup = "Shoulders", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the rear deltoids for balanced shoulder development.", Tips = "Set cables at face height and cross the handles.\nKeep a slight bend in your elbows.\nSqueeze the rear delts at full extension.", IsSystemExercise = true },
                        new Exercise { Name = "Barbell Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "The foundational bicep exercise for size and strength.", Tips = "Keep your elbows pinned to your sides.\nFully extend at the bottom.\nSqueeze hard at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Dumbbell Hammer Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the brachialis for overall arm thickness.", Tips = "Keep palms facing each other throughout.\nControl the weight.\nCurl to shoulder height and squeeze at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Incline Dumbbell Curl", MuscleGroup = "Biceps", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Greater stretch on the long head of the bicep.", Tips = "Set the bench to 45-60 degrees.\nLet your arms hang fully at the bottom.\nCurl without letting elbows move forward.", IsSystemExercise = true },
                        new Exercise { Name = "Close Grip Bench Press", MuscleGroup = "Triceps", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "The most effective tricep mass builder.", Tips = "Use a shoulder-width grip.\nKeep your elbows close to your body.\nFocus on pushing through your triceps.", IsSystemExercise = true },
                        new Exercise { Name = "Cable Pushdown", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolation exercise targeting the lateral head of the tricep.", Tips = "Keep your elbows pinned at your sides.\nFully extend your arms at the bottom.\nControl the weight on the way up.", IsSystemExercise = true },
                        new Exercise { Name = "Overhead Tricep Extension", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the long head of the tricep.", Tips = "Keep your elbows pointed forward.\nGet a full stretch at the bottom.\nExtend fully at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Skull Crusher", MuscleGroup = "Triceps", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Lying tricep extension loading the long head heavily.", Tips = "Lower the bar to your forehead or just behind your head.\nKeep upper arms perpendicular to the floor.\nExtend fully at the top.", IsSystemExercise = true },
                        new Exercise { Name = "Squat", MuscleGroup = "Legs", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The king of lower body exercises.", Tips = "Keep your chest up and core braced.\nPush your knees out in line with your toes.\nDescend until hips are at or below parallel.\nDrive through your heels to stand.", IsSystemExercise = true },
                        new Exercise { Name = "Romanian Deadlift", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the hamstrings and glutes with a hip hinge.", Tips = "Keep a slight bend in your knees.\nHinge at the hips and push them back.\nFeel a deep stretch in your hamstrings at the bottom.", IsSystemExercise = true },
                        new Exercise { Name = "Leg Press", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "10-12", Description = "Machine-based quad dominant movement.", Tips = "Place feet shoulder width on the platform.\nDo not lock out your knees.\nLower until knees reach 90 degrees.", IsSystemExercise = true },
                        new Exercise { Name = "Walking Lunge", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12 each leg", Description = "Builds unilateral leg strength and stability.", Tips = "Take a long stride forward.\nLower your back knee toward the floor.\nKeep your front knee over your ankle.", IsSystemExercise = true },
                        new Exercise { Name = "Leg Curl", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the hamstrings for targeted development.", Tips = "Curl fully to your glutes and squeeze at the top.\nControl the negative slowly.\nAvoid letting your hips rise off the pad.", IsSystemExercise = true },
                        new Exercise { Name = "Calf Raise", MuscleGroup = "Legs", Category = "Hypertrophy", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "15-20", Description = "Targets the gastrocnemius and soleus.", Tips = "Get a full stretch at the bottom of every rep.\nHold the contraction at the top.\nVary foot position to target different areas.", IsSystemExercise = true },
                        new Exercise { Name = "Plank", MuscleGroup = "Core", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "30-60 sec", Description = "The foundational core stability exercise.", Tips = "Keep your body in a straight line.\nEngage your glutes and brace your abs hard.\nDo not let your hips sag or pike up.", IsSystemExercise = true },
                        new Exercise { Name = "Cable Crunch", MuscleGroup = "Core", Category = "Hypertrophy", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A loaded ab exercise allowing progressive overload.", Tips = "Kneel facing the cable machine.\nCrunch down by flexing your spine.\nTouch your elbows to your knees at the bottom.", IsSystemExercise = true },
                        new Exercise { Name = "Hanging Leg Raise", MuscleGroup = "Core", Category = "Conditioning", Equipment = "Pull Up Bar", RecommendedSets = 3, RecommendedReps = "10-15", Description = "Targets the lower abs and hip flexors.", Tips = "Hang from a bar with shoulder width grip.\nRaise your legs by flexing your hips.\nAvoid swinging.", IsSystemExercise = true },
                        new Exercise { Name = "Ab Wheel Rollout", MuscleGroup = "Core", Category = "Strength", Equipment = "Ab Wheel", RecommendedSets = 3, RecommendedReps = "8-12", Description = "One of the most effective core exercises for anti-extension strength.", Tips = "Start on your knees with the wheel below your shoulders.\nRoll out slowly keeping your core braced.\nPull back by contracting your abs.", IsSystemExercise = true },
                        new Exercise { Name = "Treadmill Run", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Treadmill", RecommendedSets = 1, RecommendedReps = "20-30 min", Description = "Steady state cardio for cardiovascular health.", Tips = "Maintain an upright posture.\nLand with your foot under your center of mass.\nAim for a pace where you can still hold a conversation.", IsSystemExercise = true },
                        new Exercise { Name = "Rowing Machine", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Rowing Machine", RecommendedSets = 1, RecommendedReps = "15-20 min", Description = "Full body cardio that also works the back and arms.", Tips = "Drive with your legs first, then lean back, then pull with your arms.\nMaintain a strong posture throughout.", IsSystemExercise = true },
                        new Exercise { Name = "Jump Rope", MuscleGroup = "Cardio", Category = "Conditioning", Equipment = "Jump Rope", RecommendedSets = 3, RecommendedReps = "3-5 min", Description = "High intensity cardio improving coordination and footwork.", Tips = "Jump on the balls of your feet.\nKeep your elbows close to your sides.\nUse your wrists to turn the rope.", IsSystemExercise = true },
                        new Exercise { Name = "Clean and Press", MuscleGroup = "Full Body", Category = "Strength", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "A total body power movement combining a clean and overhead press.", Tips = "Start with the bar at mid-shin.\nExplosively extend your hips and shrug.\nDrop under the bar and catch it at shoulder height.", IsSystemExercise = true },
                        new Exercise { Name = "Kettlebell Swing", MuscleGroup = "Full Body", Category = "Conditioning", Equipment = "Kettlebell", RecommendedSets = 4, RecommendedReps = "15-20", Description = "A ballistic hip hinge building posterior chain power.", Tips = "Hinge at the hips not the knees.\nDrive your hips forward explosively.\nThe swing is powered by your hips not your arms.", IsSystemExercise = true },
                        new Exercise { Name = "Burpee", MuscleGroup = "Full Body", Category = "Conditioning", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "10-15", Description = "A high intensity full body conditioning exercise.", Tips = "Move at a consistent pace.\nKeep your core engaged during the plank position.\nScale by stepping instead of jumping if needed.", IsSystemExercise = true },
                    };
                    context.Exercises.AddRange(exercises);
                    await context.SaveChangesAsync();
                }

                if (!context.SupplementLibraryItems.Any())
                {
                    var supplements = new List<SupplementLibraryItem>
                    {
                        new SupplementLibraryItem { Name = "Creatine Monohydrate", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3-5g daily", WhenToTake = "Any time, consistency matters most", Description = "The most researched supplement in sports nutrition.", Benefits = "Increased strength and power output\nImproved muscle mass over time\nEnhanced high-intensity exercise performance", InfoUrl = "https://examine.com/supplements/creatine/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Caffeine", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3-6mg per kg bodyweight", WhenToTake = "30-60 minutes before training", Description = "Blocks adenosine receptors, reducing perceived fatigue.", Benefits = "Increased strength and endurance\nReduced perceived effort\nImproved focus and alertness", InfoUrl = "https://examine.com/supplements/caffeine/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Whey Protein", Category = "Recovery", EvidenceLevel = "Strong", RecommendedDosage = "20-40g per serving", WhenToTake = "Post-workout or any time to hit protein goals", Description = "Fast-digesting complete protein for muscle protein synthesis.", Benefits = "Supports muscle protein synthesis\nConvenient way to hit daily protein targets", InfoUrl = "https://examine.com/supplements/whey-protein/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Magnesium", Category = "Recovery", EvidenceLevel = "Moderate", RecommendedDosage = "200-400mg", WhenToTake = "Before bed", Description = "Involved in over 300 enzymatic reactions.", Benefits = "Improved sleep quality\nReduced muscle cramps\nSupports energy production", InfoUrl = "https://examine.com/supplements/magnesium/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Vitamin D3", Category = "Vitamins & Minerals", EvidenceLevel = "Strong", RecommendedDosage = "1000-4000 IU daily", WhenToTake = "With a meal containing fat", Description = "Essential for bone health, immune function, and hormonal balance.", Benefits = "Bone health and calcium absorption\nImmune system support\nMay support testosterone production", InfoUrl = "https://examine.com/supplements/vitamin-d/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Omega-3 Fish Oil", Category = "Health", EvidenceLevel = "Strong", RecommendedDosage = "1-3g EPA+DHA daily", WhenToTake = "With meals", Description = "EPA and DHA support cardiovascular health and reduce inflammation.", Benefits = "Reduced inflammation\nCardiovascular health support\nJoint health support", InfoUrl = "https://examine.com/supplements/fish-oil/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Ashwagandha", Category = "Recovery", EvidenceLevel = "Moderate", RecommendedDosage = "300-600mg KSM-66 extract", WhenToTake = "Morning or before bed", Description = "An adaptogen that helps the body manage stress.", Benefits = "Reduced cortisol and stress\nImproved strength and muscle mass\nBetter sleep quality", InfoUrl = "https://examine.com/supplements/ashwagandha/", IsRecommended = true },
                        new SupplementLibraryItem { Name = "Beta-Alanine", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3.2-6.4g daily", WhenToTake = "Split into multiple doses throughout the day", Description = "Increases muscle carnosine levels, buffering acid buildup.", Benefits = "Delayed muscular fatigue\nImproved endurance in 1-4 minute efforts", InfoUrl = "https://examine.com/supplements/beta-alanine/", IsRecommended = false },
                        new SupplementLibraryItem { Name = "Zinc", Category = "Vitamins & Minerals", EvidenceLevel = "Moderate", RecommendedDosage = "25-45mg daily", WhenToTake = "Before bed or with meals", Description = "Essential for testosterone production and immune function.", Benefits = "Supports testosterone production\nImmune system function\nProtein synthesis support", InfoUrl = "https://examine.com/supplements/zinc/", IsRecommended = false },
                        new SupplementLibraryItem { Name = "Protein Powder", Category = "Weight Management", EvidenceLevel = "Strong", RecommendedDosage = "20-40g per serving as needed", WhenToTake = "Any time to hit protein goals", Description = "High protein intake supports satiety and preserves muscle.", Benefits = "Increased satiety\nMuscle preservation during fat loss\nHigher thermic effect", InfoUrl = "https://examine.com/supplements/whey-protein/", IsRecommended = true },
                    };
                    context.SupplementLibraryItems.AddRange(supplements);
                    await context.SaveChangesAsync();
                }

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

                if (!context.NutritionLogs.Any())
                {
                    var nutritionLogs = new List<NutritionLog>();
                    for (int i = 6; i >= 0; i--)
                    {
                        var logDate = DateTime.Today.AddDays(-i);
                        nutritionLogs.AddRange(new[]
                        {
                            new NutritionLog { UserId = testUser.Id, LogDate = logDate, MealName = "Breakfast", FoodItem = "Eggs and Oatmeal", Calories = 520, Protein = 32, Carbs = 58, Fat = 14, ServingSize = "2 eggs + 1 cup oats" },
                            new NutritionLog { UserId = testUser.Id, LogDate = logDate, MealName = "Lunch", FoodItem = "Grilled Chicken Breast with Rice", Calories = 620, Protein = 52, Carbs = 68, Fat = 10, ServingSize = "6oz chicken + 1 cup rice" },
                            new NutritionLog { UserId = testUser.Id, LogDate = logDate, MealName = "Snack", FoodItem = "Greek Yogurt with Blueberries", Calories = 180, Protein = 18, Carbs = 22, Fat = 3, ServingSize = "1 cup" },
                            new NutritionLog { UserId = testUser.Id, LogDate = logDate, MealName = "Dinner", FoodItem = "Salmon with Sweet Potato", Calories = 680, Protein = 48, Carbs = 62, Fat = 22, ServingSize = "6oz salmon + 1 medium potato" },
                            new NutritionLog { UserId = testUser.Id, LogDate = logDate, MealName = "Post-Workout", FoodItem = "Whey Protein Shake", Calories = 160, Protein = 30, Carbs = 8, Fat = 2, ServingSize = "1 scoop" },
                        });
                    }
                    context.NutritionLogs.AddRange(nutritionLogs);
                    await context.SaveChangesAsync();
                }

                if (!context.WeightLogs.Any())
                {
                    var weightLogs = new List<WeightLog>();
                    decimal startWeight = 188m;
                    for (int i = 29; i >= 0; i--)
                    {
                        var variation = (decimal)(new Random(i).NextDouble() * 1.4 - 0.4);
                        var trend = (29 - i) * 0.1m;
                        weightLogs.Add(new WeightLog
                        {
                            UserId = testUser.Id,
                            LogDate = DateTime.Today.AddDays(-i),
                            WeightLbs = Math.Round(startWeight - trend + variation, 1),
                            Notes = i == 0 ? "Morning weigh-in" : ""
                        });
                    }
                    context.WeightLogs.AddRange(weightLogs);
                    await context.SaveChangesAsync();
                }

                if (!context.Supplements.Any(s => s.UserId == testUser.Id))
                {
                    context.Supplements.AddRange(new[]
                    {
                        new Supplement { UserId = testUser.Id, Name = "Creatine Monohydrate", Dosage = "5g", TimeToTake = "Any time", Notes = "Mix with water or protein shake", IsActive = true },
                        new Supplement { UserId = testUser.Id, Name = "Whey Protein", Dosage = "1 scoop (25g protein)", TimeToTake = "Post-Workout", Notes = "Mix with 8oz milk or water", IsActive = true },
                        new Supplement { UserId = testUser.Id, Name = "Vitamin D3", Dosage = "2000 IU", TimeToTake = "Morning", Notes = "Take with breakfast", IsActive = true },
                        new Supplement { UserId = testUser.Id, Name = "Omega-3 Fish Oil", Dosage = "2g EPA+DHA", TimeToTake = "With Meals", Notes = "Take with largest meal", IsActive = true },
                        new Supplement { UserId = testUser.Id, Name = "Magnesium Glycinate", Dosage = "400mg", TimeToTake = "Before Bed", Notes = "Helps with sleep and recovery", IsActive = true },
                    });
                    await context.SaveChangesAsync();
                }

                if (!context.WaterLogs.Any(w => w.UserId == testUser.Id))
                {
                    var waterLogs = new List<WaterLog>();
                    for (int i = 6; i >= 0; i--)
                    {
                        waterLogs.Add(new WaterLog
                        {
                            UserId = testUser.Id,
                            LogDate = DateTime.Today.AddDays(-i),
                            AmountOz = 96 + (i % 3) * 16,
                            DailyGoalOz = 128
                        });
                    }
                    context.WaterLogs.AddRange(waterLogs);
                    await context.SaveChangesAsync();
                }
            }

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
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();
            app.Run();
        }
    }
}