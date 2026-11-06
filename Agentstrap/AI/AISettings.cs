using System.Text.Json.Serialization;

namespace Agentstrap.AI
{
    public class AISettingsData
    {
        public string SelectedProvider { get; set; } = "";
        public string SelectedModel { get; set; } = "";
        public string CustomModelId { get; set; } = "";
        public bool StreamingEnabled { get; set; } = true;
        public bool SaveConversationHistory { get; set; } = true;
        public string PermissionMode { get; set; } = nameof(AIPermissionMode.ChatOnly);
        public bool AIControlEnabled { get; set; } = false;
        public bool AIControlApproved { get; set; } = false;
        public double DashboardLeft { get; set; } = -1;
        public double DashboardTop { get; set; } = -1;
        public double DashboardWidth { get; set; } = 400;
        public double DashboardHeight { get; set; } = 560;
        public List<string> AllowedToolCategories { get; set; } = new() { "navigation", "settings_read", "app_info" };
        public bool AutoFreeModelSelection { get; set; } = true;
        public bool AutoFallbackEnabled { get; set; } = true;
        public bool SelectedModelManually { get; set; } = false;
        public bool ModelDiscoveryCached { get; set; } = false;
    }
}
