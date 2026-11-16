using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>The same native IconText component used by the game's status HUD; no ImGui overlay.</summary>
internal sealed unsafe class EmoteStatusIconNode : ComponentNode<AtkComponentIconText, AtkUldComponentDataIconText>
{
    private readonly IconImageNode image;
    private bool hovered;

    public uint IconId { get => image.IconId; set => image.IconId = value; }
    public bool TextureReady => image.IsTextureReady && image.LoadedIconId == image.IconId;
    public void HideStatus()
    {
        if (hovered) { HideTooltip(); hovered = false; }
        IsVisible = false;
    }
    public void RefreshHoveredTooltip() { if (hovered) ShowTooltip(); }

    public EmoteStatusIconNode()
    {
        SetInternalComponentType(ComponentType.IconText);
        Size = new(24, 41);
        var text = new TextNode { NodeId = 2, Position = new(0, 23), Size = new(24, 18), FontSize = 12, String = "" };
        image = new IconImageNode { NodeId = 3, Size = new(24, 32), FitTexture = true };
        // Status ULDs reserve node 4 for the top marker; this display has neither stacks nor a timer.
        var marker = new IconImageNode { NodeId = 4, Position = new(-4, -4), Size = new(32, 12), IsVisible = false };
        text.AttachNode(this); image.AttachNode(this); marker.AttachNode(this);
        Data->Nodes[0] = text.NodeId;
        Data->Nodes[1] = image.NodeId;
        InitializeComponentEvents();
        AddEvent(AtkEventType.MouseOver, () => hovered = true);
        AddEvent(AtkEventType.MouseOut, () => hovered = false);
        IsVisible = false;
    }
}
