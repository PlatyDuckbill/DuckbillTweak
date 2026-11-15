using System.Numerics;

namespace DuckbillTweak.Plugin.UI;

internal sealed class ThemeScope(bool light) : IDisposable
{
    private readonly GameThemeScope scope = new(light);
    public static bool IsLight(string? theme) => theme is "pearl" or "paper" or "cloud" or "light";
    public static Vector4 Accent(bool light) => GameThemePalette.For(light).Accent;
    public static Vector4 Hex(uint rgb) => GameThemePalette.Hex(rgb);
    public void Dispose() => scope.Dispose();
}
