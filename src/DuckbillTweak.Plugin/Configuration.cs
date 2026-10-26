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
    public Dictionary<uint, bool> MerchantCategoryHideOwned { get; set; } = [];
    public Dictionary<string, bool> ExchangeCategoryHideOwned { get; set; } = [];
    public ShopFilter ShopFilter { get; set; } = new();
}
