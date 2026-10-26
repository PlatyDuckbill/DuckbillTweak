namespace DuckbillTweak.Core.Loot;

public readonly record struct PlayerKey(string Name, uint WorldId);
public enum RollKind { Unknown, Need, Greed }
public sealed record ObservedRoll(PlayerKey Player, RollKind Kind, string KindText, int Value);

public sealed class LootEntry(long id, uint rawItemId, string name, bool alliance, DateTimeOffset createdAt)
{
    public long Id { get; } = id;
    public uint RawItemId { get; } = rawItemId;
    public string Name { get; } = name;
    public bool Alliance { get; } = alliance;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public DateTimeOffset LastEventAt { get; internal set; } = createdAt;
    public Dictionary<PlayerKey, ObservedRoll> Rolls { get; } = [];
    public HashSet<PlayerKey> Decided { get; } = [];
    public PlayerKey? Winner { get; internal set; }
    public bool Ambiguous { get; internal set; }
}

public sealed class LootLedger
{
    private long sequence;
    private readonly List<LootEntry> entries = [];
    public IReadOnlyList<LootEntry> Entries => entries;

    public void Clear() => entries.Clear();

    public void MarkAmbiguous(uint rawItemId)
    {
        foreach (var entry in entries.Where(e => e.RawItemId == rawItemId && e.Winner is null)) entry.Ambiguous = true;
    }

    public LootEntry Add(uint rawItemId, string name, bool alliance, DateTimeOffset now)
    {
        var duplicates = entries.Where(x => x.RawItemId == rawItemId && x.Winner is null).ToArray();
        var entry = new LootEntry(++sequence, rawItemId, name, alliance, now) { Ambiguous = duplicates.Length > 0 };
        foreach (var duplicate in duplicates) duplicate.Ambiguous = true;
        entries.Add(entry);
        if (entries.Count > 48) entries.RemoveAt(0);
        return entry;
    }

    public bool Observe(uint rawItemId, string name, bool alliance, PlayerKey player, ObservedRoll? roll,
        bool awarded, DateTimeOffset now)
    {
        var candidates = entries.Where(x => x.RawItemId == rawItemId && x.Winner is null).ToArray();
        // Item-only log events cannot identify two identical simultaneous drops.
        if (candidates.Length > 1 || candidates is [{ Ambiguous: true }]) return false;
        var entry = candidates.SingleOrDefault();
        if (entry is null)
        {
            if (awarded) return false;
            entry = Add(rawItemId, name, alliance, now);
        }
        entry.LastEventAt = now;
        if (awarded) entry.Winner = player;
        else if (roll is not null && roll.Value is >= 1 and <= 99) entry.Rolls[player] = roll;
        else entry.Decided.Add(player);
        return true;
    }

    public void Prune(DateTimeOffset now) => entries.RemoveAll(x => now - x.LastEventAt > TimeSpan.FromMinutes(10));
}
