using System.Collections.ObjectModel;

namespace Agentstrap.Models.Persistable
{
    public class Settings
    {
        // bloxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Dark;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool ConfirmLaunches { get; set; } = false;
        public string RobloxProcessPriority { get; set; } = "Normal";
        public string RobloxDeploymentChannel { get; set; } = "";
        public bool AutoJoinExperienceEnabled { get; set; } = false;
        public string AutoJoinExperiencePlaceId { get; set; } = "";
        public string AutoJoinExperienceName { get; set; } = "";
        [JsonPropertyName("VoidstrapMatchmakerEnabled")]
        public bool AgentstrapMatchmakerEnabled { get; set; } = false;
        [JsonPropertyName("VoidstrapMatchmakerPlaceId")]
        public string AgentstrapMatchmakerPlaceId { get; set; } = "";
        [JsonPropertyName("VoidstrapMatchmakerPreferLowPopulation")]
        public bool AgentstrapMatchmakerPreferLowPopulation { get; set; } = true;
        [JsonPropertyName("VoidstrapMatchmakerMinimumFreeSlots")]
        public int AgentstrapMatchmakerMinimumFreeSlots { get; set; } = 1;
        public string Locale { get; set; } = "nil";
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = true;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public string? SelectedCustomTheme { get; set; } = null;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // Main sidebar layout: order stores navigation tags; hidden tabs are omitted from the sidebar.
        public List<string> NavigationTabOrder { get; set; } = new()
        {
            "dashboard", "roulette", "experiences", "notifications", "analytics", "sidequest", "integrations", "bootstrapper",
            "mods", "fastflags", "qol", "globalsettings", "appearance", "shortcuts", "updates",
            "diagnostics", "deployment", "advanced"
        };
        public List<string> HiddenNavigationTabs { get; set; } = new();

        // integration configuration
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public bool ShowServerConnectionOverlay { get; set; } = true;
        public int ServerConnectionOverlayDuration { get; set; } = 5;

        // server connection push notifications (Bark)
        public bool ServerConnectionNotificationsEnabled { get; set; } = false;
        public bool ServerConnectionNotificationIncludeIpAddress { get; set; } = false;
        public bool ServerConnectionNotificationDiagnostics { get; set; } = false;
        public int ServerConnectionNotificationSuppressionMinutes { get; set; } = 30;

        public bool ServerConnectionNotificationDisableIpInfo { get; set; } = false;

        public bool ServerConnectionNotificationDisableIpWho { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // Notification Center
        public bool NotificationCenterEnabled { get; set; } = true;
        public bool NotificationCategoryIpWhoEnabled { get; set; } = true;
        public bool NotificationCategoryIpInfoEnabled { get; set; } = true;
        public bool NotificationCategoryRobloxEnabled { get; set; } = true;
        public bool NotificationCategoryAgentstrapEnabled { get; set; } = true;
        public bool NotificationCategorySystemEnabled { get; set; } = true;
        public bool StoreIpAddressesInNotifications { get; set; } = true;
        public bool ShowLocationInfoInNotifications { get; set; } = true;
        public int NotificationRetentionDays { get; set; } = 30;
        public bool AutoRemoveOldNotifications { get; set; } = true;

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;

        // quality of life configuration
        public bool CustomWindowAttributesEnabled { get; set; } = false;
        public int? WindowWidth { get; set; } = null;
        public int? WindowHeight { get; set; } = null;
        public int? WindowPositionX { get; set; } = null;
        public int? WindowPositionY { get; set; } = null;
        public string WindowTitle { get; set; } = "";

        // Roblox Player window presentation preferences.
        public bool RobloxBorderlessWindowEnabled { get; set; } = false;
        public bool RobloxAutomaticBorderlessModeEnabled { get; set; } = false;
        public bool RobloxBorderlessCenterWindow { get; set; } = false;

        // Roblox Apple Emoji Mode configuration. This feature never replaces Windows
        // system fonts; it only records whether a user-supplied, compatible emoji
        // font should be applied to Roblox through the Modifications overlay.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RobloxAppleEmojiModeEnabled { get; set; } = false;

        public string RobloxAppleEmojiModeFontPath { get; set; } = "";

        public string RobloxAppleEmojiModeAppliedHash { get; set; } = "";

        // NEXUS configuration
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NexusModeEnabled { get; set; } = false;
        public string NexusChannel { get; set; } = "stable";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NexusVerboseLogging { get; set; } = false;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NexusAllowModOverrides { get; set; } = false;
    }
}

