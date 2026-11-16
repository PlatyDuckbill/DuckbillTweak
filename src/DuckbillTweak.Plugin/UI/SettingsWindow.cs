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
    private readonly Func<FeatureStatuses?>? runtimeStatus;
    private bool showFood, showColors, showSocial;
    private Vector3 colorDraft;
    public bool SaveFailed { get; set; }

    public SettingsWindow(Configuration config, Action<Action<Configuration>> change, Action showUpdates, Func<bool> unread,
        bool pvpAvailable, bool exchangeAvailable, bool positionalAvailable, bool foodAvailable, Action<uint, Vector2>? drawGameIcon = null,
        Func<FeatureStatuses?>? runtimeStatus = null) : base("DuckbillTweak###DuckbillTweak.Settings")
    {
        this.config = config; this.change = change; this.showUpdates = showUpdates; this.unread = unread; this.drawGameIcon = drawGameIcon;
        this.pvpAvailable = pvpAvailable; this.exchangeAvailable = exchangeAvailable;
        this.positionalAvailable = positionalAvailable; this.foodAvailable = foodAvailable;
        this.runtimeStatus = runtimeStatus;
        Size = new(720, 720); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(420, 400), MaximumSize = new(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = .94f;
    }
    public (int Brightness, int Depth, int Transparency) ResolveAppearance() =>
        appearance.Resolve(config.ThemeBrightness, config.ThemeDepth, config.ThemeTransparency, IsOpen);
    public override void PreDraw() => BgAlpha = GameThemeScope.Current?.Surface.W
        ?? GameThemePalette.For(ThemeScope.IsLight(config.Theme), config.ThemeBrightness, config.ThemeDepth, config.ThemeTransparency).Surface.W;
    public override void OnOpen() { if (unread()) showUpdates(); }
    public override void Draw()
    {
        var runtime = runtimeStatus?.Invoke();
        var fresh = runtime is not null && Environment.TickCount64 - runtime.Tick < 2000;
        FeatureStatus? Current(FeatureStatus? status) => runtimeStatus is null ? null : fresh ? status : new(FeatureState.Stale);
        using var contentStyle = GameThemeChrome.Content();
        GameThemeChrome.WindowSurface();
        if (GameThemeChrome.SettingsHeader(UiText.Get(T.SettingsTitle), UiText.Get(unread() ? T.UpdatesNew : T.UpdatesOpen))) showUpdates();
        var footerHeight = ImGui.GetFrameHeightWithSpacing() + (SaveFailed
            ? ImGui.CalcTextSize(UiText.Get(T.SaveFailed), false, ImGui.GetContentRegionAvail().X).Y + ImGui.GetStyle().ItemSpacing.Y : 0);
        if (ImGui.BeginChild("###SettingsContent", new(0, -footerHeight)))
        {
            UiWidgets.Section(T.AppearanceStyle);
            Appearance();
            UiWidgets.Secondary(T.SettingsSummary);
            UiWidgets.MajorSection(T.Combat);
            Feature(T.Food, T.FoodDescription, config.FoodReminder, v => change(c => c.FoodReminder = v), () => showFood = !showFood);
            DrawStatus(Current(runtime?.Food));
            if (runtimeStatus is null && !foodAvailable) UiWidgets.Secondary(T.FoodUnavailable);
            if (showFood) DetailGroup("FoodDetails", MeasureFoodDetails, FoodSettings);
            Feature(T.Position, T.PositionDescription, config.PositionalFlyText, v => change(c => c.PositionalFlyText = v), () => showColors = !showColors);
            DrawStatus(Current(runtime?.Positional));
            if (runtimeStatus is null && !positionalAvailable) UiWidgets.Secondary(T.Unavailable);
            if (showColors) DetailGroup("ColorDetails", MeasureColorDetails, ColorSettings);
            Feature(T.Pvp, T.PvpDescription, config.PvpBubbles, v => change(c => c.PvpBubbles = v));
            DrawStatus(Current(runtime?.Pvp));
            if (runtimeStatus is null && !pvpAvailable) UiWidgets.Secondary(T.Unavailable);
            UiWidgets.MajorSection(T.Shops);
            Feature(T.Merchant, T.MerchantDescription, config.MerchantTools, v => change(c => c.MerchantTools = v));
            DrawStatus(Current(runtime?.Merchant));
            if (runtimeStatus is null && !exchangeAvailable) UiWidgets.Secondary(T.ExchangeUnavailable);
            Feature(T.Loot, T.LootDescription, config.LootRolls, v => change(c => c.LootRolls = v));
            DrawStatus(Current(runtime?.Loot));
            UiWidgets.MajorSection(T.Social);
            Feature(T.Emote, T.EmoteDescription, config.EmoteFollow, v => change(c => c.EmoteFollow = v), () => showSocial = !showSocial);
            DrawStatus(Current(runtime?.Emote));
            if (showSocial) DetailGroup("EmoteDetails", MeasureSocialDetails, SocialSettings);
        }
        ImGui.EndChild();
        var version = UiText.Get(T.Version, ReleaseHistory.CurrentVersion);
        ImGui.TextDisabled(version);
        if (SaveFailed) UiWidgets.Secondary(T.SaveFailed);
    }

    private static void DrawStatus(FeatureStatus? status)
    {
        if (status is null) return;
        var light = GameThemeScope.Current?.Light == true;
        var color = status.State switch
        {
            FeatureState.Off => GameThemeScope.Current?.Muted ?? new Vector4(.6f, .6f, .6f, 1),
            FeatureState.Ready or FeatureState.Observing or FeatureState.Syncing or FeatureState.PvpOnly
                or FeatureState.WaitingEmote => GameThemePalette.Hex(light ? 0x247147u : 0x79cf9cu),
            FeatureState.ContractUnavailable or FeatureState.InitializationFailed or FeatureState.ObservationFailed
                or FeatureState.ReadyCheckFailed or FeatureState.PvpPatchFailed => GameThemePalette.Hex(light ? 0xa42e2eu : 0xf08b83u),
            _ => GameThemePalette.Hex(light ? 0x8c6318u : 0xe6c36au)
        };
        var radius = ImGui.GetFontSize() * .22f;
        var origin = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        ImGui.GetWindowDrawList().AddCircleFilled(origin + new Vector2(radius, lineHeight * .5f), radius, ImGui.GetColorU32(color), 16);
        ImGui.Dummy(new Vector2(radius * 2, lineHeight));
        var dotHovered = ImGui.IsItemHovered();
        ImGui.SameLine(0, ImGui.GetFontSize() * .35f);
        UiWidgets.Secondary(T.RuntimeStatus, UiText.Get(status.State switch
        {
            FeatureState.Off => T.StatusOff, FeatureState.LoginRequired => T.StatusLogin,
            FeatureState.Loading => T.StatusLoading, FeatureState.Ready => T.StatusReady,
            FeatureState.Observing => T.StatusObserving, FeatureState.PvpOnly => T.StatusPvp,
            FeatureState.ExcludedArea => T.StatusArea, FeatureState.NoActions => T.StatusActions,
            FeatureState.ContractUnavailable => T.StatusContract, FeatureState.InitializationFailed => T.StatusInitialization,
            FeatureState.ObservationFailed => T.StatusObservation, FeatureState.ReadyCheckFailed => T.StatusReadyCheck,
            FeatureState.PvpPatchFailed => T.StatusPatch, FeatureState.WaitingFood => T.StatusFood,
            FeatureState.WaitingEmote => T.StatusEmote, FeatureState.Syncing => T.StatusSyncing,
            FeatureState.CombatBlocked => T.StatusCombat, FeatureState.ShopPartial => T.StatusShopPartial, _ => T.StatusStale
        }));
        if (status.Diagnostic.Length > 0 && (dotHovered || ImGui.IsItemHovered()))
            ImGui.SetTooltip(UiText.Get(T.StatusDiagnostic, status.Diagnostic));
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
        if (GameThemeChrome.BeginSettingsFields(2))
        {
            GameThemeChrome.SettingsField(UiText.Get(T.Theme));
            var theme = ThemeScope.IsLight(config.Theme) ? 1 : 0;
            var themes = new[] { UiText.Get(T.Graphite), UiText.Get(T.Paper) };
            if (GameThemeChrome.Combo("###Theme", ref theme, themes, themes.Length)) { var value = theme == 1 ? "pearl" : "smoke"; change(c => c.Theme = value); }
            GameThemeChrome.SettingsField(UiText.Get(T.Language));
            var language = config.UiLanguage == "en" ? 1 : 0;
            if (GameThemeChrome.Combo("###Language", ref language, new[] { "한국어", "English" }, 2))
            { var value = language == 1 ? "en" : "ko"; change(c => c.UiLanguage = value); }
            ImGui.EndTable();
        }
        if (appearance.Draw(ThemeScope.IsLight(config.Theme), config.ThemeBrightness, config.ThemeDepth, config.ThemeTransparency,
            UiText.Get(T.AppearanceBrightness), UiText.Get(T.AppearanceDepth), UiText.Get(T.AppearanceTransparency), UiText.Get(T.AppearanceHint),
            UiText.Get(T.AppearanceReset), UiText.Get(T.AppearanceDetails)) is { } adjustment)
            change(c => { c.ThemeBrightness = adjustment.Brightness; c.ThemeDepth = adjustment.Depth; c.ThemeTransparency = adjustment.Transparency; });
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
    private static void DetailGroup(string id, Func<float, float> measure, Action drawContent)
    {
        var f = ImGui.GetFontSize();
        var p = GameThemeScope.Current!;
        var indent = MathF.Round(f * 1.1f);
        ImGui.Indent(indent);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - f * .4f);
        var pad = new Vector2(MathF.Round(f * .7f), MathF.Round(f * .65f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, pad);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, p.Inset with { W = .55f });
        ImGui.PushStyleColor(ImGuiCol.Border, p.Border with { W = .45f });
        var start = ImGui.GetCursorScreenPos();
        var height = MathF.Ceiling(measure(Math.Max(1, width - pad.X * 2)) + pad.Y * 2 + 2);
        try
        {
            var visible = ImGui.BeginChild("###" + id, new(width, height), true, ImGuiWindowFlags.NoScrollWithMouse);
            try { if (visible) drawContent(); }
            finally { ImGui.EndChild(); }
        }
        finally { ImGui.PopStyleColor(2); ImGui.PopStyleVar(); ImGui.Unindent(indent); }
        var line = start - new Vector2(f * .55f, 0);
        var color = ImGui.GetColorU32(p.Border with { W = .6f });
        ImGui.GetWindowDrawList().AddLine(line, line + new Vector2(0, height), color, 2);
        ImGui.GetWindowDrawList().AddLine(line, start, color, 2);
        ImGui.Spacing();
    }
    private static float WrappedHeight(T text, float width) => ImGui.CalcTextSize(UiText.Get(text), false, width).Y;
    private static float MeasureFoodDetails(float width) => ImGui.GetFontSize() + ImGui.GetFrameHeight() * 3 +
        WrappedHeight(T.FoodHint, width) + WrappedHeight(T.FoodScope, width) + ImGui.GetStyle().ItemSpacing.Y * 7;
    private static float MeasureSocialDetails(float width) => ImGui.GetFontSize() + ImGui.GetFrameHeight() +
        WrappedHeight(T.TimingHint, width) + ImGui.GetStyle().ItemSpacing.Y * 3;
    private static float OutcomeHeight(float width)
    {
        var wide = width >= ImGui.GetFontSize() * 29;
        return ImGui.GetFrameHeight() * (wide ? 3 : 5) + ImGui.GetStyle().ItemSpacing.Y * (wide ? 5 : 7) + ImGui.GetStyle().WindowPadding.Y * 2;
    }
    private static float MeasureColorDetails(float width) => ImGui.GetFontSize() + ImGui.GetFrameHeight() + OutcomeHeight(width) * 3 +
        WrappedHeight(T.PositionHint, width) + ImGui.GetStyle().ItemSpacing.Y * 11;
    private void SocialSettings()
    {
        ImGui.TextUnformatted(UiText.Get(T.Timing));
        ImGui.SetNextItemWidth(Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 20));
        int[] offsets = [0, 40, 80, 120];
        var timing = Math.Max(0, Array.IndexOf(offsets, config.EmoteFollowOffsetMs));
        var timings = new[] { T.TimingOwn, T.TimingMiddle, T.TimingOthers, T.TimingEarly }.Select(k => UiText.Get(k)).ToArray();
        if (GameThemeChrome.Combo("###EmoteTiming", ref timing, timings, timings.Length))
        { var ms = offsets[timing]; change(c => c.EmoteFollowOffsetMs = ms); }
        UiWidgets.Secondary(T.TimingHint);
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
        var height = OutcomeHeight(ImGui.GetContentRegionAvail().X);
        var p = GameThemeScope.Current!;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, p.Surface with { W = .65f });
        ImGui.PushStyleColor(ImGuiCol.Border, p.Border with { W = .25f });
        if (ImGui.BeginChild("###Outcome", new(0, height), true, ImGuiWindowFlags.NoScrollWithMouse))
        {
            var header = ImGui.GetCursorScreenPos() - new Vector2(f * .2f, f * .1f);
            ImGui.GetWindowDrawList().AddRectFilled(header,
                header + new Vector2(ImGui.GetContentRegionAvail().X + f * .4f, ImGui.GetFrameHeight() + f * .2f),
                ImGui.GetColorU32(p.Field with { W = .6f }), 2);
            if (ImGui.Checkbox(UiText.Get(label) + "###Enabled", ref enabled))
            { var v = enabled; Set(slot, c => c.PositionalHitEnabled = v, c => c.PositionalMissEnabled = v, c => c.ComboFailFlyText = v); }
            ImGui.Spacing();
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
        ImGui.EndChild(); ImGui.PopStyleColor(2);
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
    public static void Secondary(T key, params object[] args)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(UiText.Get(key, args)); ImGui.PopStyleColor();
    }
    public static void Section(T key)
    {
        GameThemeChrome.SectionLabel(UiText.Get(key));
    }
    public static void MajorSection(T key)
    {
        ImGui.Spacing();
        var p = GameThemeScope.Current!;
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight() * 1.3f);
        ImGui.Dummy(size);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + size, ImGui.GetColorU32(p.Field with { W = .8f }), 3);
        draw.AddRectFilled(start, start + new Vector2(3, size.Y), ImGui.GetColorU32(p.Accent with { W = .8f }), 2);
        var text = UiText.Get(key);
        draw.AddText(Vector2.Round(start + new Vector2(ImGui.GetFontSize() * .7f, (size.Y - ImGui.CalcTextSize(text).Y) / 2)),
            ImGui.GetColorU32(p.Text), text);
        ImGui.Spacing();
    }
}
