using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record LootPlayerRow(string Name, string Detail, string Choice, string Number, bool Winner, bool Left);

internal sealed class LootPlayerRowNode : ListItemNode<LootPlayerRow>, IListItemNode
{
    public static float ItemHeight => 28;
    private readonly TextNode name = new() { Position = new(2, 1), Size = new(180, 26), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode choice = new() { Position = new(190, 1), Size = new(104, 26), FontSize = 13, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode number = new() { Position = new(300, 1), Size = new(44, 26), FontSize = 16, AlignmentType = AlignmentType.Right };

    public LootPlayerRowNode()
    {
        name.AttachNode(this); choice.AttachNode(this); number.AttachNode(this);
        EnableSelection = false;
        RemoveEvent(AtkEventType.MouseDown);
    }

    protected override void SetNodeData(LootPlayerRow row)
    {
        name.String = (row.Winner ? "★ " : "") + row.Name;
        name.TextTooltip = row.Detail;
        choice.String = row.Choice;
        choice.TextTooltip = row.Detail;
        number.String = row.Number;
        number.TextTooltip = row.Number == "—" ? "숫자 미수신" : row.Detail;
        var standard = ColorHelper.GetColor(8);
        name.TextColor = row.Winner ? new(1f, .82f, .34f, 1f) : standard;
        choice.TextColor = standard; number.TextColor = row.Winner ? new(1f, .82f, .34f, 1f) : standard;
        Alpha = row.Left && !row.Winner ? .7f : 1f;
    }
}
