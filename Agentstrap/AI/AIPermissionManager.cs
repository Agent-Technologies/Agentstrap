namespace Agentstrap.AI
{
    public static class AIPermissionManager
    {
        public static AIPermissionMode Mode
        {
            get
            {
                var s = App.AISettings.Prop.PermissionMode;
                return Enum.TryParse<AIPermissionMode>(s, out var m) ? m : AIPermissionMode.ChatOnly;
            }
            set { App.AISettings.Prop.PermissionMode = value.ToString(); App.AISettings.Save(); }
        }

        public static bool ControlEnabled
        {
            get => App.AISettings.Prop.AIControlEnabled;
            set { App.AISettings.Prop.AIControlEnabled = value; App.AISettings.Save(); }
        }

        public static bool RequiresConfirmation(string toolName, AIPermissionMode mode)
        {
            if (mode == AIPermissionMode.FullControl)
            {
                // sensitive/destructive always require confirmation
                string[] sensitive = { "delete", "reset", "install" };
                return sensitive.Any(s => toolName.Contains(s, StringComparison.OrdinalIgnoreCase));
            }
            if (mode == AIPermissionMode.AssistedControl)
            {
                // writes require confirmation
                return toolName.StartsWith("set_") || toolName.StartsWith("toggle_");
            }
            return false;
        }
    }
}
