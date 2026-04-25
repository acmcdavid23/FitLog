using FitLog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FitLog.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var context = sp.GetRequiredService<ApplicationDbContext>();

        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = Environment.GetEnvironmentVariable("FITLOG_SEED_ADMIN_EMAIL") ?? "admin@fitlog.com";
        var adminPassword = Environment.GetEnvironmentVariable("FITLOG_SEED_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(adminPassword))
            logger.LogWarning("FITLOG_SEED_ADMIN_PASSWORD is not set. Skipping seeded Admin user. Set it via environment variable or dotnet user-secrets.");
        else
            await EnsureUserAsync(userManager, adminEmail, adminPassword, "Admin", logger);

        var demoPassword = Environment.GetEnvironmentVariable("FITLOG_SEED_DEMO_USER_PASSWORD");
        if (string.IsNullOrWhiteSpace(demoPassword))
            logger.LogWarning("FITLOG_SEED_DEMO_USER_PASSWORD is not set. Skipping seeded demo User accounts. Set it via environment variable or dotnet user-secrets.");
        else
        {
            var demos = new[]
            {
                ("user1@fitlog.com", "Alex Demo", "alexdemo1"),
                ("user2@fitlog.com", "Sam Demo", "samdemo2"),
                ("user3@fitlog.com", "Jordan Demo", "jordandemo3")
            };
            foreach (var (email, display, username) in demos)
                await EnsureUserAsync(userManager, email, demoPassword, "User", logger);
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser != null)
            await EnsureUserSettingsAsync(context, adminUser.Id, "Admin", "admin", 2500, 175, 175);

        var ix = 0;
        foreach (var email in new[] { "user1@fitlog.com", "user2@fitlog.com", "user3@fitlog.com" })
        {
            ix++;
            var u = await userManager.FindByEmailAsync(email);
            if (u == null) continue;
            var (display, uname) = ix switch
            {
                1 => ("Alex Demo", "alexdemo1"),
                2 => ("Sam Demo", "samdemo2"),
                _ => ("Jordan Demo", "jordandemo3")
            };
            await EnsureUserSettingsAsync(context, u.Id, display, uname, 2800 - ix * 80, 185 - ix, 195 - ix);
            await SeedUserDomainDataAsync(context, u.Id, ix, logger);
        }

        if (adminUser != null)
            await SeedUserDomainDataAsync(context, adminUser.Id, 0, logger);

        if (!await context.Exercises.AnyAsync())
        {
            context.Exercises.AddRange(GetSeedExercises());
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded exercise library.");
        }

        if (!await context.SupplementLibraryItems.AnyAsync())
        {
            context.SupplementLibraryItems.AddRange(GetSeedSupplements());
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded supplement library.");
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password,
        string role,
        ILogger logger)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Created seeded user {Email} in role {Role}.", email, role);
    }

    private static async Task EnsureUserSettingsAsync(
        ApplicationDbContext context,
        string userId,
        string displayName,
        string username,
        int calorieGoal,
        decimal currentWeight,
        decimal goalWeight)
    {
        if (await context.UserSettings.AnyAsync(s => s.UserId == userId)) return;

        context.UserSettings.Add(new UserSettings
        {
            UserId = userId,
            DisplayName = displayName,
            Username = username,
            CalorieGoal = calorieGoal,
            ProteinGoal = 200,
            CarbGoal = 280,
            FatGoal = 75,
            WaterGoal = 128,
            WeightUnit = "lbs",
            FitnessGoal = "General Fitness",
            BodyGoal = "Recomp",
            CurrentWeight = currentWeight,
            GoalWeight = goalWeight,
            HeightInches = 70,
            GoalTimeframeWeeks = 12,
            Age = 24,
            Gender = "Other",
            ShowOnLeaderboard = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedUserDomainDataAsync(ApplicationDbContext context, string userId, int userIndex, ILogger logger)
    {
        var dayOffset = userIndex * 3;

        if (!await context.WorkoutSessions.AnyAsync(s => s.UserId == userId))
        {
            var s1 = new WorkoutSession
            {
                UserId = userId,
                SessionName = "Seeded Push Day",
                SessionDate = DateTime.Today.AddDays(-10 + dayOffset),
                Notes = "Seeded workout"
            };
            var s2 = new WorkoutSession
            {
                UserId = userId,
                SessionName = "Seeded Leg Day",
                SessionDate = DateTime.Today.AddDays(-3 + dayOffset),
                Notes = "Seeded workout"
            };
            context.WorkoutSessions.AddRange(s1, s2);
            await context.SaveChangesAsync();

            var w = 185 - userIndex * 5m;
            context.WorkoutEntries.AddRange(
                new WorkoutEntry { UserId = userId, SessionId = s1.Id, ExerciseName = "Bench Press", MuscleGroup = "Chest", WorkoutDate = s1.SessionDate, Sets = 4, Reps = 8, WeightLbs = w, Notes = "Seeded", IsCompleted = true },
                new WorkoutEntry { UserId = userId, SessionId = s1.Id, ExerciseName = "Overhead Press", MuscleGroup = "Shoulders", WorkoutDate = s1.SessionDate, Sets = 3, Reps = 8, WeightLbs = 95 - userIndex * 3m, Notes = "Seeded", IsCompleted = true },
                new WorkoutEntry { UserId = userId, SessionId = s1.Id, ExerciseName = "Cable Pushdown", MuscleGroup = "Triceps", WorkoutDate = s1.SessionDate, Sets = 3, Reps = 12, WeightLbs = 45, Notes = "Seeded", IsCompleted = true },
                new WorkoutEntry { UserId = userId, SessionId = s2.Id, ExerciseName = "Squat", MuscleGroup = "Legs", WorkoutDate = s2.SessionDate, Sets = 4, Reps = 8, WeightLbs = 225 - userIndex * 10m, Notes = "Seeded", IsCompleted = true },
                new WorkoutEntry { UserId = userId, SessionId = s2.Id, ExerciseName = "Leg Press", MuscleGroup = "Legs", WorkoutDate = s2.SessionDate, Sets = 4, Reps = 12, WeightLbs = 340 - userIndex * 15m, Notes = "Seeded", IsCompleted = true },
                new WorkoutEntry { UserId = userId, SessionId = null, ExerciseName = "Deadlift", MuscleGroup = "Back", WorkoutDate = DateTime.Today.AddDays(-1 + dayOffset), Sets = 3, Reps = 5, WeightLbs = 275 - userIndex * 15m, Notes = "Loose entry (seed)", IsCompleted = true });
            await context.SaveChangesAsync();
        }

        if (!await context.NutritionLogs.AnyAsync(n => n.UserId == userId))
        {
            var nutritionLogs = new List<NutritionLog>();
            var c = 40 * userIndex;
            (string Food, string Serving, int Cal, int Prot, int Carb, int Fat)[] breakfastTemplates =
            {
                ("Eggs and Oatmeal", "2 eggs + 1 cup oats", 520, 32, 58, 14),
                ("Greek Yogurt Bowl", "12oz yogurt + granola + berries", 480, 28, 62, 12),
                ("Protein Pancakes", "3 pancakes + maple syrup", 510, 34, 55, 16),
                ("Avocado Toast & Turkey", "2 slices + 4oz turkey", 495, 30, 44, 22),
            };
            (string Food, string Serving, int Cal, int Prot, int Carb, int Fat)[] lunchTemplates =
            {
                ("Grilled Chicken Breast with Rice", "6oz chicken + 1 cup rice", 620, 52, 68, 10),
                ("Turkey Wrap & Soup", "wrap + cup vegetable soup", 590, 48, 72, 14),
                ("Steak Bowl", "5oz sirloin + quinoa + veg", 640, 54, 58, 18),
                ("Tuna Poke Bowl", "6oz tuna + rice + edamame", 605, 50, 70, 12),
            };
            (string Food, string Serving, int Cal, int Prot, int Carb, int Fat)[] dinnerTemplates =
            {
                ("Salmon with Sweet Potato", "6oz salmon + 1 medium potato", 680, 48, 62, 22),
                ("Lean Beef Stir-Fry", "5oz beef + jasmine rice", 655, 46, 66, 20),
                ("Shrimp Pasta", "6oz shrimp + whole wheat pasta", 695, 44, 78, 18),
                ("Baked Cod with Veggies", "6oz cod + roasted veg", 625, 50, 54, 20),
            };

            for (var i = 6; i >= 0; i--)
            {
                var logDate = DateTime.Today.AddDays(-i);
                var dayKey = 6 - i;
                var h0 = (userIndex * 7919 + i * 997 + 1 * 13) & 0x7fff;
                var h1 = (userIndex * 7919 + i * 997 + 2 * 13) & 0x7fff;
                var dayCalDelta = (h0 % 401) - 200;
                var dayProtDelta = (h1 % 41) - 20;
                var b = (dayKey + userIndex * 2) % breakfastTemplates.Length;
                var l = (dayKey + userIndex) % lunchTemplates.Length;
                var d = (dayKey + userIndex * 3) % dinnerTemplates.Length;

                void addMeal((string Food, string Serving, int Cal, int Prot, int Carb, int Fat) tpl, string mealName, int slot)
                {
                    var h = (userIndex * 7919 + i * 997 + slot * 13) & 0x7fff;
                    var calPart = dayCalDelta / 3 + (h % 21) - 10;
                    var protPart = dayProtDelta / 3 + ((h / 21) % 5) - 2;
                    var carbScale = 1m + (dayCalDelta / 2000m) * (0.85m + (slot % 3) * 0.1m);
                    var fatScale = 1m + (dayCalDelta / 2200m) * (0.9m + (slot % 2) * 0.08m);
                    var adjCal = Math.Max(120, tpl.Cal + c + calPart);
                    var adjProt = Math.Max(8, tpl.Prot + protPart);
                    var adjCarb = Math.Max(10, (int)Math.Round(tpl.Carb * carbScale));
                    var adjFat = Math.Max(6, (int)Math.Round(tpl.Fat * fatScale));
                    var portionNotes = new[] { "extra greens", "light dressing", "small fruit", "½ cup less rice", "drizzle olive oil" };
                    var servingExtra = ((h >> 5) + dayKey + slot) % 3 == 0 ? "" : " — " + portionNotes[(h + slot + dayKey) % portionNotes.Length];
                    var foodNote = new[] { "", " (side salad)", " (no butter)", " (extra veg)" }[(h + dayKey + slot) % 4];
                    nutritionLogs.Add(new NutritionLog
                    {
                        UserId = userId,
                        LogDate = logDate,
                        MealName = mealName,
                        FoodItem = tpl.Food + foodNote,
                        Calories = adjCal,
                        Protein = adjProt,
                        Carbs = adjCarb,
                        Fat = adjFat,
                        ServingSize = tpl.Serving + servingExtra
                    });
                }

                addMeal(breakfastTemplates[b], "Breakfast", 1);
                addMeal(lunchTemplates[l], "Lunch", 2);
                addMeal(dinnerTemplates[d], "Dinner", 3);
            }

            context.NutritionLogs.AddRange(nutritionLogs);
            await context.SaveChangesAsync();
        }

        if (!await context.WeightLogs.AnyAsync(w => w.UserId == userId))
        {
            var weightLogs = new List<WeightLog>();
            var startWeight = 188m - userIndex * 4m;
            for (var i = 29; i >= 0; i--)
            {
                var variation = (decimal)(new Random(i + userIndex * 100).NextDouble() * 1.4 - 0.4);
                var trend = (29 - i) * 0.1m;
                weightLogs.Add(new WeightLog
                {
                    UserId = userId,
                    LogDate = DateTime.Today.AddDays(-i),
                    WeightLbs = Math.Round(startWeight - trend + variation, 1),
                    Notes = i == 0 ? "Morning weigh-in" : ""
                });
            }

            context.WeightLogs.AddRange(weightLogs);
            await context.SaveChangesAsync();
        }

        if (!await context.Supplements.AnyAsync(s => s.UserId == userId))
        {
            context.Supplements.AddRange(
                new Supplement { UserId = userId, Name = "Creatine Monohydrate", Dosage = "5g", TimeToTake = "Any time", Notes = "Seeded", IsActive = true },
                new Supplement { UserId = userId, Name = "Whey Protein", Dosage = "1 scoop", TimeToTake = "Post-Workout", Notes = "Seeded", IsActive = true },
                new Supplement { UserId = userId, Name = "Vitamin D3", Dosage = "2000 IU", TimeToTake = "Morning", Notes = "Seeded", IsActive = true });
            await context.SaveChangesAsync();
        }

        if (!await context.WaterLogs.AnyAsync(w => w.UserId == userId))
        {
            var waterLogs = new List<WaterLog>();
            for (var i = 6; i >= 0; i--)
            {
                waterLogs.Add(new WaterLog
                {
                    UserId = userId,
                    LogDate = DateTime.Today.AddDays(-i),
                    AmountOz = 96 + (i + userIndex) % 3 * 16,
                    DailyGoalOz = 128
                });
            }

            context.WaterLogs.AddRange(waterLogs);
            await context.SaveChangesAsync();
        }

        logger.LogDebug("Ensured demo domain data for user index {UserIndex}.", userIndex);
    }

    private static List<Exercise> GetSeedExercises()
    {
        var items = new List<Exercise>
    {
        new Exercise { Name = "Bench Press", MuscleGroup = "Chest", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The bench press is the king of upper body strength exercises.", Tips = "Keep your shoulder blades retracted.\nMaintain a slight arch in your lower back.\nGrip the bar just outside shoulder width.\nLower the bar to your mid-chest under control.\nDrive your feet into the floor for leg drive.", IsSystemExercise = true },
        new Exercise { Name = "Incline Dumbbell Press", MuscleGroup = "Chest", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the upper chest with greater range of motion.", Tips = "Set the bench to 30-45 degrees.\nControl the descent.\nPress the dumbbells together at the top.\nKeep elbows at roughly 75 degrees from torso.", IsSystemExercise = true },
        new Exercise { Name = "Cable Fly", MuscleGroup = "Chest", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Keeps constant tension on the chest throughout the movement.", Tips = "Maintain a slight bend in your elbows.\nFocus on squeezing the chest at peak contraction.\nControl the weight on the way back.", IsSystemExercise = true },
        new Exercise { Name = "Push Up", MuscleGroup = "Chest", Category = "General", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A fundamental bodyweight exercise for chest endurance.", Tips = "Keep your body in a straight line.\nPlace hands slightly wider than shoulder width.\nLower your chest to just above the floor.", IsSystemExercise = true },
        new Exercise { Name = "Deadlift", MuscleGroup = "Back", Category = "Performance", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "3-5", Description = "The ultimate full-body strength exercise.", Tips = "Keep the bar close to your body.\nHinge at the hips.\nKeep your chest up and spine neutral.\nDrive through the floor with your legs.", IsSystemExercise = true },
        new Exercise { Name = "Barbell Row", MuscleGroup = "Back", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "A compound pulling movement for upper and mid back thickness.", Tips = "Hinge forward to about 45 degrees.\nPull the bar to your lower chest.\nKeep elbows close to your body.\nSqueeze shoulder blades at the top.", IsSystemExercise = true },
        new Exercise { Name = "Pull Up", MuscleGroup = "Back", Category = "Performance", Equipment = "Pull Up Bar", RecommendedSets = 4, RecommendedReps = "5-8", Description = "One of the best bodyweight exercises for back width.", Tips = "Start from a dead hang.\nPull your elbows down and back.\nAim to get your chin over the bar.\nLower yourself under full control.", IsSystemExercise = true },
        new Exercise { Name = "Lat Pulldown", MuscleGroup = "Back", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the lats for back width.", Tips = "Lean back slightly and pull bar to upper chest.\nFocus on pulling with your elbows.\nStretch fully at the top of each rep.", IsSystemExercise = true },
        new Exercise { Name = "Seated Cable Row", MuscleGroup = "Back", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Builds mid-back thickness and improves posture.", Tips = "Keep your torso upright.\nPull the handle to your lower chest.\nSqueeze shoulder blades at full contraction.", IsSystemExercise = true },
        new Exercise { Name = "Face Pull", MuscleGroup = "Back", Category = "General", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "Targets the rear deltoids and external rotators.", Tips = "Set cable at face height.\nPull to your face keeping elbows high.\nFinish with hands beside your ears.", IsSystemExercise = true },
        new Exercise { Name = "Overhead Press", MuscleGroup = "Shoulders", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-6", Description = "The standing overhead press builds total shoulder strength.", Tips = "Brace your core and squeeze your glutes.\nPress the bar in a straight line.\nLock out your elbows fully at the top.", IsSystemExercise = true },
        new Exercise { Name = "Dumbbell Lateral Raise", MuscleGroup = "Shoulders", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the lateral head of the deltoid for shoulder width.", Tips = "Lead with your elbows.\nRaise to just above shoulder height.\nControl the descent.", IsSystemExercise = true },
        new Exercise { Name = "Arnold Press", MuscleGroup = "Shoulders", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "A rotational shoulder press hitting all three deltoid heads.", Tips = "Start with palms facing you at chin height.\nRotate your wrists as you press up.\nControl the rotation on the way down.", IsSystemExercise = true },
        new Exercise { Name = "Cable Reverse Fly", MuscleGroup = "Shoulders", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the rear deltoids for balanced shoulder development.", Tips = "Set cables at face height and cross the handles.\nKeep a slight bend in your elbows.\nSqueeze the rear delts at full extension.", IsSystemExercise = true },
        new Exercise { Name = "Barbell Curl", MuscleGroup = "Biceps", Category = "Performance", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "The foundational bicep exercise for size and strength.", Tips = "Keep your elbows pinned to your sides.\nFully extend at the bottom.\nSqueeze hard at the top.", IsSystemExercise = true },
        new Exercise { Name = "Dumbbell Hammer Curl", MuscleGroup = "Biceps", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Targets the brachialis for overall arm thickness.", Tips = "Keep palms facing each other throughout.\nControl the weight.\nCurl to shoulder height and squeeze at the top.", IsSystemExercise = true },
        new Exercise { Name = "Incline Dumbbell Curl", MuscleGroup = "Biceps", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Greater stretch on the long head of the bicep.", Tips = "Set the bench to 45-60 degrees.\nLet your arms hang fully at the bottom.\nCurl without letting elbows move forward.", IsSystemExercise = true },
        new Exercise { Name = "Close Grip Bench Press", MuscleGroup = "Triceps", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "5-8", Description = "The most effective tricep mass builder.", Tips = "Use a shoulder-width grip.\nKeep your elbows close to your body.\nFocus on pushing through your triceps.", IsSystemExercise = true },
        new Exercise { Name = "Cable Pushdown", MuscleGroup = "Triceps", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolation exercise targeting the lateral head of the tricep.", Tips = "Keep your elbows pinned at your sides.\nFully extend your arms at the bottom.\nControl the weight on the way up.", IsSystemExercise = true },
        new Exercise { Name = "Overhead Tricep Extension", MuscleGroup = "Triceps", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Targets the long head of the tricep.", Tips = "Keep your elbows pointed forward.\nGet a full stretch at the bottom.\nExtend fully at the top.", IsSystemExercise = true },
        new Exercise { Name = "Skull Crusher", MuscleGroup = "Triceps", Category = "Performance", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "10-12", Description = "Lying tricep extension loading the long head heavily.", Tips = "Lower the bar to your forehead or just behind your head.\nKeep upper arms perpendicular to the floor.\nExtend fully at the top.", IsSystemExercise = true },
        new Exercise { Name = "Squat", MuscleGroup = "Legs", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "The king of lower body exercises.", Tips = "Keep your chest up and core braced.\nPush your knees out in line with your toes.\nDescend until hips are at or below parallel.\nDrive through your heels to stand.", IsSystemExercise = true },
        new Exercise { Name = "Romanian Deadlift", MuscleGroup = "Legs", Category = "Performance", Equipment = "Barbell", RecommendedSets = 3, RecommendedReps = "8-12", Description = "Targets the hamstrings and glutes with a hip hinge.", Tips = "Keep a slight bend in your knees.\nHinge at the hips and push them back.\nFeel a deep stretch in your hamstrings at the bottom.", IsSystemExercise = true },
        new Exercise { Name = "Leg Press", MuscleGroup = "Legs", Category = "Performance", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "10-12", Description = "Machine-based quad dominant movement.", Tips = "Place feet shoulder width on the platform.\nDo not lock out your knees.\nLower until knees reach 90 degrees.", IsSystemExercise = true },
        new Exercise { Name = "Walking Lunge", MuscleGroup = "Legs", Category = "Performance", Equipment = "Dumbbells", RecommendedSets = 3, RecommendedReps = "10-12 each leg", Description = "Builds unilateral leg strength and stability.", Tips = "Take a long stride forward.\nLower your back knee toward the floor.\nKeep your front knee over your ankle.", IsSystemExercise = true },
        new Exercise { Name = "Leg Curl", MuscleGroup = "Legs", Category = "Performance", Equipment = "Machine", RecommendedSets = 3, RecommendedReps = "12-15", Description = "Isolates the hamstrings for targeted development.", Tips = "Curl fully to your glutes and squeeze at the top.\nControl the negative slowly.\nAvoid letting your hips rise off the pad.", IsSystemExercise = true },
        new Exercise { Name = "Calf Raise", MuscleGroup = "Legs", Category = "Performance", Equipment = "Machine", RecommendedSets = 4, RecommendedReps = "15-20", Description = "Targets the gastrocnemius and soleus.", Tips = "Get a full stretch at the bottom of every rep.\nHold the contraction at the top.\nVary foot position to target different areas.", IsSystemExercise = true },
        new Exercise { Name = "Plank", MuscleGroup = "Core", Category = "General", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "30-60 sec", Description = "The foundational core stability exercise.", Tips = "Keep your body in a straight line.\nEngage your glutes and brace your abs hard.\nDo not let your hips sag or pike up.", IsSystemExercise = true },
        new Exercise { Name = "Cable Crunch", MuscleGroup = "Core", Category = "Performance", Equipment = "Cable Machine", RecommendedSets = 3, RecommendedReps = "15-20", Description = "A loaded ab exercise allowing progressive overload.", Tips = "Kneel facing the cable machine.\nCrunch down by flexing your spine.\nTouch your elbows to your knees at the bottom.", IsSystemExercise = true },
        new Exercise { Name = "Hanging Leg Raise", MuscleGroup = "Core", Category = "General", Equipment = "Pull Up Bar", RecommendedSets = 3, RecommendedReps = "10-15", Description = "Targets the lower abs and hip flexors.", Tips = "Hang from a bar with shoulder width grip.\nRaise your legs by flexing your hips.\nAvoid swinging.", IsSystemExercise = true },
        new Exercise { Name = "Ab Wheel Rollout", MuscleGroup = "Core", Category = "Performance", Equipment = "Ab Wheel", RecommendedSets = 3, RecommendedReps = "8-12", Description = "One of the most effective core exercises for anti-extension strength.", Tips = "Start on your knees with the wheel below your shoulders.\nRoll out slowly keeping your core braced.\nPull back by contracting your abs.", IsSystemExercise = true },
        new Exercise { Name = "Treadmill Run", MuscleGroup = "Cardio", Category = "General", Equipment = "Treadmill", RecommendedSets = 1, RecommendedReps = "20-30 min", Description = "Steady state cardio for cardiovascular health.", Tips = "Maintain an upright posture.\nLand with your foot under your center of mass.\nAim for a pace where you can still hold a conversation.", IsSystemExercise = true },
        new Exercise { Name = "Rowing Machine", MuscleGroup = "Cardio", Category = "General", Equipment = "Rowing Machine", RecommendedSets = 1, RecommendedReps = "15-20 min", Description = "Full body cardio that also works the back and arms.", Tips = "Drive with your legs first, then lean back, then pull with your arms.\nMaintain a strong posture throughout.", IsSystemExercise = true },
        new Exercise { Name = "Jump Rope", MuscleGroup = "Cardio", Category = "General", Equipment = "Jump Rope", RecommendedSets = 3, RecommendedReps = "3-5 min", Description = "High intensity cardio improving coordination and footwork.", Tips = "Jump on the balls of your feet.\nKeep your elbows close to your sides.\nUse your wrists to turn the rope.", IsSystemExercise = true },
        new Exercise { Name = "Clean and Press", MuscleGroup = "Full Body", Category = "Performance", Equipment = "Barbell", RecommendedSets = 4, RecommendedReps = "3-5", Description = "A total body power movement combining a clean and overhead press.", Tips = "Start with the bar at mid-shin.\nExplosively extend your hips and shrug.\nDrop under the bar and catch it at shoulder height.", IsSystemExercise = true },
        new Exercise { Name = "Kettlebell Swing", MuscleGroup = "Full Body", Category = "General", Equipment = "Kettlebell", RecommendedSets = 4, RecommendedReps = "15-20", Description = "A ballistic hip hinge building posterior chain power.", Tips = "Hinge at the hips not the knees.\nDrive your hips forward explosively.\nThe swing is powered by your hips not your arms.", IsSystemExercise = true },
        new Exercise { Name = "Burpee", MuscleGroup = "Full Body", Category = "General", Equipment = "Bodyweight", RecommendedSets = 3, RecommendedReps = "10-15", Description = "A high intensity full body conditioning exercise.", Tips = "Move at a consistent pace.\nKeep your core engaged during the plank position.\nScale by stepping instead of jumping if needed.", IsSystemExercise = true },
    };

        return items;
    }

    private static List<SupplementLibraryItem> GetSeedSupplements() =>
    [
        new SupplementLibraryItem { Name = "Creatine Monohydrate", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3-5g daily", WhenToTake = "Any time, consistency matters most", Description = "The most researched supplement in sports nutrition.", Benefits = "Increased strength and power output\nImproved muscle mass over time\nEnhanced high-intensity exercise performance", InfoUrl = "https://examine.com/supplements/creatine/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Caffeine", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3-6mg per kg bodyweight", WhenToTake = "30-60 minutes before training", Description = "Blocks adenosine receptors, reducing perceived fatigue.", Benefits = "Increased strength and endurance\nReduced perceived effort\nImproved focus and alertness", InfoUrl = "https://examine.com/supplements/caffeine/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Whey Protein", Category = "Recovery", EvidenceLevel = "Strong", RecommendedDosage = "20-40g per serving", WhenToTake = "Post-workout or any time to hit protein goals", Description = "Fast-digesting complete protein for muscle protein synthesis.", Benefits = "Supports muscle protein synthesis\nConvenient way to hit daily protein targets", InfoUrl = "https://examine.com/supplements/whey-protein/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Magnesium", Category = "Recovery", EvidenceLevel = "Moderate", RecommendedDosage = "200-400mg", WhenToTake = "Before bed", Description = "Involved in over 300 enzymatic reactions.", Benefits = "Improved sleep quality\nReduced muscle cramps\nSupports energy production", InfoUrl = "https://examine.com/supplements/magnesium/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Vitamin D3", Category = "Vitamins & Minerals", EvidenceLevel = "Strong", RecommendedDosage = "1000-4000 IU daily", WhenToTake = "With a meal containing fat", Description = "Essential for bone health, immune function, and hormonal balance.", Benefits = "Bone health and calcium absorption\nImmune system support\nMay support testosterone production", InfoUrl = "https://examine.com/supplements/vitamin-d/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Omega-3 Fish Oil", Category = "Health", EvidenceLevel = "Strong", RecommendedDosage = "1-3g EPA+DHA daily", WhenToTake = "With meals", Description = "EPA and DHA support cardiovascular health and reduce inflammation.", Benefits = "Reduced inflammation\nCardiovascular health support\nJoint health support", InfoUrl = "https://examine.com/supplements/fish-oil/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Ashwagandha", Category = "Recovery", EvidenceLevel = "Moderate", RecommendedDosage = "300-600mg KSM-66 extract", WhenToTake = "Morning or before bed", Description = "An adaptogen that helps the body manage stress.", Benefits = "Reduced cortisol and stress\nImproved strength and muscle mass\nBetter sleep quality", InfoUrl = "https://examine.com/supplements/ashwagandha/", IsRecommended = true, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Beta-Alanine", Category = "Performance", EvidenceLevel = "Strong", RecommendedDosage = "3.2-6.4g daily", WhenToTake = "Split into multiple doses throughout the day", Description = "Increases muscle carnosine levels, buffering acid buildup.", Benefits = "Delayed muscular fatigue\nImproved endurance in 1-4 minute efforts", InfoUrl = "https://examine.com/supplements/beta-alanine/", IsRecommended = false, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Zinc", Category = "Vitamins & Minerals", EvidenceLevel = "Moderate", RecommendedDosage = "25-45mg daily", WhenToTake = "Before bed or with meals", Description = "Essential for testosterone production and immune function.", Benefits = "Supports testosterone production\nImmune system function\nProtein synthesis support", InfoUrl = "https://examine.com/supplements/zinc/", IsRecommended = false, IsSystemItem = true },
        new SupplementLibraryItem { Name = "Protein Powder", Category = "Weight Management", EvidenceLevel = "Strong", RecommendedDosage = "20-40g per serving as needed", WhenToTake = "Any time to hit protein goals", Description = "High protein intake supports satiety and preserves muscle.", Benefits = "Increased satiety\nMuscle preservation during fat loss\nHigher thermic effect", InfoUrl = "https://examine.com/supplements/whey-protein/", IsRecommended = true, IsSystemItem = true },
    ];
}
