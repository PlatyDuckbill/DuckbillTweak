using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>One scrolling list of every embedded release (newest first): a version header followed by its notes.</summary>
internal sealed unsafe class NativeChangelogAddon(Configuration config, Action save) : NativeAddon
{
    private ListNode<ChangelogLine, ChangelogRowNode>? entries;
    private StringDropDownNode? languages;
    private bool shown;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        shown = false;
        new TextNode { Position = new(28, 54), Size = new(380, 24), FontSize = 13, String = "최근 업데이트부터 표시합니다." }.AttachNode(this);
        entries = new ListNode<ChangelogLine, ChangelogRowNode>
        { Position = new(28, 88), Size = new(600, 274), OptionsList = [], AutoResetScroll = false };
        entries.NoResultsTextNode.String = "업데이트 내역이 없습니다.";
        entries.AttachNode(this);
        languages = new StringDropDownNode { Position = new(432, 50), Size = new(184, 28), Options = ["한국어", "English"] };
        languages.SelectedOption = languages.Options[0];
        languages.OnOptionSelected = _ => Refresh();
        languages.AttachNode(this);
        new TextButtonNode { Position = new(478, 374), Size = new(138, 28), String = "닫기", OnClick = Close }.AttachNode(this);
        Refresh();
    }

    private void Refresh()
    {
        if (entries is null || languages is null) return;
        var english = languages.SelectedOption == "English";
        entries.OptionsList = ReleaseHistory.Entries.SelectMany(release =>
            (english ? release.English : release.Korean).Select(note => new ChangelogLine(note, false))
                .Prepend(new ChangelogLine($"DuckbillTweak {release.Version} · {release.Date}", true))).ToList();
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
    { entries = null; languages = null; shown = false; }
}

internal sealed record ChangelogLine(string Text, bool Header);

internal sealed class ChangelogRowNode : ListItemNode<ChangelogLine>, IListItemNode
{
    public static float ItemHeight => 48;
    private static readonly Vector4 HeaderColor = new(1f, .82f, .34f, 1f);
    private readonly TextNode text = new() { Position = new(2, 2), Size = new(572, 44), FontSize = 14,
        LineSpacing = 18, TextFlags = TextFlags.MultiLine | TextFlags.WordWrap };
    private readonly Vector4 noteColor;
    public ChangelogRowNode()
    {
        noteColor = text.TextColor; // Keep the previous default note color.
        text.AttachNode(this); EnableSelection = false; EnableHighlight = false;
        RemoveEvent(AtkEventType.MouseDown);
    }
    protected override void SetNodeData(ChangelogLine value)
    {
        // Headers sit at the bottom of their row so they read as the start of the notes below.
        text.Position = value.Header ? new(2, 22) : new(2, 2);
        text.Size = value.Header ? new(572, 24) : new(572, 44);
        text.FontSize = value.Header ? 16u : 14u;
        text.TextColor = value.Header ? HeaderColor : noteColor;
        text.String = value.Header ? value.Text : "• " + value.Text;
        text.TextTooltip = value.Header ? "" : value.Text;
    }
}
