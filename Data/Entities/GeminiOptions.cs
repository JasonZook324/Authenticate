namespace Authenticate.Infrastructure.Gemini
{
    public class GeminiOptions
    {
        // Discovery + API base
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
        // API key for models.list (discovery doesn’t require it)
        public string? ApiKey { get; set; }
    }
}