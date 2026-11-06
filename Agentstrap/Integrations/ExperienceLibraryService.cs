using System.Text.Json;
using System.Text.RegularExpressions;
using Agentstrap.Models.Persistable;

namespace Agentstrap.Integrations;

/// <summary>Validated local history and favorites. This stores only user-facing experience metadata.</summary>
public static partial class ExperienceLibraryService
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string FilePath = Path.Combine(Paths.Base, "ExperienceLibrary.json");

    public static IReadOnlyList<ExperienceRecord> GetRecords()
    {
        lock (Sync) return Load().Records.OrderByDescending(x => x.LastLaunchedUtc ?? x.AddedUtc).ToArray();
    }

    public static bool TryParsePlaceId(string? input, out string placeId)
    {
        placeId = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var value = input.Trim();
        if (Regex.IsMatch(value, @"^\d{1,20}$", RegexOptions.CultureInvariant))
        {
            placeId = value;
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !(uri.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase)))
            return false;

        var match = ExperienceIdRegex().Match(uri.AbsolutePath);
        if (!match.Success) return false;
        placeId = match.Groups[1].Value;
        return true;
    }

    public static ExperienceRecord AddOrUpdate(string placeId, string? name = null, bool launch = false)
    {
        if (!TryParsePlaceId(placeId, out var validId)) throw new ArgumentException("Enter a valid Roblox experience URL or numeric place ID.", nameof(placeId));
        lock (Sync)
        {
            var library = Load();
            var record = library.Records.FirstOrDefault(x => x.PlaceId == validId);
            if (record is null)
            {
                record = new ExperienceRecord { PlaceId = validId, Name = Clean(name) ?? $"Experience {validId}" };
                library.Records.Add(record);
            }
            else if (!string.IsNullOrWhiteSpace(name)) record.Name = Clean(name) ?? record.Name;
            if (launch) { record.LaunchCount++; record.LastLaunchedUtc = DateTime.UtcNow; }
            Save(library);
            return record;
        }
    }

    public static void SetFavorite(string placeId, bool value)
    {
        lock (Sync) { var lib = Load(); var record = lib.Records.FirstOrDefault(x => x.PlaceId == placeId); if (record is null) return; record.IsFavorite = value; Save(lib); }
    }

    public static void SetNotes(string placeId, string notes)
    {
        lock (Sync) { var lib = Load(); var record = lib.Records.FirstOrDefault(x => x.PlaceId == placeId); if (record is null) return; record.Notes = (notes ?? string.Empty)[..Math.Min((notes ?? string.Empty).Length, 2000)]; Save(lib); }
    }

    public static void Remove(string placeId)
    {
        lock (Sync) { var lib = Load(); lib.Records.RemoveAll(x => x.PlaceId == placeId); Save(lib); }
    }

    public static void Clear()
    {
        lock (Sync) { var lib = Load(); lib.Records.Clear(); lib.RouletteHistory.Clear(); Save(lib); }
    }

    public static void RecordRouletteResult(string placeId)
    {
        lock (Sync) { var lib = Load(); lib.RouletteHistory.RemoveAll(x => x == placeId); lib.RouletteHistory.Insert(0, placeId); if (lib.RouletteHistory.Count > 50) lib.RouletteHistory.RemoveRange(50, lib.RouletteHistory.Count - 50); Save(lib); }
    }

    public static IReadOnlyList<string> GetRouletteHistory()
    {
        lock (Sync) return Load().RouletteHistory.ToArray();
    }

    public static void Export(string destination)
    {
        lock (Sync) File.WriteAllText(destination, JsonSerializer.Serialize(Load(), JsonOptions));
    }

    private static ExperienceLibrary Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var library = JsonSerializer.Deserialize<ExperienceLibrary>(File.ReadAllText(FilePath)) ?? new();
            library.Records ??= new(); library.RouletteHistory ??= new();
            library.Records = library.Records.Where(x => TryParsePlaceId(x.PlaceId, out _)).ToList();
            foreach (var record in library.Records) { record.Name = Clean(record.Name) ?? $"Experience {record.PlaceId}"; record.Notes ??= string.Empty; }
            return library;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            App.Logger.WriteLine("ExperienceLibrary", $"Could not read experience library: {ex.Message}");
            return new();
        }
    }

    private static void Save(ExperienceLibrary library)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(library, JsonOptions));
            if (File.Exists(FilePath)) File.Move(temporary, FilePath, true); else File.Move(temporary, FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Logger.WriteLine("ExperienceLibrary", $"Could not save experience library: {ex.Message}");
            throw;
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 160)];

    [GeneratedRegex(@"/games/(\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExperienceIdRegex();
}
