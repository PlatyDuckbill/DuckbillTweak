using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace DuckbillTweak.Plugin.Native;

/// <summary>The same native IconText component used by the game's status HUD; no ImGui overlay.</summary>
internal sealed unsafe class EmoteStatusIconNode : ComponentNode<AtkComponentIconText, AtkUldComponentDataIconText>
{
    private readonly IconImageNode image;
    private bool hovered;

    public uint IconId { get => image.IconId; set => image.IconId = value; }
    public bool TextureReady => image.IsTextureReady && image.LoadedIconId == image.IconId;
    // Hover events belong to the component's collision node, not the outer IconText owner.
    public override ReadOnlySeString TextTooltip
    { get => CollisionNode.TextTooltip; set => CollisionNode.TextTooltip = value; }
    public void ShowStatus(AtkUnitBase* addon)
    {
        if (IsVisible) return;
        IsVisible = true;
        CollisionNode.IsVisible = true;
        addon->UpdateCollisionNodeList(false);
    }
    public void HideStatus()
    {
        if (hovered) { CollisionNode.HideTooltip(); hovered = false; }
        CollisionNode.IsVisible = false;
        IsVisible = false;
    }
    public void RefreshHoveredTooltip() { if (hovered) CollisionNode.ShowTooltip(); }

    public EmoteStatusIconNode()
    {
        SetInternalComponentType(ComponentType.IconText);
        Size = new(24, 41);
        var text = new TextNode { NodeId = 2, Position = new(0, 23), Size = new(24, 18), FontSize = 12, String = "" };
        // Emote icons are square; status textures are portrait. Keep the HUD slot but never stretch the icon.
        image = new IconImageNode { NodeId = 3, Position = new(0, 4), Size = new(24, 24), FitTexture = true };
        // Status ULDs reserve node 4 for the top marker; this display has neither stacks nor a timer.
        var marker = new IconImageNode { NodeId = 4, Position = new(-4, -4), Size = new(32, 12), IsVisible = false };
        text.AttachNode(this); image.AttachNode(this); marker.AttachNode(this);
        Data->Nodes[0] = text.NodeId;
        Data->Nodes[1] = image.NodeId;
        InitializeComponentEvents();
        CollisionNode.Position = image.Position;
        CollisionNode.Size = image.Size;
        CollisionNode.AddEvent(AtkEventType.MouseOver, () => hovered = true);
        CollisionNode.AddEvent(AtkEventType.MouseOut, () => hovered = false);
        HideStatus();
    }
}
