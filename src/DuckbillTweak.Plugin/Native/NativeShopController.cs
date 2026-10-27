using System.Diagnostics;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Native controls in the inspected KR Shop ULD. The agent's item array is never reordered.</summary>
internal sealed unsafe class NativeShopController : IDisposable
{
    private readonly MerchantService merchants;
    private readonly NativeCurrencyAddon currency;
    private readonly IPluginLog log;
    private readonly AddonController<AddonShop> controller;
    private readonly NativeListInput originalInput = new();
    private CheckboxNode? missing;
    private CheckboxNode? consumables;
    private DropDownNode<CategoryOption>? categories;
    private TextButtonNode? calculate;
    private ListNode<NativeShopRow, NativeShopRowNode>? filteredList;
    private readonly List<(uint Id, float Y)> shifted = [];
    private IReadOnlyList<NativeShopRow> rows = [];
    private uint categoryId;
    private long lastRead;
    private nint boundAddon;
    private float listY;
    private ushort listHeight;
    private byte originalAlpha;
    private bool originalInteraction;
    private bool usingFilteredList;
    private bool syncing;
    private bool renderFailed;
    private (int Total, int Visible)? lastApplied;

    public NativeShopController(MerchantService merchants, NativeCurrencyAddon currency, IPluginLog log)
    {
        this.merchants = merchants; this.currency = currency; this.log = log;
        controller = new AddonController<AddonShop> { AddonName = "Shop", OnUpdate = Update, OnFinalize = Detach,
            OnPreRefresh = addon =>
            {
                if (boundAddon == (nint)addon) { RestoreList(addon); merchants.ClearDisplayedShop(addon->Id); }
                lastRead = 0;
            } };
        controller.Enable();
    }

    private static bool Shape(AddonShop* addon) => addon->RootNode is not null &&
        addon->RootNode->Width == 584 && addon->RootNode->Height == 512 && addon->BuyList is not null &&
        addon->BuyList->OwnerNode is not null && addon->BuyList->OwnerNode->NodeId == 16 &&
        addon->GetNodeById(6) is not null && addon->GetNodeById(7) is not null &&
        addon->GetNodeById(12) is not null && addon->GetNodeById(13) is not null;

    private void Update(AddonShop* addon)
    {
        try { UpdateCore(addon); }
        catch (Exception ex)
        {
            // Managed rendering failures must not leave the game's input disabled.
            if (boundAddon == (nint)addon) { RestoreList(addon); merchants.ClearDisplayedShop(addon->Id); }
            if (!renderFailed) log.Error(ex, "Shop filter rendering failed; original list input restored. Reopen the shop to retry.");
            renderFailed = true;
        }
    }

    private void UpdateCore(AddonShop* addon)
    {
        if (!merchants.SessionReady || !addon->IsVisible || !Shape(addon)) { if (boundAddon == (nint)addon) Detach(addon); return; }
        var list = addon->BuyList;
        var selling = (list->OwnerNode->NodeFlags & NodeFlags.Visible) != 0;
        if (!selling) { if (boundAddon == (nint)addon) Detach(addon); return; }
        if (boundAddon == 0) Attach(addon);
        if (filteredList is null || missing is null || categories is null) return;
        var filters = merchants.FiltersFor(addon->Id);
        syncing = true;
        if (missing.IsChecked != filters.HideOwned) missing.IsChecked = filters.HideOwned;
        if (consumables is not null && consumables.IsChecked != filters.HideConsumables) consumables.IsChecked = filters.HideConsumables;
        syncing = false;
        var requested = filters.HideOwned || categoryId != 0 || filters.HideConsumables;
        if (!requested || renderFailed) RestoreList(addon);
        if (renderFailed) return;
        if (usingFilteredList)
        {
            if (!originalInput.Suppress(list, out var dirty)) { RestoreList(addon); return; }
            filteredList.Update();
            list->OwnerNode->SetAlpha(0); list->IsItemInteractionEnabled = false;
            if (dirty) addon->UpdateCollisionNodeList(false);
        }
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 300) return;
        lastRead = Stopwatch.GetTimestamp();
        var agent = AgentShop.Instance();
        if (agent is null || agent->AddonId != addon->Id || agent->ItemReceive is null ||
            agent->ItemReceiveCount is < 0 or > 1000 || agent->ItemReceiveCount != list->ListLength ||
            agent->ItemCost is null || agent->ItemCostCount != agent->ItemReceiveCount * 3)
        { RestoreList(addon); merchants.ClearDisplayedShop(addon->Id); return; }
        var view = merchants.View;
        var next = new List<NativeShopRow>();
        var offers = new List<Offer>();
        var balances = new Dictionary<CurrencyKey, long>();
        var itemInfo = view.Offers.SelectMany(o => o.Rewards).Select(r => r.Item).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        for (var index = 0; index < agent->ItemReceiveCount; index++)
        {
            ref var item = ref agent->ItemReceive[index];
            var baseId = item.ItemId >= 1_000_000 ? item.ItemId - 1_000_000 : item.ItemId;
            var detail = itemInfo.GetValueOrDefault(baseId) ?? merchants.ItemDetails(baseId);
            if (baseId == 0 || detail is null) { RestoreList(addon); merchants.ClearDisplayedShop(addon->Id); return; }
            itemInfo[baseId] = detail;
            ref var cost = ref agent->ItemCost[index * 3];
            if (cost.ItemId != 1) { RestoreList(addon); merchants.ClearDisplayedShop(addon->Id); return; }
            next.Add(new(index, item.ItemId, item.ItemIconId, merchants.ItemDisplayName(item.ItemId), cost.ItemCount.ToString("N0"),
                item.OwnedItemCount.ToString("N0"), detail.CategoryId, !list->GetItemDisabledState(index),
                merchants.ItemOwnership(item.ItemId, item.OwnedItemCount) == Ownership.Owned, cost.ItemCount, item.ItemCount));
            var price = merchants.NativePrice(0, cost.ItemId, cost.ItemCount);
            balances[price.Key] = cost.OwnedItemCount;
            if (merchants.NativeReward(item.ItemId, item.ItemCount, [price]) is { } reward)
                offers.Add(merchants.DisplayedOffer($"gil:{addon->Id}:{index}", [reward], [price], !list->GetItemDisabledState(index)));
        }
        merchants.PublishDisplayedShop(addon->Id, $"gil:{merchants.View.NpcId}", offers, balances, next.Where(r => r.OwnedAlready)
            .Select(r => r.RawItemId >= 1_000_000 ? r.RawItemId - 1_000_000 : r.RawItemId).ToHashSet(), next.Count);
        var options = new[] { new CategoryOption(0, "모든 종류") }.Concat(next.Select(r =>
            new CategoryOption(r.CategoryId, itemInfo[r.RawItemId >= 1_000_000 ? r.RawItemId - 1_000_000 : r.RawItemId].Category))
            .DistinctBy(c => c.Id).OrderBy(c => merchants.CategoryOrder(c.Id))).ToList();
        missing.IsEnabled = true;
        if (!categories.Options.SequenceEqual(options))
        {
            categories.Options = options;
            categories.SelectedOption = options.FirstOrDefault(o => o.Id == categoryId) ?? options[0];
            categoryId = categories.SelectedOption.Id;
        }
        missing.TextTooltip = merchants.OwnershipHelp;
        var filtered = next.Where(r => (categoryId == 0 || r.CategoryId == categoryId) &&
            (!filters.HideOwned || !merchants.CanHideOwned(r.RawItemId) || !r.OwnedAlready) &&
            (!filters.HideConsumables || !merchants.IsExcludedSupply(r.RawItemId))).ToArray();
        // A filter setting alone must not replace the native list when it removes no rows.
        if (filtered.Length == next.Count) { RestoreList(addon); return; }
        var dataChanged = !rows.SequenceEqual(filtered);
        if (dataChanged)
        {
            if (!rows.Select(r => (r.OriginalIndex, r.RawItemId)).SequenceEqual(filtered.Select(r => (r.OriginalIndex, r.RawItemId)))) filteredList.ResetScroll();
            filteredList.OptionsList = filtered.ToList(); rows = filtered;
        }
        filteredList.Update();
        var changed = !usingFilteredList || !filteredList.IsVisible || dataChanged;
        // Commit the replacement only after every visible row has rendered successfully.
        if (!usingFilteredList) { originalAlpha = list->OwnerNode->Color.A; originalInteraction = list->IsItemInteractionEnabled; }
        if (!originalInput.Suppress(list, out var inputChanged)) { RestoreList(addon); return; }
        filteredList.IsVisible = true;
        usingFilteredList = true;
        list->OwnerNode->SetAlpha(0);
        list->IsItemInteractionEnabled = false;
        if (changed || inputChanged) addon->UpdateCollisionNodeList(false);
        if (lastApplied != (next.Count, filtered.Length))
        {
            log.Information("Shop filter applied: Rows={Rows}; Visible={Visible}", next.Count, filtered.Length);
            lastApplied = (next.Count, filtered.Length);
        }
    }

    private void Attach(AddonShop* addon)
    {
        boundAddon = (nint)addon;
        categoryId = 0; merchants.ResetFilters(addon->Id);
        var list = addon->BuyList;
        listY = list->OwnerNode->Y; listHeight = list->OwnerNode->Height;
        originalAlpha = list->OwnerNode->Color.A; originalInteraction = list->IsItemInteractionEnabled;
        foreach (var id in new uint[] { 7, 12, 13 })
        {
            var node = addon->GetNodeById(id);
            shifted.Add((id, node->Y)); node->SetYFloat(node->Y + 28);
        }
        list->OwnerNode->SetYFloat(listY + 28);
        list->SetSize(list->OwnerNode->Width, (ushort)(listHeight - 28));
        missing = new CheckboxNode { Position = new(28, 84), Size = new(144, 24), String = ShopItemPolicy.OwnedFilterLabel };
        missing.OnClick = value =>
        {
            if (syncing) return;
            RestoreList(addon);
            merchants.FiltersFor(addon->Id).HideOwned = value; lastRead = 0;
        };
        missing.AttachNode(&addon->AtkUnitBase);
        consumables = new CheckboxNode { Position = new(174, 84), Size = new(132, 24), String = ShopItemPolicy.FilterLabel,
            TextTooltip = ShopItemPolicy.FilterHelp };
        consumables.OnClick = value => { if (!syncing) { RestoreList(addon); merchants.FiltersFor(addon->Id).HideConsumables = value; lastRead = 0; } };
        consumables.AttachNode(&addon->AtkUnitBase);
        categories = new DropDownNode<CategoryOption> { Position = new(310, 80), Size = new(122, 28),
            GetLabelFunction = c => c.Name, Options = [new(0, "모든 종류")] };
        categories.SelectedOption = categories.Options[0];
        categories.OnOptionSelected = c => { categoryId = c.Id; filteredList?.ResetScroll(); lastRead = 0; };
        categories.AttachNode(&addon->AtkUnitBase);
        calculate = new TextButtonNode { Position = new(444, 80), Size = new(110, 28), String = "필요 재화",
            OnClick = () => currency.OpenForShop(addon->Id) };
        calculate.AttachNode(&addon->AtkUnitBase);
        filteredList = new ListNode<NativeShopRow, NativeShopRowNode> { Position = new(list->OwnerNode->X, listY + 28),
            Size = new(list->OwnerNode->Width, listHeight - 28), OptionsList = [], IsVisible = false, AutoResetScroll = false, OnItemSelected = Select };
        filteredList.NoResultsTextNode.String = "해당하는 품목이 없습니다.";
        filteredList.AttachNode(&addon->AtkUnitBase);
        rows = []; lastRead = 0;
    }

    private void Select(NativeShopRow? row)
    {
        if (row is null || !merchants.SessionReady || boundAddon == 0 || !usingFilteredList || !rows.Contains(row)) return;
        var addon = (AddonShop*)boundAddon;
        var agent = AgentShop.Instance();
        if (!addon->IsVisible || !Shape(addon) || agent is null || agent->AddonId != addon->Id || agent->ItemReceive is null ||
            agent->ItemCost is null || agent->ItemReceiveCount is < 0 or > 1000 || agent->ItemCostCount != agent->ItemReceiveCount * 3 ||
            (addon->BuyList->OwnerNode->NodeFlags & NodeFlags.Visible) == 0 ||
            row.OriginalIndex < 0 || row.OriginalIndex >= agent->ItemReceiveCount ||
            row.OriginalIndex >= addon->BuyList->ListLength || agent->ItemReceive[row.OriginalIndex].ItemId != row.RawItemId ||
            agent->ItemReceive[row.OriginalIndex].ItemCount != row.Quantity ||
            agent->ItemCost[row.OriginalIndex * 3].ItemId != 1 || agent->ItemCost[row.OriginalIndex * 3].ItemCount != row.UnitPrice) return;
        // Only a real left-click reaches here. Forward its original index, never a filtered row number.
        var list = addon->BuyList;
        if (list->GetItemDisabledState(row.OriginalIndex)) return;
        list->IsItemInteractionEnabled = true;
        try
        {
            list->SelectItem(row.OriginalIndex, false);
            list->DispatchItemEvent(row.OriginalIndex, AtkEventType.ListItemClick);
        }
        finally { list->IsItemInteractionEnabled = false; }
    }

    private void RestoreList(AddonShop* addon)
    {
        // Restore native input before touching any custom node.
        var changed = usingFilteredList || filteredList is { IsVisible: true };
        originalInput.Restore(addon->BuyList);
        if (usingFilteredList && addon->BuyList is not null && addon->BuyList->OwnerNode is not null)
        { addon->BuyList->OwnerNode->SetAlpha(originalAlpha); addon->BuyList->IsItemInteractionEnabled = originalInteraction; }
        usingFilteredList = false;
        lastApplied = null;
        if (filteredList is not null) filteredList.IsVisible = false;
        if (changed) addon->UpdateCollisionNodeList(false);
    }

    private void Detach(AddonShop* addon)
    {
        if (boundAddon != (nint)addon) return;
        RestoreList(addon);
        merchants.ClearDisplayedShop(addon->Id);
        currency.CloseForShop(addon->Id);
        merchants.ForgetFilters(addon->Id);
        filteredList?.Dispose(); filteredList = null;
        missing?.Dispose(); missing = null; consumables?.Dispose(); consumables = null; categories?.Dispose(); categories = null; calculate?.Dispose(); calculate = null;
        foreach (var (id, y) in shifted) { var node = addon->GetNodeById(id); if (node is not null) node->SetYFloat(y); }
        shifted.Clear();
        if (addon->BuyList is not null && addon->BuyList->OwnerNode is not null)
        { addon->BuyList->OwnerNode->SetYFloat(listY); addon->BuyList->SetSize(addon->BuyList->OwnerNode->Width, listHeight); }
        addon->UpdateCollisionNodeList(false);
        boundAddon = 0; rows = []; lastRead = 0; renderFailed = false;
    }

    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AddonShop*)boundAddon); }
}
