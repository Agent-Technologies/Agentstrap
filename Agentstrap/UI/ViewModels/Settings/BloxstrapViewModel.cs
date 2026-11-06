using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;

namespace Agentstrap.UI.ViewModels.Settings
{
    public class BloxstrapViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<NavigationTabOption> NavigationTabs { get; } = new();

        private static readonly (string Tag, string Label)[] NavigationTabDefinitions =
        {
            ("dashboard", "Home"), ("roulette", "Game Roulette"), ("experiences", "Experience Records"),
            ("notifications", "Notification Center"), ("analytics", "Roblox Analytics"),
            ("sidequest", "Sidequest Generator"), ("integrations", "Extensions"), ("bootstrapper", "Behaviour"),
            ("mods", "Mods"), ("fastflags", "Fast Flags"), ("qol", "Quality of Life"),
            ("globalsettings", "Global Settings"), ("appearance", "Appearance"), ("shortcuts", "Shortcuts"),
            ("updates", "Updates"), ("diagnostics", "Diagnostics"), ("deployment", "Deployment"), ("advanced", "Advanced")
        };

        public BloxstrapViewModel()
        {
            var settings = App.Settings.Prop;
            settings.NavigationTabOrder ??= new List<string>();
            foreach (var definition in NavigationTabDefinitions)
                if (!settings.NavigationTabOrder.Contains(definition.Tag, StringComparer.OrdinalIgnoreCase)) settings.NavigationTabOrder.Add(definition.Tag);
            foreach (var tag in settings.NavigationTabOrder.ToList())
            {
                var definition = NavigationTabDefinitions.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
                if (definition.Tag is not null) NavigationTabs.Add(new NavigationTabOption(this, definition.Tag, definition.Label, !settings.HiddenNavigationTabs.Contains(definition.Tag, StringComparer.OrdinalIgnoreCase)));
            }
        }

        public void SetTabVisibility(string tag, bool isVisible)
        {
            if (isVisible) App.Settings.Prop.HiddenNavigationTabs.RemoveAll(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
            else if (!App.Settings.Prop.HiddenNavigationTabs.Contains(tag, StringComparer.OrdinalIgnoreCase)) App.Settings.Prop.HiddenNavigationTabs.Add(tag);
        }

        public void MoveTab(NavigationTabOption option, int offset)
        {
            var index = NavigationTabs.IndexOf(option);
            var target = index + offset;
            if (target < 0 || target >= NavigationTabs.Count) return;
            NavigationTabs.Move(index, target);
            App.Settings.Prop.NavigationTabOrder = NavigationTabs.Select(x => x.Tag).ToList();
        }

        public void MoveTabUp(NavigationTabOption option) => MoveTab(option, -1);
        public void MoveTabDown(NavigationTabOption option) => MoveTab(option, 1);

        public WebEnvironment[] WebEnvironments => Enum.GetValues<WebEnvironment>();

        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        public bool AnalyticsEnabled
        {
            get => App.Settings.Prop.EnableAnalytics;
            set => App.Settings.Prop.EnableAnalytics = value;
        }

        public WebEnvironment WebEnvironment
        {
            get => App.Settings.Prop.WebEnvironment;
            set => App.Settings.Prop.WebEnvironment = value;
        }

        public Visibility WebEnvironmentVisibility => App.Settings.Prop.DeveloperMode ? Visibility.Visible : Visibility.Collapsed;

        public bool ShouldExportConfig { get; set; } = true;

        public bool ShouldExportLogs { get; set; } = true;

        public ICommand ExportDataCommand => new RelayCommand(ExportData);

        private void ExportData()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

            var dialog = new SaveFileDialog 
            { 
                FileName = $"Agentstrap-export-{timestamp}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip" 
            };

            if (dialog.ShowDialog() != true)
                return;

            using var memStream = new MemoryStream();
            using var zipStream = new ZipOutputStream(memStream);

            if (ShouldExportConfig)
            {
                var files = new List<string>()
                {
                    App.Settings.FileLocation,
                    App.State.FileLocation,
                    App.FastFlags.FileLocation
                };

                AddFilesToZipStream(zipStream, files, "Config/");
            }

            if (ShouldExportLogs && Directory.Exists(Paths.Logs))
            {
                var files = Directory.GetFiles(Paths.Logs)
                    .Where(x => !x.Equals(App.Logger.FileLocation, StringComparison.OrdinalIgnoreCase));

                AddFilesToZipStream(zipStream, files, "Logs/");
            }

            zipStream.CloseEntry();
            zipStream.Finish();
            memStream.Position = 0;

            using var outputStream = File.OpenWrite(dialog.FileName);
            memStream.CopyTo(outputStream);

            Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
        }

        private void AddFilesToZipStream(ZipOutputStream zipStream, IEnumerable<string> files, string directory)
        {
            const string LOG_IDENT = "BloxstrapViewModel::AddFilesToZipStream";

            foreach (string file in files)
            {
                if (!File.Exists(file))
                    continue;

                try
                {
                    using FileStream fileStream = File.OpenRead(file);

                    var entry = new ZipEntry(directory + Path.GetFileName(file));
                    entry.DateTime = DateTime.Now;

                    zipStream.PutNextEntry(entry);

                    fileStream.CopyTo(zipStream);
                }
                catch (IOException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to open '{file}'");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }
    }

    public sealed class NavigationTabOption : NotifyPropertyChangedViewModel
    {
        private readonly BloxstrapViewModel _owner;
        private bool _isVisible;
        public NavigationTabOption(BloxstrapViewModel owner, string tag, string label, bool isVisible) { _owner = owner; Tag = tag; Label = label; _isVisible = isVisible; }
        public string Tag { get; }
        public string Label { get; }
        public bool IsVisible { get => _isVisible; set { if (_isVisible == value) return; _isVisible = value; _owner.SetTabVisibility(Tag, value); OnPropertyChanged(nameof(IsVisible)); } }
        public ICommand MoveUpCommand => new RelayCommand(() => _owner.MoveTabUp(this));
        public ICommand MoveDownCommand => new RelayCommand(() => _owner.MoveTabDown(this));
    }
}

