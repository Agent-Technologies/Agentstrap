namespace Agentstrap.AI.Providers
{
    public interface IAIProvider
    {
        string Id { get; }
        string DisplayName { get; }
        bool SupportsStreaming { get; }
        bool SupportsToolCalling { get; }
        IEnumerable<string> GetModels();
        void SetModels(IEnumerable<string> models);

        Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct);
        Task<AIChatResponse> ChatAsync(AIChatRequest request, string apiKey, CancellationToken ct);
        IAsyncEnumerable<AIStreamEvent> StreamAsync(AIChatRequest request, string apiKey, CancellationToken ct);
    }
}
