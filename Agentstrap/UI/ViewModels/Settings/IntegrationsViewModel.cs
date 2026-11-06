using System.Collections.ObjectModel;
using System.Windows.Input;

using Microsoft.Win32;

using CommunityToolkit.Mvvm.Input;

namespace Agentstrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        private string _autoJoinPlaceId = App.Settings.Prop.AutoJoinExperiencePlaceId;
        private string _autoJoinStatus = GetAutoJoinExperienceLabel();
        private readonly AsyncRelayCommand _saveAutoJoinCommand;

        public IntegrationsViewModel()
        {
            _saveAutoJoinCommand = new AsyncRelayCommand(SaveAutoJoinAsync);
        }

        public string AutoJoinPlaceId
        {
            get => _autoJoinPlaceId;
            set
            {
                if (_autoJoinPlaceId == value)
                    return;
                _autoJoinPlaceId = value;
                OnPropertyChanged(nameof(AutoJoinPlaceId));
            }
        }

        public string AutoJoinStatus
        {
            get => _autoJoinStatus;
            private set
            {
                if (_autoJoinStatus == value)
                    return;
                _autoJoinStatus = value;
                OnPropertyChanged(nameof(AutoJoinStatus));
            }
        }

        public ICommand SaveAutoJoinCommand => _saveAutoJoinCommand;
        public ICommand DeleteAutoJoinCommand => new RelayCommand(DeleteAutoJoin);

        private async Task SaveAutoJoinAsync()
        {
            if (!Integrations.ExperienceLibraryService.TryParsePlaceId(AutoJoinPlaceId, out string placeId))
            {
                AutoJoinStatus = "Enter a valid Roblox place ID or experience link.";
                return;
            }

            AutoJoinPlaceId = placeId;
            App.Settings.Prop.AutoJoinExperiencePlaceId = placeId;
            App.Settings.Prop.AutoJoinExperienceEnabled = true;
            AutoJoinStatus = "Saved. Looking up the experience name…";

            try
            {
                string? experienceName = await GetExperienceNameAsync(placeId);
                if (!String.IsNullOrWhiteSpace(experienceName))
                    App.Settings.Prop.AutoJoinExperienceName = experienceName;

                var record = Integrations.ExperienceLibraryService.AddOrUpdate(placeId, experienceName);
                if (String.IsNullOrWhiteSpace(experienceName))
                    experienceName = record.Name;

                App.Settings.Prop.AutoJoinExperienceName = experienceName;
                App.Settings.Save();
                AutoJoinStatus = $"Will automatically join {experienceName} (Place ID {placeId}) when Roblox launches.";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("IntegrationsViewModel::SaveAutoJoinAsync", ex);
                string name = Integrations.ExperienceLibraryService.AddOrUpdate(placeId).Name;
                App.Settings.Prop.AutoJoinExperienceName = name;
                App.Settings.Save();
                AutoJoinStatus = $"Will automatically join {name} (Place ID {placeId}) when Roblox launches.";
            }
        }

        private static async Task<string?> GetExperienceNameAsync(string placeId)
        {
            using var universeResponse = await App.HttpClient.GetAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe");
            universeResponse.EnsureSuccessStatusCode();
            using var universeJson = System.Text.Json.JsonDocument.Parse(await universeResponse.Content.ReadAsStringAsync());
            if (!universeJson.RootElement.TryGetProperty("universeId", out var universeId))
                return null;

            using var gameResponse = await App.HttpClient.GetAsync($"https://games.roblox.com/v1/games?universeIds={universeId.GetInt64()}");
            gameResponse.EnsureSuccessStatusCode();
            using var gameJson = System.Text.Json.JsonDocument.Parse(await gameResponse.Content.ReadAsStringAsync());
            if (!gameJson.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
                return null;

            return data[0].TryGetProperty("name", out var name) ? name.GetString() : null;
        }

        private void DeleteAutoJoin()
        {
            App.Settings.Prop.AutoJoinExperienceEnabled = false;
            App.Settings.Prop.AutoJoinExperiencePlaceId = String.Empty;
            App.Settings.Prop.AutoJoinExperienceName = String.Empty;
            App.Settings.Save();
            AutoJoinPlaceId = String.Empty;
            AutoJoinStatus = "Automatic experience joining is off.";
        }

        private static string GetAutoJoinExperienceLabel()
        {
            if (!App.Settings.Prop.AutoJoinExperienceEnabled
                || !Integrations.ExperienceLibraryService.TryParsePlaceId(App.Settings.Prop.AutoJoinExperiencePlaceId, out string placeId))
                return "Automatic experience joining is off.";

            string name = App.Settings.Prop.AutoJoinExperienceName;
            if (String.IsNullOrWhiteSpace(name))
                name = $"Experience {placeId}";
            return $"Will automatically join {name} (Place ID {placeId}) when Roblox launches.";
        }

        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);

        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);

        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;
            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = value;
                    ServerConnectionOverlayEnabled = value;
                    DisableAppPatchEnabled = value;
                    DiscordActivityEnabled = value;
                    DiscordActivityJoinEnabled = value;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(ServerConnectionOverlayEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                }
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool ServerConnectionOverlayEnabled
        {
            get => App.Settings.Prop.ShowServerConnectionOverlay;
            set
            {
                App.Settings.Prop.ShowServerConnectionOverlay = value;

                OnPropertyChanged(nameof(ServerConnectionOverlayEnabled));
            }
        }

        public double ServerConnectionOverlayDuration
        {
            get => App.Settings.Prop.ServerConnectionOverlayDuration;
            set
            {
                App.Settings.Prop.ServerConnectionOverlayDuration = (int)Math.Clamp(value, 1, 30);

                OnPropertyChanged(nameof(ServerConnectionOverlayDuration));
            }
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    DiscordAccountOnProfile = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                }
            }
        }

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }
        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public ServerConnectionNotificationsViewModel ServerConnectionNotifications { get; } = new ServerConnectionNotificationsViewModel();

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;
    }
}

