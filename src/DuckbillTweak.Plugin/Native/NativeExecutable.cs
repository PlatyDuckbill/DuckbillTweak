using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DuckbillTweak.Plugin.Native;

/// <summary>
/// Exact KR executable contract for the only features that depend on executable-specific bytes or addon offsets:
/// the PvP bubble instruction patch and the ShopExchangeCurrency compact row map/tab byte. Everything else uses the
/// installed Dalamud/ClientStructs plus its own UI shape and row checks. Evidence: docs/NATIVE-UI.md.
/// </summary>
internal sealed record NativeExecutable(bool Matches, nint ImageBase, string Reason)
{
    internal const string GameSha256 = "DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728";
    internal const string GameBuild = "2026.10.06";
    // Same image-size bound as the former NativeCompatibility guard; it covers every PvP patch RVA.
    private const int MinimumImageSize = 0xCD598E + 6;

    public static NativeExecutable Inspect()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var module = process.MainModule;
            if (module is null || !Path.GetFileName(module.FileName).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                return new(false, 0, "game process unavailable");
            using var stream = File.OpenRead(module.FileName);
            if (Convert.ToHexString(SHA256.HashData(stream)) != GameSha256)
                return new(false, 0, $"executable differs from inspected KR {GameBuild}");
            if (module.ModuleMemorySize <= MinimumImageSize)
                return new(false, 0, "game module size mismatch");
            return new(true, module.BaseAddress, $"KR {GameBuild}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            return new(false, 0, $"executable unreadable ({ex.GetType().Name})");
        }
    }
}
