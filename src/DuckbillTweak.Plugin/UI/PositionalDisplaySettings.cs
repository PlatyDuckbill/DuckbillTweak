namespace DuckbillTweak.Plugin.UI;

internal sealed record PositionalColorPreset(string Name, uint Hit, uint Miss, uint Combo);
internal sealed record IconChoice(uint Id, TextKey Name);

// Existing display defaults and migrations. Independent of the settings renderer.
internal static class PositionalDisplaySettings
{
    // Missed combo is grey (user choice); the positional miss uses a hue that is not orange (own damage), green
    // (heal/buff), red (damage taken) or grey (missed combo).
    internal const uint DefaultHit = 0xFFC828;   // gold
    internal const uint DefaultMiss = 0xA070FF;  // violet
    internal const uint DefaultCombo = 0x8C8C8C; // grey
    internal const uint DefaultHitIcon = 99;     // bright burst (PriorityWorld)
    internal const uint DefaultMissIcon = 110;   // orange diamond: neutral marker (question box was too strong in game)
    internal const uint DefaultComboIcon = 63;   // warning
    // Earlier defaults; saved configs still holding them are moved to the current defaults on load.
    internal static readonly (uint Hit, uint Miss)[] PreviousDefaults = [(0x5AC8FF, 0x6E6E6E), (0x00D8FF, 0x505050)];
    internal const uint PreviousDefaultCombo = 0xFF0000;
    // Previous preset triples (hit, miss, combo) mapped to their current versions.
    internal static readonly ((uint, uint, uint) Old, (uint, uint, uint) New)[] PreviousPresets =
    [
        ((0xFFC828, 0x8C8C8C, 0x00FFFF), (DefaultHit, DefaultMiss, DefaultCombo)),
        ((0xFF00FF, 0x000000, 0x00FFFF), (0xFF00FF, 0x00FFFF, 0x8C8C8C)),
    ];
    // Rejected icons: silver star (68), damage-type impacts (119 special, 185 blunt), question box (182, too strong).
    internal static readonly (uint Old, uint New)[] PreviousHitIcons = [(119, DefaultHitIcon)];
    internal static readonly (uint Old, uint New)[] PreviousMissIcons = [(68, DefaultMissIcon), (185, DefaultMissIcon), (182, DefaultMissIcon)];

    internal static readonly PositionalColorPreset[] Presets =
    [
        new("아이콘 맞춤 (금색 · 보라 · 회색)", DefaultHit, DefaultMiss, DefaultCombo),
        new("선명 (마젠타 · 청록 · 회색)", 0xFF00FF, 0x00FFFF, 0x8C8C8C),
    ];

    // Filled game icons reviewed from common/font/gfdata.gfd (GameDataProbe gfd-icons). Excluded: thin white chevrons
    // (105/106, hard to see), gold/silver stars (rejected in game), damage-type icons (117-119, 185-188), job/role icons.
    internal static readonly IconChoice[] IconChoices =
    [
        new(99, TextKey.IconSpark), new(110, TextKey.IconDiamond), new(63, TextKey.IconWarning), new(78, TextKey.IconCrown), new(182, TextKey.IconQuestion),
        new(101, TextKey.IconExclamation), new(74, TextKey.IconWings), new(104, TextKey.IconBell), new(124, TextKey.IconRedCycle), new(126, TextKey.IconGoldRing),
        new(65, TextKey.IconAetheryte), new(70, TextKey.IconGreenDot), new(121, TextKey.IconBlueStar), new(184, TextKey.IconRedStar), new(76, TextKey.IconProhibited),
    ];

}
