namespace DuckbillTweak.Core.Shops;

public enum Ownership { Unknown, Missing, Owned }
public enum TradeFilter { All, Tradable, Untradable }
public enum ItemQuality { Normal, High }

public sealed record ShopItem(uint Id, string Name, uint CategoryId, string Category, bool Untradable, bool Consumable = false);
public sealed record Reward(ShopItem Item, uint Quantity, ItemQuality Quality);
public readonly record struct CurrencyKey(uint ItemId, ItemQuality Quality, ushort Collectability);
public sealed record Price(CurrencyKey Key, string Name, uint Quantity, bool Resolved = true);
public sealed record Offer(string Id, string Shop, IReadOnlyList<Reward> Rewards, IReadOnlyList<Price> Prices,
    bool ConditionsKnown, string? Limitation = null);
public sealed record CurrencyTotal(CurrencyKey Key, string Name, long Required, long? Owned)
{
    public long? Shortfall => Owned is { } count ? Math.Max(0, Required - count) : null;
}

public sealed class ShopFilter
{
    public bool HideOwned { get; set; } = true;
    public bool HideConsumables { get; set; }
    public Dictionary<uint, bool> CategoryHideOwned { get; set; } = [];
    public HashSet<uint> Categories { get; set; } = [];
    public bool FilterCategories { get; set; }
    public TradeFilter Trade { get; set; }
    public string Search { get; set; } = string.Empty;

    public bool Matches(Offer offer, Func<uint, Ownership> ownership) => offer.Rewards.Any(reward =>
    {
        var item = reward.Item;
        if (HideConsumables && item.Consumable) return false;
        if (FilterCategories && !Categories.Contains(item.CategoryId)) return false;
        if (Trade == TradeFilter.Tradable && item.Untradable || Trade == TradeFilter.Untradable && !item.Untradable) return false;
        if (!string.IsNullOrWhiteSpace(Search) && !item.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)) return false;
        return !CategoryHideOwned.GetValueOrDefault(item.CategoryId, HideOwned) || ownership(item.Id) != Ownership.Owned;
    });
}

public static class PurchasePlanner
{
    // An exchange can give two items. Charge once for the offer, not once for each reward.
    public static IReadOnlyList<CurrencyTotal> Calculate(IEnumerable<Offer> offers, Func<CurrencyKey, long?> stock)
    {
        var totals = new Dictionary<CurrencyKey, (string Name, long Count)>();
        foreach (var offer in offers)
        foreach (var price in offer.Prices.Where(p => p.Resolved))
        {
            var previous = totals.GetValueOrDefault(price.Key);
            totals[price.Key] = (price.Name, checked(previous.Count + price.Quantity));
        }
        return totals.Select(x => new CurrencyTotal(x.Key, x.Value.Name, x.Value.Count, stock(x.Key)))
            .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    }

    // Alternative exchanges for the same reward are never silently added together.
    public static HashSet<string> SelectDefaults(IEnumerable<Offer> offers)
    {
        var rewards = new HashSet<(uint, ItemQuality)>();
        var selected = new HashSet<string>();
        foreach (var offer in offers)
        {
            if (offer.Rewards.Any(r => rewards.Contains((r.Item.Id, r.Quality)))) continue;
            selected.Add(offer.Id);
            foreach (var reward in offer.Rewards) rewards.Add((reward.Item.Id, reward.Quality));
        }
        return selected;
    }
}
