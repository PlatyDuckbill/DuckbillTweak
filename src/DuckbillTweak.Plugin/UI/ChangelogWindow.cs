using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using T = DuckbillTweak.Plugin.UI.TextKey;

namespace DuckbillTweak.Plugin.UI;

internal sealed class ChangelogWindow(Configuration config, Action<Action<Configuration>> change) : Window("DuckbillTweak###DuckbillTweak.Changelog")
{
    private bool contentShown, acknowledged, resetScroll;
    public bool IsUnread => !acknowledged && (!Version.TryParse(config.LastSeenChangelogVersion, out var seen) || seen < Version.Parse(ReleaseHistory.CurrentVersion));
    public void Open()
    {
        Size = new(640, 580); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(360, 300), MaximumSize = new(float.MaxValue) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = .94f;
        IsOpen = true;
    }
    public override void PreDraw()
    {
        WindowName = UiText.Get(T.UpdatesTitle) + "###DuckbillTweak.Changelog";
        BgAlpha = GameThemePalette.OverlayOpacity;
    }
    public override void OnOpen() { contentShown = false; resetScroll = true; }
    public override void OnClose()
    {
        if (!contentShown) return;
        acknowledged = true;
        change(c => c.LastSeenChangelogVersion = ReleaseHistory.CurrentVersion);
    }
    public override void Draw()
    {
        GameThemeChrome.WindowSurface();
        if (ImGui.BeginChild("###ChangelogContent", new(0, -ImGui.GetFrameHeightWithSpacing())))
        {
            if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
            ImGui.TextUnformatted(UiText.Get(T.CurrentVersion, ReleaseHistory.CurrentVersion));
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(UiText.Get(T.Offline, ReleaseHistory.Entries.Count)); ImGui.PopStyleColor(); ImGui.Spacing();
            foreach (var release in ReleaseHistory.Entries)
            {
                ImGui.SetNextItemOpen(release.Version == ReleaseHistory.CurrentVersion, ImGuiCond.Once);
                if (!GameThemeChrome.Disclosure(UiText.Get(T.Version, release.Version) + "###Release." + release.Version)) continue;
                ImGui.TextDisabled(release.Date is { } date ? UiText.Get(T.Published, date) : UiText.Get(T.Development));
                ImGui.Spacing();
                GameThemeChrome.FeatureTitle(UiText.English ? release.TitleEnglish : release.TitleKorean);
                var summary = UiText.English ? release.SummaryEnglish : release.SummaryKorean;
                if (summary.Length > 0) ImGui.TextWrapped(summary);
                ImGui.Spacing();
                foreach (var text in UiText.English ? release.English : release.Korean) Entry(text);
                var other = UiText.English ? release.OtherEnglish : release.OtherKorean;
                if (other.Length > 0)
                {
                    GameThemeChrome.SectionLabel(UiText.Get(T.Other));
                    foreach (var text in other) Entry(text);
                }
                if (release.Version == ReleaseHistory.CurrentVersion) contentShown = true;
                ImGui.Spacing();
            }
        }
        ImGui.EndChild();
        if (GameThemeChrome.Button(UiText.Get(T.Confirm) + "###DismissChangelog")) IsOpen = false;
    }
    private static void Entry(string text) => GameThemeChrome.Note(text);

}
