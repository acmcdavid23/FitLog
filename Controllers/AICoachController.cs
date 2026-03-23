using FitLog.Data;
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
            {
                workoutSummary.AppendLine($"- {w.WorkoutDate.ToShortDateString()}: {w.ExerciseName} ({w.MuscleGroup}) - {w.Sets} sets x {w.Reps} reps @ {w.WeightLbs} lbs. Notes: {w.Notes}");
            }
            workoutSummary.AppendLine("\nBased on this history, what should my next workout be? Give me a specific plan with exercises, sets, reps, and weight recommendations. Keep it concise and actionable.");

            var apiKey = _configuration["OpenAI:ApiKey"];

            using var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are an expert personal fitness coach. Give specific, actionable workout recommendations based on the user's training history. Be concise and professional." },
                    new { role = "user", content = workoutSummary.ToString() }
                },
                max_tokens = 500
            };

            var response = await client.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            );

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            var recommendation = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            ViewBag.Recommendation = recommendation;
            ViewBag.WorkoutSummary = recentWorkouts;

            return View("Index");
        }
    }
}