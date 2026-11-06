using Agentstrap.AI.Providers;

namespace Agentstrap.AI
{
    public static class AIProviderManager
    {
        public static IAIProvider Get(AIProviderId id)
        {
            return id switch
            {
                AIProviderId.OpenAI => new OpenAICompatibleProvider("OpenAI", "OpenAI", "https://api.openai.com/v1", new[] { "gpt-4o", "gpt-4o-mini", "gpt-4.1", "gpt-4.1-mini" }),
                AIProviderId.Grok => new OpenAICompatibleProvider("Grok", "xAI Grok", "https://api.x.ai/v1", new[] { "grok-3", "grok-3-mini", "grok-2-latest" }),
                AIProviderId.Groq => new GroqProvider(),
                AIProviderId.DeepSeek => new OpenAICompatibleProvider("DeepSeek", "DeepSeek", "https://api.deepseek.com/v1", new[] { "deepseek-chat", "deepseek-reasoner" }),
                AIProviderId.OpenRouter => new OpenAICompatibleProvider("OpenRouter", "OpenRouter", "https://openrouter.ai/api/v1", new[] { "openai/gpt-4o-mini", "anthropic/claude-3.5-sonnet", "deepseek/deepseek-chat" }),
                AIProviderId.Anthropic => new AnthropicProvider(),
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            };
        }

        public static string DisplayName(AIProviderId id) => Get(id).DisplayName;
    }
}
