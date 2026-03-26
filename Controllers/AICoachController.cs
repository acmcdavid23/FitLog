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
        private readonly IConfiguration _configuration;

        public AICoachController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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

            var response = await CallOpenAI(new[]
            {
                new { role = "system", content = "You are an expert personal fitness coach. Give specific, actionable workout recommendations based on the user's training history. Be concise and professional." },
                new { role = "user", content = workoutSummary.ToString() }
            });

            ViewBag.Recommendation = response;
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
                systemPrompt.AppendLine($"- Current Weight: {settings.CurrentWeight} {settings.WeightUnit}");
                systemPrompt.AppendLine($"- Goal Weight: {settings.GoalWeight} {settings.WeightUnit}");
                systemPrompt.AppendLine($"- Daily Calorie Goal: {settings.CalorieGoal} kcal");
                systemPrompt.AppendLine($"- Protein Goal: {settings.ProteinGoal}g");
            }

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
            systemPrompt.AppendLine("Be conversational, encouraging, and professional. Keep responses focused and actionable.");

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt.ToString() }
            };

            foreach (var msg in request.History)
                messages.Add(new { role = msg.Role, content = msg.Content });

            messages.Add(new { role = "user", content = request.Message });

            var response = await CallOpenAI(messages.ToArray());

            return Json(new { response });
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

                foreach (var ex in request.Exercises)
                {
                    _context.WorkoutEntries.Add(new WorkoutEntry
                    {
                        UserId = userId ?? string.Empty,
                        SessionId = session.Id,
                        ExerciseName = ex.Name,
                        MuscleGroup = string.IsNullOrEmpty(ex.MuscleGroup) ? "General" : ex.MuscleGroup,
                        Sets = ex.Sets,
                        Reps = ex.Reps,
                        WeightLbs = ex.Weight,
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

        private async Task<string> CallOpenAI(object[] messages)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];

            using var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages,
                max_tokens = 800
            };

            var response = await client.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            );

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response";
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