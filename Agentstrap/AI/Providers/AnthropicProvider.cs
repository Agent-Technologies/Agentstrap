using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Agentstrap.AI.Providers
{
    public class AnthropicProvider : IAIProvider
    {
        public string Id => "Anthropic";
        public string DisplayName => "Anthropic Claude";
        public string BaseUrl => "https://api.anthropic.com/v1";
        public bool SupportsStreaming => true;
        public bool SupportsToolCalling => true;

        private List<string> _models = new()
        {
            "claude-3-5-sonnet-20241022",
            "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229"
        };

        public IEnumerable<string> GetModels() => _models;
        public void SetModels(IEnumerable<string> models) => _models = models.ToList();

        public async Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct)
        {
            try
            {
                var m = string.IsNullOrWhiteSpace(model) ? _models.First() : model;
                var req = new AIChatRequest { Model = m, Messages = new() { new AIChatMessage { Role = "user", Content = "Reply with OK." } }, Stream = false };
                var resp = await ChatAsync(req, apiKey, ct);
                return resp.Success;
            }
            catch { return false; }
        }

        public async Task<AIChatResponse> ChatAsync(AIChatRequest request, string apiKey, CancellationToken ct)
        {
            var response = new AIChatResponse();
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(120);
                http.DefaultRequestHeaders.Add("x-api-key", apiKey);
                http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                string model = string.IsNullOrWhiteSpace(request.Model) ? _models.First() : request.Model;
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"model\":\"{OpenAICompatibleProvider.Escape(model)}\",");
                sb.Append("\"max_tokens\":4096,");
                // system
                var systemMsgs = request.Messages.Where(m => m.Role == "system").ToList();
                if (systemMsgs.Count > 0)
                {
                    sb.Append($"\"system\":\"{OpenAICompatibleProvider.Escape(string.Join("\\n", systemMsgs.Select(m => m.Content)))}\",");
                }
                sb.Append("\"messages\":[");
                var msgs = request.Messages.Where(m => m.Role != "system").ToList();
                for (int i = 0; i < msgs.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var m = msgs[i];
                    if (m.Role == "tool")
                    {
                        sb.Append("{\"role\":\"user\",\"content\":[");
                        sb.Append($"{{\"type\":\"tool_result\",\"tool_use_id\":\"{OpenAICompatibleProvider.Escape(m.ToolCallId ?? "")}\",\"content\":\"{OpenAICompatibleProvider.Escape(m.Content)}\"}}");
                        sb.Append("]}");
                    }
                    else if (m.ToolCalls.Count > 0)
                    {
                        sb.Append("{\"role\":\"assistant\",\"content\":[");
                        if (!string.IsNullOrEmpty(m.Content))
                            sb.Append($"{{\"type\":\"text\",\"text\":\"{OpenAICompatibleProvider.Escape(m.Content)}\"}},");
                        foreach (var tc in m.ToolCalls)
                        {
                            sb.Append($"{{\"type\":\"tool_use\",\"id\":\"{OpenAICompatibleProvider.Escape(tc.Id)}\",\"name\":\"{OpenAICompatibleProvider.Escape(tc.Name)}\",\"input\":{JsonSerializer.Serialize(tc.Arguments)}}},");
                        }
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append("]}");
                    }
                    else
                    {
                        sb.Append($"{{\"role\":\"{m.Role}\",\"content\":\"{OpenAICompatibleProvider.Escape(m.Content)}\"}}");
                    }
                }
                sb.Append("]");
                if (request.Tools.Count > 0)
                {
                    sb.Append(",\"tools\":[");
                    for (int i = 0; i < request.Tools.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        var t = request.Tools[i];
                        sb.Append($"{{\"name\":\"{OpenAICompatibleProvider.Escape(t.Name)}\",\"description\":\"{OpenAICompatibleProvider.Escape(t.Description)}\",\"input_schema\":{t.ParametersJsonSchema}}}");
                    }
                    sb.Append("]");
                }
                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var res = await http.PostAsync($"{BaseUrl}/messages", content, ct);
                string json = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                {
                    response.Error = $"HTTP {(int)res.StatusCode}: {OpenAICompatibleProvider.ExtractError(json)}";
                    return response;
                }

                using var doc = JsonDocument.Parse(json);
                var contentArr = doc.RootElement.GetProperty("content");
                foreach (var block in contentArr.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() == "text")
                    {
                        response.Content = (response.Content ?? "") + (block.GetProperty("text").GetString() ?? "");
                    }
                    else if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        var call = new AIToolCall
                        {
                            Id = block.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                            Name = block.GetProperty("name").GetString() ?? ""
                        };
                        if (block.TryGetProperty("input", out var inp))
                        {
                            call.Arguments = new Dictionary<string, object?>();
                            if (inp.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var p in inp.EnumerateObject())
                                    call.Arguments[p.Name] = p.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => p.Value.GetString(),
                                        JsonValueKind.Number => p.Value.GetDouble(),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        _ => p.Value.GetRawText()
                                    };
                            }
                        }
                        response.ToolCalls.Add(call);
                    }
                }
            }
            catch (Exception ex)
            {
                response.Error = ex.Message;
            }
            return response;
        }

        public async IAsyncEnumerable<AIStreamEvent> StreamAsync(AIChatRequest request, string apiKey, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            // Anthropic streaming not fully implemented; fall back to chat
            var resp = await ChatAsync(request, apiKey, ct);
            if (resp.Error is not null) { yield return new AIStreamEvent { Error = resp.Error, Done = true }; yield break; }
            if (!string.IsNullOrEmpty(resp.Content)) yield return new AIStreamEvent { TextDelta = resp.Content };
            if (resp.ToolCalls.Count > 0) yield return new AIStreamEvent { ToolCalls = resp.ToolCalls, Done = true };
            else yield return new AIStreamEvent { Done = true };
        }
    }
}
