using Dalamud.Configuration;

namespace DuckbillTweak.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool PvpBubbles { get; set; }
    public bool MerchantTools { get; set; }
    public bool LootRolls { get; set; }
}
