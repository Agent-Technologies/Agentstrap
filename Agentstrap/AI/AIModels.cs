namespace Agentstrap.AI
{
    public enum AIProviderId
    {
        OpenAI,
        Anthropic,
        Grok,
        Groq,
        DeepSeek,
        OpenRouter
    }

    public enum AIPermissionMode
    {
        ChatOnly,
        AssistedControl,
        FullControl
    }

    public class AIChatMessage
    {
        public string Role { get; set; } = "user"; // system/user/assistant/tool
        public string Content { get; set; } = "";
        public string? ToolCallId { get; set; }
        public string? ToolName { get; set; }
        public List<AIToolCall> ToolCalls { get; set; } = new();
    }

    public class AIToolCall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public Dictionary<string, object?> Arguments { get; set; } = new();
    }

    public class AIToolDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ParametersJsonSchema { get; set; } = "{}";
    }

    public class AIToolResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }

    public class AIChatRequest
    {
        public string Model { get; set; } = "";
        public List<AIChatMessage> Messages { get; set; } = new();
        public List<AIToolDefinition> Tools { get; set; } = new();
        public bool Stream { get; set; } = true;
        public double Temperature { get; set; } = 0.7;
    }

    public class AIChatResponse
    {
        public string? Content { get; set; }
        public List<AIToolCall> ToolCalls { get; set; } = new();
        public string? Error { get; set; }
        public bool Success => Error is null;
    }

    public class AIStreamEvent
    {
        public string? TextDelta { get; set; }
        public List<AIToolCall>? ToolCalls { get; set; }
        public bool Done { get; set; }
        public string? Error { get; set; }
    }

    public enum AIModelPricingStatus
    {
        Free,
        FreeTier,
        Paid,
        Trial,
        Unknown
    }

    public class AIModelInfo
    {
        public string ProviderId { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public AIModelPricingStatus PricingStatus { get; set; } = AIModelPricingStatus.Unknown;
        public double? InputPricePer1M { get; set; }
        public double? OutputPricePer1M { get; set; }
        public int? ContextWindow { get; set; }
        public bool? SupportsTools { get; set; }
        public bool? SupportsStreaming { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;
        public string PricingLabel => PricingStatus switch
        {
            AIModelPricingStatus.Free => "FREE",
            AIModelPricingStatus.FreeTier => "FREE TIER",
            AIModelPricingStatus.Paid => "PAID",
            AIModelPricingStatus.Trial => "TRIAL",
            _ => "UNKNOWN"
        };
    }

    public class AIConversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New conversation";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        public List<AIChatMessage> Messages { get; set; } = new();
    }
}
