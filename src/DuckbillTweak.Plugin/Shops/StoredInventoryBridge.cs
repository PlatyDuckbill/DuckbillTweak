using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace DuckbillTweak.Plugin.Shops;

/// <summary>Optional positive ownership evidence from the current character's stored outfits.</summary>
internal sealed class StoredInventoryBridge
{
    // Public Allagan Tools IPC, verified against the installed 1.15.0.13 provider.
    // Its storage IDs are custom IDs, not FFXIV InventoryType values.
    private readonly ICallGateSubscriber<uint, ulong, int, uint> count;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, bool> found = [];
    private ulong character;
    private bool reported;
    private bool failed;
    private bool? lastAvailable;
    public bool Available => count.HasFunction;
    public bool HasEvidence { get; private set; }

    public StoredInventoryBridge(IDalamudPluginInterface plugin, IPluginLog log)
    {
        count = plugin.GetIpcSubscriber<uint, ulong, int, uint>("AllaganTools.ItemCount");
        this.log = log;
    }

    public void Refresh(ulong contentId)
    {
        if (character != contentId) Clear();
        character = contentId;
        if (lastAvailable != Available)
        {
            lastAvailable = Available;
            log.Information("Allagan Tools storage IPC available: {Available}", lastAvailable);
        }
        found.Clear();
        HasEvidence = false;
    }

    public bool Contains(uint itemId)
    {
        if (character == 0 || !Available) return false;
        if (found.TryGetValue(itemId, out var known)) return known;
        try
        {
            // No retainers, other characters, bags, or currency balances are queried.
            var owned = count.InvokeFunc(itemId, character, 2501) > 0 || count.InvokeFunc(itemId, character, 2500) > 0;
            found[itemId] = owned;
            HasEvidence |= owned;
            if (owned && !reported)
            {
                reported = true;
                log.Information("Stored ownership confirmed through Allagan Tools IPC for the current character.");
            }
            return owned;
        }
        catch (Exception ex)
        {
            // Optional provider may unload. A failed/empty query is never proof of absence.
            found[itemId] = false;
            if (!failed) log.Warning("Allagan Tools storage query unavailable ({Kind}); ownership remains unknown.", ex.GetType().Name);
            failed = true;
            return false;
        }
    }

    public void Clear() { character = 0; found.Clear(); HasEvidence = false; reported = false; failed = false; lastAvailable = null; }
}
