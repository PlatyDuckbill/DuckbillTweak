namespace DuckbillTweak.Plugin.UI;

internal sealed record PositionalColorPreset(string Name, uint Hit, uint Miss, uint Combo);
internal sealed record IconChoice(uint Id, string Name);

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
        new(99, "반짝임"), new(110, "주황 마름모"), new(63, "경고"), new(78, "왕관"), new(182, "물음표 상자"),
        new(101, "느낌표 상자"), new(74, "금빛 날개"), new(104, "종"), new(124, "빨강 순환"), new(126, "금빛 고리"),
        new(65, "에테라이트"), new(70, "초록 점"), new(121, "파랑별"), new(184, "빨강별"), new(76, "금지"),
    ];

}
