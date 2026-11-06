using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Agentstrap.UI.ViewModels.Settings
{
    public sealed class ExtensionCardViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Action<ExtensionCardViewModel, bool> _toggle;
        private bool _enabled;
        public ExtensionManifest Manifest { get; }
        public bool IsBuiltIn { get; }
        public bool IsInstalled => !IsBuiltIn;
        public bool HasSourceUrl => Uri.TryCreate(Manifest.SourceUrl, UriKind.Absolute, out _);
        public string Icon => Manifest.Category switch { "Audio" => "♫", "Graphics" => "◈", "Performance" => "⌁", "Configuration" => "⚙", _ => "◉" };
        public string KindLabel => Manifest.Type == "AssetPack" ? "LOCAL ASSET PACK" : "CLIENT CONFIGURATION";
        public string DetailsLabel => Manifest.Type == "ClientConfiguration" ? "Configure" : "Details";
        public string Compatibility => Manifest.AssetMappings.Count > 0 ? "Client asset path checked before activation · Restart required" : "Roblox allowlist snapshot: 2025-09-29 · Restart required";
        public string Status => _enabled ? Error is null ? "Enabled · applies at next launch" : "Enabled · compatibility issue" : "Disabled";
        public bool Enabled { get => _enabled; set { if (_enabled == value) return; _toggle(this, value); } }
        public string? Error { get; private set; }
        public ICommand DetailsCommand { get; }
        public ICommand RemoveCommand { get; }
        public ExtensionCardViewModel(ExtensionManifest manifest, bool enabled, bool builtIn, Action<ExtensionCardViewModel, bool> toggle, Action<ExtensionCardViewModel> details, Action<ExtensionCardViewModel> remove)
        {
            Manifest = manifest; _enabled = enabled; IsBuiltIn = builtIn; _toggle = toggle;
            DetailsCommand = new RelayCommand(() => details(this)); RemoveCommand = new RelayCommand(() => remove(this));
        }
        public void SetEnabled(bool value) { _enabled = value; Error = null; OnPropertyChanged(nameof(Enabled)); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(Error)); }
        public void SetError(string? error) { Error = error; OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(Status)); }
    }

    public sealed class ExtensionsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ExtensionManager _manager;
        private string _search = "";
        private string _category = "All categories";
        private string _status = "All";
        public ObservableCollection<ExtensionCardViewModel> Extensions { get; } = new();
        public IEnumerable<ExtensionCardViewModel> VisibleExtensions => Extensions.Where(Matches);
        public string Search { get => _search; set { _search = value; Refresh(); } }
        public string Category { get => _category; set { _category = value; Refresh(); } }
        public string StatusFilter { get => _status; set { _status = value; Refresh(); } }
        public string StatusSummary => $"{Extensions.Count(x => x.Enabled)} enabled · changes apply on next Roblox launch";
        public IEnumerable<ExtensionCardViewModel> VisibleBuiltIns => VisibleExtensions.Where(x => x.IsBuiltIn);
        public IEnumerable<ExtensionCardViewModel> VisibleInstalled => VisibleExtensions.Where(x => x.IsInstalled);
        public ICommand ImportCommand { get; }
        public ICommand RestoreCommand { get; }

        public ExtensionsViewModel()
        {
            string directory = Path.Combine(Paths.LocalAppData, App.ProjectName, "Extensions");
            _manager = new ExtensionManager(directory);
            SeedBuiltIns();
            Reload();
            ImportCommand = new AsyncRelayCommand(ImportAsync);
            RestoreCommand = new RelayCommand(RestoreAll);
        }

        private void SeedBuiltIns()
        {
            AddCursor("builtin.cursor-classic-2006", "Classic Cursor", "A classic Roblox cursor set from the existing Agentstrap cursor resources.", "2006", "Historical classic cursor artwork", "MIT (repository license)");
            AddCursor("builtin.cursor-classic-2013", "Minimal Cursor", "A compact cursor set from the existing Agentstrap cursor resources.", "2013", "Minimal legacy cursor artwork", "MIT (repository license)");
            AddConfiguration("builtin.graphics-performance", "Performance Textures", "Sets the published texture override to the lowest listed level and selects 1x MSAA. Actual impact varies by client and hardware.", "Performance", new() { ["DFFlagTextureQualityOverrideEnabled"] = "True", ["DFIntTextureQualityOverride"] = "0", ["FIntDebugForceMSAASamples"] = "1" });
            AddConfiguration("builtin.graphics-balanced", "Balanced Graphics", "Sets the published texture override to level 1 and selects 2x MSAA. Actual impact varies by client and hardware.", "Graphics", new() { ["DFFlagTextureQualityOverrideEnabled"] = "True", ["DFIntTextureQualityOverride"] = "1", ["FIntDebugForceMSAASamples"] = "2" });
            AddConfiguration("builtin.graphics-quality", "Quality Graphics", "Sets the published texture override to level 3 and selects 4x MSAA. Higher quality may increase GPU load.", "Graphics", new() { ["DFFlagTextureQualityOverrideEnabled"] = "True", ["DFIntTextureQualityOverride"] = "3", ["FIntDebugForceMSAASamples"] = "4" });
            AddConfiguration("builtin.disable-dpi-scaling", "Disable Client DPI Scaling", "Requests disabled Roblox client DPI scaling. This can make interface elements smaller on high-DPI displays; leave it disabled if the result is uncomfortable.", "Configuration", new() { ["DFFlagDisableDPIScale"] = "True" });
            AddConfiguration("builtin.backend-d3d11", "Direct3D 11 Backend", "Requests Direct3D 11 where the client accepts the preference. Driver support and stability vary; Roblox may ignore it.", "Graphics", new() { ["FFlagDebugGraphicsPreferD3D11"] = "True" }, "rendering-backend");
            AddConfiguration("builtin.backend-vulkan", "Vulkan Backend", "Requests Vulkan where the client and driver accept the preference. It may fail or be ignored on some systems.", "Graphics", new() { ["FFlagDebugGraphicsPreferVulkan"] = "True" }, "rendering-backend");
            AddConfiguration("builtin.backend-opengl", "OpenGL Backend", "Requests OpenGL where the client and driver accept the preference. It may fail or be ignored on some systems.", "Graphics", new() { ["FFlagDebugGraphicsPreferOpenGL"] = "True" }, "rendering-backend");
        }

        private void AddConfiguration(string id, string name, string description, string category, Dictionary<string, string> values, string? conflictGroup = null)
        {
            if (_manager.Get(id) is not null) return;
            var manifest = new ExtensionManifest
            {
                Id = id, Name = name, Version = "1.0.0", Description = description, Author = "Agentstrap", License = "MIT",
                Category = category, Type = "ClientConfiguration", MinimumAgentstrapVersion = "2.3.2", SupportedOperatingSystems = new() { "Windows" },
                ConflictGroup = conflictGroup, Tags = new() { "client settings", "restart", "allowlist" }, Configuration = values,
                SourceUrl = "https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569",
                Attribution = "Flag names and setting definitions come from Agentstrap's existing FastFlag presets and Roblox's published allowlist."
            };
            _manager.InstallBuiltIn(manifest, new Dictionary<string, byte[]>());
        }

        private void AddCursor(string id, string name, string description, string variant, string author, string license)
        {
            if (_manager.Get(id) is not null) return;
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arrow.png"] = Resource.Get($"Resources.Mods.Cursor.From{variant}.ArrowCursor.png").GetAwaiter().GetResult(),
                ["arrow-far.png"] = Resource.Get($"Resources.Mods.Cursor.From{variant}.ArrowFarCursor.png").GetAwaiter().GetResult()
            };
            var manifest = new ExtensionManifest
            {
                Id = id, Name = name, Version = "1.0.0", Description = description, Author = author, License = license,
                Category = "Visuals", Type = "AssetPack", MinimumAgentstrapVersion = "2.3.2", SupportedOperatingSystems = new() { "Windows" },
                ConflictGroup = "roblox-cursor", Tags = new() { "cursor", "mouse", variant }, Attribution = "Existing cursor asset bundled with the Agentstrap repository.",
                SourceUrl = "https://github.com/bloxstraplabs/bloxstrap/wiki/Adding-custom-mods",
                AssetMappings = new() {
                    ["arrow.png"] = @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",
                    ["arrow-far.png"] = @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png"
                }
            };
            _manager.InstallBuiltIn(manifest, files);
        }

        private void Reload()
        {
            Extensions.Clear();
            var installed = _manager.ListInstalled();
            foreach (var ext in installed.OrderByDescending(x => x.Manifest.Id.StartsWith("builtin.")))
            {
                bool builtIn = ext.Manifest.Id.StartsWith("builtin.", StringComparison.Ordinal);
                var card = new ExtensionCardViewModel(ext.Manifest, ext.Enabled, builtIn, Toggle, ShowDetails, Remove);
                if (ext.Error is not null) card.SetError(ext.Error);
                Extensions.Add(card);
            }
            Refresh();
        }

        private void Toggle(ExtensionCardViewModel card, bool enabled)
        {
            try { if (enabled) _manager.Enable(card.Manifest.Id); else _manager.Disable(card.Manifest.Id); card.SetEnabled(enabled); }
            catch (Exception ex) { card.SetEnabled(_manager.Get(card.Manifest.Id)?.Enabled == true); card.SetError(ex.Message); }
            OnPropertyChanged(nameof(StatusSummary)); Refresh();
        }

        private async Task ImportAsync()
        {
            var dialog = new OpenFileDialog { Title = "Import a declarative Agentstrap extension", Filter = "Extension package (*.zip)|*.zip|Extension manifest (manifest.json)|manifest.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try
            {
                InstalledExtension ext = await Task.Run(() => string.Equals(Path.GetExtension(dialog.FileName), ".zip", StringComparison.OrdinalIgnoreCase)
                    ? _manager.InstallFromZip(dialog.FileName)
                    : _manager.InstallFromDirectory(Path.GetDirectoryName(dialog.FileName)!));
                Reload();
                Frontend.ShowMessageBox($"{ext.Manifest.Name} was validated and installed disabled. Review its details, then enable it.", MessageBoxImage.Information);
            }
            catch (Exception ex) { Frontend.ShowMessageBox("The extension was rejected or could not be installed. Roblox was not changed.\n\n" + ex.Message, MessageBoxImage.Error); }
        }

        private void Remove(ExtensionCardViewModel card)
        {
            if (card.IsBuiltIn) { ShowDetails(card); return; }
            try { _manager.Remove(card.Manifest.Id); Reload(); }
            catch (Exception ex) { card.SetError(ex.Message); }
        }

        private void RestoreAll()
        {
            if (Frontend.ShowMessageBox("Disable every enabled extension and restore Agentstrap-managed assets and settings at the next Roblox launch?", MessageBoxImage.Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            foreach (var ext in _manager.ListInstalled().Where(x => x.Enabled).ToList())
            {
                try { _manager.Disable(ext.Manifest.Id); }
                catch (Exception ex) { Extensions.FirstOrDefault(x => x.Manifest.Id == ext.Manifest.Id)?.SetError(ex.Message); }
            }
            Reload();
        }

        private static void ShowDetails(ExtensionCardViewModel card)
        {
            var m = card.Manifest;
            string mappings = m.AssetMappings.Count == 0 ? "None" : string.Join("\n", m.AssetMappings.Values);
            string flags = m.Configuration.Count == 0 ? "None" : string.Join("\n", m.Configuration.Select(x => $"{x.Key} = {x.Value}"));
            Frontend.ShowMessageBox($"{m.Name} · {m.Version}\n\n{m.Description}\n\nType: {card.KindLabel}\nAuthor: {m.Author}\nLicense: {m.License}\nAttribution: {m.Attribution ?? "See the extension source and license."}\nSource: {m.SourceUrl ?? "Built into Agentstrap"}\nRestart: Required; applies before next launch.\nCompatibility: Asset target paths are checked against the active Agentstrap client before enabling and again before launch. Configuration entries are limited to the Roblox allowlist published on 2025-09-29; Roblox may change the list or ignore settings on later client versions.\n\nAsset mappings:\n{mappings}\n\nSettings:\n{flags}\n\nConflicts: {m.ConflictGroup ?? "Target-file and setting conflicts are checked before activation."}\nDisable: Switch this extension off. Agentstrap removes only files whose content still matches the managed copy and restores saved configuration values.", MessageBoxImage.Information);
        }

        private bool Matches(ExtensionCardViewModel item)
        {
            if (_category != "All categories" && !item.Manifest.Category.Equals(_category, StringComparison.OrdinalIgnoreCase)) return false;
            if (_status == "Enabled" && !item.Enabled || _status == "Disabled" && item.Enabled || _status == "Available" && item.Enabled) return false;
            string query = _search.Trim();
            return query.Length == 0 || new[] { item.Manifest.Name, item.Manifest.Description, item.Manifest.Category, item.Manifest.Author }.Concat(item.Manifest.Tags).Any(x => x.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        private void Refresh() { OnPropertyChanged(nameof(VisibleExtensions)); OnPropertyChanged(nameof(VisibleBuiltIns)); OnPropertyChanged(nameof(VisibleInstalled)); OnPropertyChanged(nameof(StatusSummary)); }
    }
}
