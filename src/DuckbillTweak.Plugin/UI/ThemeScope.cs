using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DuckbillTweak.Plugin.UI;

// Same suite palette as Fisher Graphite / Calendar Charcoal; scoped to plugin management only.
internal sealed class ThemeScope : IDisposable
{
    private int colors;
    public static Vector4 Accent(bool light) => Hex(light ? 0x356c64u : 0x8acdbdu);
    public ThemeScope(bool light)
    {
        var bg = Hex(light ? 0xeeece5u : 0x191c1eu);
        var surface = Hex(light ? 0xfaf9f4u : 0x24282au);
        var control = Hex(light ? 0xe0e4dcu : 0x303639u);
        var input = Hex(light ? 0xffffffu : 0x1c2022u);
        var hover = Hex(light ? 0xe6eee9u : 0x2d3537u);
        var selection = Hex(light ? 0xdbe8e1u : 0x344541u);
        var accent = Accent(light);
        Add(ImGuiCol.Text, Hex(light ? 0x2a342fu : 0xeef0ecu));
        Add(ImGuiCol.TextDisabled, Hex(light ? 0x59665fu : 0xa8b1b2u));
        Add(ImGuiCol.WindowBg, surface); Add(ImGuiCol.ChildBg, surface); Add(ImGuiCol.PopupBg, surface);
        Add(ImGuiCol.TitleBg, bg); Add(ImGuiCol.TitleBgActive, bg); Add(ImGuiCol.TitleBgCollapsed, bg);
        Add(ImGuiCol.Border, Hex(light ? 0x87958du : 0x454e51u));
        Add(ImGuiCol.Separator, Hex(light ? 0xd6dcd5u : 0x353e40u));
        Add(ImGuiCol.Button, control); Add(ImGuiCol.ButtonHovered, hover); Add(ImGuiCol.ButtonActive, selection);
        Add(ImGuiCol.FrameBg, input); Add(ImGuiCol.FrameBgHovered, hover); Add(ImGuiCol.FrameBgActive, selection);
        Add(ImGuiCol.Header, selection); Add(ImGuiCol.HeaderHovered, hover); Add(ImGuiCol.HeaderActive, selection);
        Add(ImGuiCol.CheckMark, accent); Add(ImGuiCol.SliderGrab, accent); Add(ImGuiCol.SliderGrabActive, accent);
        Add(ImGuiCol.ScrollbarBg, bg); Add(ImGuiCol.ScrollbarGrab, control); Add(ImGuiCol.ScrollbarGrabHovered, hover);
        Add(ImGuiCol.ScrollbarGrabActive, accent); Add(ImGuiCol.TextSelectedBg, selection);
        var f = ImGui.GetFontSize();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(f * .8f, f * .65f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(f * .5f, f * .24f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(f * .5f, f * .4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(f * .35f, f * .25f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, f * .28f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, f * .28f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, f * .2f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, f * .28f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
    }
    private void Add(ImGuiCol key, Vector4 color) { ImGui.PushStyleColor(key, color); colors++; }
    public static Vector4 Hex(uint rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1);
    public void Dispose() { ImGui.PopStyleVar(11); ImGui.PopStyleColor(colors); }
}
