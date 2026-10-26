using System.Numerics;
using Dalamud.Bindings.ImGui;
using DuckbillTweak.Plugin.Chat;

namespace DuckbillTweak.Plugin.UI;

internal static class BubbleRenderer
{
    public static void Draw(PvpBubbleService service, Configuration config)
    {
        if (!config.PvpBubbles) return;
        foreach (var bubble in service.View)
        {
            ImGui.SetNextWindowPos(bubble.Position, ImGuiCond.Always, new Vector2(0.5f, 1));
            ImGui.SetNextWindowBgAlpha(0.88f * bubble.Alpha);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, bubble.Alpha);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));
            var opened = ImGui.Begin($"###DuckbillBubble:{bubble.Id}", ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
            if (opened)
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Clamp(config.BubbleWidth, 180, 500));
                ImGui.TextColored(new Vector4(0.6f, 0.85f, 1, 1), bubble.Name);
                ImGui.TextUnformatted(bubble.Text);
                ImGui.PopTextWrapPos();
            }
            ImGui.End();
            ImGui.PopStyleVar(3);
        }
    }
}
