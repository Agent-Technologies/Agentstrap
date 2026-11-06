using Agentstrap.AppData;
using Agentstrap.Integrations;
using Agentstrap.Models;
using Agentstrap.UI.Elements.Overlay;

namespace Agentstrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;

        private readonly ServerConnectionNotifier? _serverConnectionNotifier;

        private readonly ServerConnectionNotificationWatcher? _serverConnectionNotificationWatcher;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);
            }

            if (ActivityWatcher is not null && App.Settings.Prop.ShowServerConnectionOverlay)
                _serverConnectionNotifier = new(ActivityWatcher, _watcherData.ProcessId);

            if (ActivityWatcher is not null && App.Settings.Prop.ServerConnectionNotificationsEnabled)
                _serverConnectionNotificationWatcher = new(ActivityWatcher);

            bool showNotifyIcon = ActivityWatcher is not null
                || _watcherData.AutoclosePids?.Any() == true
                || App.LaunchSettings.TestModeFlag.Active;

            if (showNotifyIcon)
                _notifyIcon = new(this);
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            DateTime sessionStart = DateTime.UtcNow;
            try
            {
                using var proc = Process.GetProcessById(_watcherData.ProcessId);
                sessionStart = proc.StartTime.ToUniversalTime();
            }
            catch { }

            string? analyticsSessionId = RobloxAnalyticsService.StartSession(_watcherData.ProcessId, null, null, null, sessionStart);

            ActivityWatcher?.Start();

            if (App.Settings.Prop.CustomWindowAttributesEnabled)
                _ = ApplyWindowAttributesAsync(_watcherData.ProcessId);

            while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
                await Task.Delay(1000);

            // finalize analytics session
            try
            {
                string? placeId = null;
                string? universeId = null;
                if (ActivityWatcher is not null)
                {
                    var data = ActivityWatcher.Data;
                    if (data.PlaceId == 0 && ActivityWatcher.History.Count > 0)
                        data = ActivityWatcher.History[0];
                    if (data.PlaceId != 0) placeId = data.PlaceId.ToString();
                    if (data.UniverseId != 0) universeId = data.UniverseId.ToString();
                }
                string status = "Completed";
                RobloxAnalyticsService.EndSession(analyticsSessionId, placeId, universeId, null, status, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("Watcher::Analytics", ex);
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();
            _serverConnectionNotifier?.Dispose();
            _serverConnectionNotificationWatcher?.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Waits for the Roblox player window to appear, then applies any custom
        /// size, position and title the user configured.
        /// </summary>
        private static async Task ApplyWindowAttributesAsync(int processId)
        {
            const string LOG_IDENT = "Watcher::ApplyWindowAttributes";

            try
            {
                var settings = App.Settings.Prop;

                Process? process = null;
                IntPtr windowHandle = IntPtr.Zero;
                DateTime deadline = DateTime.UtcNow.AddSeconds(60);

                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        process ??= Process.GetProcessById(processId);
                    }
                    catch (ArgumentException)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Roblox process exited before its window appeared");
                        return;
                    }

                    process.Refresh();
                    windowHandle = process.MainWindowHandle;

                    if (windowHandle != IntPtr.Zero)
                        break;

                    await Task.Delay(500);
                }

                if (windowHandle == IntPtr.Zero || process is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not find the Roblox window in time");
                    return;
                }

                // give Roblox a moment to finish creating and sizing its window
                await Task.Delay(1500);

                process.Refresh();

                if (process.MainWindowHandle != IntPtr.Zero)
                    windowHandle = process.MainWindowHandle;

                bool applied = WindowAttributes.Apply(
                    windowHandle,
                    settings.WindowWidth,
                    settings.WindowHeight,
                    settings.WindowPositionX,
                    settings.WindowPositionY,
                    settings.WindowTitle
                );

                App.Logger.WriteLine(LOG_IDENT, applied
                    ? "Applied custom window attributes"
                    : "No custom window attributes were set");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}

