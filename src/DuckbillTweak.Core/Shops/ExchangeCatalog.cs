namespace DuckbillTweak.Core.Shops;

public sealed record ExchangeReward(uint RawItemId, uint Quantity);
public sealed record ExchangeCost(byte Type, uint Id, uint Quantity, ushort Collectability);
public sealed record ExchangeTemplate(uint ShopId, string ShopName, IReadOnlyList<ExchangeReward> Rewards, IReadOnlyList<ExchangeCost> Costs);
public sealed record ExchangeObservation(uint RawItemId, uint Quantity, IReadOnlyList<Price> Prices);
public sealed record ResolvedExchange(uint Quantity, IReadOnlyList<Price> Prices);

/// <summary>Matches a whole displayed page, never an item name or a single reward alone.</summary>
public sealed class ExchangeCatalog(IEnumerable<ExchangeTemplate> templates)
{
    private readonly Dictionary<(int Count, uint First), ExchangeTemplate[][]> pages = templates.GroupBy(t => t.ShopId)
        .Select(g => g.ToArray()).GroupBy(rows => (rows.Length, rows[0].Rewards.FirstOrDefault()?.RawItemId ?? 0))
        .ToDictionary(g => g.Key, g => g.ToArray());

    public IReadOnlyList<ResolvedExchange>? Match(string shopName, IReadOnlyList<ExchangeObservation> page, Func<ExchangeCost, Price> price)
    {
        if (page.Count == 0) return null;
        var candidates = (pages.GetValueOrDefault((page.Count, page[0].RawItemId)) ?? []).Where(rows => rows.Select((row, index) =>
            row.Rewards is [var reward] && reward.RawItemId == page[index].RawItemId &&
            (page[index].Quantity == 0 || page[index].Quantity == reward.Quantity)).All(match => match)).ToArray();
        // A native shop name disambiguates identical pages sold for different currencies.
        var named = candidates.Where(rows => rows[0].ShopName == shopName).ToArray();
        if (named.Length > 0) candidates = named;
        ResolvedExchange[]? result = null;
        foreach (var candidate in candidates)
        {
            var decoded = candidate.Select(row => new ResolvedExchange(row.Rewards[0].Quantity, row.Costs.Select(price).ToArray())).ToArray();
            if (decoded.Where((row, index) => !ContainsKnownPrices(row.Prices, page[index].Prices)).Any()) continue;
            if (result is null) { result = decoded; continue; }
            // Multiple catalog pages are acceptable only if they describe identical exchanges.
            if (result.Where((row, index) => row.Quantity != decoded[index].Quantity ||
                !SamePrices(row.Prices, decoded[index].Prices)).Any()) return null;
        }
        return result;
    }

    private static bool ContainsKnownPrices(IReadOnlyList<Price> catalog, IReadOnlyList<Price> observed)
    {
        var remaining = catalog.ToList();
        foreach (var native in observed.Where(price => price.Resolved))
        {
            var index = remaining.FindIndex(price => price.Resolved && price.Key == native.Key && price.Quantity == native.Quantity);
            if (index < 0) return false;
            remaining.RemoveAt(index);
        }
        return true;
    }

    public static bool SamePrices(IEnumerable<Price> first, IEnumerable<Price> second) =>
        first.Select(p => (p.Key, p.Quantity, p.Resolved)).OrderBy(p => p.Key.ItemId).ThenBy(p => p.Key.Quality).ThenBy(p => p.Key.Collectability).ThenBy(p => p.Quantity).ThenBy(p => p.Resolved)
            .SequenceEqual(second.Select(p => (p.Key, p.Quantity, p.Resolved)).OrderBy(p => p.Key.ItemId).ThenBy(p => p.Key.Quality).ThenBy(p => p.Key.Collectability).ThenBy(p => p.Quantity).ThenBy(p => p.Resolved));
}
