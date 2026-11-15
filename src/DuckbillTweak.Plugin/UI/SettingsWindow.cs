using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using T = DuckbillTweak.Plugin.UI.TextKey;

namespace DuckbillTweak.Plugin.UI;

internal sealed class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly ThemeAdjustmentEditor appearance = new();
    private readonly Action<Action<Configuration>> change;
    private readonly Action showUpdates;
    private readonly Func<bool> unread;
    private readonly Action<uint, Vector2>? drawGameIcon;
    private readonly bool pvpAvailable, exchangeAvailable, positionalAvailable, foodAvailable;
    private bool showFood, showColors;
    private Vector3 colorDraft;
    public bool SaveFailed { get; set; }

    public SettingsWindow(Configuration config, Action<Action<Configuration>> change, Action showUpdates, Func<bool> unread,
        bool pvpAvailable, bool exchangeAvailable, bool positionalAvailable, bool foodAvailable, Action<uint, Vector2>? drawGameIcon = null) : base("DuckbillTweak###DuckbillTweak.Settings")
    {
        this.config = config; this.change = change; this.showUpdates = showUpdates; this.unread = unread; this.drawGameIcon = drawGameIcon;
        this.pvpAvailable = pvpAvailable; this.exchangeAvailable = exchangeAvailable;
        this.positionalAvailable = positionalAvailable; this.foodAvailable = foodAvailable;
        Size = new(720, 720); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(420, 400), MaximumSize = new(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = .94f;
    }
    public override void PreDraw() => BgAlpha = GameThemeScope.Current?.Surface.W
        ?? GameThemePalette.For(ThemeScope.IsLight(config.Theme), config.ThemeBrightness, config.ThemeDepth).Surface.W;
    public override void OnOpen() { if (unread()) showUpdates(); }
    public override void Draw()
    {
        GameThemeChrome.WindowSurface();
        UiWidgets.Heading(T.SettingsTitle);
        UiWidgets.Secondary(T.SettingsSummary);
        ImGui.Spacing();
        var footerHeight = ImGui.GetFrameHeightWithSpacing() + (SaveFailed
            ? ImGui.CalcTextSize(UiText.Get(T.SaveFailed), false, ImGui.GetContentRegionAvail().X).Y + ImGui.GetStyle().ItemSpacing.Y : 0);
        if (ImGui.BeginChild("###SettingsContent", new(0, -footerHeight)))
        {
            UiWidgets.Section(T.Combat);
            Feature(T.Food, T.FoodDescription, config.FoodReminder, v => change(c => c.FoodReminder = v), () => showFood = !showFood);
            if (!foodAvailable) UiWidgets.Secondary(T.FoodUnavailable);
            if (showFood) FoodSettings();
            Feature(T.Position, T.PositionDescription, config.PositionalFlyText, v => change(c => c.PositionalFlyText = v), () => showColors = !showColors);
            if (!positionalAvailable) UiWidgets.Secondary(T.Unavailable);
            if (showColors) ColorSettings();
            Feature(T.Pvp, T.PvpDescription, config.PvpBubbles, v => change(c => c.PvpBubbles = v));
            if (!pvpAvailable) UiWidgets.Secondary(T.Unavailable);
            UiWidgets.Section(T.Shops);
            Feature(T.Merchant, T.MerchantDescription, config.MerchantTools, v => change(c => c.MerchantTools = v));
            if (!exchangeAvailable) UiWidgets.Secondary(T.ExchangeUnavailable);
            Feature(T.Loot, T.LootDescription, config.LootRolls, v => change(c => c.LootRolls = v));
            UiWidgets.Section(T.Social);
            Feature(T.Emote, T.EmoteDescription, config.EmoteFollow, v => change(c => c.EmoteFollow = v));
            ImGui.TextUnformatted(UiText.Get(T.Timing));
            ImGui.SetNextItemWidth(Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 17));
            int[] offsets = [0, 40, 80, 120];
            var timing = Math.Max(0, Array.IndexOf(offsets, config.EmoteFollowOffsetMs));
            var timings = new[] { T.TimingOwn, T.TimingMiddle, T.TimingOthers, T.TimingEarly }.Select(k => UiText.Get(k)).ToArray();
            if (GameThemeChrome.Combo("###EmoteTiming", ref timing, timings, timings.Length))
            { var ms = offsets[timing]; change(c => c.EmoteFollowOffsetMs = ms); }
            UiWidgets.Secondary(T.TimingHint);
            UiWidgets.Section(T.Appearance);
            Appearance();
        }
        ImGui.EndChild();
        if (GameThemeChrome.Button(UiText.Get(unread() ? T.UpdatesNew : T.UpdatesOpen) + "###ShowChangelog")) showUpdates();
        var version = UiText.Get(T.Version, ReleaseHistory.CurrentVersion);
        if (UiWidgets.FitsOnLine(ImGui.CalcTextSize(version).X))
        { ImGui.SameLine(); ImGui.TextDisabled(version); }
        if (SaveFailed) UiWidgets.Secondary(T.SaveFailed);
    }

    private static void Feature(T title, T description, bool enabled, Action<bool> set, Action? details = null)
    {
        ImGui.PushID(title.ToString());
        var wide = ImGui.GetContentRegionAvail().X > ImGui.GetFontSize() * 30;
        if (ImGui.BeginTable("###Feature", wide && details is not null ? 2 : 1, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("###Body", ImGuiTableColumnFlags.WidthStretch);
            if (wide && details is not null) ImGui.TableSetupColumn("###Details", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
            ImGui.TableNextColumn();
            var toggled = ImGui.Checkbox("###Enabled", ref enabled);
            ImGui.SameLine(); ImGui.BeginGroup(); ImGui.TextWrapped(UiText.Get(title)); ImGui.EndGroup();
            if (ImGui.IsItemClicked()) { enabled = !enabled; toggled = true; }
            if (toggled) set(enabled);
            UiWidgets.Secondary(description);
            if (details is not null)
            {
                if (wide) ImGui.TableNextColumn();
                if (GameThemeChrome.Button(UiText.Get(T.Details) + "###Details")) details();
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.PopID();
    }

    private void Appearance()
    {
        if (!ImGui.BeginTable("###Appearance", 2, ImGuiTableFlags.SizingStretchSame)) return;
        ImGui.TableNextColumn(); ImGui.TextUnformatted(UiText.Get(T.Theme));
        var theme = ThemeScope.IsLight(config.Theme) ? 1 : 0;
        var themes = new[] { UiText.Get(T.Graphite), UiText.Get(T.Paper) };
        ImGui.SetNextItemWidth(-1);
        if (GameThemeChrome.Combo("###Theme", ref theme, themes, themes.Length)) { var value = theme == 1 ? "pearl" : "smoke"; change(c => c.Theme = value); }
        ImGui.TableNextColumn(); ImGui.TextUnformatted(UiText.Get(T.Language));
        var language = config.UiLanguage == "en" ? 1 : 0;
        ImGui.SetNextItemWidth(-1);
        if (GameThemeChrome.Combo("###Language", ref language, new[] { "한국어", "English" }, 2))
        { var value = language == 1 ? "en" : "ko"; change(c => c.UiLanguage = value); }
        ImGui.EndTable();
        if (appearance.Draw(ThemeScope.IsLight(config.Theme), config.ThemeBrightness, config.ThemeDepth,
            UiText.Get(T.AppearanceBrightness), UiText.Get(T.AppearanceDepth), UiText.Get(T.AppearanceHint),
            UiText.Get(T.AppearanceReset), UiText.Get(T.AppearancePreview)) is { } adjustment)
            change(c => { c.ThemeBrightness = adjustment.Brightness; c.ThemeDepth = adjustment.Depth; });
    }

    private void FoodSettings()
    {
        ImGui.PushID("FoodDetails");
        UiWidgets.Secondary(T.FoodMinutes);
        Minutes(T.Extreme, config.FoodExtremeMinutes, (c, v) => c.FoodExtremeMinutes = v);
        Minutes(T.Savage, config.FoodSavageMinutes, (c, v) => c.FoodSavageMinutes = v);
        Minutes(T.Ultimate, config.FoodUltimateMinutes, (c, v) => c.FoodUltimateMinutes = v);
        UiWidgets.Secondary(T.FoodHint); UiWidgets.Secondary(T.FoodScope);
        ImGui.Spacing(); ImGui.PopID();
    }
    private void Minutes(T label, int value, Action<Configuration, int> set)
    {
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
        if (ImGui.SliderInt(UiText.Get(label) + "###" + label, ref value, 1, 60))
        { var minutes = Math.Clamp(value, 1, 60); change(c => set(c, minutes)); }
    }

    private enum Slot { Hit, Miss, Combo }
    private void ColorSettings()
    {
        ImGui.PushID("ColorDetails");
        var palette = PositionalDisplaySettings.Presets;
        var selected = Array.FindIndex(palette, p => p.Hit == config.PositionalHitColor && p.Miss == config.PositionalMissColor && p.Combo == config.ComboFailColor);
        var label = selected >= 0 ? UiText.Get(selected == 0 ? T.PresetDefault : T.PresetVivid) : UiText.Get(T.Custom);
        ImGui.TextUnformatted(UiText.Get(T.Preset));
        var resetWidth = ImGui.CalcTextSize(UiText.Get(T.Defaults)).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetNextItemWidth(Math.Max(1, Math.Min(ImGui.GetFontSize() * 22, ImGui.GetContentRegionAvail().X - resetWidth - ImGui.GetStyle().ItemSpacing.X)));
        if (GameThemeChrome.BeginCombo("###Preset", label))
        {
            for (var i = 0; i < palette.Length; i++)
            {
                if (!ImGui.Selectable(UiText.Get(i == 0 ? T.PresetDefault : T.PresetVivid), selected == i)) continue;
                var preset = palette[i];
                change(c => { c.PositionalHitColor = preset.Hit; c.PositionalMissColor = preset.Miss; c.ComboFailColor = preset.Combo; });
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (GameThemeChrome.Button(UiText.Get(T.Defaults) + "###ResetColors")) change(c =>
        {
            c.PositionalHitEnabled = c.PositionalMissEnabled = true;
            c.PositionalHitIconEnabled = c.PositionalMissIconEnabled = c.ComboFailIconEnabled = true;
            c.PositionalHitColorEnabled = c.PositionalMissColorEnabled = c.ComboFailColorEnabled = true;
            c.PositionalHitIcon = PositionalDisplaySettings.DefaultHitIcon;
            c.PositionalMissIcon = PositionalDisplaySettings.DefaultMissIcon;
            c.ComboFailIcon = PositionalDisplaySettings.DefaultComboIcon;
            c.PositionalHitColor = PositionalDisplaySettings.DefaultHit;
            c.PositionalMissColor = PositionalDisplaySettings.DefaultMiss;
            c.ComboFailColor = PositionalDisplaySettings.DefaultCombo;
        });
        ImGui.Spacing();
        Outcome(Slot.Hit, T.Hit, config.PositionalHitEnabled, config.PositionalHitIconEnabled, config.PositionalHitIcon, config.PositionalHitColorEnabled, config.PositionalHitColor);
        Outcome(Slot.Miss, T.Miss, config.PositionalMissEnabled, config.PositionalMissIconEnabled, config.PositionalMissIcon, config.PositionalMissColorEnabled, config.PositionalMissColor);
        Outcome(Slot.Combo, T.Combo, config.ComboFailFlyText, config.ComboFailIconEnabled, config.ComboFailIcon, config.ComboFailColorEnabled, config.ComboFailColor);
        UiWidgets.Secondary(T.PositionHint);
        ImGui.Spacing(); ImGui.PopID();
    }
    private void Outcome(Slot slot, T label, bool enabled, bool iconOn, uint iconId, bool colorOn, uint rgb)
    {
        ImGui.PushID(slot.ToString());
        var f = ImGui.GetFontSize();
        var wide = ImGui.GetContentRegionAvail().X >= f * 29;
        var height = ImGui.GetFrameHeight() * (wide ? 3 : 5) + ImGui.GetStyle().ItemSpacing.Y * (wide ? 4 : 6) + ImGui.GetStyle().WindowPadding.Y * 2;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, (GameThemeScope.Current?.Inset ?? Vector4.Zero) with { W = .42f });
        if (ImGui.BeginChild("###Outcome", new(0, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.Checkbox(UiText.Get(label) + "###Enabled", ref enabled))
            { var v = enabled; Set(slot, c => c.PositionalHitEnabled = v, c => c.PositionalMissEnabled = v, c => c.ComboFailFlyText = v); }
            if (ImGui.BeginTable("###Fields", wide ? 2 : 1, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(UiText.Get(T.Icon) + "###IconOn", ref iconOn))
                { var v = iconOn; Set(slot, c => c.PositionalHitIconEnabled = v, c => c.PositionalMissIconEnabled = v, c => c.ComboFailIconEnabled = v); }
                ImGui.SetNextItemWidth(-1);
                IconSelector(slot, iconId);
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(UiText.Get(T.Color) + "###ColorOn", ref colorOn))
                { var v = colorOn; Set(slot, c => c.PositionalHitColorEnabled = v, c => c.PositionalMissColorEnabled = v, c => c.ComboFailColorEnabled = v); }
                var color = ThemeScope.Hex(rgb);
                if (ImGui.ColorButton("###ColorPreview", color, ImGuiColorEditFlags.NoTooltip, new(ImGui.GetFrameHeight())))
                { colorDraft = new(color.X, color.Y, color.Z); ImGui.OpenPopup("###ColorPicker"); }
                ImGui.SameLine();
                if (GameThemeChrome.Button(UiText.Get(T.ChangeColor) + "###ChangeColor"))
                { colorDraft = new(color.X, color.Y, color.Z); ImGui.OpenPopup("###ColorPicker"); }
                ColorPicker(slot);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild(); ImGui.PopStyleColor();
        ImGui.Spacing(); ImGui.PopID();
    }
    private void IconSelector(Slot slot, uint iconId)
    {
        var choices = PositionalDisplaySettings.IconChoices;
        var choice = Array.FindIndex(choices, x => x.Id == iconId);
        var name = choice >= 0 ? UiText.Get(choices[choice].Name) : UiText.Get(T.IconChoice, UiText.Get(T.Icon), iconId);
        var min = ImGui.GetCursorScreenPos(); var width = ImGui.CalcItemWidth(); var height = ImGui.GetFrameHeight();
        if (GameThemeChrome.BeginCombo("###IconChoice", "", ImGuiComboFlags.HeightRegular))
        {
            foreach (var icon in choices)
            {
                if (ImGui.Selectable("###Icon." + icon.Id, icon.Id == iconId, ImGuiSelectableFlags.None, new(0, ImGui.GetFrameHeight())))
                { var id = icon.Id; Set(slot, c => c.PositionalHitIcon = id, c => c.PositionalMissIcon = id, c => c.ComboFailIcon = id); }
                IconLabel(icon.Id, UiText.Get(icon.Name), ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("#" + icon.Id);
            }
            ImGui.EndCombo();
        }
        IconLabel(iconId, name, min, min + new Vector2(Math.Max(1, width - height), height));
    }
    private void IconLabel(uint id, string text, Vector2 min, Vector2 max)
    {
        var f = ImGui.GetFontSize(); var pad = ImGui.GetStyle().FramePadding.X;
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(min, max, true);
        drawGameIcon?.Invoke(id, Vector2.Round(min + new Vector2(pad, (max.Y - min.Y - f) / 2)));
        draw.AddText(Vector2.Round(min + new Vector2(pad + f * 1.5f, (max.Y - min.Y - ImGui.CalcTextSize(text).Y) / 2)), ImGui.GetColorU32(ImGuiCol.Text), text);
        draw.PopClipRect();
    }
    private void ColorPicker(Slot slot)
    {
        if (ImGui.BeginPopup("###ColorPicker"))
        {
            ImGui.SetNextItemWidth(Math.Max(1, Math.Min(ImGui.GetFontSize() * 18, ImGui.GetMainViewport().WorkSize.X - ImGui.GetFontSize() * 3)));
            ImGui.ColorPicker3("###Picker", ref colorDraft, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.DisplayHex);
            if (GameThemeChrome.Button(UiText.Get(T.Confirm) + "###Confirm"))
            {
                static uint Channel(float x) => (uint)Math.Clamp((int)MathF.Round(x * 255), 0, 255);
                var value = Channel(colorDraft.X) << 16 | Channel(colorDraft.Y) << 8 | Channel(colorDraft.Z);
                Set(slot, c => c.PositionalHitColor = value, c => c.PositionalMissColor = value, c => c.ComboFailColor = value);
                ImGui.CloseCurrentPopup();
            }
            if (UiWidgets.FitsOnLine(ImGui.CalcTextSize(UiText.Get(T.Defaults)).X + ImGui.GetStyle().FramePadding.X * 2)) ImGui.SameLine();
            if (GameThemeChrome.Button(UiText.Get(T.Defaults) + "###Defaults"))
            { var v = ThemeScope.Hex(slot == Slot.Hit ? PositionalDisplaySettings.DefaultHit : slot == Slot.Miss ? PositionalDisplaySettings.DefaultMiss : PositionalDisplaySettings.DefaultCombo); colorDraft = new(v.X, v.Y, v.Z); }
            if (UiWidgets.FitsOnLine(ImGui.CalcTextSize(UiText.Get(T.Cancel)).X + ImGui.GetStyle().FramePadding.X * 2)) ImGui.SameLine();
            if (GameThemeChrome.Button(UiText.Get(T.Cancel) + "###Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }
    private void Set(Slot slot, Action<Configuration> hit, Action<Configuration> miss, Action<Configuration> combo) => change(slot == Slot.Hit ? hit : slot == Slot.Miss ? miss : combo);
}

internal static class UiWidgets
{
    public static bool FitsOnLine(float width) => ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width
        <= ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
    public static void Heading(T key) { ImGui.TextUnformatted(UiText.Get(key)); }
    public static void Secondary(T key)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(UiText.Get(key)); ImGui.PopStyleColor();
    }
    public static void Section(T key)
    {
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextUnformatted(UiText.Get(key)); ImGui.Spacing();
    }
}
