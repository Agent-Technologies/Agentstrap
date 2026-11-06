using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Agentstrap.AI.Providers
{
    public class GroqProvider : OpenAICompatibleProvider
    {
        public GroqProvider() : base("Groq", "Groq", "https://api.groq.com/openai/v1", new[]
        {
            "llama-3.3-70b-versatile",
            "llama-3.1-8b-instant",
            "mixtral-8x7b-32768",
            "gemma2-9b-it"
        }) { }

        public override string Id => "Groq";

        // Dynamic model discovery
        public async Task<List<string>> DiscoverModelsAsync(string apiKey, CancellationToken ct)
        {
            var models = new List<string>();
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var res = await http.GetAsync("https://api.groq.com/openai/v1/models", ct);
                if (!res.IsSuccessStatusCode) return models;
                string json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in data.EnumerateArray())
                    {
                        if (m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        {
                            var mid = id.GetString();
                            if (!string.IsNullOrWhiteSpace(mid)) models.Add(mid!);
                        }
                    }
                }
            }
            catch { }
            if (models.Count > 0) SetModels(models);
            return models;
        }
    }
}
