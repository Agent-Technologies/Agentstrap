namespace Agentstrap.AI
{
    public static class ModelSelectionService
    {
        public static AIModelInfo? SelectRandomFree(List<AIModelInfo> models, AIPermissionMode mode)
        {
            var eligible = models.Where(m => m.IsAvailable).ToList();
            // pricing filter
            eligible = eligible.Where(m => m.PricingStatus == AIModelPricingStatus.Free || m.PricingStatus == AIModelPricingStatus.FreeTier).ToList();
            // capability filter
            if (mode != AIPermissionMode.ChatOnly)
                eligible = eligible.Where(m => m.SupportsTools == true).ToList();
            if (eligible.Count == 0) return null;
            int idx = Random.Shared.Next(eligible.Count);
            return eligible[idx];
        }

        public static AIModelInfo? CheapestPaid(List<AIModelInfo> models)
        {
            var paid = models.Where(m => m.PricingStatus == AIModelPricingStatus.Paid && m.InputPricePer1M.HasValue && m.OutputPricePer1M.HasValue)
                .OrderBy(m => (m.InputPricePer1M ?? 0) + (m.OutputPricePer1M ?? 0)).FirstOrDefault();
            return paid;
        }

        public static bool RequiresToolSupport(AIPermissionMode mode) => mode != AIPermissionMode.ChatOnly;
    }
}
