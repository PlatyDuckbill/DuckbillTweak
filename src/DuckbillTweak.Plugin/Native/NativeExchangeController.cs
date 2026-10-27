using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record ExchangeRow(int OriginalIndex, uint ItemId, uint Icon, string Name, string Cost, string Owned,
    bool AllOwned, string Context, bool Enabled = true, bool AllExcludedSupplies = false, bool AllOwnedOrSupply = false);

/// <summary>Native integration for the inspected inclusion, item and currency exchange layouts.</summary>
internal sealed unsafe class NativeExchangeController : IDisposable
{
    private readonly string name;
    private readonly MerchantService merchants;
    private readonly NativeCurrencyAddon currency;
    private readonly IPluginLog log;
    private readonly AddonController controller;
    private readonly NativeTreeFilter nativeFilter = new();
    private CheckboxNode? missing;
    private CheckboxNode? consumables;
    private TextButtonNode? calculate;
    private nint boundAddon;
    private long lastRead;
    private string context = "";
    private string pageIdentity = "";
    private bool sync;
    private bool renderFailed;
    private (int Total, int Visible)? lastApplied;
    private (int SourceRows, int ListRows)? lastShape;
    private string? lastFailure;
    private string? lastReadFailure;
    private readonly Dictionary<uint, float> positions = [];
    private ushort listHeight;
    private uint ListId => name == "InclusionShop" ? 19u : 20u;
    private bool CurrencyShop => name == "ShopExchangeCurrency";
    private uint[] ShiftIds => name == "InclusionShop" ? [11, 17] : CurrencyShop ? [13, 19] : [12, 18];
    private bool Shape(AtkUnitBase* addon) => addon->RootNode is not null && addon->RootNode->Width == (CurrencyShop ? 680 : 760) &&
        addon->RootNode->Height == (name == "InclusionShop" ? 586 : CurrencyShop ? 528 : 556) && addon->GetNodeById(ListId) is not null;

    public NativeExchangeController(string name, MerchantService merchants, NativeCurrencyAddon currency, IPluginLog log)
    {
        this.name = name; this.merchants = merchants; this.currency = currency; this.log = log;
        controller = new AddonController { AddonName = name, OnUpdate = Update, OnFinalize = Detach,
            OnPreRefresh = addon =>
            {
                if (boundAddon == (nint)addon) { Restore(addon); merchants.ClearDisplayedShop(addon->Id); }
                lastRead = 0;
            } };
        controller.Enable();
    }

    private void Update(AtkUnitBase* addon)
    {
        try { UpdateCore(addon); }
        catch (Exception ex)
        {
            // Only managed render errors are handled here; native pointers still require the inspected shape/lifetime.
            if (boundAddon == (nint)addon) { Restore(addon); merchants.ClearDisplayedShop(addon->Id); }
            if (!renderFailed) log.Error(ex, "{Shop} native filtering failed; original hidden flags restored. Reopen the shop to retry.", name);
            renderFailed = true;
        }
    }

    private void UpdateCore(AtkUnitBase* addon)
    {
        if (!merchants.SessionReady || !addon->IsVisible || !Shape(addon)) { if (boundAddon == (nint)addon) Detach(addon); return; }
        if (boundAddon == 0) Attach(addon);
        if (missing is null) return;
        var page = ReadPageIdentity(addon);
        if (page is not null && page != pageIdentity)
        {
            if (pageIdentity.Length > 0)
            {
                Restore(addon); merchants.ClearDisplayedShop(addon->Id);
                merchants.ResetFilters(addon->Id); lastRead = 0;
            }
            pageIdentity = page;
        }
        var filters = merchants.FiltersFor(addon->Id);
        sync = true;
        if (missing.IsChecked != filters.HideOwned) missing.IsChecked = filters.HideOwned;
        if (consumables is not null && consumables.IsChecked != filters.HideConsumables) consumables.IsChecked = filters.HideConsumables;
        sync = false;
        var list = addon->GetComponentListById(ListId);
        nativeFilter.FinishLayout(addon, list);
        if (renderFailed) Restore(addon);
        if (renderFailed) return;
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 300) return;
        lastRead = Stopwatch.GetTimestamp();
        if (list is not null)
        {
            var shape = (SourceRows: NativeTreeFilter.SourceCount(list), ListRows: list->ListLength);
            if (lastShape != shape)
            {
                log.Information("{Shop} native tree shape: SourceRows={Source}; ListRows={List}", name, shape.SourceRows, shape.ListRows);
                lastShape = shape;
            }
        }
        if (list is null || !TryRead(addon, list, out var rows, out var key))
        { Restore(addon); merchants.ClearDisplayedShop(addon->Id); missing.TextTooltip = "상점 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요."; return; }
        missing.IsEnabled = true;
        if (consumables is not null) consumables.IsEnabled = true;
        if (context != key) Restore(addon);
        context = key;
        var hide = filters.HideOwned;
        missing.TextTooltip = merchants.OwnershipHelp;
        if (!hide && !filters.HideConsumables) { Restore(addon); return; }
        var hidden = rows.Select(r => hide && r.AllOwned || filters.HideConsumables && r.AllExcludedSupplies ||
            hide && filters.HideConsumables && r.AllOwnedOrSupply).ToArray();
        if (!nativeFilter.Apply(addon, list, hidden, out var visible))
        {
            if (lastFailure != nativeFilter.Failure) log.Warning("{Shop} filter contract: {Reason}", name, nativeFilter.Failure);
            lastFailure = nativeFilter.Failure;
            Restore(addon);
            missing.TextTooltip = "현재 목록에 필터를 적용하지 못했습니다. 체크를 해제하면 원래 목록을 사용합니다.\n" + nativeFilter.Failure;
            return;
        }
        lastFailure = null;
        if (consumables is not null) consumables.IsEnabled = true;
        if (lastApplied != (rows.Count, visible))
        {
            log.Information("{Shop} original-row filter applied: Rows={Rows}; Visible={Visible}; PooledRenderers={Renderers}", name, rows.Count, visible, nativeFilter.RendererCount);
            lastApplied = (rows.Count, visible);
        }
    }

    private bool TryRead(AtkUnitBase* addon, AtkComponentList* list, out List<ExchangeRow> rows, out string key)
    {
        rows = []; key = "";
        var sourceCount = NativeTreeFilter.SourceCount(list);
        if (sourceCount <= 0) return false; // Tab refresh briefly publishes an empty intermediate tree.
        if (name != "InclusionShop")
        {
            var shop = AgentShop.Instance();
            if (shop is null || shop->AddonId != addon->Id || shop->ItemReceive is null ||
                (!CurrencyShop && shop->ItemReceiveCount != sourceCount) || shop->ItemReceiveCount is < 0 or > 1000 ||
                shop->ItemCost is null || shop->ItemCostCount != shop->ItemReceiveCount * 3)
            {
                var reason = shop is null ? "agent unavailable" : $"agentMatch={shop->AddonId == addon->Id}; source={sourceCount}; receive={shop->ItemReceiveCount}; cost={shop->ItemCostCount}";
                if (lastReadFailure != reason) log.Information("{Shop} snapshot waiting: {Reason}", name, reason);
                lastReadFailure = reason;
                return false;
            }
            // Currency exchange tabs expose a subset of AgentShop. Use the game's own
            // row -> compact page entry -> item identity mapping, never row == agent index.
            var sources = Enumerable.Range(0, sourceCount).ToArray();
            if (CurrencyShop && !TryMapCurrencyRows(addon, list, shop, sources, out var mappingFailure))
            {
                var reason = "currency tab mapping: " + mappingFailure;
                if (lastReadFailure != reason) log.Information("{Shop} snapshot waiting: {Reason}", name, reason);
                lastReadFailure = reason;
                return false;
            }
            lastReadFailure = null;
            var observations = new List<ExchangeObservation>();
            for (var i = 0; i < shop->ItemReceiveCount; i++)
            {
                var nativePrices = new List<Price>();
                for (var costIndex = 0; costIndex < 3; costIndex++)
                {
                    ref var cost = ref shop->ItemCost[i * 3 + costIndex];
                    if (cost.ItemId == 0) continue;
                    var raw = cost.ItemId;
                    nativePrices.Add(merchants.NativePrice(raw >= 1_000_000 ? (byte)1 : (byte)0,
                        raw >= 1_000_000 ? raw - 1_000_000 : raw, cost.ItemCount));
                }
                observations.Add(new(shop->ItemReceive[i].ItemId, shop->ItemReceive[i].ItemCount, nativePrices));
            }
            var shopName = ReadShopName(shop);
            var resolved = merchants.ResolveNativePage(shopName, observations);
            // One NPC can open several exchange lists. Page membership, native shop identity
            // and currency IDs distinguish them independently of stock or selected quantity.
            var membership = new List<string>();
            for (var i = 0; i < shop->ItemReceiveCount; i++)
                membership.Add($"{shop->ItemReceive[i].ItemId}:{shop->ItemCost[i * 3].ItemId}:{shop->ItemCost[i * 3 + 1].ItemId}:{shop->ItemCost[i * 3 + 2].ItemId}");
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shopName + ":" + string.Join(";", membership.Order(StringComparer.Ordinal)))));
            key = $"exchange:{merchants.View.NpcId}:{digest}:{string.Join(',', sources)}";
            var offers = new List<Offer>();
            var balances = new Dictionary<CurrencyKey, long>();
            var ownedItems = new HashSet<uint>();
            for (var rowIndex = 0; rowIndex < sources.Length; rowIndex++)
            {
                var i = sources[rowIndex];
                ref var item = ref shop->ItemReceive[i];
                var id = item.ItemId >= 1_000_000 ? item.ItemId - 1_000_000 : item.ItemId;
                if (id == 0) return false;
                var prices = resolved[i]?.Prices ?? observations[i].Prices;
                for (var costIndex = 0; costIndex < 3; costIndex++)
                {
                    ref var cost = ref shop->ItemCost[i * 3 + costIndex];
                    if (cost.ItemId != 0)
                    {
                        var raw = cost.ItemId;
                        var native = merchants.NativePrice(raw >= 1_000_000 ? (byte)1 : (byte)0, raw >= 1_000_000 ? raw - 1_000_000 : raw, cost.ItemCount);
                        if (native.Resolved && prices.Any(price => price.Resolved && price.Key == native.Key)) balances[native.Key] = cost.OwnedItemCount;
                    }
                }
                var nativeOwned = item.OwnedItemCount;
                // This native row describes one reward. Other catalog offers are not part of it.
                var owned = merchants.ItemOwnership(item.ItemId, nativeOwned) == Ownership.Owned;
                var hideOwned = owned && merchants.CanHideOwned(item.ItemId);
                var supply = merchants.IsExcludedSupply(item.ItemId);
                if (owned) ownedItems.Add(id);
                rows.Add(new(rowIndex, item.ItemId, item.ItemIconId, merchants.ItemDisplayName(item.ItemId),
                    string.Join(" / ", prices.Select(price => $"{price.Name} ×{price.Quantity:N0}")), item.OwnedItemCount.ToString("N0"), hideOwned, key, nativeFilter.IsEnabled(list, rowIndex), supply, hideOwned || supply));
                if (resolved[i] is { } exchange && merchants.NativeReward(item.ItemId, exchange.Quantity, prices) is { } reward)
                    offers.Add(merchants.DisplayedOffer($"{key}:{rowIndex}", [reward], prices, nativeFilter.IsEnabled(list, rowIndex)));
            }
            merchants.PublishDisplayedShop(addon->Id, key, offers, balances, ownedItems, rows.Count);
            return true;
        }
        var agent = AgentInclusionShop.Instance();
        if (agent is null || agent->AddonId != addon->Id || agent->Data is null) return false;
        var data = agent->Data;
        if (!data->IsShopReady || data->CategoryCount > data->Categories.Length || data->SelectedCategoryIndex >= data->CategoryCount) return false;
        var categoryIndex = data->CategoryIndexMap[data->SelectedCategoryIndex];
        if (categoryIndex >= data->Categories.Length) return false;
        ref var category = ref data->Categories[categoryIndex];
        if (category.SubCategories is null || category.SubCategoryCount is < 1 or > 64 || data->SelectedSubCategoryIndex >= category.SubCategoryCount) return false;
        ref var sub = ref category.SubCategories[data->SelectedSubCategoryIndex];
        if (sub.ItemCount > sub.Items.Length || sourceCount > sub.ItemCount || sourceCount > data->ItemIndexMap.Length) return false;
        key = $"inclusion:{data->InclusionShopId}:{category.InclusionShopRowId}:{category.InclusionShopSeriesId}:{data->SelectedSubCategoryIndex}";
        var used = new HashSet<int>();
        var inclusionOffers = new List<Offer>();
        var inclusionBalances = new Dictionary<CurrencyKey, long>();
        var inclusionOwned = new HashSet<uint>();
        for (var i = 0; i < sourceCount; i++)
        {
            var source = data->ItemIndexMap[i];
            if (source >= sub.ItemCount || !used.Add(source)) return false;
            ref var item = ref sub.Items[source];
            var ids = item.GiveItemId.ToArray().Where(x => x != 0).ToArray();
            if (ids.Length == 0) return false;
            var allOwned = true;
            var allOwnedOrSupply = true;
            for (var reward = 0; reward < item.GiveItemId.Length; reward++)
                if (item.GiveItemId[reward] != 0)
                {
                    var owned = merchants.ItemOwnership(item.GiveItemId[reward], item.GiveItemOwnedCount[reward]) == Ownership.Owned;
                    if (owned) inclusionOwned.Add(item.GiveItemId[reward] >= 1_000_000 ? item.GiveItemId[reward] - 1_000_000 : item.GiveItemId[reward]);
                    if (!owned || !merchants.CanHideOwned(item.GiveItemId[reward]))
                    {
                        allOwned = false;
                        if (!merchants.IsExcludedSupply(item.GiveItemId[reward])) allOwnedOrSupply = false;
                    }
                }
            var costs = new List<string>();
            var prices = new List<Price>();
            for (var cost = 0; cost < item.CostItemId.Length; cost++)
                if (item.CostItemId[cost] != 0)
                {
                    var price = merchants.NativePrice(item.CostType[cost], item.CostItemId[cost], item.CostAmount[cost]);
                    prices.Add(price); costs.Add($"{price.Name} ×{price.Quantity:N0}");
                    if (price.Resolved) inclusionBalances[price.Key] = item.CostOwnedCount[cost];
                }
            rows.Add(new(i, ids[0], merchants.Icon(ids[0]), string.Join(" / ", ids.Select(merchants.ItemName)),
                string.Join(" / ", costs), item.GiveItemOwnedCount[0].ToString("N0"), allOwned, key, nativeFilter.IsEnabled(list, i), ids.All(merchants.IsExcludedSupply), allOwnedOrSupply));
            var rewards = new List<Reward>();
            for (var reward = 0; reward < item.GiveItemId.Length; reward++)
                if (item.GiveItemId[reward] != 0 && merchants.NativeReward(item.GiveItemId[reward] + (item.GiveItemHq[reward] != 0 ? 1_000_000u : 0), item.GiveAmount[reward]) is { } received)
                    rewards.Add(received);
            if (rewards.Count == ids.Length) inclusionOffers.Add(merchants.DisplayedOffer($"{key}:{i}", rewards, prices, nativeFilter.IsEnabled(list, i)));
        }
        merchants.PublishDisplayedShop(addon->Id, key, inclusionOffers, inclusionBalances, inclusionOwned, rows.Count);
        return true;
    }

    private static bool TryMapCurrencyRows(AtkUnitBase* addon, AtkComponentList* list, AgentShop* shop, int[] sources, out string failure)
    {
        // Inspected KR 2026.10.06: refresh RVA12C17A0, row render RVA12C2D10,
        // tooltip RVA12C2B74. Compatibility's exact executable hash/MVID still applies.
        // The 60-entry compact map is native addon state, not a filtered purchase index.
        const int capacity = 60;
        const int itemIdValue = 0x42A;
        const int iconValue = 0xD4;
        failure = "native value layout unavailable";
        if (addon->AtkValues is null || addon->AtkValuesCount <= itemIdValue + capacity - 1 ||
            addon->AtkValues[3].Type is not (AtkValueType.UInt or AtkValueType.Int)) return false;
        var count = *(uint*)((byte*)addon + 0x3B38);
        failure = $"page count mismatch: rows={sources.Length}; page={count}; values={addon->AtkValues[3].UInt}";
        if (count is 0 or > capacity || addon->AtkValues[3].UInt != count || sources.Length > count) return false;
        var tree = (AtkComponentTreeList*)list;
        var used = new HashSet<int>();
        for (var rowIndex = 0; rowIndex < sources.Length; rowIndex++)
        {
            var row = tree->Items[rowIndex].Value;
            failure = $"row {rowIndex}: unsupported payload";
            if (row is null || row->UIntValues.LongCount != 3 || row->UIntValues.First is null ||
                row->UIntValues[0] != 0 || row->UIntValues[1] >= count || row->UIntValues[2] != 0) return false;
            var entry = ((uint*)((byte*)addon + 0x21A0))[row->UIntValues[1]];
            failure = $"row {rowIndex}: compact map outside page";
            if (entry >= count) return false;
            ref var id = ref addon->AtkValues[itemIdValue + entry];
            ref var icon = ref addon->AtkValues[iconValue + entry];
            var record = (uint*)((byte*)addon + 0x320 + entry * 0x40);
            failure = $"row {rowIndex}: item identity mismatch";
            if (id.Type is not (AtkValueType.UInt or AtkValueType.Int) ||
                icon.Type is not (AtkValueType.UInt or AtkValueType.Int) ||
                id.UInt == 0 || record[0] != id.UInt || record[1] != icon.UInt) return false;
            var source = -1;
            for (var i = 0; i < shop->ItemReceiveCount; i++)
            {
                ref var item = ref shop->ItemReceive[i];
                // AgentShop's presentation icon is not part of item identity. Currency
                // shops can use a different icon value from their rendered tab records.
                // A unique raw ID (including HQ) is required; duplicates still fail closed.
                if (item.ItemId != id.UInt) continue;
                failure = $"row {rowIndex}: duplicate agent item {id.UInt}";
                if (source != -1) return false; // Ambiguous duplicate reward: retain the original list.
                source = i;
            }
            failure = $"row {rowIndex}: no unique agent item {id.UInt}";
            if (source < 0 || !used.Add(source)) return false;
            sources[rowIndex] = source;
        }
        failure = "";
        return true;
    }

    private static string ReadShopName(AgentShop* shop)
    {
        var pointer = shop->ShopName.Value;
        if (pointer is null) return "";
        var length = 0;
        while (length < 512 && pointer[length] != 0) length++;
        return length == 512 ? "" : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(pointer, length));
    }

    private string? ReadPageIdentity(AtkUnitBase* addon)
    {
        if (name == "InclusionShop")
        {
            var agent = AgentInclusionShop.Instance();
            if (agent is null || agent->AddonId != addon->Id || agent->Data is null) return null;
            var data = agent->Data;
            if (!data->IsShopReady || data->SelectedCategoryIndex >= data->CategoryCount || data->CategoryCount > data->Categories.Length) return null;
            return $"{data->InclusionShopId}:{data->SelectedCategoryIndex}:{data->SelectedSubCategoryIndex}";
        }
        var shop = AgentShop.Instance();
        if (shop is null || shop->AddonId != addon->Id) return null;
        // KR setup RVA12C1416 and tab selection RVA12C26CB use this selected tab byte.
        var tab = CurrencyShop ? *((byte*)addon + 0x318) : 0;
        return tab is < 0 or > 3 ? null : $"{ReadShopName(shop)}:{tab}";
    }

    private void Attach(AtkUnitBase* addon)
    {
        var list = addon->GetComponentListById(ListId);
        if (list is null || list->OwnerNode is null || list->OwnerNode->Height < 100) return;
        boundAddon = (nint)addon; listHeight = list->OwnerNode->Height;
        merchants.ResetFilters(addon->Id);
        foreach (var id in ShiftIds.Append(ListId))
        { var node = addon->GetNodeById(id); if (node is null) continue; positions[id] = node->Y; node->SetYFloat(node->Y + 28); }
        list->SetSize(list->OwnerNode->Width, (ushort)(listHeight - 28));
        var y = name == "InclusionShop" ? 118 : CurrencyShop ? 92 : 194;
        missing = new CheckboxNode { Position = new(28, y), Size = new(200, 24), String = ShopItemPolicy.OwnedFilterLabel };
        missing.OnClick = value =>
        {
            if (!sync)
            { Restore(addon); merchants.FiltersFor(addon->Id).HideOwned = value; lastRead = 0; }
        };
        missing.AttachNode(addon);
        consumables = new CheckboxNode { Position = new(244, y), Size = new(190, 24), String = ShopItemPolicy.FilterLabel,
            TextTooltip = ShopItemPolicy.FilterHelp };
        consumables.OnClick = value => { if (!sync) { Restore(addon); merchants.FiltersFor(addon->Id).HideConsumables = value; lastRead = 0; } };
        consumables.AttachNode(addon);
        calculate = new TextButtonNode { Position = new(CurrencyShop ? 476 : 556, y - 3), Size = new(176, 28), String = "필요 재화", OnClick = () => currency.OpenForShop(addon->Id) };
        calculate.AttachNode(addon);
        lastRead = 0;
    }

    private void Restore(AtkUnitBase* addon)
    {
        nativeFilter.Restore(addon, addon->GetComponentListById(ListId));
        lastApplied = null;
    }
    private void Detach(AtkUnitBase* addon)
    {
        if (boundAddon != (nint)addon) return;
        Restore(addon); merchants.ClearDisplayedShop(addon->Id);
        currency.CloseForShop(addon->Id);
        merchants.ForgetFilters(addon->Id);
        missing?.Dispose(); missing = null; consumables?.Dispose(); consumables = null; calculate?.Dispose(); calculate = null;
        foreach (var (id, y) in positions) { var node = addon->GetNodeById(id); if (node is not null) node->SetYFloat(y); }
        var list = addon->GetComponentListById(ListId); if (list is not null && list->OwnerNode is not null) list->SetSize(list->OwnerNode->Width, listHeight);
        addon->UpdateCollisionNodeList(false);
        positions.Clear(); boundAddon = 0; context = ""; pageIdentity = ""; renderFailed = false; lastShape = null; lastFailure = null; lastReadFailure = null;
    }
    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AtkUnitBase*)boundAddon); }
}
