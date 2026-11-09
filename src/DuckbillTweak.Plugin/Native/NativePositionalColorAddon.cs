using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Addons;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using LuminaSeStringBuilder = Lumina.Text.SeStringBuilder;

namespace DuckbillTweak.Plugin.Native;

internal sealed record PositionalColorPreset(string Name, uint Hit, uint Miss, uint Combo);

internal sealed record IconChoice(uint Id, string Name);

/// <summary>
/// Detail settings for the combo/positional FlyText. One row per outcome (positional hit, positional miss, missed
/// combo) with its own enable, icon and color switches, plus color presets. Icons are game BitmapFontIcon ids
/// rendered in the lists; colors use the KamiToolKit ColorPickerAddon (ring, square, hex) and are saved as RGB.
/// </summary>
internal sealed unsafe class NativePositionalColorAddon(Configuration config, Action save, ColorPickerAddon picker) : NativeAddon
{
    private enum Slot { Hit, Miss, Combo }

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
    private const string CustomName = "사용자 지정";

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

    private sealed record Row(CheckboxNode Enabled, CheckboxNode Icon, DropDownNode<IconChoice> Icons,
        CheckboxNode ColorOn, ColorSquareNode Square);

    private StringDropDownNode? presets;
    private readonly Dictionary<Slot, Row> rows = [];
    private bool previewing;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        new TextNode { Position = new(28, 54), Size = new(80, 28), FontSize = 14, String = "색 프리셋" }.AttachNode(this);
        new TextButtonNode { Position = new(500, 52), Size = new(110, 28), String = "기본값", OnClick = ResetDefaults }.AttachNode(this);
        var labels = new Dictionary<Slot, (string Text, float Y)>
        {
            [Slot.Hit] = ("측후 성공", 104), [Slot.Miss] = ("측후 실패", 148), [Slot.Combo] = ("콤보 실패", 192),
        };
        var pending = new List<(Slot Slot, float Y, CheckboxNode Enabled, CheckboxNode Icon, CheckboxNode ColorOn, ColorSquareNode Square)>();
        foreach (var (slot, (text, y)) in labels)
        {
            new TextNode { Position = new(28, y), Size = new(80, 28), FontSize = 14, String = text }.AttachNode(this);
            var enabled = Checkbox("활성화", 110, y, 84, value => { SetEnabled(slot, value); save(); });
            var icon = Checkbox("아이콘", 200, y, 76, value => { SetIconOn(slot, value); save(); });
            var colorOn = Checkbox("색", 446, y, 48, value => { SetColorOn(slot, value); save(); });
            var square = new ColorSquareNode { Position = new(498, y + 2), Size = new(24, 24) };
            square.AttachNode(this);
            new TextButtonNode { Position = new(530, y), Size = new(80, 28), String = "변경", OnClick = () => OpenPicker(slot) }.AttachNode(this);
            pending.Add((slot, y, enabled, icon, colorOn, square));
        }
        new TextButtonNode { Position = new(500, 340), Size = new(110, 28), String = "닫기", OnClick = Close }.AttachNode(this);
        new TextNode
        {
            Position = new(28, 240), Size = new(460, 54), FontSize = 13, TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
            String = "콤보 실패가 측후보다 우선합니다. 아이콘은 스킬 이름 뒤에 붙고, 색은 데미지 숫자에 적용됩니다. '변경'에서 색상 링이나 HEX 값으로 정할 수 있습니다.",
        }.AttachNode(this);

        // Dropdowns go last so opened lists draw above the rows: bottom row first, then upward, then the presets.
        foreach (var entry in Enumerable.Reverse(pending))
        {
            var slot = entry.Slot;
            var icons = new DropDownNode<IconChoice> { Position = new(282, entry.Y), Size = new(156, 28), MaxListOptions = 6,
                GetLabelFunction = choice => Label(choice), Options = [.. IconChoices] };
            icons.OnOptionSelected = choice => { SetIcon(slot, choice.Id); save(); };
            icons.AttachNode(this);
            rows[slot] = new(entry.Enabled, entry.Icon, icons, entry.ColorOn, entry.Square);
        }
        presets = new StringDropDownNode { Position = new(110, 52), Size = new(380, 28),
            Options = Presets.Select(preset => preset.Name).Append(CustomName).ToList() };
        presets.OnOptionSelected = name =>
        {
            if (Presets.FirstOrDefault(preset => preset.Name == name) is { } preset)
            {
                config.PositionalHitColor = preset.Hit;
                config.PositionalMissColor = preset.Miss;
                config.ComboFailColor = preset.Combo;
                save();
            }
            Refresh();
        };
        presets.AttachNode(this);
        Refresh();
    }

    private CheckboxNode Checkbox(string text, float x, float y, float width, Action<bool> changed)
    {
        var node = new CheckboxNode { Position = new(x, y + 2), Size = new(width, 24), String = text };
        node.OnClick = changed;
        node.AttachNode(this);
        return node;
    }

    private static ReadOnlySeString Label(IconChoice choice) =>
        new LuminaSeStringBuilder().AppendIcon(choice.Id).Append($" {choice.Name}").ToReadOnlySeString();

    private (bool Enabled, bool IconOn, uint Icon, bool ColorOn, uint Color) Get(Slot slot) => slot switch
    {
        Slot.Hit => (config.PositionalHitEnabled, config.PositionalHitIconEnabled, config.PositionalHitIcon, config.PositionalHitColorEnabled, config.PositionalHitColor),
        Slot.Miss => (config.PositionalMissEnabled, config.PositionalMissIconEnabled, config.PositionalMissIcon, config.PositionalMissColorEnabled, config.PositionalMissColor),
        _ => (config.ComboFailFlyText, config.ComboFailIconEnabled, config.ComboFailIcon, config.ComboFailColorEnabled, config.ComboFailColor),
    };

    private void SetEnabled(Slot slot, bool value)
    {
        if (slot == Slot.Hit) config.PositionalHitEnabled = value;
        else if (slot == Slot.Miss) config.PositionalMissEnabled = value;
        else config.ComboFailFlyText = value;
    }

    private void SetIconOn(Slot slot, bool value)
    {
        if (slot == Slot.Hit) config.PositionalHitIconEnabled = value;
        else if (slot == Slot.Miss) config.PositionalMissIconEnabled = value;
        else config.ComboFailIconEnabled = value;
    }

    private void SetIcon(Slot slot, uint id)
    {
        if (slot == Slot.Hit) config.PositionalHitIcon = id;
        else if (slot == Slot.Miss) config.PositionalMissIcon = id;
        else config.ComboFailIcon = id;
    }

    private void SetColorOn(Slot slot, bool value)
    {
        if (slot == Slot.Hit) config.PositionalHitColorEnabled = value;
        else if (slot == Slot.Miss) config.PositionalMissColorEnabled = value;
        else config.ComboFailColorEnabled = value;
    }

    private void SetColor(Slot slot, uint rgb)
    {
        if (slot == Slot.Hit) config.PositionalHitColor = rgb;
        else if (slot == Slot.Miss) config.PositionalMissColor = rgb;
        else config.ComboFailColor = rgb;
    }

    private static uint DefaultColor(Slot slot) => slot switch { Slot.Hit => DefaultHit, Slot.Miss => DefaultMiss, _ => DefaultCombo };
    private static uint DefaultIcon(Slot slot) => slot switch { Slot.Hit => DefaultHitIcon, Slot.Miss => DefaultMissIcon, _ => DefaultComboIcon };

    private void OpenPicker(Slot slot)
    {
        if (picker.IsOpen) picker.Close();
        picker.Title = slot switch { Slot.Hit => "측후 성공 색", Slot.Miss => "측후 실패 색", _ => "콤보 실패 색" };
        picker.InitialColor = ToVector(Get(slot).Color);
        picker.DefaultColor = ToVector(DefaultColor(slot));
        picker.OnColorPreviewed = color => ShowColor(slot, FromVector(color));
        picker.OnColorCancelled = Refresh;
        picker.OnColorConfirmed = color => { SetColor(slot, FromVector(color)); save(); Refresh(); };
        previewing = true;
        picker.Open();
    }

    /// <summary>Restores icons, colors and the per-row switches; the combo row stays as the user set it (opt-in).</summary>
    private void ResetDefaults()
    {
        foreach (var slot in Enum.GetValues<Slot>())
        {
            SetIconOn(slot, true); SetIcon(slot, DefaultIcon(slot));
            SetColorOn(slot, true); SetColor(slot, DefaultColor(slot));
            if (slot != Slot.Combo) SetEnabled(slot, true);
        }
        save();
        Refresh();
    }

    private void Refresh()
    {
        foreach (var (slot, row) in rows)
        {
            var state = Get(slot);
            row.Enabled.IsChecked = state.Enabled;
            row.Icon.IsChecked = state.IconOn;
            row.Icons.SelectedOption = IconChoices.FirstOrDefault(choice => choice.Id == state.Icon) ?? IconChoices[0];
            row.ColorOn.IsChecked = state.ColorOn;
            ShowColor(slot, state.Color);
        }
        if (presets is not null)
            presets.SelectedOption = Presets.FirstOrDefault(preset => preset.Hit == config.PositionalHitColor
                && preset.Miss == config.PositionalMissColor && preset.Combo == config.ComboFailColor)?.Name ?? CustomName;
    }

    private void ShowColor(Slot slot, uint rgb)
    {
        if (rows.TryGetValue(slot, out var row)) row.Square.Color = ToVector(rgb);
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        // The picker does not always report a cancel after an earlier confirm; drop any stale preview once it closes.
        if (previewing && !picker.IsOpen) { previewing = false; Refresh(); }
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        if (picker.IsOpen) picker.Close();
        presets = null; rows.Clear(); previewing = false;
    }

    private static Vector4 ToVector(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    private static uint FromVector(Vector4 color)
    {
        static uint Channel(float value) => (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        return (Channel(color.X) << 16) | (Channel(color.Y) << 8) | Channel(color.Z);
    }
}
