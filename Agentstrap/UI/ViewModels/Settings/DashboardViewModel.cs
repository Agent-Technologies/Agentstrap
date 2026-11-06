using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Agentstrap.AppData;
using Agentstrap.UI.Elements.Settings.Pages;
using Agentstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Agentstrap.UI.ViewModels.Settings
{
    public class DashboardViewModel : NotifyPropertyChangedViewModel
    {
        private static RobloxPlayerData PlayerData => new();

        public string RobloxInstallStatus =>
            File.Exists(PlayerData.ExecutablePath) ? "Installed" : "Not detected";

        public string RobloxInstallDetails =>
            File.Exists(PlayerData.ExecutablePath)
                ? "Roblox Player files are ready for Agentstrap."
                : "Roblox Player will be installed when you launch it.";

        public string ProcessPriority =>
            App.Settings.Prop.RobloxProcessPriority;

        public string LastLaunchStatus =>
            App.State.Prop.LastRobloxLaunchStatus;

        public string LastLaunchDetails =>
            App.State.Prop.LastRobloxLaunchDetails;

        public string LastLaunchTime =>
            App.State.Prop.LastRobloxLaunchTimeUtc is DateTime timestamp
                ? timestamp.ToLocalTime().ToString("g")
                : "No previous launch";

        /// <summary>
        /// Raised when the command center asks the shell to show a different page.
        /// The page translates the page type into a navigation call, keeping this
        /// view model free of any dependency on the window.
        /// </summary>
        public event EventHandler<Type>? RequestNavigateEvent;

        public ICommand LaunchRobloxCommand =>
            new RelayCommand(() => LaunchRoblox(LaunchMode.Player));

        public ICommand LaunchRobloxStudioCommand =>
            new RelayCommand(() => LaunchRoblox(LaunchMode.Studio));

        public ICommand CheckUpdatesCommand =>
            new RelayCommand(() => NavigateTo(typeof(UpdatePage)));

        public ICommand RunDiagnosticsCommand =>
            new RelayCommand(() => NavigateTo(typeof(DiagnosticsPage)));

        public ICommand OpenAdvancedCommand =>
            new RelayCommand(() => NavigateTo(typeof(AdvancedPage)));

        public ICommand RefreshInstallStatusCommand =>
            new RelayCommand(RefreshInstallStatus);

        public ICommand OpenAgentstrapLogsCommand =>
            new RelayCommand(OpenAgentstrapLogs);

        public ICommand OpenLogsCommand =>
            new RelayCommand(OpenAgentstrapLogs);

        public string AgentstrapVersion => App.Version;

        public string RecentExperienceSummary
        {
            get
            {
                var recent = ExperienceLibraryService.GetRecords().Where(x => x.LastLaunchedUtc.HasValue).OrderByDescending(x => x.LastLaunchedUtc).Take(3).ToList();
                return recent.Count == 0 ? "Recently launched experiences will appear here." : string.Join("  ·  ", recent.Select(x => x.Name));
            }
        }

        public ICommand OpenExperiencesCommand => new RelayCommand(() => NavigateTo(typeof(ExperienceRecordsPage)));
        public ICommand OpenProfilesCommand => new RelayCommand(() => NavigateTo(typeof(ProfilesPage)));
        public ICommand OpenRouletteCommand => new RelayCommand(() => NavigateTo(typeof(GameRoulettePage)));
        public ICommand OpenSidequestCommand => new RelayCommand(() => NavigateTo(typeof(SidequestPage)));
        public ICommand OpenModsCommand => new RelayCommand(() => NavigateTo(typeof(ModsPage)));
        public ICommand OpenGlobalSettingsCommand => new RelayCommand(() => NavigateTo(typeof(GlobalSettingsPage)));

        public string NexusMode =>
            App.Settings.Prop.NexusModeEnabled
                ? $"On ({App.Settings.Prop.NexusChannel})"
                : "Off";

        public string TestModeText =>
            App.LaunchSettings.TestModeFlag.Active
                ? "On"
                : "Off";

        public ICommand RepairLinksCommand =>
            new RelayCommand(RepairRobloxLink);

        private void LaunchRoblox(LaunchMode mode)
        {
            App.Logger.WriteLine(
                "Dashboard",
                $"Launch requested from the command center ({mode})"
            );

            LaunchHandler.LaunchRoblox(mode);
        }

        private void NavigateTo(Type pageType) =>
            RequestNavigateEvent?.Invoke(this, pageType);

        public ICommand OpenRobloxLogsCommand =>
            new RelayCommand(OpenRobloxLogs);

        public ICommand RepairRobloxLinkCommand =>
            new RelayCommand(RepairRobloxLink);

        private void RefreshInstallStatus()
        {
            OnPropertyChanged(nameof(RobloxInstallStatus));
            OnPropertyChanged(nameof(RobloxInstallDetails));
        }

        private static void OpenAgentstrapLogs()
        {
            OpenFolder(Paths.Logs);
        }

        public static void OpenRobloxLogs()
        {
            string path = Path.Combine(Paths.LocalAppData, "Roblox", "logs");

            if (!Directory.Exists(path))
            {
                Frontend.ShowMessageBox(
                    "Roblox has not created a log folder yet.",
                    MessageBoxImage.Information
                );

                return;
            }

            OpenFolder(path);
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Could not open the folder.\n\n{ex.Message}",
                    MessageBoxImage.Error
                );
            }
        }

        public static void RepairRobloxLink()
        {
            WindowsRegistry.RegisterPlayer(
                Paths.Process,
                "-player \"%1\""
            );

            App.Logger.WriteLine(
                "Dashboard",
                $"Registered Roblox links to {Paths.Process}"
            );

            Frontend.ShowMessageBox(
                "Roblox links will now open with Agentstrap.",
                MessageBoxImage.Information
            );
        }
    }
}
