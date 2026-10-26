using System.Text.Json;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Shops;

internal static class CurrencyData
{
    public static SpecialCurrencyResolver Load(IDataManager data)
    {
        using var stream = typeof(CurrencyData).Assembly.GetManifestResourceStream("DuckbillTweak.SpecialCurrencies")
            ?? throw new InvalidDataException("Currency mapping resource missing.");
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported currency mapping schema.");
        var items = data.GetExcelSheet<Item>();
        var expectedVersion = document.RootElement.GetProperty("gameVersion").GetString();
        var versionPath = Path.Combine(data.GameData.DataPath.Parent?.FullName ?? "", "ffxivgame.ver");
        string? actualVersion = null;
        try { if (File.Exists(versionPath)) actualVersion = File.ReadAllText(versionPath).Trim(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        var tokens = document.RootElement.GetProperty("items").EnumerateObject()
            .Select(p => (Id: uint.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), Item: p.Value.GetUInt32()))
            .Where(p => actualVersion == expectedVersion && items.TryGetRow(p.Item, out var row) && !row.Name.IsEmpty)
            .ToDictionary(p => p.Id, p => p.Item);
        var tomestones = data.GetExcelSheet<TomestonesItem>().Where(t => t.Tomestones.RowId != 0 && t.Item.RowId != 0)
            .GroupBy(t => t.Tomestones.RowId).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Item.RowId);
        return new(tokens, tomestones);
    }
}
