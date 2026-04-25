namespace FitLog.Configuration;

/// <summary>
/// Resolves the OpenAI API key from environment variables only (Azure App Service, Docker, dotnet user-secrets mapped to env, etc.).
/// </summary>
public static class OpenAiApiKeyResolver
{
    public static string? Resolve() =>
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey");
}
