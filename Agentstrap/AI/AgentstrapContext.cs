namespace Agentstrap.AI
{
    public static class AgentstrapContext
    {
        public static string BuildSystemPrompt(AIPermissionMode mode, bool controlEnabled)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are Agentstrap AI, the built-in assistant for Agentstrap, a Windows application for customizing and launching Roblox.");
            sb.AppendLine("Agentstrap features: profiles, extensions, mods, fast flags, integrations, quality of life, global settings, appearance, shortcuts, updates, diagnostics, deployment, advanced settings, accounts, experience records, notification center, roblox analytics, game roulette, sidequest generator.");
            sb.AppendLine("Navigation pages include: dashboard, roulette, experiences, notifications, analytics, sidequest, integrations, bootstrapper, mods, fastflags, qol, extensions, globalsettings, appearance, shortcuts, updates, diagnostics, deployment, advanced, install, agentstrap (settings), profiles, accounts.");
            sb.AppendLine("You can answer questions about these features and, when permitted, perform application actions via tools.");
            sb.AppendLine("Think step-by-step before acting. Plan the required actions, check current state with read tools, then execute changes, and verify results.");
            sb.AppendLine("When Full Control is enabled, you may access and modify the full set of application settings and features through the approved tools.");
            sb.AppendLine($"Permission mode: {mode}. AI Control enabled: {controlEnabled}.");
            if (mode == AIPermissionMode.ChatOnly)
                sb.AppendLine("You may NOT modify settings or execute actions. Only answer questions and read permitted info.");
            sb.AppendLine("Never invent settings, pages, or features. If unsure, say you don't know.");
            sb.AppendLine("When you perform an action, verify and report result.");
            return sb.ToString();
        }

        public static string GetFeatureDescription(string featureId)
        {
            return featureId.ToLowerInvariant() switch
            {
                "profiles" => "Profiles let you save and switch between local Agentstrap settings configurations.",
                "extensions" => "Extensions add functionality to Agentstrap; can be installed from the Extensions page.",
                "backups" => "Agentstrap stores settings in JSON; manual backups can be created via profile export.",
                _ => "No description available."
            };
        }
    }
}
