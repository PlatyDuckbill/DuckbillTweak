using Dalamud;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Native;

namespace DuckbillTweak.Plugin.Chat;

/// <summary>Only relaxes the native bubble suppression gates while inside PvP. Native settings/rendering remain in control.</summary>
internal sealed class NativePvpBubbles : IDisposable
{
    private sealed record Patch(int Rva, byte[] Original, byte[] Replacement);
    // Verified against the exact KR executable above; not upstream signatures or global-client offsets.
    private static readonly Patch[] Patches =
    [
        new(0xCD5682, [0x40, 0xB6, 0x01], [0x40, 0x30, 0xF6]),
        new(0xCD568A, [0xE8, 0xE1, 0xC6, 0xB6, 0xFF], [0x31, 0xC0, 0x90, 0x90, 0x90]),
        new(0xCD571E, [0x40, 0xB6, 0x01], [0x90, 0x90, 0x90]),
    ];
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly NativeCompatibility compatibility;
    private readonly List<Patch> applied = [];
    private bool failed;

    public NativePvpBubbles(IFramework framework, IClientState client, IPluginLog log,
        Configuration config, NativeCompatibility compatibility)
    {
        this.framework = framework; this.client = client; this.log = log;
        this.config = config; this.compatibility = compatibility;
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        var enabled = compatibility.Supported && config.PvpBubbles && client.IsLoggedIn && client.IsPvPExcludingDen;
        if (!enabled) { Restore(); failed = false; return; }
        if (applied.Count != 0 || failed) return;
        if (Patches.Any(p => !Matches(p, p.Original)))
        {
            failed = true;
            log.Warning("PvP chat bubbles: inspected instruction bytes do not match; native patch not applied.");
            return;
        }
        foreach (var patch in Patches)
        {
            if (!SafeMemory.WriteBytes(compatibility.ImageBase + patch.Rva, patch.Replacement))
            {
                failed = true; Restore();
                log.Error("PvP chat bubbles: native patch write failed.");
                return;
            }
            applied.Add(patch);
        }
    }

    private bool Matches(Patch patch, byte[] expected) =>
        SafeMemory.ReadBytes(compatibility.ImageBase + patch.Rva, expected.Length, out var bytes) && bytes.AsSpan().SequenceEqual(expected);

    private void Restore()
    {
        for (var i = applied.Count - 1; i >= 0; i--)
        {
            var patch = applied[i];
            if (Matches(patch, patch.Replacement))
            {
                if (!SafeMemory.WriteBytes(compatibility.ImageBase + patch.Rva, patch.Original))
                    log.Error("PvP chat bubbles: restoring instruction at {Rva:X} failed.", patch.Rva);
            }
            else log.Warning("PvP chat bubbles: instruction at {Rva:X} changed externally; not overwriting.", patch.Rva);
        }
        applied.Clear();
    }

    public void Dispose() { framework.Update -= Update; Restore(); }
}
