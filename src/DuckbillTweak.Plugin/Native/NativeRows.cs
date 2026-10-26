using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record CategoryOption(uint Id, string Name);
internal sealed record TotalRow(string Name, string Value);
internal sealed class TotalRowNode : ListItemNode<TotalRow>, IListItemNode
{
    public static float ItemHeight => 48;
    private readonly TextNode name = new() { Position = new(4, 0), Size = new(540, 20), FontSize = 14 };
    private readonly TextNode value = new() { Position = new(4, 22), Size = new(540, 22), FontSize = 13 };
    public TotalRowNode() { name.AttachNode(this); value.AttachNode(this); DisableInteractions(); }
    protected override void SetNodeData(TotalRow row) { name.String = row.Name; value.String = row.Value; }
}

internal sealed record NativeShopRow(int OriginalIndex, uint RawItemId, uint IconId, string Name,
    string Price, string Owned, uint CategoryId, bool Enabled = true);
internal sealed unsafe class NativeShopRowNode : ListItemNode<NativeShopRow>, IListItemNode
{
    public static float ItemHeight => 26;
    private readonly IconImageNode icon = new() { Position = new(6, 2), Size = new(22, 22), FitTexture = true };
    private readonly TextNode name = new() { Position = new(32, 1), Size = new(268, 24), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode price = new() { Position = new(325, 1), Size = new(98, 24), FontSize = 14, AlignmentType = AlignmentType.Right };
    private readonly TextNode owned = new() { Position = new(435, 1), Size = new(74, 24), FontSize = 14, AlignmentType = AlignmentType.Right };
    public NativeShopRowNode()
    {
        icon.AttachNode(this); name.AttachNode(this); price.AttachNode(this); owned.AttachNode(this);
        RemoveEvent(AtkEventType.MouseDown);
        AddEvent(AtkEventType.MouseUp, (_, _, _, _, data) =>
        {
            if (EnableSelection && data is not null && data->MouseData.ButtonId == 0) OnClick?.Invoke(this);
        });
    }
    protected override void SetNodeData(NativeShopRow row)
    {
        icon.IconId = row.IconId; name.String = row.Name; price.String = row.Price; owned.String = row.Owned; ItemTooltip = row.RawItemId;
        EnableSelection = row.Enabled; EnableHighlight = row.Enabled; Alpha = row.Enabled ? 1 : 0.5f;
    }
}
