using System.Net.Http.Headers;
using System.Text.Json;

namespace Agentstrap.AI
{
    public static class ModelDiscoveryService
    {
        private static string CacheDir => Path.Combine(Paths.LocalAppData, App.ProjectName, "AI", "ModelCache");

        public static async Task<List<AIModelInfo>> DiscoverAsync(AIProviderId provider, string apiKey, bool force = false, CancellationToken ct = default)
        {
            // Try cache if not forced and recent
            if (!force)
            {
                var cached = LoadCache(provider);
                if (cached is not null && cached.Count > 0 && (DateTime.UtcNow - cached[0].LastVerifiedUtc).TotalHours < 12)
                    return cached;
            }

            List<AIModelInfo> models = new();
            try
            {
                models = provider switch
                {
                    AIProviderId.OpenAI => await DiscoverOpenAICompatible(provider, "https://api.openai.com/v1", apiKey, ct),
                    AIProviderId.Grok => await DiscoverOpenAICompatible(provider, "https://api.x.ai/v1", apiKey, ct),
                    AIProviderId.Groq => await DiscoverGroq(provider, apiKey, ct),
                    AIProviderId.DeepSeek => await DiscoverOpenAICompatible(provider, "https://api.deepseek.com/v1", apiKey, ct),
                    AIProviderId.OpenRouter => await DiscoverOpenRouter(provider, apiKey, ct),
                    AIProviderId.Anthropic => await DiscoverAnthropic(provider, apiKey, ct),
                    _ => new()
                };
            }
            catch { models = new(); }

            // Classify each model via catalog
            foreach (var m in models)
            {
                var entry = AIModelPricingCatalog.Get(provider.ToString(), m.Id);
                if (entry is not null)
                {
                    m.PricingStatus = entry.Status;
                    m.InputPricePer1M = entry.InputPricePer1M;
                    m.OutputPricePer1M = entry.OutputPricePer1M;
                    m.ContextWindow = entry.ContextWindow ?? m.ContextWindow;
                    m.SupportsTools = entry.SupportsTools ?? m.SupportsTools;
                    m.SupportsStreaming = entry.SupportsStreaming ?? m.SupportsStreaming;
                }
                else
                {
                    m.PricingStatus = AIModelPricingStatus.Unknown;
                }
                m.LastVerifiedUtc = DateTime.UtcNow;
            }

            if (models.Count > 0) SaveCache(provider, models);
            return models;
        }

        private static async Task<List<AIModelInfo>> DiscoverOpenAICompatible(AIProviderId provider, string baseUrl, string apiKey, CancellationToken ct)
        {
            var list = new List<AIModelInfo>();
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var res = await http.GetAsync($"{baseUrl}/models", ct);
            if (!res.IsSuccessStatusCode) return list;
            string json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return list;
            foreach (var m in data.EnumerateArray())
            {
                if (!m.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var info = new AIModelInfo { ProviderId = provider.ToString(), Id = id!, Name = id! };
                if (m.TryGetProperty("context_window", out var cw) && cw.ValueKind == JsonValueKind.Number) info.ContextWindow = cw.GetInt32();
                list.Add(info);
            }
            return list;
        }

        private static async Task<List<AIModelInfo>> DiscoverGroq(AIProviderId provider, string apiKey, CancellationToken ct)
        {
            var list = await DiscoverOpenAICompatible(provider, "https://api.groq.com/openai/v1", apiKey, ct);
            // Groq /models sometimes includes owned_by; not needed. Update catalog entries already FreeTier.
            return list;
        }

        private static async Task<List<AIModelInfo>> DiscoverOpenRouter(AIProviderId provider, string apiKey, CancellationToken ct)
        {
            var list = new List<AIModelInfo>();
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(45);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var res = await http.GetAsync("https://openrouter.ai/api/v1/models", ct);
            if (!res.IsSuccessStatusCode) return list;
            string json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return list;
            foreach (var m in data.EnumerateArray())
            {
                if (!m.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var info = new AIModelInfo { ProviderId = provider.ToString(), Id = id!, Name = m.TryGetProperty("name", out var n) ? (n.GetString() ?? id!) : id! };
                if (m.TryGetProperty("context_length", out var cl) && cl.ValueKind == JsonValueKind.Number) info.ContextWindow = cl.GetInt32();
                // pricing
                if (m.TryGetProperty("pricing", out var pr) && pr.ValueKind == JsonValueKind.Object)
                {
                    double? prompt = null, completion = null;
                    if (pr.TryGetProperty("prompt", out var p) && p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out var pp)) prompt = pp * 1_000_000.0;
                    if (pr.TryGetProperty("completion", out var c) && c.ValueKind == JsonValueKind.String && double.TryParse(c.GetString(), out var cc)) completion = cc * 1_000_000.0;
                    info.InputPricePer1M = prompt;
                    info.OutputPricePer1M = completion;
                    // classify
                    if (prompt.HasValue && completion.HasValue)
                    {
                        if (prompt.Value == 0 && completion.Value == 0) info.PricingStatus = AIModelPricingStatus.Free;
                        else info.PricingStatus = AIModelPricingStatus.Paid;
                    }
                    else info.PricingStatus = AIModelPricingStatus.Unknown;
                }
                // supported parameters / tools
                if (m.TryGetProperty("supported_parameters", out var sp) && sp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var param in sp.EnumerateArray())
                    {
                        if (param.GetString() == "tools") { info.SupportsTools = true; break; }
                    }
                    if (info.SupportsTools is null) info.SupportsTools = false;
                }
                list.Add(info);
            }
            return list;
        }

        private static async Task<List<AIModelInfo>> DiscoverAnthropic(AIProviderId provider, string apiKey, CancellationToken ct)
        {
            var list = new List<AIModelInfo>();
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var res = await http.GetAsync("https://api.anthropic.com/v1/models", ct);
            if (!res.IsSuccessStatusCode) return list;
            string json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            JsonElement data = doc.RootElement;
            if (doc.RootElement.TryGetProperty("data", out var d)) data = d;
            if (data.ValueKind != JsonValueKind.Array) return list;
            foreach (var m in data.EnumerateArray())
            {
                if (!m.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var info = new AIModelInfo { ProviderId = provider.ToString(), Id = id!, Name = m.TryGetProperty("display_name", out var dn) ? (dn.GetString() ?? id!) : id! };
                if (m.TryGetProperty("context_window", out var cw) && cw.ValueKind == JsonValueKind.Number) info.ContextWindow = cw.GetInt32();
                list.Add(info);
            }
            return list;
        }

        private static string CachePath(AIProviderId provider) => Path.Combine(CacheDir, provider + ".json");

        private static void SaveCache(AIProviderId provider, List<AIModelInfo> models)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(CachePath(provider), JsonSerializer.Serialize(models));
            }
            catch { }
        }

        private static List<AIModelInfo>? LoadCache(AIProviderId provider)
        {
            try
            {
                string p = CachePath(provider);
                if (!File.Exists(p)) return null;
                return JsonSerializer.Deserialize<List<AIModelInfo>>(File.ReadAllText(p));
            }
            catch { return null; }
        }
    }
}
