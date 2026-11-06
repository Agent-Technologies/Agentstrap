using System.Text.Json;

namespace Agentstrap.Integrations
{
    public static class RobloxAnalyticsService
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
                return Path.Combine(baseDir, "RobloxAnalytics.json");
            }
        }

        private class Store
        {
            public List<RobloxSessionRecord> Sessions { get; set; } = new();
        }

        private static Store Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                var store = JsonSerializer.Deserialize<Store>(File.ReadAllText(FilePath)) ?? new();
                store.Sessions ??= new();
                foreach (var s in store.Sessions)
                {
                    if (string.IsNullOrWhiteSpace(s.Id)) s.Id = Guid.NewGuid().ToString("N");
                    if (s.StartUtc == default) s.StartUtc = DateTime.UtcNow;
                    if (s.IsComplete && s.EndUtc.HasValue)
                        s.DurationSeconds = Math.Max(0, (s.EndUtc.Value - s.StartUtc).TotalSeconds);
                }
                return store;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                App.Logger.WriteLine("RobloxAnalytics", $"Could not read analytics: {ex.Message}");
                return new();
            }
        }

        private static void Save(Store store)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(store, JsonOptions));
                if (File.Exists(FilePath)) File.Move(tmp, FilePath, true); else File.Move(tmp, FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine("RobloxAnalytics", $"Could not save analytics: {ex.Message}");
                throw;
            }
        }

        public static IReadOnlyList<RobloxSessionRecord> GetSessions()
        {
            lock (Sync) return Load().Sessions.OrderByDescending(x => x.StartUtc).ToArray();
        }

        public static string StartSession(int? processId = null, string? placeId = null, string? universeId = null, string? experienceName = null, DateTime? startUtc = null)
        {
            lock (Sync)
            {
                var store = Load();
                // avoid duplicate for same process+start
                var existing = store.Sessions.FirstOrDefault(x => !x.IsComplete && processId.HasValue && x.ProcessId == processId);
                if (existing is not null) return existing.Id;

                var session = new RobloxSessionRecord
                {
                    ProcessId = processId,
                    PlaceId = string.IsNullOrWhiteSpace(placeId) ? null : placeId,
                    UniverseId = string.IsNullOrWhiteSpace(universeId) ? null : universeId,
                    ExperienceName = string.IsNullOrWhiteSpace(experienceName) ? null : experienceName,
                    StartUtc = startUtc ?? DateTime.UtcNow,
                    IsComplete = false,
                    Status = "Running",
                };
                store.Sessions.Add(session);
                Save(store);
                return session.Id;
            }
        }

        public static void EndSession(string sessionId, string? placeId = null, string? universeId = null, string? experienceName = null, string status = "Completed", DateTime? endUtc = null)
        {
            lock (Sync)
            {
                var store = Load();
                var session = store.Sessions.FirstOrDefault(x => x.Id == sessionId);
                if (session is null) return;

                if (!string.IsNullOrWhiteSpace(placeId)) session.PlaceId = placeId;
                if (!string.IsNullOrWhiteSpace(universeId)) session.UniverseId = universeId;
                if (!string.IsNullOrWhiteSpace(experienceName)) session.ExperienceName = experienceName;

                var end = endUtc ?? DateTime.UtcNow;
                if (end < session.StartUtc) end = session.StartUtc;
                session.EndUtc = end;
                session.DurationSeconds = Math.Max(0, (end - session.StartUtc).TotalSeconds);
                session.Status = status;
                session.IsComplete = true;

                // resolve experience name
                if (string.IsNullOrWhiteSpace(session.ExperienceName) && !string.IsNullOrWhiteSpace(session.PlaceId))
                {
                    try
                    {
                        var record = ExperienceLibraryService.GetRecords().FirstOrDefault(x => x.PlaceId == session.PlaceId);
                        if (record is not null) session.ExperienceName = record.Name;
                    }
                    catch { }
                }

                Save(store);
            }
        }

        public static void DeleteSession(string sessionId)
        {
            lock (Sync)
            {
                var store = Load();
                store.Sessions.RemoveAll(x => x.Id == sessionId);
                Save(store);
            }
        }

        public static void Clear()
        {
            lock (Sync) Save(new Store());
        }

        public static void ReconcileUnfinishedSessions()
        {
            lock (Sync)
            {
                var store = Load();
                bool changed = false;
                foreach (var s in store.Sessions)
                {
                    if (!s.IsComplete)
                    {
                        s.Status = "Incomplete";
                        s.IsComplete = false;
                        // do not guess duration
                        changed = true;
                    }
                }
                if (changed) Save(store);
            }
        }

        public static void ExportJson(string destination)
        {
            lock (Sync)
            {
                File.WriteAllText(destination, JsonSerializer.Serialize(Load(), JsonOptions));
            }
        }

        public static void ExportCsv(string destination)
        {
            var sessions = GetSessions();
            var sb = new StringBuilder();
            sb.AppendLine("Id,ExperienceName,PlaceId,UniverseId,StartUtc,EndUtc,DurationSeconds,Status,IsComplete");
            foreach (var s in sessions)
            {
                sb.AppendLine($"{Escape(s.Id)},{Escape(s.ExperienceName)},{Escape(s.PlaceId)},{Escape(s.UniverseId)},{s.StartUtc:O},{s.EndUtc:O},{s.DurationSeconds},{Escape(s.Status)},{s.IsComplete}");
            }
            File.WriteAllText(destination, sb.ToString());
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // Aggregations
        public static double TotalPlaytimeSeconds(string period = "All") => SessionsInPeriod(period).Where(x => x.IsComplete).Sum(x => x.DurationSeconds);

        public static int TotalSessions(string period = "All") => SessionsInPeriod(period).Count();

        public static double AverageSessionSeconds(string period = "All")
        {
            var list = SessionsInPeriod(period).Where(x => x.IsComplete).ToList();
            if (list.Count == 0) return 0;
            return list.Average(x => x.DurationSeconds);
        }

        public static IEnumerable<RobloxSessionRecord> SessionsInPeriod(string period)
        {
            var now = DateTime.UtcNow;
            var sessions = GetSessions();
            return period switch
            {
                "Today" => sessions.Where(x => x.StartUtc.Date == now.Date),
                "Last7" => sessions.Where(x => x.StartUtc >= now.AddDays(-7)),
                "Last30" => sessions.Where(x => x.StartUtc >= now.AddDays(-30)),
                "Last90" => sessions.Where(x => x.StartUtc >= now.AddDays(-90)),
                _ => sessions,
            };
        }

        public static Dictionary<DateTime, double> DailyPlaytime(int days = 30)
        {
            var result = new Dictionary<DateTime, double>();
            var now = DateTime.UtcNow.Date;
            for (int i = days - 1; i >= 0; i--) result[now.AddDays(-i)] = 0;
            foreach (var s in GetSessions().Where(x => x.IsComplete))
            {
                var day = s.StartUtc.Date;
                if (result.ContainsKey(day)) result[day] += s.DurationSeconds;
            }
            return result;
        }

        public static Dictionary<string, (double TotalSeconds, int Sessions, double Longest, DateTime? LastPlayed)> ExperienceStats()
        {
            var dict = new Dictionary<string, (double, int, double, DateTime?)>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in GetSessions().Where(x => x.IsComplete))
            {
                var key = string.IsNullOrWhiteSpace(s.ExperienceName) ? (string.IsNullOrWhiteSpace(s.PlaceId) ? "Unknown Experience" : $"Experience {s.PlaceId}") : s.ExperienceName!;
                if (!dict.TryGetValue(key, out var agg))
                    agg = (0, 0, 0, null);
                agg.Item1 += s.DurationSeconds;
                agg.Item2 += 1;
                if (s.DurationSeconds > agg.Item3) agg.Item3 = s.DurationSeconds;
                if (agg.Item4 is null || s.StartUtc > agg.Item4) agg.Item4 = s.StartUtc;
                dict[key] = agg;
            }
            return dict;
        }

        public static string MostPlayedExperience()
        {
            var stats = ExperienceStats();
            if (stats.Count == 0) return "None";
            return stats.OrderByDescending(x => x.Value.TotalSeconds).First().Key;
        }

        public static string RecentlyPlayedExperience()
        {
            var sessions = GetSessions().Where(x => x.IsComplete).OrderByDescending(x => x.StartUtc).FirstOrDefault();
            if (sessions is null) return "None";
            return string.IsNullOrWhiteSpace(sessions.ExperienceName) ? (string.IsNullOrWhiteSpace(sessions.PlaceId) ? "Unknown Experience" : $"Experience {sessions.PlaceId}") : sessions.ExperienceName!;
        }
    }
}