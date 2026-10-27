using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record LootPlayerRow(string Name, string Detail, string Choice, string Number, int? Rank, bool Tied, bool Winner, bool Left);

internal sealed class LootPlayerRowNode : ListItemNode<LootPlayerRow>, IListItemNode
{
    public static float ItemHeight => 28;
    private readonly TextNode name = new() { Position = new(2, 1), Size = new(142, 26), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode choice = new() { Position = new(150, 1), Size = new(98, 26), FontSize = 13, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode number = new() { Position = new(250, 1), Size = new(40, 26), FontSize = 16, AlignmentType = AlignmentType.Right };
    private readonly TextNode rank = new() { Position = new(296, 1), Size = new(40, 26), FontSize = 13, AlignmentType = AlignmentType.Right };

    public LootPlayerRowNode()
    {
        name.AttachNode(this); choice.AttachNode(this); number.AttachNode(this); rank.AttachNode(this);
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
        rank.String = row.Rank is { } place ? (row.Tied ? "=" : "") + place + "위" : "—";
        rank.TextTooltip = row.Rank is { } r ? $"수신된 결과 기준 {(row.Tied ? "공동 " : "")}{r}위\n선입찰이 입찰보다 우선합니다. 실제 획득자는 별도로 표시합니다." : "순위를 확정할 숫자/입찰 종류가 없습니다.";
        var standard = ColorHelper.GetColor(8);
        var highlight = row.Rank switch
        {
            1 => new Vector4(1f, .82f, .34f, 1f),
            2 => new Vector4(.65f, .84f, 1f, 1f),
            3 => new Vector4(.95f, .66f, .46f, 1f),
            _ => standard,
        };
        name.TextColor = row.Winner ? new(1f, .82f, .34f, 1f) : standard;
        choice.TextColor = standard; number.TextColor = rank.TextColor = highlight;
        Alpha = row.Left && !row.Winner ? .7f : 1f;
    }
}
