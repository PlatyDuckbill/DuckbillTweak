using DuckbillTweak.Core.Shops;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DuckbillTweak.Plugin.Shops;

/// <summary>Read-only wallet access under the inspected KR hash/MVID gate.</summary>
internal sealed unsafe class NativeCurrencyReader
{
    public bool Enabled { get; set; }

    public uint? SpecialItem(uint reference)
    {
        if (!Enabled || reference is 0 or > byte.MaxValue) return null;
        var manager = CurrencyManager.Instance();
        if (manager is null) return null;
        var item = manager->GetItemIdBySpecialId((byte)reference);
        return item != 0 ? item : null;
    }

    public long? Quantity(CurrencyKey key)
    {
        if (!Enabled || key.Quality != ItemQuality.Normal || key.Collectability != 0) return null;
        var manager = CurrencyManager.Instance();
        // HasItem checks membership in all three wallet buckets, including zero balances.
        return manager is not null && manager->HasItem(key.ItemId) ? manager->GetItemCount(key.ItemId) : null;
    }
}
