namespace DuckbillTweak.Core.Loot;

public readonly record struct PlayerKey(string Name, uint WorldId);
public enum RollKind { Unknown, Need, Greed, Pass }
public sealed record ObservedRoll(PlayerKey Player, RollKind Kind, string KindText, int Value);
public sealed record LootMember(PlayerKey Player, bool OwnParty);

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
    public Dictionary<PlayerKey, RollKind> Choices { get; } = [];
    public List<LootMember> Members { get; } = [];
    public HashSet<PlayerKey> Left { get; } = [];
    internal Dictionary<PlayerKey, int> MissingSamples { get; } = [];
    public PlayerKey? Winner { get; internal set; }
    public bool Ambiguous { get; internal set; }
    public DateTimeOffset? EndedAt { get; internal set; }
    public bool IsOpen => Winner is null && EndedAt is null;
}

public sealed class LootLedger
{
    private long sequence;
    private readonly List<LootEntry> entries = [];
    public IReadOnlyList<LootEntry> Entries => entries;

    public void Clear() => entries.Clear();

    public void MarkAmbiguous(uint rawItemId)
    {
        foreach (var entry in entries.Where(e => e.RawItemId == rawItemId && e.IsOpen)) entry.Ambiguous = true;
    }

    public LootEntry Add(uint rawItemId, string name, bool alliance, DateTimeOffset now)
    {
        var duplicates = entries.Where(x => x.RawItemId == rawItemId && x.IsOpen).ToArray();
        var entry = new LootEntry(++sequence, rawItemId, name, alliance, now) { Ambiguous = duplicates.Length > 0 };
        foreach (var duplicate in duplicates) duplicate.Ambiguous = true;
        entries.Add(entry);
        if (entries.Count > 48) entries.RemoveAt(0);
        return entry;
    }

    public bool Observe(uint rawItemId, string name, bool alliance, PlayerKey player, ObservedRoll? roll,
        bool awarded, DateTimeOffset now)
    {
        // Native disappearance and the final log can be delivered in adjacent frames.
        // Include both old and new candidates: never attach a delayed result to a new
        // identical drop merely because that is the only currently open entry.
        var candidates = entries.Where(x => x.RawItemId == rawItemId &&
            (x.IsOpen || x.EndedAt is { } ended && now - ended < TimeSpan.FromSeconds(3))).ToArray();
        // Item-only log events cannot identify two identical simultaneous drops.
        if (candidates.Length > 1 || candidates is [{ Ambiguous: true }]) return false;
        var entry = candidates.SingleOrDefault();
        if (entry is null)
        {
            if (awarded) return false;
            entry = Add(rawItemId, name, alliance, now);
        }
        entry.LastEventAt = now;
        if (awarded) { entry.Winner = player; entry.EndedAt ??= now; }
        else if (roll is not null && roll.Value is >= 1 and <= 99)
        { entry.Rolls[player] = roll; entry.Choices[player] = roll.Kind; entry.Decided.Add(player); }
        else entry.Decided.Add(player);
        return true;
    }

    public void Prune(DateTimeOffset now) => entries.RemoveAll(x => now - x.LastEventAt > TimeSpan.FromMinutes(10));

    public void ObserveChoice(LootEntry entry, PlayerKey player, RollKind choice)
    {
        if (!entry.IsOpen || entry.Ambiguous || choice == RollKind.Unknown) return;
        // A public result takes precedence over an earlier local snapshot.
        if (!entry.Rolls.ContainsKey(player)) entry.Choices[player] = choice;
        entry.Decided.Add(player);
    }

    public void CaptureRoster(LootEntry entry, IReadOnlyList<PlayerKey> own, IReadOnlyList<PlayerKey> other, bool observeDepartures)
    {
        if (!entry.IsOpen || own.Count == 0) return;
        var current = own.Concat(entry.Alliance ? other : []).Where(p => !string.IsNullOrWhiteSpace(p.Name)).Distinct().ToHashSet();
        foreach (var member in current)
            if (!entry.Members.Any(m => m.Player == member)) entry.Members.Add(new(member, own.Contains(member)));
        if (!observeDepartures) return;
        foreach (var member in entry.Members)
        {
            if (current.Contains(member.Player)) { entry.MissingSamples.Remove(member.Player); entry.Left.Remove(member.Player); continue; }
            // Two consecutive roster snapshots avoid treating a transient empty slot as departure.
            var samples = entry.MissingSamples.GetValueOrDefault(member.Player) + 1;
            entry.MissingSamples[member.Player] = samples;
            if (samples >= 2) entry.Left.Add(member.Player);
        }
    }

    public void End(long id, DateTimeOffset now)
    {
        var entry = entries.FirstOrDefault(e => e.Id == id);
        if (entry is null || !entry.IsOpen) return;
        entry.EndedAt = now; entry.LastEventAt = now;
        // No winner/roll is not evidence of a pass or a timeout.
    }

    public static IReadOnlyDictionary<PlayerKey, int> Ranks(LootEntry entry)
    {
        if (entry.Ambiguous) return new Dictionary<PlayerKey, int>();
        var rolls = entry.Rolls.Values.Where(r => r.Kind is RollKind.Need or RollKind.Greed && r.Value is >= 1 and <= 99).ToArray();
        return rolls.ToDictionary(r => r.Player, r => 1 + rolls.Count(other =>
            (other.Kind == RollKind.Need && r.Kind == RollKind.Greed) || other.Kind == r.Kind && other.Value > r.Value));
    }
}
