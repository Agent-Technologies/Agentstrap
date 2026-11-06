namespace Agentstrap.Models.Persistable
{
    public class RobloxSessionRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EndUtc { get; set; }

        public double DurationSeconds { get; set; }

        public string? ExperienceName { get; set; }

        public string? PlaceId { get; set; }

        public string? UniverseId { get; set; }

        public int? ProcessId { get; set; }

        public string Status { get; set; } = "Completed";

        public bool IsComplete { get; set; } = true;

        public string? ExperienceId => PlaceId;
    }

    public class RobloxSession : RobloxSessionRecord { }

    public class RobloxAnalyticsSession : RobloxSessionRecord { }

    public class AnalyticsSession : RobloxSessionRecord { }
}
