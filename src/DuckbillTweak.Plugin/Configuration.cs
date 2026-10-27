using Dalamud.Configuration;
using DuckbillTweak.Core.Shops;

namespace DuckbillTweak.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool PvpBubbles { get; set; }
    public bool MerchantTools { get; set; }
    public bool LootRolls { get; set; }
    public bool MerchantHideOwned { get; set; }
    // Keep the persisted key so existing check states survive the expanded supply policy.
    public bool MerchantHideConsumables { get; set; }
    public Dictionary<uint, bool> MerchantCategoryHideOwned { get; set; } = [];
    public Dictionary<string, bool> ExchangeCategoryHideOwned { get; set; } = [];
    public ShopFilter ShopFilter { get; set; } = new();

    public bool HideOwnedInCategory(uint category) => MerchantCategoryHideOwned.GetValueOrDefault(category, MerchantHideOwned);
    public bool HasOwnedFilter(uint category) => category != 0 ? HideOwnedInCategory(category) :
        MerchantHideOwned || MerchantCategoryHideOwned.Values.Any(value => value);
}
