using System.Diagnostics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;

namespace DuckbillTweak.Plugin.Shops;

internal enum StorageRole { Personal, Appearance, Shared, Listed, Equipped }
internal sealed record StoredLocation(string Name, long Quantity, StorageRole Role);
internal sealed record StoredCurrency(bool Available, IReadOnlyList<StoredLocation> Locations)
{
    public long Personal => Locations.Where(x => x.Role == StorageRole.Personal).Sum(x => x.Quantity);
}

/// <summary>Managed public Allagan Tools records; no gameplay or provider file access.</summary>
internal sealed class StoredInventoryBridge
{
    private readonly ICallGateSubscriber<ulong> active;
    private readonly ICallGateSubscriber<bool> initialized;
    private readonly ICallGateSubscriber<bool, HashSet<ulong>> owners;
    private readonly ICallGateSubscriber<ulong, HashSet<ulong[]>> items;
    private readonly IPluginLog log;
    private Dictionary<CurrencyKey, List<StoredLocation>> currencies = [];
    private HashSet<uint> owned = [];
    private HashSet<uint> appearances = [];
    private ulong character;
    private long lastRead;
    private bool failed;
    private (int Owners, int Items)? reported;
    public bool Available { get; private set; }
    public string Help => Available
        ? "Allagan Tools 저장 기록: 현재 캐릭터·소속 집사·초코보 가방·보관함 및 연결된 공유 창고.\n기록이 없는 곳은 미확인입니다. 기록의 실제 갱신 시각/인출 권한은 확인할 수 없습니다."
        : "전체 보관 기록 미확인: Allagan Tools를 활성화하고 각 보관함을 열어 기록해야 합니다.";

    public StoredInventoryBridge(IDalamudPluginInterface plugin, IPluginLog log)
    {
        active = plugin.GetIpcSubscriber<ulong>("AllaganTools.CurrentCharacter");
        initialized = plugin.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
        owners = plugin.GetIpcSubscriber<bool, HashSet<ulong>>("AllaganTools.GetCharactersOwnedByActive");
        items = plugin.GetIpcSubscriber<ulong, HashSet<ulong[]>>("AllaganTools.GetCharacterItems");
        this.log = log;
    }

    public bool Contains(uint id, bool includeAppearance) => Available && (owned.Contains(id) || includeAppearance && appearances.Contains(id));
    public StoredCurrency Currency(CurrencyKey key) => new(Available,
        key.Collectability == 0 && currencies.TryGetValue(key, out var found) ? found : []);

    public void Refresh(ulong contentId)
    {
        if (character != contentId) Clear();
        character = contentId;
        if (contentId == 0 || !active.HasFunction || !initialized.HasFunction || !owners.HasFunction || !items.HasFunction)
        { Invalidate(); return; }
        try
        {
            if (!initialized.InvokeFunc() || active.InvokeFunc() != character) { Invalidate(); return; }
            if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalSeconds < 2) return;
            lastRead = Stopwatch.GetTimestamp();
            var scope = owners.InvokeFunc(true);
            if (scope is null || scope.Count > 128 || !scope.Contains(character)) { Invalidate(); return; }
            var nextOwned = new HashSet<uint>();
            var nextAppearances = new HashSet<uint>();
            var quantities = new Dictionary<(CurrencyKey Key, string Name, StorageRole Role), long>();
            var seen = new HashSet<(ulong Owner, ulong Container, ulong Slot)>();
            var retainerNumber = 0;
            var itemCount = 0;
            foreach (var owner in scope.Order())
            {
                var rows = items.InvokeFunc(owner);
                if (rows is null || rows.Count > 30000) throw new InvalidDataException("Unexpected storage snapshot size.");
                var retainer = rows.Any(row => row.Length >= 25 && row[20] is >= 10000 and <= 12002);
                var label = retainer ? $"집사 기록 {++retainerNumber}" : "";
                foreach (var row in rows)
                {
                    // ToNumeric v1: item2, quantity3, flags6, sorted container20, slot22, owner23.
                    if (row.Length < 25) throw new InvalidDataException("Unexpected storage record shape.");
                    if (row[2] == 0 || row[3] == 0) continue;
                    if (row[23] != owner || row[2] >= 1_000_000 || row[3] > uint.MaxValue || row[6] > 15)
                        throw new InvalidDataException("Unexpected storage record values.");
                    var place = Locate(row[20], owner == character, label);
                    if (place is null) continue;
                    if (!seen.Add((owner, row[20], row[22])))
                        throw new InvalidDataException("Duplicate storage slot.");
                    itemCount++;
                    if (place.Value.Role is StorageRole.Personal or StorageRole.Listed) nextOwned.Add((uint)row[2]);
                    if (place.Value.Role == StorageRole.Appearance) nextAppearances.Add((uint)row[2]);
                    // Collectable quality is not ordinary currency; do not count it as NQ.
                    if ((row[6] & 8) != 0) continue;
                    var key = new CurrencyKey((uint)row[2], (row[6] & 1) != 0 ? ItemQuality.High : ItemQuality.Normal, 0);
                    var location = (key, place.Value.Name, place.Value.Role);
                    quantities[location] = checked(quantities.GetValueOrDefault(location) + (long)row[3]);
                }
            }
            if (active.InvokeFunc() != character || !initialized.InvokeFunc()) { Invalidate(); return; }
            var next = quantities.GroupBy(x => x.Key.Key).ToDictionary(g => g.Key,
                g => g.Select(x => new StoredLocation(x.Key.Name, x.Value, x.Key.Role)).OrderBy(x => x.Name).ToList());
            currencies = next; owned = nextOwned; appearances = nextAppearances; Available = true; failed = false;
            if (reported != (scope.Count, itemCount))
            {
                reported = (scope.Count, itemCount);
                log.Information("Storage records refreshed: Owners={Owners}; ExternalItems={Items}", scope.Count, itemCount);
            }
        }
        catch (Exception ex)
        {
            Invalidate();
            if (!failed) log.Warning("Storage records unavailable ({Kind}); no absence inferred.", ex is InvalidDataException ? ex.Message : ex.GetType().Name);
            failed = true;
        }
    }

    private static (string Name, StorageRole Role)? Locate(ulong container, bool current, string retainer) => container switch
    {
        // Current bags/currency are read live elsewhere. Never add their cached copy again.
        4000 or 4001 when current => ("초코보 가방", StorageRole.Personal),
        4100 or 4101 when current => ("초코보 가방 확장", StorageRole.Personal),
        2500 when current => ("추억의 보관함", StorageRole.Appearance),
        2501 when current => ("환상투영함", StorageRole.Appearance),
        1000 or >= 3200 and <= 3500 when current => ("착용 장비·장비함", StorageRole.Equipped),
        >= 10000 and <= 10006 or 12000 or 12001 when !current && retainer.Length > 0 => (retainer, StorageRole.Personal),
        11000 when !current && retainer.Length > 0 => (retainer + " · 착용", StorageRole.Personal),
        12002 when !current && retainer.Length > 0 => (retainer + " · 장터 출품", StorageRole.Listed),
        >= 20000 and <= 20010 or 22000 or 22001 or 2502 => ("부대 금고 · 공유", StorageRole.Shared),
        >= 27000 and <= 27011 or 27200 => ("하우징 보관함 · 권한 확인", StorageRole.Shared),
        >= 25000 and <= 25014 or 25200 => ("하우징 배치·외장 · 권한 확인", StorageRole.Shared),
        _ => null,
    };

    private void Invalidate() { Available = false; currencies.Clear(); owned.Clear(); appearances.Clear(); }
    public void Clear() { Invalidate(); character = 0; lastRead = 0; failed = false; reported = null; }
}
