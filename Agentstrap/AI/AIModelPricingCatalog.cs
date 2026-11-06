namespace Agentstrap.AI
{
    /// <summary>
    /// Centralized, easily updateable pricing/capability metadata.
    /// Keys are provider name (AIProviderId.ToString()) -> model id.
    /// Unknown models are not listed and default to Unknown.
    /// </summary>
    public static class AIModelPricingCatalog
    {
        public class Entry
        {
            public AIModelPricingStatus Status { get; set; } = AIModelPricingStatus.Unknown;
            public double? InputPricePer1M { get; set; }
            public double? OutputPricePer1M { get; set; }
            public int? ContextWindow { get; set; }
            public bool? SupportsTools { get; set; }
            public bool? SupportsStreaming { get; set; }
        }

        private static readonly Dictionary<string, Dictionary<string, Entry>> _data =
            new(StringComparer.OrdinalIgnoreCase);

        static AIModelPricingCatalog()
        {
            // Groq: documented free tier with rate limits
            Add("Groq", "llama-3.3-70b-versatile", AIModelPricingStatus.FreeTier, 0, 0, 131072, true, true);
            Add("Groq", "llama-3.1-8b-instant", AIModelPricingStatus.FreeTier, 0, 0, 131072, true, true);
            Add("Groq", "mixtral-8x7b-32768", AIModelPricingStatus.FreeTier, 0, 0, 32768, true, true);
            Add("Groq", "gemma2-9b-it", AIModelPricingStatus.FreeTier, 0, 0, 8192, true, true);

            // OpenRouter free models (suffix :free) -> FreeTier/Free
            // (discovered dynamically; classify any id ending in :free as FreeTier at runtime)

            // DeepSeek (paid, low cost)
            Add("DeepSeek", "deepseek-chat", AIModelPricingStatus.Paid, 0.27, 1.10, 64000, true, true);
            Add("DeepSeek", "deepseek-reasoner", AIModelPricingStatus.Paid, 0.55, 2.19, 64000, true, true);

            // OpenAI (paid)
            Add("OpenAI", "gpt-4o", AIModelPricingStatus.Paid, 2.50, 10.00, 128000, true, true);
            Add("OpenAI", "gpt-4o-mini", AIModelPricingStatus.Paid, 0.15, 0.60, 128000, true, true);
            Add("OpenAI", "gpt-4.1", AIModelPricingStatus.Paid, 2.00, 8.00, 1000000, true, true);
            Add("OpenAI", "gpt-4.1-mini", AIModelPricingStatus.Paid, 0.40, 1.60, 1000000, true, true);

            // Anthropic (paid)
            Add("Anthropic", "claude-3-5-sonnet-20241022", AIModelPricingStatus.Paid, 3.00, 15.00, 200000, true, true);
            Add("Anthropic", "claude-3-5-haiku-20241022", AIModelPricingStatus.Paid, 0.80, 4.00, 200000, true, true);
            Add("Anthropic", "claude-3-opus-20240229", AIModelPricingStatus.Paid, 15.00, 75.00, 200000, true, true);

            // xAI Grok (paid)
            Add("Grok", "grok-3", AIModelPricingStatus.Paid, 3.00, 15.00, 131072, true, true);
            Add("Grok", "grok-3-mini", AIModelPricingStatus.Paid, 0.30, 0.50, 131072, true, true);
            Add("Grok", "grok-2-latest", AIModelPricingStatus.Paid, 2.00, 10.00, 131072, true, true);
        }

        private static void Add(string provider, string model, AIModelPricingStatus status, double? input, double? output, int? ctx, bool? tools, bool? stream)
        {
            if (!_data.TryGetValue(provider, out var dict)) { dict = new(StringComparer.OrdinalIgnoreCase); _data[provider] = dict; }
            dict[model] = new Entry { Status = status, InputPricePer1M = input, OutputPricePer1M = output, ContextWindow = ctx, SupportsTools = tools, SupportsStreaming = stream };
        }

        public static Entry? Get(string provider, string modelId)
        {
            if (_data.TryGetValue(provider, out var dict) && dict.TryGetValue(modelId, out var e)) return e;
            // OpenRouter free detection
            if (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase) && modelId.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
                return new Entry { Status = AIModelPricingStatus.FreeTier, InputPricePer1M = 0, OutputPricePer1M = 0, SupportsTools = true, SupportsStreaming = true };
            return null;
        }

        public static void Set(string provider, string modelId, Entry entry)
        {
            if (!_data.TryGetValue(provider, out var dict)) { dict = new(StringComparer.OrdinalIgnoreCase); _data[provider] = dict; }
            dict[modelId] = entry;
        }
    }
}
