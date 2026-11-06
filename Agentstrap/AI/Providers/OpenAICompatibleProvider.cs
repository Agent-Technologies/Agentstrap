using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Agentstrap.AI.Providers
{
    public class OpenAICompatibleProvider : IAIProvider
    {
        public virtual string Id { get; protected set; }
        public string DisplayName { get; }
        public string BaseUrl { get; }
        public bool SupportsStreaming => true;
        public bool SupportsToolCalling => true;
        private List<string> _models = new();

        public OpenAICompatibleProvider(string id, string displayName, string baseUrl, IEnumerable<string>? defaultModels = null)
        {
            Id = id;
            DisplayName = displayName;
            BaseUrl = baseUrl.TrimEnd('/');
            if (defaultModels is not null) _models = defaultModels.ToList();
        }

        public IEnumerable<string> GetModels() => _models;
        public void SetModels(IEnumerable<string> models) => _models = models.ToList();

        public virtual async Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct)
        {
            try
            {
                var req = new AIChatRequest
                {
                    Model = string.IsNullOrWhiteSpace(model) ? (_models.FirstOrDefault() ?? "gpt-4o-mini") : model,
                    Messages = new() { new AIChatMessage { Role = "user", Content = "Reply with OK." } },
                    Stream = false
                };
                var resp = await ChatAsync(req, apiKey, ct);
                return resp.Success;
            }
            catch { return false; }
        }

        public virtual async Task<AIChatResponse> ChatAsync(AIChatRequest request, string apiKey, CancellationToken ct)
        {
            var response = new AIChatResponse();
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(120);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var body = BuildBody(request, stream: false);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var res = await http.PostAsync($"{BaseUrl}/chat/completions", content, ct);
                string json = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                {
                    response.Error = $"HTTP {(int)res.StatusCode}: {ExtractError(json)}";
                    return response;
                }

                using var doc = JsonDocument.Parse(json);
                var choice = doc.RootElement.GetProperty("choices")[0];
                var msg = choice.GetProperty("message");
                if (msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    response.Content = c.GetString();

                if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tc.EnumerateArray())
                    {
                        var call = new AIToolCall
                        {
                            Id = t.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                            Name = t.GetProperty("function").GetProperty("name").GetString() ?? ""
                        };
                        string argsStr = t.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                        call.Arguments = ParseArgs(argsStr);
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

        public virtual async IAsyncEnumerable<AIStreamEvent> StreamAsync(AIChatRequest request, string apiKey, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var events = new List<AIStreamEvent>();
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(300);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var body = BuildBody(request, stream: true);

            var toolArgBuffers = new Dictionary<int, StringBuilder>();
            var toolNames = new Dictionary<int, string>();
            var toolIds = new Dictionary<int, string>();

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions");
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!res.IsSuccessStatusCode)
                {
                    string err = await res.Content.ReadAsStringAsync(ct);
                    events.Add(new AIStreamEvent { Error = $"HTTP {(int)res.StatusCode}: {ExtractError(err)}", Done = true });
                }
                else
                {
                    using var stream = await res.Content.ReadAsStreamAsync(ct);
                    using var reader = new StreamReader(stream);
                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                        string data = line.Substring(5).Trim();
                        if (data == "[DONE]") break;
                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            var choices = doc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() == 0) continue;
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                            {
                                var txt = c.GetString();
                                if (!string.IsNullOrEmpty(txt))
                                    events.Add(new AIStreamEvent { TextDelta = txt });
                            }
                            if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var tc in tcs.EnumerateArray())
                                {
                                    int idx = tc.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                                    if (tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                        toolIds[idx] = idEl.GetString()!;
                                    if (tc.TryGetProperty("function", out var fn))
                                    {
                                        if (fn.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                                            toolNames[idx] = nameEl.GetString()!;
                                        if (fn.TryGetProperty("arguments", out var argEl) && argEl.ValueKind == JsonValueKind.String)
                                        {
                                            if (!toolArgBuffers.ContainsKey(idx)) toolArgBuffers[idx] = new StringBuilder();
                                            toolArgBuffers[idx].Append(argEl.GetString());
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    var tools = new List<AIToolCall>();
                    foreach (var idx in toolNames.Keys.Union(toolArgBuffers.Keys).Union(toolIds.Keys))
                    {
                        var call = new AIToolCall
                        {
                            Id = toolIds.TryGetValue(idx, out var i) ? i : Guid.NewGuid().ToString("N"),
                            Name = toolNames.TryGetValue(idx, out var n) ? n : ""
                        };
                        if (toolArgBuffers.TryGetValue(idx, out var sb))
                            call.Arguments = ParseArgs(sb.ToString());
                        tools.Add(call);
                    }
                    if (tools.Count > 0)
                        events.Add(new AIStreamEvent { ToolCalls = tools, Done = true });
                    else
                        events.Add(new AIStreamEvent { Done = true });
                }
            }
            catch (Exception e)
            {
                events.Add(new AIStreamEvent { Error = e.Message, Done = true });
            }

            foreach (var ev in events)
                yield return ev;
        }

        // Override with real SSE for OpenAI-compatible
        public static async Task<OpenAIStreamResult> StreamSseAsync(HttpClient http, string url, string body, CancellationToken ct, Action<string> onText, Action<List<AIToolCall>> onTools)
        {
            var tools = new List<AIToolCall>();
            var toolArgBuffers = new Dictionary<int, StringBuilder>();
            var toolNames = new Dictionary<int, string>();
            var toolIds = new Dictionary<int, string>();

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                string err = await res.Content.ReadAsStringAsync(ct);
                throw new Exception($"HTTP {(int)res.StatusCode}: {err}");
            }
            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                string data = line.Substring(5).Trim();
                if (data == "[DONE]") break;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        var txt = c.GetString();
                        if (!string.IsNullOrEmpty(txt)) onText(txt!);
                    }
                    if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tc in tcs.EnumerateArray())
                        {
                            int idx = tc.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                            if (tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                toolIds[idx] = idEl.GetString()!;
                            if (tc.TryGetProperty("function", out var fn))
                            {
                                if (fn.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                                    toolNames[idx] = nameEl.GetString()!;
                                if (fn.TryGetProperty("arguments", out var argEl) && argEl.ValueKind == JsonValueKind.String)
                                {
                                    if (!toolArgBuffers.ContainsKey(idx)) toolArgBuffers[idx] = new StringBuilder();
                                    toolArgBuffers[idx].Append(argEl.GetString());
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // finalize tools
            foreach (var idx in toolNames.Keys.Union(toolArgBuffers.Keys).Union(toolIds.Keys))
            {
                var call = new AIToolCall
                {
                    Id = toolIds.TryGetValue(idx, out var i) ? i : Guid.NewGuid().ToString("N"),
                    Name = toolNames.TryGetValue(idx, out var n) ? n : ""
                };
                if (toolArgBuffers.TryGetValue(idx, out var sb))
                    call.Arguments = ParseArgs(sb.ToString());
                tools.Add(call);
            }
            if (tools.Count > 0) onTools(tools);
            return new OpenAIStreamResult();
        }

        protected string BuildBody(AIChatRequest request, bool stream)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{Escape(request.Model)}\",");
            sb.Append("\"messages\":[");
            for (int i = 0; i < request.Messages.Count; i++)
            {
                var m = request.Messages[i];
                if (i > 0) sb.Append(",");
                sb.Append("{\"role\":\"").Append(m.Role).Append("\",");
                if (m.Role == "tool")
                {
                    sb.Append($"\"tool_call_id\":\"{Escape(m.ToolCallId ?? "")}\",");
                    sb.Append($"\"content\":\"{Escape(m.Content)}\"");
                }
                else if (m.ToolCalls.Count > 0)
                {
                    sb.Append($"\"content\":{(m.Content is null ? "null" : $"\"{Escape(m.Content)}\"")},");
                    sb.Append("\"tool_calls\":[");
                    for (int j = 0; j < m.ToolCalls.Count; j++)
                    {
                        if (j > 0) sb.Append(",");
                        var tc = m.ToolCalls[j];
                        sb.Append($"{{\"id\":\"{Escape(tc.Id)}\",\"type\":\"function\",\"function\":{{\"name\":\"{Escape(tc.Name)}\",\"arguments\":\"{Escape(JsonSerializer.Serialize(tc.Arguments))}\"}}}}");
                    }
                    sb.Append("]");
                }
                else
                {
                    sb.Append($"\"content\":\"{Escape(m.Content)}\"");
                }
                sb.Append("}");
            }
            sb.Append("]");
            if (request.Tools.Count > 0)
            {
                sb.Append(",\"tools\":[");
                for (int i = 0; i < request.Tools.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    var t = request.Tools[i];
                    sb.Append($"{{\"type\":\"function\",\"function\":{{\"name\":\"{Escape(t.Name)}\",\"description\":\"{Escape(t.Description)}\",\"parameters\":{t.ParametersJsonSchema}}}}}");
                }
                sb.Append("],\"tool_choice\":\"auto\"");
            }
            if (stream) sb.Append(",\"stream\":true");
            sb.Append("}");
            return sb.ToString();
        }

        public static string Escape(string s)
        {
            if (s is null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        protected static Dictionary<string, object?> ParseArgs(string s)
        {
            var dict = new Dictionary<string, object?>();
            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch { }
            return dict;
        }

        public static string ExtractError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var e))
                {
                    if (e.TryGetProperty("message", out var m)) return m.GetString() ?? json;
                    return e.ToString();
                }
            }
            catch { }
            return json.Length > 200 ? json[..200] : json;
        }

    }

    public class OpenAIStreamResult { }
}
