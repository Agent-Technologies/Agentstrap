using System.Windows;
using System.Reflection;
using Agentstrap.UI.Elements.Settings;

namespace Agentstrap.AI
{
    public static class AgentstrapActionRegistry
    {
        // Hold current settings window for navigation
        public static UI.Elements.Settings.MainWindow? SettingsWindow;

        public static List<AIToolDefinition> GetToolDefinitions() => new()
        {
            new AIToolDefinition { Name = "navigate_to_page", Description = "Navigate to an Agentstrap page by page id.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{\"page_id\":{\"type\":\"string\"}},\"required\":[\"page_id\"]}" },
            new AIToolDefinition { Name = "get_application_version", Description = "Get Agentstrap version.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "get_current_profile", Description = "Get current profile info (none tracked globally).", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "get_setting", Description = "Get any Agentstrap setting value by property name.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{\"setting_id\":{\"type\":\"string\"}},\"required\":[\"setting_id\"]}" },
            new AIToolDefinition { Name = "set_setting", Description = "Set any Agentstrap setting by property name and value.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{\"setting_id\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"setting_id\",\"value\"]}" },
            new AIToolDefinition { Name = "toggle_feature", Description = "Toggle a boolean feature/setting.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{\"feature_id\":{\"type\":\"string\"},\"enabled\":{\"type\":\"boolean\"}},\"required\":[\"feature_id\",\"enabled\"]}" },
            new AIToolDefinition { Name = "get_all_settings", Description = "Get all Agentstrap settings as JSON.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "list_profiles", Description = "List saved profile names.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "get_enabled_extensions", Description = "List enabled extensions info (count).", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "get_backup_status", Description = "Get backup status.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
            new AIToolDefinition { Name = "return_to_home", Description = "Navigate to home dashboard.", ParametersJsonSchema = "{\"type\":\"object\",\"properties\":{}}" },
        };

        public static async Task<AIToolResult> ExecuteAsync(string toolName, Dictionary<string, object?> args, AIPermissionMode mode, bool controlEnabled)
        {
            // Permission enforcement
            bool isRead = toolName.StartsWith("get_") || toolName == "get_application_version";
            bool isNav = toolName.StartsWith("navigate_") || toolName == "return_to_home";
            bool isWrite = toolName.StartsWith("set_") || toolName.StartsWith("toggle_");

            if (!controlEnabled && (isNav || isWrite))
                return new AIToolResult { Success = false, Message = "AI Control is disabled." };

            if (mode == AIPermissionMode.ChatOnly && (isNav || isWrite))
                return new AIToolResult { Success = false, Message = "Chat Only mode does not allow actions." };

            // Assisted control requires confirmation handled by caller; here we execute only if approved. 
            // For simplicity, Assisted mode blocks writes unless confirmed externally; but engine handles confirmation. 
            // We'll allow nav in assisted, writes require confirmation (handled by UI). Here execute writes regardless; permission gating done in UI layer.

            try
            {
                switch (toolName)
                {
                    case "navigate_to_page":
                        {
                            string pageId = args.TryGetValue("page_id", out var v) ? v?.ToString() ?? "" : "";
                            var type = ResolvePageType(pageId);
                            if (type is null) return new AIToolResult { Success = false, Message = $"Unknown page: {pageId}" };
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                SettingsWindow?.Navigate(type);
                            });
                            return new AIToolResult { Success = true, Message = $"Navigated to {pageId}." };
                        }
                    case "return_to_home":
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => SettingsWindow?.Navigate(typeof(UI.Elements.Settings.Pages.DashboardPage)));
                            return new AIToolResult { Success = true, Message = "Navigated home." };
                        }
                    case "get_application_version":
                        return new AIToolResult { Success = true, Message = App.Version, Data = App.Version };
                    case "get_current_profile":
                        return new AIToolResult { Success = true, Message = "No global current profile tracked." };
                    case "get_setting":
                        {
                            string id = args.TryGetValue("setting_id", out var v) ? v?.ToString() ?? "" : "";
                            var prop = typeof(Settings).GetProperty(id, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (prop is null) return new AIToolResult { Success = false, Message = $"Unknown setting: {id}" };
                            var val = prop.GetValue(App.Settings.Prop);
                            return new AIToolResult { Success = true, Message = $"{id} = {val}", Data = val };
                        }
                    case "get_all_settings":
                        {
                            var json = JsonSerializer.Serialize(App.Settings.Prop);
                            return new AIToolResult { Success = true, Message = json, Data = json };
                        }
                    case "list_profiles":
                        {
                            var pm = new Profiles.ProfileManager(Path.Combine(Paths.Base, "Profiles"));
                            var names = pm.ListProfiles();
                            return new AIToolResult { Success = true, Message = string.Join(", ", names), Data = names };
                        }
                    case "set_setting":
                        {
                            string id = args.TryGetValue("setting_id", out var v) ? v?.ToString() ?? "" : "";
                            var prop = typeof(Settings).GetProperty(id, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (prop is null || !prop.CanWrite) return new AIToolResult { Success = false, Message = $"Unknown setting: {id}" };
                            object? converted = ConvertValue(args.TryGetValue("value", out var vv) ? vv : null, prop.PropertyType);
                            prop.SetValue(App.Settings.Prop, converted);
                            App.Settings.Save();
                            return new AIToolResult { Success = true, Message = $"Set {id} = {converted}." };
                        }
                    case "toggle_feature":
                        {
                            string id = args.TryGetValue("feature_id", out var v) ? v?.ToString() ?? "" : "";
                            bool enabled = args.TryGetValue("enabled", out var vv) && (vv is bool b ? b : bool.TryParse(vv?.ToString(), out var bb) && bb);
                            var prop = typeof(Settings).GetProperty(id, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (prop is null || !prop.CanWrite) return new AIToolResult { Success = false, Message = $"Unknown feature: {id}" };
                            prop.SetValue(App.Settings.Prop, ConvertValue(enabled, prop.PropertyType));
                            App.Settings.Save();
                            return new AIToolResult { Success = true, Message = $"Toggled {id} = {enabled}." };
                        }
                    case "get_enabled_extensions":
                        return new AIToolResult { Success = true, Message = "Extensions info not enumerated." };
                    case "get_backup_status":
                        return new AIToolResult { Success = true, Message = "Backup status: manual profiles available." };
                    default:
                        return new AIToolResult { Success = false, Message = $"Unknown tool: {toolName}" };
                }
            }
            catch (Exception ex)
            {
                return new AIToolResult { Success = false, Message = ex.Message };
            }
        }

        private static Type? ResolvePageType(string pageId)
        {
            var pages = typeof(UI.Elements.Settings.Pages.DashboardPage).Assembly.GetTypes()
                .Where(t => t.Namespace == "Agentstrap.UI.Elements.Settings.Pages" && t.Name.EndsWith("Page")).ToList();
            // map by tag-ish names
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dashboard"] = "DashboardPage",
                ["home"] = "DashboardPage",
                ["extensions"] = "ExtensionsPage",
                ["profiles"] = "ProfilesPage",
                ["accounts"] = "AccountsPage",
                ["integrations"] = "IntegrationsPage",
                ["mods"] = "ModsPage",
                ["fastflags"] = "FastFlagsPage",
                ["qol"] = "QoLPage",
                ["appearance"] = "AppearancePage",
                ["shortcuts"] = "ShortcutsPage",
                ["updates"] = "UpdatePage",
                ["diagnostics"] = "DiagnosticsPage",
                ["deployment"] = "DeploymentPage",
                ["advanced"] = "AdvancedPage",
                ["globalsettings"] = "GlobalSettingsPage",
                ["bootstrapper"] = "BootstrapperPage",
            };
            if (map.TryGetValue(pageId, out var name))
            {
                return pages.FirstOrDefault(t => t.Name == name);
            }
            // try direct type name
            return pages.FirstOrDefault(t => t.Name.Equals(pageId, StringComparison.OrdinalIgnoreCase) || t.Name.Equals(pageId + "Page", StringComparison.OrdinalIgnoreCase));
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value is null) return null;
            try
            {
                if (targetType == typeof(bool)) return value is bool b ? b : bool.Parse(value.ToString()!);
                if (targetType == typeof(int)) return value is int i ? i : int.Parse(value.ToString()!);
                if (targetType == typeof(double)) return value is double d ? d : double.Parse(value.ToString()!);
                if (targetType == typeof(string)) return value.ToString();
                return Convert.ChangeType(value, targetType);
            }
            catch { return null; }
        }

        private static object? GetSetting(string id)
        {
            var p = App.Settings.Prop;
            return id.ToLowerInvariant() switch
            {
                "developermode" => p.DeveloperMode,
                "enableanalytics" => p.EnableAnalytics,
                "confirmlaunches" => p.ConfirmLaunches,
                "checkforupdates" => p.CheckForUpdates,
                "usediscordrichpresence" => p.UseDiscordRichPresence,
                "enableactivitytracking" => p.EnableActivityTracking,
                _ => null
            };
        }

        private static bool SetSetting(string id, bool value)
        {
            var p = App.Settings.Prop;
            switch (id.ToLowerInvariant())
            {
                case "developermode": p.DeveloperMode = value; return true;
                case "enableanalytics": p.EnableAnalytics = value; return true;
                case "confirmlaunches": p.ConfirmLaunches = value; return true;
                case "checkforupdates": p.CheckForUpdates = value; return true;
                case "usediscordrichpresence": p.UseDiscordRichPresence = value; return true;
                case "enableactivitytracking": p.EnableActivityTracking = value; return true;
                default: return false;
            }
        }
    }
}
