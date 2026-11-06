using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using Agentstrap.UI.Elements.AI;
using Agentstrap.UI.ViewModels.Settings;

namespace Agentstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        private AIWindow? _aiWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();
            viewModel.RequestLaunchRobloxEvent += (_, _) => LaunchHandler.LaunchRoblox(LaunchMode.Player);
            viewModel.RequestApplyNavigationLayoutEvent += (_, _) => ApplyNavigationLayout();

            DataContext = viewModel;

            InitializeComponent();

            Agentstrap.AI.AgentstrapActionRegistry.SettingsWindow = this;

            ApplyNavigationLayout();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            LoadState();
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        private void ApplyNavigationLayout()
        {
            var settings = App.Settings.Prop;
            var items = RootNavigation.Items.OfType<Wpf.Ui.Controls.NavigationItem>().ToList();
            var separators = RootNavigation.Items.OfType<Wpf.Ui.Controls.Navigation.NavigationSeparator>().ToList();
            var order = settings.NavigationTabOrder ?? new List<string>();
            var hidden = settings.HiddenNavigationTabs ?? new List<string>();
            var ordered = items
                .Where(item => item.Tag is string tag && !string.Equals(tag, "fastflageditor", StringComparison.OrdinalIgnoreCase) && !hidden.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .OrderBy(item => { var index = order.FindIndex(tag => string.Equals(tag, item.Tag?.ToString(), StringComparison.OrdinalIgnoreCase)); return index < 0 ? int.MaxValue : index; })
                .ToList();

            RootNavigation.Items.Clear();
            string? previousGroup = null;
            var separatorIndex = 0;
            foreach (var item in ordered)
            {
                var tag = item.Tag?.ToString() ?? string.Empty;
                var group = tag is "dashboard" ? "home" : tag is "roulette" or "experiences" or "sidequest" ? "discover" : "settings";
                if (previousGroup is not null && previousGroup != group)
                {
                    RootNavigation.Items.Add(separatorIndex < separators.Count ? separators[separatorIndex++] : new Wpf.Ui.Controls.Navigation.NavigationSeparator());
                }
                RootNavigation.Items.Add(item);
                previousGroup = group;
            }
            if (items.Any(item => string.Equals(item.Tag?.ToString(), "fastflageditor", StringComparison.OrdinalIgnoreCase)))
            {
                var editor = items.First(item => string.Equals(item.Tag?.ToString(), "fastflageditor", StringComparison.OrdinalIgnoreCase));
                editor.Visibility = Visibility.Collapsed;
                RootNavigation.Items.Add(editor);
            }
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void AILauncher_Click(object sender, RoutedEventArgs e)
        {
            if (_aiWindow is null || !_aiWindow.IsVisible)
            {
                _aiWindow = new AIWindow();
                _aiWindow.Show();
            }
            else
            {
                _aiWindow.Activate();
            }
        }

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }
    }
}
