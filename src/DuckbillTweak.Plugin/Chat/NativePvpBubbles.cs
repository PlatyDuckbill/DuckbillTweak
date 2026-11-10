using Dalamud;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Native;

namespace DuckbillTweak.Plugin.Chat;

/// <summary>Only relaxes the native bubble suppression gates while inside PvP. Native settings/rendering remain in control.</summary>
internal sealed class NativePvpBubbles : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private readonly Configuration config;
    // Resolved in the running game from KR instruction signatures (NativeCode); empty when the contract failed.
    private readonly NativePatch[] patches;
    private readonly List<NativePatch> applied = [];
    private bool failed;

    public NativePvpBubbles(IFramework framework, IClientState client, IPluginLog log,
        Configuration config, NativeCodeResult<NativePatch[]> code)
    {
        this.framework = framework; this.client = client; this.log = log;
        this.config = config; patches = code.Value ?? [];
        if (!code.Available) log.Warning("PvP chat bubbles disabled: {Reason}", code.Reason);
        framework.Update += Update;
    }

    public bool Available => patches.Length != 0;

    private void Update(IFramework _)
    {
        var enabled = Available && config.PvpBubbles && client.IsLoggedIn && client.IsPvPExcludingDen;
        if (!enabled) { Restore(); failed = false; return; }
        if (applied.Count != 0 || failed) return;
        if (patches.Any(p => !Matches(p, p.Original)))
        {
            failed = true;
            log.Warning("PvP chat bubbles: inspected instruction bytes do not match; native patch not applied.");
            return;
        }
        foreach (var patch in patches)
        {
            if (!SafeMemory.WriteBytes(patch.Address, patch.Replacement))
            {
                failed = true; Restore();
                log.Error("PvP chat bubbles: native patch write failed.");
                return;
            }
            applied.Add(patch);
        }
    }

    private static bool Matches(NativePatch patch, byte[] expected) =>
        SafeMemory.ReadBytes(patch.Address, expected.Length, out var bytes) && bytes.AsSpan().SequenceEqual(expected);

    private void Restore()
    {
        for (var i = applied.Count - 1; i >= 0; i--)
        {
            var patch = applied[i];
            if (Matches(patch, patch.Replacement))
            {
                if (!SafeMemory.WriteBytes(patch.Address, patch.Original))
                    log.Error("PvP chat bubbles: restoring instruction at {Address:X} failed.", patch.Address);
            }
            else log.Warning("PvP chat bubbles: instruction at {Address:X} changed externally; not overwriting.", patch.Address);
        }
        applied.Clear();
    }

    public void Dispose() { framework.Update -= Update; Restore(); }
}
