using DuckbillTweak.Core.Loot;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GameLoot = FFXIVClientStructs.FFXIV.Client.Game.UI.Loot;

namespace DuckbillTweak.Plugin.Loot;

/// <summary>Read-only local choice and lifetime observation under the inspected KR profile.</summary>
internal sealed unsafe class NativeLootObserver
{
    private readonly record struct Identity(uint Chest, uint Slot, uint Item);
    private readonly Dictionary<Identity, (long Entry, int Missing)> tracked = [];
    private readonly HashSet<Identity> present = [];
    private DateTimeOffset lastMissingSample;
    public bool Enabled { get; set; }

    public LootEntry? FindCurrentEntry(LootLedger ledger, uint rawItemId, bool duplicate)
    {
        var matches = tracked.Where(p => p.Key.Item == rawItemId && present.Contains(p.Key))
            .Select(p => ledger.Entries.FirstOrDefault(e => e.Id == p.Value.Entry)).OfType<LootEntry>().Distinct().ToArray();
        // Never select a historical completed entry by item ID alone. The reward must
        // still be linked to a chest/slot in the latest valid native snapshot.
        if (matches.Length == 1) return matches[0];
        if (!duplicate || matches.Length == 0) return null;
        foreach (var entry in matches) ledger.MarkAmbiguous(entry);
        return matches[0];
    }

    public void Update(LootLedger ledger, PlayerKey self, Func<uint, LootEntry?> createEntry, DateTimeOffset now)
    {
        present.Clear();
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
            if (!live.Add(new(item.ChestObjectId, item.ChestItemIndex, item.ItemId))) return;
            if (item.RollResult == RollResult.Awarded) continue;
            counts[item.ItemId] = counts.GetValueOrDefault(item.ItemId) + 1;
        }
        foreach (ref var item in loot->Items)
        {
            if (item.ItemId == 0) continue;
            var key = new Identity(item.ChestObjectId, item.ChestItemIndex, item.ItemId);
            var rawId = item.ItemId;
            var entry = tracked.TryGetValue(key, out var link) ? ledger.Entries.FirstOrDefault(e => e.Id == link.Entry) : null;
            if (entry is null)
            {
                // Adopt an item-only log record once, or start tracking directly from
                // the native reward. Opening our window is never needed to seed it.
                var candidates = ledger.Entries.Where(e => e.RawItemId == rawId && e.IsOpen &&
                    !tracked.Values.Any(t => t.Entry == e.Id)).ToArray();
                if (candidates.Length == 1) entry = candidates[0];
                else if (item.RollResult != RollResult.Awarded) entry = createEntry(rawId);
                if (entry is null) continue;
            }
            tracked[key] = (entry.Id, 0);
            ledger.ObservePresence(entry, now);
            if (item.RollResult == RollResult.Awarded)
            {
                ledger.End(entry.Id, now);
                continue;
            }
            if (counts[rawId] > 1) { ledger.MarkAmbiguous(rawId); continue; }
            var choice = item.RollResult switch { RollResult.Needed => RollKind.Need, RollResult.Greeded => RollKind.Greed,
                RollResult.Passed => RollKind.Pass, _ => RollKind.Unknown };
            ledger.ObserveChoice(entry, self, choice);
            // Do not read RollValue: numbers remain based on the public result log.
        }
        // Preserve the original one-second disappearance sampling while choices are
        // read every Framework update. A transient empty frame must not end a reward.
        if (now - lastMissingSample >= TimeSpan.FromSeconds(1))
        {
            lastMissingSample = now;
            foreach (var (key, value) in tracked.ToArray())
            {
                if (live.Contains(key)) continue;
                if (value.Missing < 1) { tracked[key] = (value.Entry, value.Missing + 1); continue; }
                ledger.End(value.Entry, now);
                tracked.Remove(key);
            }
        }
        present.UnionWith(live);
    }

    public void Clear() { tracked.Clear(); present.Clear(); lastMissingSample = default; }
}
