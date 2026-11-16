using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace DuckbillTweak.Plugin.Native;

/// <summary>The same native IconText component used by the game's status HUD; no ImGui overlay.</summary>
internal sealed unsafe class EmoteStatusIconNode : ComponentNode<AtkComponentIconText, AtkUldComponentDataIconText>
{
    private readonly IconImageNode image;
    // ImGuiImageNode is a native AtkImageNode texture owner; it does not draw an ImGui overlay.
    private readonly ImGuiImageNode frame;
    private bool hovered;

    public uint IconId { get => image.IconId; set => image.IconId = value; }
    public bool TextureReady => image.IsTextureReady && image.LoadedIconId == image.IconId;
    public void LoadFrame(IDalamudTextureWrap texture) => frame.LoadTexture(texture);
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
        frame.AddColor = Vector3.Zero;
        CollisionNode.IsVisible = false;
        IsVisible = false;
    }
    public void RefreshHoveredTooltip() { if (hovered) CollisionNode.ShowTooltip(); }

    public EmoteStatusIconNode()
    {
        SetInternalComponentType(ComponentType.IconText);
        Size = new(24, 41);
        var text = new TextNode { NodeId = 2, Position = new(0, 23), Size = new(24, 18), FontSize = 12, String = "" };
        // Keep the original square artwork within the upward-pointing status outline.
        image = new IconImageNode
        {
            NodeId = 3, Position = new(2, 8), Size = new(20, 20), FitTexture = true,
            TextureSize = new(40, 40) // Emote textures are 40x40; IconImageNode's generic default is 32x32.
        };
        // Status ULDs reserve node 4 for the top marker; this display has neither stacks nor a timer.
        var marker = new IconImageNode { NodeId = 4, Position = new(-4, -4), Size = new(32, 12), IsVisible = false };
        text.AttachNode(this); image.AttachNode(this); marker.AttachNode(this);
        frame = new ImGuiImageNode
        {
            NodeId = 5, Size = new(24, 32), FitTexture = true,
            TextureSize = new(EmoteStatusFrame.Width, EmoteStatusFrame.Height)
        };
        frame.AttachNode(this);
        Data->Nodes[0] = text.NodeId;
        Data->Nodes[1] = image.NodeId;
        InitializeComponentEvents();
        CollisionNode.Position = frame.Position;
        CollisionNode.Size = new(24, 30);
        CollisionNode.AddEvent(AtkEventType.MouseOver, () => { hovered = true; frame.AddColor = new(.16f, .13f, .06f); });
        CollisionNode.AddEvent(AtkEventType.MouseOut, () => { hovered = false; frame.AddColor = Vector3.Zero; });
        HideStatus();
    }
}
