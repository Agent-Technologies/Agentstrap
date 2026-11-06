namespace Agentstrap.Integrations
{
    /// <summary>Client for Bark's HTTPS push API.</summary>
    public sealed class BarkClient
    {
        public const string DefaultEndpoint = "https://api.day.app/";
        public const string DefaultIconUrl = "https://raw.githubusercontent.com/Agentstrap-Labs/Agentstrap/agentstrap-icon-host/Agentstrap/Agentstrap.png";
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);
        private const int MaxResponseBytes = 16 * 1024;
        private const int MaxMessageLength = 4096;
        private readonly HttpClient _httpClient;
        private readonly TimeSpan _requestTimeout;
        public static readonly BarkClient Shared = new();

        public BarkClient(HttpClient? httpClient = null, TimeSpan? requestTimeout = null)
        {
            _httpClient = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        }

        public Task<BarkResult> SendAsync(string? deviceKey, string title, string message, CancellationToken cancellationToken = default) =>
            SendAsync(deviceKey, new BarkMessage { Title = title, Message = message }, cancellationToken);

        public async Task<BarkResult> SendAsync(string? deviceKey, BarkMessage message, CancellationToken cancellationToken = default)
        {
            const string LOG_IDENT = "BarkClient::SendAsync";
            if (String.IsNullOrWhiteSpace(deviceKey))
                return BarkResult.Failed(BarkResult.ErrorCodes.NotConfigured, "Bark is not configured: a device key is required");
            if (String.IsNullOrWhiteSpace(message.Message))
                return BarkResult.Failed(BarkResult.ErrorCodes.InvalidMessage, "The notification message is empty");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_requestTimeout);
            try
            {
                var payload = new
                {
                    title = Clamp(message.Title ?? String.Empty, BarkMessage.MaxTitleLength),
                    body = Clamp(message.Message, MaxMessageLength),
                    icon = DefaultIconUrl,
                };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                string endpoint = DefaultEndpoint + Uri.EscapeDataString(deviceKey.Trim());
                using var response = await _httpClient.PostAsync(endpoint, content, timeout.Token);
                int status = (int)response.StatusCode;
                string body = await ReadBodyAsync(response, timeout.Token);
                BarkResponsePayload? responsePayload = String.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<BarkResponsePayload>(body);

                if (response.IsSuccessStatusCode && (responsePayload is null || responsePayload.Code == 200))
                    return BarkResult.Successful(status, null);

                string errorCode = status == 429 ? BarkResult.ErrorCodes.RateLimited :
                    status == 401 || status == 403 ? BarkResult.ErrorCodes.AuthenticationError :
                    responsePayload is null ? BarkResult.ErrorCodes.MalformedResponse : BarkResult.ErrorCodes.InvalidResponse;
                string error = !String.IsNullOrWhiteSpace(responsePayload?.Message)
                    ? $"Bark did not accept the notification: {responsePayload.Message}"
                    : $"Bark returned HTTP {status} ({response.StatusCode})";
                return BarkResult.Failed(errorCode, error, status);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (ex is OperationCanceledException)
                {
                    App.Logger.WriteLine(LOG_IDENT, "The Bark request timed out");
                    return BarkResult.Failed(BarkResult.ErrorCodes.Timeout, "The Bark request timed out");
                }
                if (ex is HttpRequestException)
                {
                    App.Logger.WriteLine(LOG_IDENT, "The Bark request failed (network error)");
                    return BarkResult.Failed(BarkResult.ErrorCodes.HttpError, "The Bark request failed (network error)");
                }
                App.Logger.WriteLine(LOG_IDENT, "The Bark request failed unexpectedly");
                App.Logger.WriteException(LOG_IDENT, ex);
                return BarkResult.Failed(BarkResult.ErrorCodes.HttpError, "The Bark request failed");
            }
        }

        private static string Clamp(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content is null) return String.Empty;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var buffer = new char[MaxResponseBytes];
            int read = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (read == buffer.Length && reader.Peek() >= 0) throw new JsonException("Bark response exceeded the maximum accepted size");
            return new string(buffer, 0, read);
        }

        private sealed class BarkResponsePayload
        {
            [JsonPropertyName("code")] public int Code { get; set; }
            [JsonPropertyName("message")] public string? Message { get; set; }
        }
    }
}
