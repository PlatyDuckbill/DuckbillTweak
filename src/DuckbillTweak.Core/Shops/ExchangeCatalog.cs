namespace DuckbillTweak.Core.Shops;

public sealed record ExchangeReward(uint RawItemId, uint Quantity);
public sealed record ExchangeCost(byte Type, uint Id, uint Quantity, ushort Collectability);
public sealed record ExchangeTemplate(uint ShopId, string ShopName, IReadOnlyList<ExchangeReward> Rewards, IReadOnlyList<ExchangeCost> Costs);
public sealed record ExchangeObservation(uint RawItemId, uint Quantity, IReadOnlyList<Price> Prices);
public sealed record ResolvedExchange(uint Quantity, IReadOnlyList<Price> Prices);

/// <summary>Matches a whole displayed page, never an item name or a single reward alone.</summary>
public sealed class ExchangeCatalog(IEnumerable<ExchangeTemplate> templates)
{
    private readonly Dictionary<(int Count, string Items), ExchangeTemplate[][]> pages = templates.GroupBy(t => t.ShopId)
        .Select(g => g.ToArray()).Where(rows => rows.All(row => row.Rewards.Count == 1))
        .GroupBy(rows => (rows.Length, Membership(rows.Select(row => row.Rewards[0].RawItemId))))
        .ToDictionary(g => g.Key, g => g.ToArray());

    public IReadOnlyList<ResolvedExchange>? Match(string shopName, IReadOnlyList<ExchangeObservation> page, Func<ExchangeCost, Price> price)
    {
        if (page.Count == 0) return null;
        var candidates = pages.GetValueOrDefault((page.Count, Membership(page.Select(row => row.RawItemId)))) ?? [];
        // A native shop name disambiguates identical pages sold for different currencies.
        var named = candidates.Where(rows => rows[0].ShopName == shopName).ToArray();
        if (named.Length > 0) candidates = named;
        ResolvedExchange[]? result = null;
        foreach (var candidate in candidates)
        {
            var remaining = candidate.Select(row => (Item: row.Rewards[0].RawItemId,
                Exchange: new ResolvedExchange(row.Rewards[0].Quantity, row.Costs.Select(price).ToArray()))).ToList();
            var decoded = new ResolvedExchange[page.Count];
            var valid = true;
            for (var index = 0; index < page.Count; index++)
            {
                var observed = page[index];
                var matches = remaining.Where(row => row.Item == observed.RawItemId &&
                    (observed.Quantity == 0 || row.Exchange.Quantity == observed.Quantity) && ContainsKnownPrices(row.Exchange.Prices, observed.Prices)).ToArray();
                if (matches.Length == 0 || matches.Any(row => row.Exchange.Quantity != matches[0].Exchange.Quantity ||
                    !SamePrices(row.Exchange.Prices, matches[0].Exchange.Prices))) { valid = false; break; }
                // Remove one occurrence. Duplicate rewards must keep their multiplicity.
                decoded[index] = matches[0].Exchange;
                remaining.Remove(matches[0]);
            }
            if (!valid) continue;
            if (result is null) { result = decoded; continue; }
            // Multiple catalog pages are acceptable only if they describe identical exchanges.
            if (result.Where((row, index) => row.Quantity != decoded[index].Quantity ||
                !SamePrices(row.Prices, decoded[index].Prices)).Any()) return null;
        }
        return result;
    }

    private static string Membership(IEnumerable<uint> items) => string.Join(',', items.Order());

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
