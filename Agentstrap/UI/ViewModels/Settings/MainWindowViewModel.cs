using System.Windows;
using System.Windows.Input;

using Agentstrap.UI.Elements.About;

using CommunityToolkit.Mvvm.Input;

namespace Agentstrap.UI.ViewModels.Settings
{
    public class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand OpenAboutCommand =>
            new RelayCommand(OpenAbout);

        public ICommand SaveSettingsCommand =>
            new RelayCommand(SaveSettings);

        public ICommand CloseWindowCommand =>
            new RelayCommand(CloseWindow);

        public ICommand LaunchRobloxCommand =>
            new RelayCommand(LaunchRoblox);

        public ICommand SaveAndLaunchCommand =>
            new RelayCommand(SaveAndLaunch);

        public ICommand SaveAndLaunchSettingsCommand =>
            new RelayCommand(SaveAndLaunch);

        public ICommand SaveAndLaunchRobloxCommand =>
            new RelayCommand(SaveAndLaunch);

        public EventHandler? RequestSaveNoticeEvent;

        public EventHandler? RequestCloseWindowEvent;

        public EventHandler? RequestLaunchRobloxEvent;

        public EventHandler? RequestApplyNavigationLayoutEvent;

        /// <summary>
        /// Shown in the status bar so the build you are actually running is always
        /// visible without opening About.
        /// </summary>
        public string AppVersion =>
            $"Agentstrap {App.Version}";

        public bool TestModeEnabled
        {
            get => App.LaunchSettings.TestModeFlag.Active;

            set
            {
                if (value)
                {
                    var result = Frontend.ShowMessageBox(
                        Strings.Menu_TestMode_Prompt,
                        MessageBoxImage.Information,
                        MessageBoxButton.YesNo
                    );

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                App.LaunchSettings.TestModeFlag.Active = value;
            }
        }

        private void OpenAbout() =>
            new MainWindow().ShowDialog();

        private void CloseWindow() =>
            RequestCloseWindowEvent?.Invoke(
                this,
                EventArgs.Empty
            );

        private void LaunchRoblox() =>
            RequestLaunchRobloxEvent?.Invoke(
                this,
                EventArgs.Empty
            );

        public void SaveAndLaunch() =>
            SaveAndLaunchSettings();

        private void SaveAndLaunchSettings()
        {
            SaveSettings();

            RequestLaunchRobloxEvent?.Invoke(
                this,
                EventArgs.Empty
            );
        }

        private void SaveSettings()
        {
            const string LOG_IDENT =
                "MainWindowViewModel::SaveSettings";

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();
            App.GlobalSettings.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.WriteLine(
                        LOG_IDENT,
                        $"Executing pending task '{task}'"
                    );

                    task.Execute();
                }
            }

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();
            App.GlobalSettings.Save();

            App.PendingSettingTasks.Clear();

            RequestApplyNavigationLayoutEvent?.Invoke(this, EventArgs.Empty);

            RequestSaveNoticeEvent?.Invoke(
                this,
                EventArgs.Empty
            );
        }
    }
}
