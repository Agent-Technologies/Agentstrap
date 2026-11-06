namespace Agentstrap.AI
{
    public static class ModelManager
    {
        public static IEnumerable<string> GetModels(AIProviderId provider, string customModelId)
        {
            var list = AIProviderManager.Get(provider).GetModels().ToList();
            if (!string.IsNullOrWhiteSpace(customModelId) && !list.Contains(customModelId))
                list.Insert(0, customModelId);
            return list;
        }
    }
}
