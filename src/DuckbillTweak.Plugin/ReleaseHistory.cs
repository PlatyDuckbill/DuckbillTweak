using System.Text.Json;

namespace DuckbillTweak.Plugin;

internal sealed record ReleaseNote(string Version, string? Date, string[] Korean, string[] English,
    string TitleKorean, string TitleEnglish, string SummaryKorean, string SummaryEnglish,
    string[] OtherKorean, string[] OtherEnglish);

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
        if (entries.Length == 0 || entries.Any(e => !Version.TryParse(e.Version, out _) ||
                (e.Date is not null && !DateOnly.TryParseExact(e.Date, "yyyy-MM-dd", out _)) ||
                string.IsNullOrWhiteSpace(e.TitleKorean) || string.IsNullOrWhiteSpace(e.TitleEnglish) ||
                e.Korean is not { Length: > 0 } || e.English is null || e.Korean.Length != e.English.Length ||
                e.OtherKorean is null || e.OtherEnglish is null || e.OtherKorean.Length != e.OtherEnglish.Length ||
                e.Korean.Concat(e.English).Concat(e.OtherKorean).Concat(e.OtherEnglish).Any(string.IsNullOrWhiteSpace)) ||
            entries.Select(e => e.Version).Distinct().Count() != entries.Length)
            throw new InvalidDataException("Invalid release history.");
        return entries.Where(e => Version.Parse(e.Version) <= Version.Parse(CurrentVersion))
            .OrderByDescending(e => Version.Parse(e.Version)).ToArray();
    }
}
