using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Text.Json;
using DuckbillTweak.Core.Shops;

if (args.Length < 2)
{
    Console.Error.WriteLine("GameDataProbe <sqpack path> logs|shops|audit [currency json path]");
    return 1;
}
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.Korean });
if (args[1] == "logs")
{
    foreach (var row in game.GetExcelSheet<LogMessage>()!)
    {
        var text = row.Text.ExtractText();
        if (text.Contains("주사위") || text.Contains("입찰") || row.RowId is >= 1220 and <= 1238 or >= 5170 and <= 5200)
            Console.WriteLine($"{row.RowId}: {row.Text.ToMacroString()}");
    }
}
else if (args[1] == "shops")
{
    foreach (var group in game.GetExcelSheet<SpecialShop>()!.GroupBy(x => x.UseCurrencyType))
    {
        Console.WriteLine($"CurrencyType={group.Key}; Shops={group.Count()}");
        foreach (var shop in group.Take(3))
        {
            Console.WriteLine($" {shop.RowId}: {shop.Name}");
            foreach (var entry in shop.Item.Where(x => x.ReceiveItems.Any(i => i.Item.RowId != 0)).Take(2))
                Console.WriteLine($"  Receive={string.Join(',', entry.ReceiveItems.Select(x => $"{x.Item.RowId}x{x.ReceiveCount}"))}; Cost={string.Join(',', entry.ItemCosts.Select(x => $"{x.ItemCost.RowId}x{x.CurrencyCost}/type{x.CostType}/collect{x.CollectabilityCost}"))}");
        }
    }
}
else if (args[1] == "audit")
{
    using var mapping = JsonDocument.Parse(File.ReadAllText(args.Length > 2 ? args[2] : "data/special-currencies.kr.json"));
    var tokens = mapping.RootElement.GetProperty("items").EnumerateObject().ToDictionary(p => uint.Parse(p.Name), p => p.Value.GetUInt32());
    var items = game.GetExcelSheet<Item>()!;
    foreach (var (token, itemId) in tokens)
        Console.WriteLine($"Token {token} -> Item {itemId}: {items.GetRow(itemId).Name.ExtractText()}");
    var tomes = game.GetExcelSheet<TomestonesItem>()!.Where(t => t.Tomestones.RowId != 0 && t.Item.RowId != 0)
        .GroupBy(t => t.Tomestones.RowId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Item.RowId);
    var resolver = new SpecialCurrencyResolver(tokens, tomes);
    foreach (var (index, itemId) in tomes)
        Console.WriteLine($"Tomestone {index} -> Item {itemId}: {items.GetRow(itemId).Name.ExtractText()}");
    foreach (var kind in new byte[] { 0, 1, 2, 3 })
    {
        var examples = game.GetExcelSheet<SpecialShop>()!.SelectMany(s => s.Item.SelectMany(o => o.ItemCosts
            .Where(c => c.ItemCost.RowId != 0 && c.CostType == kind && c.ItemCost.RowId <= 28)
            .Select(c => $"{s.RowId}/{s.Name.ExtractText()}: Type={kind}, Raw={c.ItemCost.RowId}, Amount={c.CurrencyCost}")))
            .Distinct().Take(3);
        foreach (var example in examples) Console.WriteLine(example);
    }
    var unknown = new Dictionary<string, int>();
    var resolved = 0;
    foreach (var shop in game.GetExcelSheet<SpecialShop>()!)
    foreach (var offer in shop.Item.Where(e => e.ReceiveItems.Any(r => r.Item.RowId != 0)))
    foreach (var cost in offer.ItemCosts.Where(c => c.ItemCost.RowId != 0))
    {
        var id = resolver.Resolve(cost.CostType, cost.ItemCost.RowId);
        if (id is { } value && items.TryGetRow(value, out var row) && !row.Name.IsEmpty) resolved++;
        else
        {
            var key = $"Encoding={shop.UseCurrencyType};CostType={cost.CostType};Id={cost.ItemCost.RowId}";
            unknown[key] = unknown.GetValueOrDefault(key) + 1;
        }
    }
    Console.WriteLine($"Resolved prices={resolved}; Unknown prices={unknown.Values.Sum()}");
    foreach (var group in unknown.OrderByDescending(p => p.Value).Take(20)) Console.WriteLine($" {group.Key}; Count={group.Value}");
}
else return 1;
return 0;
