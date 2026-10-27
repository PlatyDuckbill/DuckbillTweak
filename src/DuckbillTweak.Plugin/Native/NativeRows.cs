using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record CategoryOption(uint Id, string Name);
internal sealed record TotalRow(uint Icon, string Name, string Required, string Usable, string Stored, string Total, string Additional, string Details);
internal sealed class TotalRowNode : ListItemNode<TotalRow>, IListItemNode
{
    public static float ItemHeight => 30;
    private readonly IconImageNode icon = new() { Position = new(2, 3), Size = new(24, 24), FitTexture = true };
    private readonly TextNode name = new() { Position = new(32, 2), Size = new(176, 26), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode required = Number(214, 80);
    private readonly TextNode usable = Number(300, 90);
    private readonly TextNode stored = Number(396, 90);
    private readonly TextNode total = Number(492, 90);
    private readonly TextNode additional = Number(588, 90);
    private static TextNode Number(float x, float width) => new() { Position = new(x, 2), Size = new(width, 26), FontSize = 13, AlignmentType = AlignmentType.Right };
    public TotalRowNode()
    {
        icon.AttachNode(this); name.AttachNode(this); required.AttachNode(this); usable.AttachNode(this);
        stored.AttachNode(this); total.AttachNode(this); additional.AttachNode(this);
        EnableSelection = false;
        RemoveEvent(AtkEventType.MouseDown);
    }
    protected override void SetNodeData(TotalRow row)
    {
        icon.IconId = row.Icon; name.String = row.Name; required.String = row.Required; usable.String = row.Usable;
        stored.String = row.Stored; total.String = row.Total; additional.String = row.Additional;
        TextTooltip = row.Details;
        foreach (var node in new[] { name, required, usable, stored, total, additional }) node.TextTooltip = row.Details;
    }
}

internal sealed record CalculationRow(uint ItemId, uint Icon, string Name, string Quantity, string Cost, string Ownership);
internal sealed class CalculationRowNode : ListItemNode<CalculationRow>, IListItemNode
{
    public static float ItemHeight => 28;
    private readonly IconImageNode icon = new() { Position = new(2, 3), Size = new(22, 22), FitTexture = true };
    private readonly TextNode name = new() { Position = new(30, 1), Size = new(280, 26), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode quantity = new() { Position = new(316, 1), Size = new(52, 26), FontSize = 14, AlignmentType = AlignmentType.Right };
    private readonly TextNode cost = new() { Position = new(384, 1), Size = new(206, 26), FontSize = 13, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode ownership = new() { Position = new(596, 1), Size = new(82, 26), FontSize = 13, AlignmentType = AlignmentType.Right };
    public CalculationRowNode()
    {
        icon.AttachNode(this); name.AttachNode(this); quantity.AttachNode(this); cost.AttachNode(this); ownership.AttachNode(this);
        EnableSelection = false;
        RemoveEvent(AtkEventType.MouseDown);
    }
    protected override void SetNodeData(CalculationRow row)
    {
        icon.IconId = row.Icon; name.String = row.Name; quantity.String = row.Quantity; cost.String = row.Cost; ownership.String = row.Ownership;
        ItemTooltip = row.ItemId;
        cost.TextTooltip = row.Cost;
        name.TextTooltip = row.Name;
        ownership.TextTooltip = row.Ownership == "미확인" ? "보유·등록 여부를 확인하지 못했습니다.\n없는 물건으로 확정한 것은 아닙니다." : row.Ownership;
    }
}

internal sealed record NativeShopRow(int OriginalIndex, uint RawItemId, uint IconId, string Name,
    string Price, string Owned, uint CategoryId, bool Enabled, bool OwnedAlready, uint UnitPrice, uint Quantity);
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
