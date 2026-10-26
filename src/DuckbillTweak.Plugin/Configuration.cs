using Dalamud.Configuration;
using DuckbillTweak.Core.Shops;

namespace DuckbillTweak.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool PvpBubbles { get; set; }
    public bool MerchantTools { get; set; }
    public bool LootRolls { get; set; }
    public bool AutoOpenLoot { get; set; } = true;
    public bool AutoOpenShop { get; set; } = true;
    public bool PartyBubbles { get; set; } = true;
    public bool AllianceBubbles { get; set; } = true;
    public bool SayBubbles { get; set; } = true;
    public bool ShoutBubbles { get; set; }
    public float BubbleSeconds { get; set; } = 6;
    public float BubbleWidth { get; set; } = 300;
    public ShopFilter ShopFilter { get; set; } = new();
}
