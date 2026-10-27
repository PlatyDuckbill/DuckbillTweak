using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeChangelogAddon(Configuration config, Action save) : NativeAddon
{
    private ListNode<string, ChangelogRowNode>? entries;
    private DropDownNode<ReleaseNote>? versions;
    private StringDropDownNode? languages;
    private TextNode? date;
    private bool shown;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        shown = false;
        date = new TextNode { Position = new(28, 88), Size = new(580, 24), FontSize = 13 };
        date.AttachNode(this);
        entries = new ListNode<string, ChangelogRowNode>
        { Position = new(28, 118), Size = new(600, 240), OptionsList = [], AutoResetScroll = false };
        entries.NoResultsTextNode.String = "업데이트 내역이 없습니다.";
        entries.AttachNode(this);
        versions = new DropDownNode<ReleaseNote> { Position = new(28, 50), Size = new(290, 28),
            GetLabelFunction = release => $"DuckbillTweak {release.Version}", Options = ReleaseHistory.Entries.ToList() };
        languages = new StringDropDownNode { Position = new(432, 50), Size = new(184, 28), Options = ["한국어", "English"] };
        languages.SelectedOption = languages.Options[0];
        languages.OnOptionSelected = _ => Refresh();
        versions.SelectedOption = versions.Options[0];
        versions.OnOptionSelected = _ => Refresh();
        versions.AttachNode(this); languages.AttachNode(this);
        new TextButtonNode { Position = new(478, 374), Size = new(138, 28), String = "닫기", OnClick = Close }.AttachNode(this);
        Refresh();
    }

    private void Refresh()
    {
        if (entries is null || versions is null || languages is null || date is null) return;
        var release = versions.SelectedOption;
        if (release is null) return;
        date.String = release.Date;
        entries.OptionsList = (languages.SelectedOption == "English" ? release.English : release.Korean).ToList();
        entries.ResetScroll();
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        entries?.Update();
        if (!shown && addon->IsVisible && entries is not null && entries.OptionsList.Count > 0)
        {
            shown = true;
            if (config.LastSeenChangelogVersion != ReleaseHistory.CurrentVersion)
            { config.LastSeenChangelogVersion = ReleaseHistory.CurrentVersion; save(); }
        }
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    { entries = null; versions = null; languages = null; date = null; shown = false; }
}

internal sealed class ChangelogRowNode : ListItemNode<string>, IListItemNode
{
    public static float ItemHeight => 48;
    private readonly TextNode text = new() { Position = new(2, 2), Size = new(572, 44), FontSize = 14,
        LineSpacing = 18, TextFlags = TextFlags.MultiLine | TextFlags.WordWrap };
    public ChangelogRowNode()
    {
        text.AttachNode(this); EnableSelection = false; EnableHighlight = false;
        RemoveEvent(AtkEventType.MouseDown);
    }
    protected override void SetNodeData(string value) { text.String = "• " + value; text.TextTooltip = value; }
}
