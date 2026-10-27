using System.Text.Json;

namespace DuckbillTweak.Plugin;

internal sealed record ReleaseNote(string Version, string Date, string[] Korean, string[] English);

internal static class ReleaseHistory
{
    public static string CurrentVersion { get; } = typeof(ReleaseHistory).Assembly.GetName().Version!.ToString(4);
    public static IReadOnlyList<ReleaseNote> Entries { get; } = Load();

    private static ReleaseNote[] Load()
    {
        using var stream = typeof(ReleaseHistory).Assembly.GetManifestResourceStream("DuckbillTweak.ReleaseHistory")
            ?? throw new InvalidDataException("Missing embedded release history.");
        var entries = JsonSerializer.Deserialize<ReleaseNote[]>(stream)
            ?? throw new InvalidDataException("Empty release history.");
        var current = Version.Parse(CurrentVersion);
        if (entries.Length == 0 || entries.Any(entry => !Version.TryParse(entry.Version, out _) ||
                !DateOnly.TryParseExact(entry.Date, "yyyy-MM-dd", out _) || entry.Korean.Length == 0 ||
                entry.Korean.Length != entry.English.Length || entry.Korean.Concat(entry.English).Any(string.IsNullOrWhiteSpace)) ||
            entries.Select(entry => entry.Version).Distinct().Count() != entries.Length)
            throw new InvalidDataException("Invalid release history.");
        return entries.Where(entry => Version.Parse(entry.Version) <= current)
            .OrderByDescending(entry => Version.Parse(entry.Version)).Take(10).ToArray();
    }
}
