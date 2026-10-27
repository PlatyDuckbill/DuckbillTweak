using DuckbillTweak.Core.Loot;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GameLoot = FFXIVClientStructs.FFXIV.Client.Game.UI.Loot;

namespace DuckbillTweak.Plugin.Loot;

/// <summary>Read-only local choice and lifetime observation under the inspected KR profile.</summary>
internal sealed unsafe class NativeLootObserver
{
    private readonly record struct Identity(uint Chest, uint Slot, uint Item);
    private readonly Dictionary<Identity, (long Entry, int Missing)> tracked = [];
    public bool Enabled { get; set; }

    public void Update(LootLedger ledger, PlayerKey self, DateTimeOffset now)
    {
        if (!Enabled || string.IsNullOrEmpty(self.Name)) return;
        var loot = GameLoot.Instance();
        if (loot is null || loot->Items.Length != 16) return;
        var live = new HashSet<Identity>();
        var counts = new Dictionary<uint, int>();
        foreach (ref var item in loot->Items)
        {
            if (item.ItemId == 0) continue;
            // Reject an inconsistent snapshot instead of interpreting it as disappearance.
            if (item.ItemId >= 2_000_000 || item.ChestItemIndex >= 16 || !Enum.IsDefined(item.RollResult)) return;
            live.Add(new(item.ChestObjectId, item.ChestItemIndex, item.ItemId));
            if (item.RollResult == RollResult.Awarded) continue;
            counts[item.ItemId] = counts.GetValueOrDefault(item.ItemId) + 1;
        }
        foreach (ref var item in loot->Items)
        {
            if (item.ItemId == 0 || item.RollResult == RollResult.Awarded) continue;
            if (counts[item.ItemId] > 1) { ledger.MarkAmbiguous(item.ItemId); continue; }
            var key = new Identity(item.ChestObjectId, item.ChestItemIndex, item.ItemId);
            var rawId = item.ItemId;
            var entry = tracked.TryGetValue(key, out var link) ? ledger.Entries.FirstOrDefault(e => e.Id == link.Entry) :
                ledger.Entries.SingleOrDefault(e => e.RawItemId == rawId && e.IsOpen && !e.Ambiguous);
            if (entry is null) continue;
            tracked[key] = (entry.Id, 0);
            var choice = item.RollResult switch { RollResult.Needed => RollKind.Need, RollResult.Greeded => RollKind.Greed,
                RollResult.Passed => RollKind.Pass, _ => RollKind.Unknown };
            ledger.ObserveChoice(entry, self, choice);
            // Do not read RollValue: numbers remain based on the public result log.
        }
        foreach (var (key, value) in tracked.ToArray())
        {
            if (live.Contains(key)) continue;
            if (value.Missing < 1) { tracked[key] = (value.Entry, value.Missing + 1); continue; }
            ledger.End(value.Entry, now);
            tracked.Remove(key);
        }
    }

    public void Clear() => tracked.Clear();
}
