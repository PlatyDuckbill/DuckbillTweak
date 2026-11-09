using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>
/// Color picker window built from KamiToolKit's ColorPickerNode (ring, square, alpha bar, hex input) with Korean
/// buttons. KamiToolKit's prebuilt ColorPickerAddon labels its confirm button with Addon sheet text 572, which is
/// "Confirm" in global data but "받은 의뢰 확인" in the KR client, so users could not tell how to apply a color.
/// The upstream source is left unchanged; this window replaces it here.
/// </summary>
internal sealed unsafe class NativeColorPickerAddon : NativeAddon
{
    private ColorPickerNode? picker;
    private bool confirmed;

    /// <summary>
    /// Window title applied during setup. Setting NativeAddon.Title on a closed window throws (its WindowNode component
    /// is already released), which made every "변경" after the first close fail in 0.2.1.0.
    /// </summary>
    public string Heading { get; set; } = "색 선택";
    public Vector4 InitialColor { get; set; } = Vector4.One;
    public Vector4 DefaultColor { get; set; } = Vector4.One;
    /// <summary>Every change while the window is open (not saved).</summary>
    public Action<Vector4>? Previewed { get; set; }
    /// <summary>"확인": apply and save.</summary>
    public Action<Vector4>? Confirmed { get; set; }
    /// <summary>Closed without confirming (취소, close button, or replaced by another open).</summary>
    public Action? Cancelled { get; set; }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        confirmed = false;
        Title = Heading;
        picker = new ColorPickerNode { Position = ContentStartPosition, Size = ContentSize };
        picker.AttachNode(this);
        picker.RgbaColorPreviewed += color => Previewed?.Invoke(color);
        picker.SetColor(InitialColor);

        new HorizontalLineNode
        {
            Position = ContentStartPosition + new Vector2(2, ContentSize.Y - 40),
            Size = new Vector2(ContentSize.X - 4, 2),
        }.AttachNode(this);
        var buttonY = ContentSize.Y - 24;
        new TextButtonNode { Position = ContentStartPosition + new Vector2(0, buttonY), Size = new(100, 24), String = "확인", OnClick = Confirm }.AttachNode(this);
        new TextButtonNode { Position = ContentStartPosition + new Vector2(ContentSize.X / 2 - 50, buttonY), Size = new(100, 24), String = "기본값",
            OnClick = () => picker?.SetColor(DefaultColor) }.AttachNode(this);
        new TextButtonNode { Position = ContentStartPosition + new Vector2(ContentSize.X - 100, buttonY), Size = new(100, 24), String = "취소", OnClick = Close }.AttachNode(this);
    }

    private void Confirm()
    {
        if (picker is null) return;
        confirmed = true;
        Confirmed?.Invoke(ColorHelpers.HsvToRgb(picker.CurrentColor));
        Close();
    }

    protected override void OnHide(AtkUnitBase* addon)
    {
        if (!confirmed) Cancelled?.Invoke();
    }

    protected override void OnFinalize(AtkUnitBase* addon) => picker = null;
}
