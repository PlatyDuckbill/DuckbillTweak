using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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

    public void Refresh()
    {
        Clear();
        if (!Enabled) return;
        cabinetRows ??= data.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(c => c.Item.RowId != 0)
            .GroupBy(c => c.Item.RowId).ToDictionary(g => g.Key, g => g.First().RowId);
        var mirage = MirageManager.Instance();
        if (mirage is not null && mirage->PrismBoxLoaded)
        {
            DresserLoaded = true;
            foreach (var raw in mirage->PrismBoxItemIds)
            {
                if (raw == 0) continue;
                var id = raw is >= 1_000_000 and < 2_000_000 ? raw - 1_000_000 : raw;
                if (data.GetExcelSheet<Item>().TryGetRow(id, out var item) && item.EquipSlotCategory.RowId != 0)
                    stored.Add(id);
                else DresserLoaded = false; // Unknown set encodings must not prove absence.
            }
        }
        var ui = UIState.Instance();
        if (ui is not null && ui->Cabinet.IsCabinetLoaded())
        {
            CabinetLoaded = true;
            foreach (var (id, row) in cabinetRows)
                if (ui->Cabinet.IsItemInCabinet(row)) stored.Add(id);
        }
    }

    public bool Contains(uint id) => stored.Contains(id);
    public bool StorageKnown(Item item) => (item.EquipSlotCategory.RowId == 0 || DresserLoaded) &&
        (cabinetRows is not null && !cabinetRows.ContainsKey(item.RowId) || CabinetLoaded);

    public Ownership? Registration(Item item)
    {
        if (registration.TryGetValue(item.RowId, out var cached)) return cached;
        Ownership? result = null;
        if (unlocks.IsItemUnlockable(item) && unlocks.IsItemUnlocked(item)) result = Ownership.Owned;
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
    }
}
