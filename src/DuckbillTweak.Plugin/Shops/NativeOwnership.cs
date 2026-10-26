using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
using Item = Lumina.Excel.Sheets.Item;

namespace DuckbillTweak.Plugin.Shops;

/// <summary>Read-only ownership queries, enabled only after the exact native profile check.</summary>
internal sealed unsafe class NativeOwnership(IDataManager data, IUnlockState unlocks)
{
    public bool Enabled { get; set; }
    public bool DresserLoaded { get; private set; }
    public bool CabinetLoaded { get; private set; }
    private readonly HashSet<uint> stored = [];
    private readonly Dictionary<uint, Ownership?> registration = [];
    private Dictionary<uint, uint>? cabinetRows;
    private Dictionary<uint, uint[]>? fadedResults;
    private readonly Dictionary<uint, uint[]> outfits = [];
    public string DresserSource { get; private set; } = "미확인";
    public string CabinetSource { get; private set; } = "미확인";

    public void Refresh(ulong contentId)
    {
        Clear();
        if (!Enabled) return;
        cabinetRows ??= data.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(c => c.Item.RowId != 0)
            .GroupBy(c => c.Item.RowId).ToDictionary(g => g.Key, g => g.First().RowId);
        fadedResults ??= BuildFadedResults();
        var finder = ItemFinderModule.Instance();
        var ownCache = finder is not null && contentId != 0 && finder->CharacterContentId == contentId;
        var mirage = MirageManager.Instance();
        if (mirage is not null && mirage->PrismBoxLoaded)
        {
            DresserLoaded = ReadDresser(mirage->PrismBoxItemIds, [], mirage);
            DresserSource = DresserLoaded ? "현재 정보" : "일부 미확인";
        }
        else if (ownCache && finder->IsGlamourDresserCached)
        {
            DresserLoaded = ReadDresser(finder->GlamourDresserItemIds, finder->GlamourDresserItemSetUnlockBits, null);
            DresserSource = DresserLoaded ? "게임 저장 정보" : "일부 미확인";
        }
        var ui = UIState.Instance();
        if (ui is not null && ui->Cabinet.IsCabinetLoaded())
        {
            CabinetLoaded = true;
            CabinetSource = "현재 정보";
            foreach (var (id, row) in cabinetRows)
                if (ui->Cabinet.IsItemInCabinet(row)) stored.Add(id);
        }
        else if (ownCache && finder->CabinetState == (byte)FFXIVClientStructs.FFXIV.Client.Game.UI.Cabinet.CabinetState.Loaded)
        {
            CabinetLoaded = true;
            CabinetSource = "게임 저장 정보";
            var words = finder->CabinetItemUnlockBits;
            foreach (var (id, row) in cabinetRows)
            {
                if (row / 32 >= words.Length) { CabinetLoaded = false; CabinetSource = "일부 미확인"; continue; }
                if ((words[(int)(row / 32)] & (1u << (int)(row % 32))) != 0) stored.Add(id);
            }
        }
    }

    private bool ReadDresser(ReadOnlySpan<uint> ids, ReadOnlySpan<ushort> missingSlots, MirageManager* live)
    {
        var complete = true;
        var items = data.GetExcelSheet<Item>();
        for (var index = 0; index < ids.Length; index++)
        {
            var raw = ids[index];
            if (raw == 0) continue;
            var id = raw is >= 1_000_000 and < 2_000_000 ? raw - 1_000_000 : raw;
            if (items.TryGetRow(id, out var item) && item.EquipSlotCategory.RowId != 0)
            { stored.Add(id); continue; }
            if (!outfits.TryGetValue(id, out var members))
            {
                if (!data.GetExcelSheet<MirageStoreSetItem>().TryGetRow(id, out var outfit)) { complete = false; continue; }
                members = [outfit.MainHand.RowId, outfit.OffHand.RowId, outfit.Head.RowId, outfit.Body.RowId,
                    outfit.Hands.RowId, outfit.Legs.RowId, outfit.Feet.RowId, outfit.Earrings.RowId,
                    outfit.Necklace.RowId, outfit.Bracelets.RowId, outfit.Ring.RowId];
                outfits[id] = members;
            }
            if (live is null && index >= missingSlots.Length) { complete = false; continue; }
            for (var slot = 0; slot < members.Length; slot++)
                if (members[slot] != 0 && (live is not null ? live->IsSetSlotUnlocked((uint)index, slot) :
                    (missingSlots[index] & (1 << slot)) == 0)) stored.Add(members[slot]);
        }
        return complete;
    }

    private Dictionary<uint, uint[]> BuildFadedResults()
    {
        var mapping = new Dictionary<uint, HashSet<uint>>();
        foreach (var recipe in data.GetExcelSheet<Recipe>())
        {
            if (!recipe.ItemResult.IsValid || recipe.ItemResult.RowId == 0) continue;
            var result = recipe.ItemResult.Value;
            if (!result.AdditionalData.Is<Orchestrion>() || result.ItemAction.RowId == 0) continue;
            foreach (var ingredient in recipe.Ingredient)
            {
                if (!ingredient.IsValid || ingredient.RowId == 0) continue;
                var item = ingredient.Value;
                if (item.ItemUICategory.RowId != 94 || item.ItemAction.RowId != 0) continue;
                if (!mapping.TryGetValue(item.RowId, out var results)) mapping[item.RowId] = results = [];
                results.Add(result.RowId);
            }
        }
        return mapping.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    public bool Contains(uint id) => stored.Contains(id);
    public bool StorageKnown(Item item) => (item.EquipSlotCategory.RowId == 0 || DresserLoaded) &&
        (cabinetRows is not null && !cabinetRows.ContainsKey(item.RowId) || CabinetLoaded);

    public Ownership? Registration(Item item)
    {
        if (registration.TryGetValue(item.RowId, out var cached)) return cached;
        Ownership? result = null;
        if (unlocks.IsItemUnlockable(item) && unlocks.IsItemUnlocked(item)) result = Ownership.Owned;
        else if (fadedResults is not null && fadedResults.TryGetValue(item.RowId, out var related))
        {
            var states = related.Select(id => data.GetExcelSheet<Item>().TryGetRow(id, out var completed)
                ? Registration(completed) : Ownership.Unknown).ToArray();
            result = states.All(s => s == Ownership.Owned) ? Ownership.Owned :
                states.Any(s => s == Ownership.Missing) ? Ownership.Missing : Ownership.Unknown;
        }
        else if (item.ItemAction.RowId != 0)
        {
            result = Ownership.Unknown;
            if (Enabled)
            {
                var row = ExdModule.GetItemRowById(item.RowId);
                var ui = UIState.Instance();
                if (row is not null && ui is not null)
                    result = ui->IsItemActionUnlocked(row) switch
                    {
                        1 => Ownership.Owned,
                        2 => Ownership.Missing,
                        4 => null,
                        _ => Ownership.Unknown,
                    };
            }
        }
        registration[item.RowId] = result;
        return result;
    }

    public void Clear()
    {
        stored.Clear(); registration.Clear(); DresserLoaded = false; CabinetLoaded = false;
        DresserSource = CabinetSource = "미확인";
    }
}
