using FitLog.Configuration;
using FitLog.Data;
using FitLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FitLog.Controllers
{
    [Authorize]
    public class AICoachController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AICoachController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetRecommendation()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var recentWorkouts = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WorkoutDate)
                .Take(10)
                .ToList();

            var workoutSummary = new StringBuilder();
            workoutSummary.AppendLine("Here are my recent workouts:");
            foreach (var w in recentWorkouts)
                workoutSummary.AppendLine($"- {w.WorkoutDate.ToShortDateString()}: {w.ExerciseName} ({w.MuscleGroup}) - {w.Sets} sets x {w.Reps} reps @ {w.WeightLbs} lbs. Notes: {w.Notes}");

            workoutSummary.AppendLine("\nBased on this history, what should my next workout be? Give me a specific plan with exercises, sets, reps, and weight recommendations. Keep it concise and actionable.");

            var (ok, response) = await TryCompleteChatAsync(new[]
            {
                new { role = "system", content = "You are an expert personal fitness coach. Give specific, actionable workout recommendations based on the user's training history. Be concise and professional." },
                new { role = "user", content = workoutSummary.ToString() }
            });

            ViewBag.Recommendation = ok ? response : null;
            ViewBag.AiError = ok ? null : response;
            ViewBag.WorkoutSummary = recentWorkouts;

            return View("Index");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var settings = _context.UserSettings.FirstOrDefault(s => s.UserId == userId);
            var recentWorkouts = _context.WorkoutEntries
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WorkoutDate)
                .Take(15)
                .ToList();
            var today = DateTime.Today;
            var waterTodayOz = _context.WaterLogs
                .Where(w => w.UserId == userId && w.LogDate == today)
                .Sum(w => (int?)w.AmountOz) ?? 0;
            var nutritionToday = _context.NutritionLogs
                .Where(n => n.UserId == userId && n.LogDate == today)
                .ToList();
            var currentWeight = _context.WeightLogs
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.LogDate)
                .ThenByDescending(w => w.Id)
                .Select(w => (decimal?)w.WeightLbs)
                .FirstOrDefault() ?? settings?.CurrentWeight ?? 0;
            var recentSession = _context.WorkoutSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .ThenByDescending(s => s.Id)
                .Select(s => new { s.SessionName, s.SessionDate })
                .FirstOrDefault();

            var prs = recentWorkouts
                .GroupBy(w => w.ExerciseName)
                .ToDictionary(g => g.Key, g => g.Max(w => w.WeightLbs));

            var systemPrompt = new StringBuilder();
            systemPrompt.AppendLine("You are FitLog AI Coach, an expert personal trainer and nutritionist.");
            systemPrompt.AppendLine("You have full context about this user:");

            if (settings != null)
            {
                systemPrompt.AppendLine($"- Fitness Goal: {settings.FitnessGoal}");
                systemPrompt.AppendLine($"- Body Goal: {settings.BodyGoal}");
                systemPrompt.AppendLine($"- Current Weight: {currentWeight} {settings.WeightUnit}");
                systemPrompt.AppendLine($"- Goal Weight: {settings.GoalWeight} {settings.WeightUnit}");
                systemPrompt.AppendLine($"- Daily Calorie Goal: {settings.CalorieGoal} kcal");
                systemPrompt.AppendLine($"- Protein Goal: {settings.ProteinGoal}g");
            }
            systemPrompt.AppendLine($"- Today's water intake: {waterTodayOz} oz");
            systemPrompt.AppendLine($"- Today's nutrition totals: {nutritionToday.Sum(n => n.Calories)} kcal, Protein {nutritionToday.Sum(n => n.Protein):0.#}g, Carbs {nutritionToday.Sum(n => n.Carbs):0.#}g, Fat {nutritionToday.Sum(n => n.Fat):0.#}g");
            if (recentSession != null)
                systemPrompt.AppendLine($"- Most recent workout session: {recentSession.SessionName} on {recentSession.SessionDate:yyyy-MM-dd}");

            if (recentWorkouts.Any())
            {
                systemPrompt.AppendLine("\nRecent workout history:");
                foreach (var w in recentWorkouts.Take(10))
                    systemPrompt.AppendLine($"- {w.WorkoutDate.ToShortDateString()}: {w.ExerciseName} ({w.MuscleGroup}) {w.Sets}x{w.Reps} @ {w.WeightLbs}lbs");

                systemPrompt.AppendLine("\nPersonal Records:");
                foreach (var pr in prs.Take(5))
                    systemPrompt.AppendLine($"- {pr.Key}: {pr.Value}lbs");
            }

            systemPrompt.AppendLine("\nWhen creating workout plans, format them clearly with exercise name, sets, reps, and weight.");
            systemPrompt.AppendLine("If the user asks to save a workout, respond with a JSON block at the end in this exact format:");
            systemPrompt.AppendLine("```json");
            systemPrompt.AppendLine("{\"saveWorkout\": true, \"workoutName\": \"Push Day\", \"exercises\": [{\"name\": \"Bench Press\", \"muscleGroup\": \"Chest\", \"sets\": 4, \"reps\": 8, \"weight\": 185}]}");
            systemPrompt.AppendLine("```");
            systemPrompt.AppendLine("When you provide concrete numeric values that could be filled in automatically (goals, macros, water, reps/weight/sets, or add-exercise suggestions), append a second block in this exact format:");
            systemPrompt.AppendLine("```fitlog-actions");
            systemPrompt.AppendLine("{\"prompt\":\"Would you like me to apply these values automatically?\",\"actions\":[{\"type\":\"setField\",\"target\":\"calorieGoal\",\"value\":2400},{\"type\":\"setField\",\"target\":\"proteinGoal\",\"value\":180}]}");
            systemPrompt.AppendLine("```");
            systemPrompt.AppendLine("Allowed action types: setField, addExercise.");
            systemPrompt.AppendLine("Allowed setField targets: calorieGoal, proteinGoal, carbGoal, fatGoal, currentWeight, goalWeight, waterOz, customOz, exerciseName, reps, sets, weight.");
            systemPrompt.AppendLine("addExercise format: {\"type\":\"addExercise\",\"name\":\"Bench Press\",\"reps\":8,\"weight\":135,\"sets\":3}.");
            systemPrompt.AppendLine("Only include a fitlog-actions block when you are confident and the user asked for recommendations/values.");
            systemPrompt.AppendLine("Be conversational, encouraging, and professional. Keep responses focused and actionable.");

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt.ToString() }
            };

            foreach (var msg in request.History)
                messages.Add(new { role = msg.Role, content = msg.Content });

            messages.Add(new { role = "user", content = request.Message });

            var (ok, text) = await TryCompleteChatAsync(messages.ToArray());
            if (!ok)
                return Json(new { error = text });

            return Json(new { response = text });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult SaveWorkout([FromBody] SaveWorkoutRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var session = new WorkoutSession
                {
                    UserId = userId ?? string.Empty,
                    SessionName = request.WorkoutName,
                    SessionDate = DateTime.Today,
                    Notes = "Generated by AI Coach"
                };

                _context.WorkoutSessions.Add(session);
                _context.SaveChanges();

                foreach (var exercise in request.Exercises)
                {
                    _context.WorkoutEntries.Add(new WorkoutEntry
                    {
                        UserId = userId ?? string.Empty,
                        SessionId = session.Id,
                        ExerciseName = exercise.Name,
                        MuscleGroup = string.IsNullOrEmpty(exercise.MuscleGroup) ? "General" : exercise.MuscleGroup,
                        Sets = exercise.Sets,
                        Reps = exercise.Reps,
                        WeightLbs = exercise.Weight,
                        WorkoutDate = DateTime.Today,
                        IsCompleted = false,
                        Notes = ""
                    });
                }

                _context.SaveChanges();
                return Json(new { success = true, sessionId = session.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        private async Task<(bool Ok, string Content)> TryCompleteChatAsync(object[] messages)
        {
            var apiKey = OpenAiApiKeyResolver.Resolve();
            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "AI Coach is not configured. Set OPENAI_API_KEY or OpenAI__ApiKey in the environment (for example Azure App Service settings or dotnet user-secrets).");

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages,
                    max_tokens = 800
                };

                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return (false, $"OpenAI returned an error ({(int)response.StatusCode}). Please try again later.");

                using var jsonDoc = JsonDocument.Parse(responseContent);
                if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    return (false, "OpenAI returned an unexpected response. Please try again.");

                var text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                return (true, text);
            }
            catch (Exception ex)
            {
                return (false, "AI is temporarily unavailable. " + ex.Message);
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessage> History { get; set; } = new();
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class SaveWorkoutRequest
    {
        public string WorkoutName { get; set; } = string.Empty;
        public List<WorkoutExercise> Exercises { get; set; } = new();
    }

    public class WorkoutExercise
    {
        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public int Sets { get; set; }
        public int Reps { get; set; }
        public decimal Weight { get; set; }
    }
}