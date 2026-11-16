using System.Numerics;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Original upward-pointing frame; the emote's square artwork remains unchanged in the opening.</summary>
internal static class EmoteStatusFrame
{
    public const int Width = 96, Height = 128;
    public static readonly byte[] Rgba = Build();

    private static byte[] Build()
    {
        // Logical size 24x32, rendered at 4x for smooth edges at the game's HUD scales.
        Vector2[] outer = [new(12, .5f), new(23.5f, 6.5f), new(23.5f, 28.5f), new(22, 30),
            new(2, 30), new(.5f, 28.5f), new(.5f, 6.5f)];
        Vector2[] inner = [new(12, 2), new(22, 7.5f), new(22, 27.8f), new(21.2f, 28.7f),
            new(2.8f, 28.7f), new(2, 27.8f), new(2, 7.5f)];
        var pixels = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var point = new Vector2((x + .5f) / 4, (y + .5f) / 4);
            if (!Contains(outer, point)) continue;
            // An exact 20x20 opening preserves the full icon and every original background color.
            if (point.X is >= 2 and < 22 && point.Y is >= 8 and < 28) continue;
            var edge = !Contains(inner, point);
            var color = edge
                ? Vector3.Lerp(new(.91f, .90f, .83f), new(.46f, .45f, .40f), point.Y / 32)
                : Vector3.Lerp(new(.29f, .29f, .27f), new(.15f, .16f, .16f), point.Y / 32);
            var offset = (y * Width + x) * 4;
            pixels[offset] = (byte)(color.X * 255);
            pixels[offset + 1] = (byte)(color.Y * 255);
            pixels[offset + 2] = (byte)(color.Z * 255);
            pixels[offset + 3] = 255;
        }
        return pixels;
    }

    private static bool Contains(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        for (var i = 0; i < polygon.Length; i++)
        {
            var start = polygon[i];
            var edge = polygon[(i + 1) % polygon.Length] - start;
            var relative = point - start;
            if (edge.X * relative.Y - edge.Y * relative.X < 0) return false;
        }
        return true;
    }
}
