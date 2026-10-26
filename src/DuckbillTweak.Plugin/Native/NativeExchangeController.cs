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
    bool AllOwned, string Context, bool Enabled = true, bool AllConsumable = false);

/// <summary>Native integration for the inspected inclusion, item and currency exchange layouts.</summary>
internal sealed unsafe class NativeExchangeController : IDisposable
{
    private readonly string name;
    private readonly MerchantService merchants;
    private readonly Configuration config;
    private readonly Action save;
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

    public NativeExchangeController(string name, MerchantService merchants, Configuration config, Action save, NativeCurrencyAddon currency, IPluginLog log)
    {
        this.name = name; this.merchants = merchants; this.config = config; this.save = save; this.currency = currency; this.log = log;
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
        { Restore(addon); merchants.ClearDisplayedShop(addon->Id); missing.IsEnabled = false; if (consumables is not null) consumables.IsEnabled = false; return; }
        missing.IsEnabled = true;
        if (consumables is not null) consumables.IsEnabled = true;
        if (context != key) Restore(addon);
        context = key;
        var defaultHide = name == "ShopExchangeItem"
            ? config.ExchangeCategoryHideOwned.GetValueOrDefault($"exchange:{merchants.View.NpcId}", config.MerchantHideOwned)
            : config.MerchantHideOwned;
        var hide = config.ExchangeCategoryHideOwned.GetValueOrDefault(key, defaultHide);
        sync = true; missing.IsChecked = hide; sync = false;
        sync = true;
        if (consumables is not null && consumables.IsChecked != config.MerchantHideConsumables) consumables.IsChecked = config.MerchantHideConsumables;
        sync = false;
        missing.TextTooltip = merchants.OwnershipHelp;
        if (!hide && !config.MerchantHideConsumables) { Restore(addon); return; }
        var hidden = rows.Select(r => hide && r.AllOwned || config.MerchantHideConsumables && r.AllConsumable).ToArray();
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
        if (sourceCount < 0) return false;
        if (name != "InclusionShop")
        {
            var shop = AgentShop.Instance();
            if (shop is null || shop->AddonId != addon->Id || shop->ItemReceive is null ||
                shop->ItemReceiveCount != sourceCount || shop->ItemReceiveCount is < 0 or > 1000 ||
                shop->ItemCost is null || shop->ItemCostCount != shop->ItemReceiveCount * 3)
            {
                var reason = shop is null ? "agent unavailable" : $"agentMatch={shop->AddonId == addon->Id}; source={sourceCount}; receive={shop->ItemReceiveCount}; cost={shop->ItemCostCount}";
                if (lastReadFailure != reason) log.Information("{Shop} snapshot waiting: {Reason}", name, reason);
                lastReadFailure = reason;
                return false;
            }
            lastReadFailure = null;
            // One NPC can open several exchange lists. Membership/currency IDs distinguish
            // them without depending on changing stock, selected quantity or translated names.
            var membership = new List<string>();
            for (var i = 0; i < shop->ItemReceiveCount; i++)
                membership.Add($"{shop->ItemReceive[i].ItemId}:{shop->ItemCost[i * 3].ItemId}:{shop->ItemCost[i * 3 + 1].ItemId}:{shop->ItemCost[i * 3 + 2].ItemId}");
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", membership.Order(StringComparer.Ordinal)))));
            key = $"exchange:{merchants.View.NpcId}:{digest}";
            var offers = new List<Offer>();
            var balances = new Dictionary<CurrencyKey, long>();
            var ownedItems = new HashSet<uint>();
            for (var i = 0; i < shop->ItemReceiveCount; i++)
            {
                ref var item = ref shop->ItemReceive[i];
                var id = item.ItemId >= 1_000_000 ? item.ItemId - 1_000_000 : item.ItemId;
                if (id == 0) return false;
                var costs = new List<string>();
                var prices = new List<Price>();
                for (var costIndex = 0; costIndex < 3; costIndex++)
                {
                    ref var cost = ref shop->ItemCost[i * 3 + costIndex];
                    if (cost.ItemId != 0)
                    {
                        var raw = cost.ItemId;
                        var price = merchants.NativePrice(raw >= 1_000_000 ? (byte)1 : (byte)0, raw >= 1_000_000 ? raw - 1_000_000 : raw, cost.ItemCount);
                        prices.Add(price); costs.Add($"{price.Name} ×{price.Quantity:N0}");
                        if (price.Resolved) balances[price.Key] = cost.OwnedItemCount;
                    }
                }
                var nativeOwned = item.OwnedItemCount;
                // This native row describes one reward. Other catalog offers are not part of it.
                var owned = merchants.ItemOwnership(item.ItemId, nativeOwned) == Ownership.Owned;
                if (owned) ownedItems.Add(id);
                rows.Add(new(i, item.ItemId, item.ItemIconId, merchants.ItemDisplayName(item.ItemId),
                    string.Join(" / ", costs), item.OwnedItemCount.ToString("N0"), owned, key, nativeFilter.IsEnabled(list, i), merchants.IsConsumable(item.ItemId)));
                if (merchants.NativeReward(item.ItemId, item.ItemCount, prices) is { } reward)
                    offers.Add(merchants.DisplayedOffer($"{key}:{i}", [reward], prices, nativeFilter.IsEnabled(list, i)));
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
            for (var reward = 0; reward < item.GiveItemId.Length; reward++)
                if (item.GiveItemId[reward] != 0)
                {
                    if (merchants.ItemOwnership(item.GiveItemId[reward], item.GiveItemOwnedCount[reward]) != Ownership.Owned) allOwned = false;
                    else inclusionOwned.Add(item.GiveItemId[reward] >= 1_000_000 ? item.GiveItemId[reward] - 1_000_000 : item.GiveItemId[reward]);
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
                string.Join(" / ", costs), item.GiveItemOwnedCount[0].ToString("N0"), allOwned, key, nativeFilter.IsEnabled(list, i), ids.All(merchants.IsConsumable)));
            var rewards = new List<Reward>();
            for (var reward = 0; reward < item.GiveItemId.Length; reward++)
                if (item.GiveItemId[reward] != 0 && merchants.NativeReward(item.GiveItemId[reward] + (item.GiveItemHq[reward] != 0 ? 1_000_000u : 0), item.GiveAmount[reward]) is { } received)
                    rewards.Add(received);
            if (rewards.Count == ids.Length) inclusionOffers.Add(merchants.DisplayedOffer($"{key}:{i}", rewards, prices, nativeFilter.IsEnabled(list, i)));
        }
        merchants.PublishDisplayedShop(addon->Id, key, inclusionOffers, inclusionBalances, inclusionOwned, rows.Count);
        return true;
    }

    private void Attach(AtkUnitBase* addon)
    {
        var list = addon->GetComponentListById(ListId);
        if (list is null || list->OwnerNode is null || list->OwnerNode->Height < 100) return;
        boundAddon = (nint)addon; listHeight = list->OwnerNode->Height;
        foreach (var id in ShiftIds.Append(ListId))
        { var node = addon->GetNodeById(id); if (node is null) continue; positions[id] = node->Y; node->SetYFloat(node->Y + 28); }
        list->SetSize(list->OwnerNode->Width, (ushort)(listHeight - 28));
        var y = name == "InclusionShop" ? 118 : CurrencyShop ? 92 : 194;
        missing = new CheckboxNode { Position = new(28, y), Size = new(200, 24), String = "없는 것만 보기" };
        missing.OnClick = value =>
        {
            if (!sync && context.Length > 0)
            { Restore(addon); config.ExchangeCategoryHideOwned[context] = value; save(); lastRead = 0; }
        };
        missing.AttachNode(addon);
        consumables = new CheckboxNode { Position = new(244, y), Size = new(190, 24), String = "소모품 제외", IsChecked = config.MerchantHideConsumables,
            TextTooltip = "약품·요리·염료·촉매·효과 설명서·PvP 소모품·폭죽을 제외합니다. 등록 수집품은 유지합니다." };
        consumables.OnClick = value => { if (!sync) { Restore(addon); config.MerchantHideConsumables = value; save(); lastRead = 0; } };
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
        missing?.Dispose(); missing = null; consumables?.Dispose(); consumables = null; calculate?.Dispose(); calculate = null;
        foreach (var (id, y) in positions) { var node = addon->GetNodeById(id); if (node is not null) node->SetYFloat(y); }
        var list = addon->GetComponentListById(ListId); if (list is not null && list->OwnerNode is not null) list->SetSize(list->OwnerNode->Width, listHeight);
        addon->UpdateCollisionNodeList(false);
        positions.Clear(); boundAddon = 0; context = ""; renderFailed = false; lastShape = null; lastFailure = null; lastReadFailure = null;
    }
    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AtkUnitBase*)boundAddon); }
}
