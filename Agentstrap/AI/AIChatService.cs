using Agentstrap.AI.Providers;

namespace Agentstrap.AI
{
    public class AIChatService
    {
        public AIConversation Conversation { get; }

        private readonly List<AIToolDefinition> _tools;

        public AIChatService(AIConversation conversation)
        {
            Conversation = conversation;
            _tools = AgentstrapActionRegistry.GetToolDefinitions();
        }

        private IAIProvider Provider
        {
            get
            {
                var id = AIKeyManager.Parse(App.AISettings.Prop.SelectedProvider) ?? AIProviderId.OpenAI;
                return AIProviderManager.Get(id);
            }
        }

        private string ApiKey
        {
            get
            {
                var id = AIKeyManager.Parse(App.AISettings.Prop.SelectedProvider) ?? AIProviderId.OpenAI;
                return AIKeyManager.Load(id) ?? "";
            }
        }

        private string Model
        {
            get
            {
                var m = App.AISettings.Prop.SelectedModel;
                if (string.IsNullOrWhiteSpace(m)) m = App.AISettings.Prop.CustomModelId;
                return m ?? "";
            }
        }

        public async Task RunTurnAsync(string userText, Action<string> onText, Action<string> onStatus, Func<AIToolCall, Task<bool>> confirmTool, CancellationToken ct)
        {
            Conversation.Messages.Add(new AIChatMessage { Role = "user", Content = userText });
            Conversation.UpdatedUtc = DateTime.UtcNow;
            if (Conversation.Title == "New conversation" && Conversation.Messages.Count <= 2)
                Conversation.Title = userText.Length > 40 ? userText[..40] : userText;

            var mode = AIPermissionManager.Mode;
            bool control = AIPermissionManager.ControlEnabled;

            // If control disabled, don't expose tools? Keep tools but execution blocked.
            var tools = control ? _tools : new List<AIToolDefinition>();

            // Tool-calling loop
            int iterations = 0;
            while (iterations < 8)
            {
                iterations++;
                var req = new AIChatRequest
                {
                    Model = Model,
                    Messages = BuildMessages(mode, control),
                    Tools = tools,
                    Stream = App.AISettings.Prop.StreamingEnabled
                };

                var provider = Provider;
                var apiKey = ApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    onStatus("No API key configured.");
                    return;
                }

                // Collect response
                string assistantText = "";
                List<AIToolCall> toolCalls = new();
                bool done = false;
                string? error = null;

                if (App.AISettings.Prop.StreamingEnabled && provider.SupportsStreaming)
                {
                    await foreach (var ev in provider.StreamAsync(req, apiKey, ct))
                    {
                        if (ev.Error is not null) { error = ev.Error; done = true; break; }
                        if (ev.TextDelta is not null) { assistantText += ev.TextDelta; onText(ev.TextDelta); }
                        if (ev.ToolCalls is not null) { toolCalls = ev.ToolCalls; done = true; }
                        if (ev.Done && toolCalls.Count == 0 && error is null) { /* may still have text */ }
                    }
                    if (!done) done = true;
                }
                else
                {
                    var resp = await provider.ChatAsync(req, apiKey, ct);
                    if (resp.Error is not null) error = resp.Error;
                    else
                    {
                        assistantText = resp.Content ?? "";
                        toolCalls = resp.ToolCalls;
                    }
                    if (!string.IsNullOrEmpty(assistantText)) onText(assistantText);
                }

                if (error is not null)
                {
                    // Attempt free-model fallback once
                    if (App.AISettings.Prop.AutoFallbackEnabled && (error.Contains("model", StringComparison.OrdinalIgnoreCase) || error.Contains("404") || error.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                    {
                        bool fb = await TryFallbackAsync(mode, control, onStatus);
                        if (fb)
                        {
                            // remove any partial assistant message already added? none yet
                            continue;
                        }
                    }
                    onStatus($"Error: {error}");
                    Conversation.Messages.Add(new AIChatMessage { Role = "assistant", Content = $"Error: {error}" });
                    ConversationManager.Save();
                    return;
                }

                // Add assistant message
                var assistantMsg = new AIChatMessage { Role = "assistant", Content = assistantText, ToolCalls = toolCalls };
                Conversation.Messages.Add(assistantMsg);

                if (toolCalls.Count == 0)
                {
                    ConversationManager.Save();
                    return;
                }

                // Execute tools
                foreach (var tc in toolCalls)
                {
                    bool requiresConfirm = AIPermissionManager.RequiresConfirmation(tc.Name, mode);
                    if (mode == AIPermissionMode.AssistedControl && (tc.Name.StartsWith("set_") || tc.Name.StartsWith("toggle_")))
                        requiresConfirm = true;

                    bool approved = true;
                    if (requiresConfirm)
                    {
                        approved = await confirmTool(tc);
                    }

                    AIToolResult result;
                    if (!approved)
                    {
                        result = new AIToolResult { Success = false, Message = "User cancelled." };
                    }
                    else
                    {
                        onStatus($"Running {tc.Name}...");
                        result = await AgentstrapActionRegistry.ExecuteAsync(tc.Name, tc.Arguments, mode, control);
                    }

                    Conversation.Messages.Add(new AIChatMessage { Role = "tool", Content = result.Message, ToolCallId = tc.Id, ToolName = tc.Name });
                    onStatus(result.Message);
                }
                // loop again to let model respond
            }
        }

        private async Task<bool> TryFallbackAsync(AIPermissionMode mode, bool control, Action<string> onStatus)
        {
            try
            {
                var provider = AIKeyManager.Parse(App.AISettings.Prop.SelectedProvider) ?? AIProviderId.OpenAI;
                var key = AIKeyManager.Load(provider);
                if (string.IsNullOrWhiteSpace(key)) return false;
                var models = await ModelDiscoveryService.DiscoverAsync(provider, key!, true);
                var current = App.AISettings.Prop.SelectedModel;
                var eligible = models.Where(m => m.IsAvailable && m.Id != current &&
                    (m.PricingStatus == AIModelPricingStatus.Free || m.PricingStatus == AIModelPricingStatus.FreeTier)).ToList();
                if (mode != AIPermissionMode.ChatOnly)
                    eligible = eligible.Where(m => m.SupportsTools == true).ToList();
                if (eligible.Count == 0) return false;
                var next = eligible[Random.Shared.Next(eligible.Count)];
                App.AISettings.Prop.SelectedModel = next.Id;
                App.AISettings.Save();
                onStatus($"Fallback to {next.Name}: previous model unavailable.");
                return true;
            }
            catch { return false; }
        }

        private List<AIChatMessage> BuildMessages(AIPermissionMode mode, bool control)
        {
            var list = new List<AIChatMessage>();
            list.Add(new AIChatMessage { Role = "system", Content = AgentstrapContext.BuildSystemPrompt(mode, control) });
            // include conversation history
            foreach (var m in Conversation.Messages)
                list.Add(m);
            return list;
        }
    }
}
