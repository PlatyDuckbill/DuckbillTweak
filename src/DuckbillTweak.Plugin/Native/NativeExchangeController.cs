using System.Diagnostics;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed record ExchangeRow(int OriginalIndex, uint ItemId, uint Icon, string Name, string Cost, string Owned,
    bool AllOwned, string Context, bool Enabled = true, bool AllConsumable = false);

internal sealed unsafe class ExchangeRowNode : ListItemNode<ExchangeRow>, IListItemNode
{
    public static float ItemHeight => 26;
    private readonly IconImageNode icon = new() { Position = new(4, 2), Size = new(22, 22), FitTexture = true };
    private readonly TextNode name = new() { Position = new(32, 1), Size = new(266, 24), FontSize = 14, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode cost = new() { Position = new(310, 1), Size = new(280, 24), FontSize = 12, TextFlags = TextFlags.Ellipsis };
    private readonly TextNode owned = new() { Position = new(600, 1), Size = new(96, 24), FontSize = 14, AlignmentType = AlignmentType.Right };
    public ExchangeRowNode()
    {
        icon.AttachNode(this); name.AttachNode(this); cost.AttachNode(this); owned.AttachNode(this);
        RemoveEvent(AtkEventType.MouseDown);
        AddEvent(AtkEventType.MouseUp, (_, _, _, _, data) => { if (EnableSelection && data is not null && data->MouseData.ButtonId == 0) OnClick?.Invoke(this); });
    }
    protected override void SetNodeData(ExchangeRow row)
    {
        icon.IconId = row.Icon; name.String = row.Name; cost.String = row.Cost; cost.TextTooltip = row.Cost; owned.String = row.Owned; ItemTooltip = row.ItemId;
        EnableSelection = row.Enabled; EnableHighlight = row.Enabled; Alpha = row.Enabled ? 1 : 0.5f;
    }
}

/// <summary>Native integration for the inspected InclusionShop and ShopExchangeItem layouts.</summary>
internal sealed unsafe class NativeExchangeController : IDisposable
{
    private readonly string name;
    private readonly MerchantService merchants;
    private readonly Configuration config;
    private readonly Action save;
    private readonly NativeCurrencyAddon currency;
    private readonly IPluginLog log;
    private readonly AddonController controller;
    private readonly NativeListInput originalInput = new();
    private CheckboxNode? missing;
    private CheckboxNode? consumables;
    private TextButtonNode? calculate;
    private ListNode<ExchangeRow, ExchangeRowNode>? filtered;
    private nint boundAddon;
    private long lastRead;
    private string context = "";
    private bool sync;
    private bool replaced;
    private bool renderFailed;
    private (int Total, int Visible)? lastApplied;
    private byte alpha;
    private bool interactive;
    private IReadOnlyList<ExchangeRow> shown = [];
    private readonly Dictionary<uint, float> positions = [];
    private ushort listHeight;
    private uint ListId => name == "InclusionShop" ? 19u : 20u;
    private uint[] ShiftIds => name == "InclusionShop" ? [11, 17] : [12, 18];
    private bool Shape(AtkUnitBase* addon) => addon->RootNode is not null && addon->RootNode->Width == 760 &&
        addon->RootNode->Height == (name == "InclusionShop" ? 586 : 556) && addon->GetNodeById(ListId) is not null;

    public NativeExchangeController(string name, MerchantService merchants, Configuration config, Action save, NativeCurrencyAddon currency, IPluginLog log)
    {
        this.name = name; this.merchants = merchants; this.config = config; this.save = save; this.currency = currency; this.log = log;
        controller = new AddonController { AddonName = name, OnUpdate = Update, OnFinalize = Detach };
        controller.Enable();
    }

    private void Update(AtkUnitBase* addon)
    {
        try { UpdateCore(addon); }
        catch (Exception ex)
        {
            // Only managed render errors are handled here; native pointers still require the inspected shape/lifetime.
            if (boundAddon == (nint)addon) Restore(addon);
            if (!renderFailed) log.Error(ex, "{Shop} filter rendering failed; original list input restored. Reopen the shop to retry.", name);
            renderFailed = true;
        }
    }

    private void UpdateCore(AtkUnitBase* addon)
    {
        if (!config.MerchantTools || !Shape(addon)) { if (boundAddon == (nint)addon) Detach(addon); return; }
        if (boundAddon == 0) Attach(addon);
        if (filtered is null || missing is null) return;
        var requested = config.ExchangeCategoryHideOwned.GetValueOrDefault(context, config.MerchantHideOwned) || config.MerchantHideConsumables;
        if (!requested || renderFailed) Restore(addon);
        if (renderFailed) return;
        if (replaced)
        {
            var original = addon->GetComponentListById(ListId);
            if (!originalInput.Suppress(original, out var dirty)) { Restore(addon); return; }
            filtered.Update();
            original->OwnerNode->SetAlpha(0); original->IsItemInteractionEnabled = false;
            if (dirty) addon->UpdateCollisionNodeList(false);
        }
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 300) return;
        lastRead = Stopwatch.GetTimestamp();
        var list = addon->GetComponentListById(ListId);
        if (list is null || !TryRead(addon, list, out var rows, out var key))
        { Restore(addon); merchants.ClearDisplayedShop(addon->Id); missing.IsEnabled = false; return; }
        missing.IsEnabled = true;
        if (context != key) filtered.ResetScroll();
        context = key;
        var hide = config.ExchangeCategoryHideOwned.GetValueOrDefault(key, config.MerchantHideOwned);
        sync = true; missing.IsChecked = hide; sync = false;
        sync = true;
        if (consumables is not null && consumables.IsChecked != config.MerchantHideConsumables) consumables.IsChecked = config.MerchantHideConsumables;
        sync = false;
        missing.TextTooltip = merchants.OwnershipHelp;
        if (!hide && !config.MerchantHideConsumables) { Restore(addon); return; }
        var next = rows.Where(r => (!hide || !r.AllOwned) && (!config.MerchantHideConsumables || !r.AllConsumable)).ToArray();
        var dataChanged = !shown.SequenceEqual(next);
        if (dataChanged)
        {
            if (!shown.Select(r => (r.OriginalIndex, r.ItemId)).SequenceEqual(next.Select(r => (r.OriginalIndex, r.ItemId)))) filtered.ResetScroll();
            filtered.OptionsList = next.ToList(); shown = next;
        }
        filtered.Update();
        var changed = !replaced || !filtered.IsVisible || dataChanged;
        if (!replaced) { alpha = list->OwnerNode->Color.A; interactive = list->IsItemInteractionEnabled; }
        if (!originalInput.Suppress(list, out var inputChanged)) { Restore(addon); missing.IsEnabled = false; return; }
        filtered.IsVisible = true;
        replaced = true;
        list->OwnerNode->SetAlpha(0); list->IsItemInteractionEnabled = false;
        // Visibility changes do not automatically rebuild the game's collision list.
        if (changed || inputChanged) addon->UpdateCollisionNodeList(false);
        if (lastApplied != (rows.Count, next.Length))
        {
            log.Information("{Shop} filter applied: Rows={Rows}; Visible={Visible}", name, rows.Count, next.Length);
            lastApplied = (rows.Count, next.Length);
        }
    }

    private bool TryRead(AtkUnitBase* addon, AtkComponentList* list, out List<ExchangeRow> rows, out string key)
    {
        rows = []; key = "";
        if (name != "InclusionShop")
        {
            var shop = AgentShop.Instance();
            if (shop is null || shop->AddonId != addon->Id || shop->ItemReceive is null ||
                shop->ItemReceiveCount != list->ListLength || shop->ItemReceiveCount is < 0 or > 1000 ||
                shop->ItemCost is null || shop->ItemCostCount != shop->ItemReceiveCount * 3) return false;
            key = $"exchange:{merchants.View.NpcId}";
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
                    string.Join(" / ", costs), item.OwnedItemCount.ToString("N0"), owned, key, !list->GetItemDisabledState(i), merchants.IsConsumable(item.ItemId)));
                if (merchants.NativeReward(item.ItemId, item.ItemCount, prices) is { } reward)
                    offers.Add(merchants.DisplayedOffer($"{key}:{i}", [reward], prices, !list->GetItemDisabledState(i)));
            }
            merchants.PublishDisplayedShop(addon->Id, offers, balances, ownedItems, rows.Count);
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
        if (sub.ItemCount > sub.Items.Length || list->ListLength < 0 || list->ListLength > sub.ItemCount || list->ListLength > data->ItemIndexMap.Length) return false;
        key = $"inclusion:{data->InclusionShopId}:{category.InclusionShopRowId}:{category.InclusionShopSeriesId}:{data->SelectedSubCategoryIndex}";
        var used = new HashSet<int>();
        var inclusionOffers = new List<Offer>();
        var inclusionBalances = new Dictionary<CurrencyKey, long>();
        var inclusionOwned = new HashSet<uint>();
        for (var i = 0; i < list->ListLength; i++)
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
                string.Join(" / ", costs), item.GiveItemOwnedCount[0].ToString("N0"), allOwned, key, !list->GetItemDisabledState(i), ids.All(merchants.IsConsumable)));
            var rewards = new List<Reward>();
            for (var reward = 0; reward < item.GiveItemId.Length; reward++)
                if (item.GiveItemId[reward] != 0 && merchants.NativeReward(item.GiveItemId[reward] + (item.GiveItemHq[reward] != 0 ? 1_000_000u : 0), item.GiveAmount[reward]) is { } received)
                    rewards.Add(received);
            if (rewards.Count == ids.Length) inclusionOffers.Add(merchants.DisplayedOffer($"{key}:{i}", rewards, prices, !list->GetItemDisabledState(i)));
        }
        merchants.PublishDisplayedShop(addon->Id, inclusionOffers, inclusionBalances, inclusionOwned, rows.Count);
        return true;
    }

    private void Attach(AtkUnitBase* addon)
    {
        var list = addon->GetComponentListById(ListId);
        if (list is null || list->OwnerNode is null || list->OwnerNode->Height < 100) return;
        boundAddon = (nint)addon; listHeight = list->OwnerNode->Height;
        alpha = list->OwnerNode->Color.A; interactive = list->IsItemInteractionEnabled;
        foreach (var id in ShiftIds.Append(ListId))
        { var node = addon->GetNodeById(id); if (node is null) continue; positions[id] = node->Y; node->SetYFloat(node->Y + 28); }
        list->SetSize(list->OwnerNode->Width, (ushort)(listHeight - 28));
        var y = name == "InclusionShop" ? 118 : 194;
        missing = new CheckboxNode { Position = new(28, y), Size = new(200, 24), String = "없는 것만 보기" };
        missing.OnClick = value =>
        {
            if (!sync && context.Length > 0)
            { Restore(addon); config.ExchangeCategoryHideOwned[context] = value; save(); lastRead = 0; }
        };
        missing.AttachNode(addon);
        consumables = new CheckboxNode { Position = new(244, y), Size = new(190, 24), String = "소모품 제외", IsChecked = config.MerchantHideConsumables,
            TextTooltip = "약품·요리·염료·촉매를 제외합니다. 등록 수집품은 유지합니다." };
        consumables.OnClick = value => { if (!sync) { Restore(addon); config.MerchantHideConsumables = value; save(); lastRead = 0; } };
        consumables.AttachNode(addon);
        calculate = new TextButtonNode { Position = new(556, y - 3), Size = new(176, 28), String = "필요 재화", OnClick = () => currency.Open() };
        calculate.AttachNode(addon);
        filtered = new ListNode<ExchangeRow, ExchangeRowNode> { Position = new(list->OwnerNode->X, list->OwnerNode->Y),
            Size = new(list->OwnerNode->Width, list->OwnerNode->Height), OptionsList = [], IsVisible = false, AutoResetScroll = false, OnItemSelected = Select };
        filtered.NoResultsTextNode.String = "해당하는 품목이 없습니다."; filtered.AttachNode(addon);
        shown = []; lastRead = 0;
    }

    private void Select(ExchangeRow? row)
    {
        if (row is null || boundAddon == 0 || !config.MerchantTools || !replaced) return;
        var addon = (AtkUnitBase*)boundAddon; var list = addon->GetComponentListById(ListId);
        if (list is null || !TryRead(addon, list, out var rows, out var key) || key != row.Context ||
            row.OriginalIndex < 0 || row.OriginalIndex >= rows.Count || rows[row.OriginalIndex].ItemId != row.ItemId ||
            rows[row.OriginalIndex].Cost != row.Cost) return;
        if (list->GetItemDisabledState(row.OriginalIndex)) return;
        list->IsItemInteractionEnabled = true;
        try { list->SelectItem(row.OriginalIndex, false); list->DispatchItemEvent(row.OriginalIndex, AtkEventType.ListItemClick); }
        finally { list->IsItemInteractionEnabled = false; }
    }

    private void Restore(AtkUnitBase* addon)
    {
        var list = addon->GetComponentListById(ListId);
        var changed = replaced || filtered is { IsVisible: true };
        originalInput.Restore(list);
        if (replaced && list is not null && list->OwnerNode is not null) { list->OwnerNode->SetAlpha(alpha); list->IsItemInteractionEnabled = interactive; }
        replaced = false;
        lastApplied = null;
        if (filtered is not null) filtered.IsVisible = false;
        if (changed) addon->UpdateCollisionNodeList(false);
    }
    private void Detach(AtkUnitBase* addon)
    {
        if (boundAddon != (nint)addon) return;
        Restore(addon); merchants.ClearDisplayedShop(addon->Id);
        filtered?.Dispose(); filtered = null; missing?.Dispose(); missing = null; consumables?.Dispose(); consumables = null; calculate?.Dispose(); calculate = null;
        foreach (var (id, y) in positions) { var node = addon->GetNodeById(id); if (node is not null) node->SetYFloat(y); }
        var list = addon->GetComponentListById(ListId); if (list is not null && list->OwnerNode is not null) list->SetSize(list->OwnerNode->Width, listHeight);
        positions.Clear(); boundAddon = 0; context = ""; shown = []; renderFailed = false;
    }
    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AtkUnitBase*)boundAddon); }
}
