namespace Agentstrap.Models.Persistable
{
    public class NotificationRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Source { get; set; } = "Agentstrap";

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

        public string? DeliveryStatus { get; set; }

        public bool? BarkDelivered { get; set; }
    }

    public class Notification : NotificationRecord { }

    public class NotificationItem : NotificationRecord { }

    public class AppNotification : NotificationRecord { }

    public class NotificationEntry : NotificationRecord { }
}
