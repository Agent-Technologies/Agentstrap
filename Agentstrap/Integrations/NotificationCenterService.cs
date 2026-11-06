using System.Text.Json;

namespace Agentstrap.Integrations
{
    public static class NotificationCenterService
    {
        private static readonly object Sync = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string FilePath
        {
            get
            {
                string baseDir = string.IsNullOrWhiteSpace(Paths.Base)
                    ? Path.Combine(Paths.LocalAppData, App.ProjectName)
                    : Paths.Base;
                return Path.Combine(baseDir, "Notifications.json");
            }
        }

        private static List<NotificationRecord> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                var list = JsonSerializer.Deserialize<List<NotificationRecord>>(File.ReadAllText(FilePath)) ?? new();
                foreach (var n in list)
                {
                    if (string.IsNullOrWhiteSpace(n.Id)) n.Id = Guid.NewGuid().ToString("N");
                    if (n.CreatedUtc == default) n.CreatedUtc = DateTime.UtcNow;
                    n.Source ??= "Agentstrap";
                    n.Title ??= string.Empty;
                    n.Message ??= string.Empty;
                }
                return list;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                App.Logger.WriteLine("NotificationCenter", $"Could not read notifications: {ex.Message}");
                return new();
            }
        }

        private static void Save(List<NotificationRecord> list)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOptions));
                if (File.Exists(FilePath)) File.Move(tmp, FilePath, true); else File.Move(tmp, FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine("NotificationCenter", $"Could not save notifications: {ex.Message}");
                throw;
            }
        }

        public static IReadOnlyList<NotificationRecord> GetAll()
        {
            lock (Sync) return Load().OrderByDescending(x => x.CreatedUtc).ToArray();
        }

        public static int UnreadCount()
        {
            lock (Sync) return Load().Count(x => !x.IsRead);
        }

        public static void Add(NotificationRecord notification)
        {
            if (notification is null) return;
            lock (Sync)
            {
                var list = Load();
                if (list.Any(x => x.Id == notification.Id)) return;
                if (string.IsNullOrWhiteSpace(notification.Id)) notification.Id = Guid.NewGuid().ToString("N");
                list.Add(notification);
                Prune(list);
                Save(list);
            }
        }

        public static void MarkRead(string id)
        {
            lock (Sync)
            {
                var list = Load();
                var n = list.FirstOrDefault(x => x.Id == id);
                if (n is null) return;
                n.IsRead = true;
                Save(list);
            }
        }

        public static void MarkUnread(string id)
        {
            lock (Sync)
            {
                var list = Load();
                var n = list.FirstOrDefault(x => x.Id == id);
                if (n is null) return;
                n.IsRead = false;
                Save(list);
            }
        }

        public static void MarkAllRead()
        {
            lock (Sync)
            {
                var list = Load();
                foreach (var n in list) n.IsRead = true;
                Save(list);
            }
        }

        public static void Delete(string id)
        {
            lock (Sync)
            {
                var list = Load();
                list.RemoveAll(x => x.Id == id);
                Save(list);
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Save(new List<NotificationRecord>());
            }
        }

        public static IEnumerable<NotificationRecord> Search(string? query)
        {
            var all = GetAll();
            if (string.IsNullOrWhiteSpace(query)) return all;
            return all.Where(x =>
                (x.Title ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (x.Message ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (x.Source ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<NotificationRecord> FilterBySource(string? source)
        {
            var all = GetAll();
            if (string.IsNullOrWhiteSpace(source) || source.Equals("All", StringComparison.OrdinalIgnoreCase)) return all;
            if (source.Equals("Unread", StringComparison.OrdinalIgnoreCase)) return all.Where(x => !x.IsRead);
            return all.Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase));
        }

        private static void Prune(List<NotificationRecord> list)
        {
            try
            {
                var settings = App.Settings?.Prop;
                if (settings is not null && settings.AutoRemoveOldNotifications && settings.NotificationRetentionDays > 0)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-settings.NotificationRetentionDays);
                    list.RemoveAll(x => x.CreatedUtc < cutoff);
                }
                // cap size
                if (list.Count > 1000)
                {
                    list.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
                    list.RemoveRange(1000, list.Count - 1000);
                }
            }
            catch { }
        }
    }
}