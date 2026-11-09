using Dalamud.Configuration;

namespace DuckbillTweak.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool PvpBubbles { get; set; }
    public bool MerchantTools { get; set; }
    public bool LootRolls { get; set; }
    /// <summary>Master switch for the combo/positional FlyText (settings "측후·콤보 표시").</summary>
    public bool PositionalFlyText { get; set; }

    // Per outcome: shown at all, inline game icon (BitmapFontIcon id) and number color (RGB 0xRRGGBB).
    // Native damage is orange 0xE36100. A missed combo takes priority over the positional outcome.
    public bool PositionalHitEnabled { get; set; } = true;
    public bool PositionalHitIconEnabled { get; set; } = true;
    public uint PositionalHitIcon { get; set; } = 99;
    public bool PositionalHitColorEnabled { get; set; } = true;
    public uint PositionalHitColor { get; set; } = 0xFFC828;

    public bool PositionalMissEnabled { get; set; } = true;
    public bool PositionalMissIconEnabled { get; set; } = true;
    public uint PositionalMissIcon { get; set; } = 110;
    public bool PositionalMissColorEnabled { get; set; } = true;
    public uint PositionalMissColor { get; set; } = 0xA070FF;

    /// <summary>Missed-combo display; off by default (non-positional jobs are not yet verified in game).</summary>
    public bool ComboFailFlyText { get; set; }
    public bool ComboFailIconEnabled { get; set; } = true;
    public uint ComboFailIcon { get; set; } = 63;
    public bool ComboFailColorEnabled { get; set; } = true;
    public uint ComboFailColor { get; set; } = 0x8C8C8C;
    public string LastSeenChangelogVersion { get; set; } = "";
}
