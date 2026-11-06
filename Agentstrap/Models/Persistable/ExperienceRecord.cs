namespace Agentstrap.Models.Persistable;

/// <summary>A locally observed or user-added Roblox experience.</summary>
public sealed class ExperienceRecord
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public DateTime? LastLaunchedUtc { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExperienceLibrary
{
    public int SchemaVersion { get; set; } = 1;
    public List<ExperienceRecord> Records { get; set; } = new();
    public List<string> RouletteHistory { get; set; } = new();
}
