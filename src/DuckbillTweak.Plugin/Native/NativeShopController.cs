using System.Diagnostics;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Native controls in the inspected KR Shop ULD. The handler's items and visible-row map are never reordered.</summary>
internal sealed unsafe class NativeShopController : IDisposable
{
    private readonly MerchantService merchants;
    private readonly NativeCurrencyAddon currency;
    private readonly IPluginLog log;
    private readonly NativeCollisionRefresh collisions;
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
    private bool filteredListAttached;
    private bool syncing;
    private bool renderFailed;
    private string? lastReadFailure;
    private (int Total, int Visible)? lastApplied;

    public NativeShopController(MerchantService merchants, NativeCurrencyAddon currency, IPluginLog log, IAddonLifecycle lifecycle)
    {
        this.merchants = merchants; this.currency = currency; this.log = log;
        collisions = new(lifecycle, "Shop");
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
            if (dirty) collisions.Request(&addon->AtkUnitBase);
        }
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 300) return;
        lastRead = Stopwatch.GetTimestamp();
        if (!TrySource(addon, out var handler, out var failure)) { ReadFailed(addon, failure); return; }
        var view = merchants.View;
        var next = new List<NativeShopRow>();
        var offers = new List<Offer>();
        var balances = new Dictionary<CurrencyKey, long>();
        var itemInfo = view.Offers.SelectMany(o => o.Rewards).Select(r => r.Item).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        for (var index = 0; index < handler->VisibleItemsCount; index++)
        {
            var sourceIndex = handler->VisibleItems[index];
            ref var item = ref handler->Items[sourceIndex];
            var baseId = item.ItemId;
            var rawId = baseId + (item.IsHQ ? 1_000_000u : 0u);
            var detail = itemInfo.GetValueOrDefault(baseId) ?? merchants.ItemDetails(baseId);
            if (baseId is 0 or >= 1_000_000 || detail is null) { ReadFailed(addon, $"item unavailable: row={index}; item={baseId}"); return; }
            if (item.PriceBuy <= 0) { ReadFailed(addon, $"gil price unavailable: row={index}"); return; }
            itemInfo[baseId] = detail;
            next.Add(new(index, rawId, item.CategoryIcon, merchants.ItemDisplayName(rawId), item.PriceBuy.ToString("N0"),
                item.NumOwned >= 0 ? item.NumOwned.ToString("N0") : "미확인", detail.CategoryId, !list->GetItemDisabledState(index),
                merchants.ItemOwnership(rawId, item.NumOwned > 0 ? (uint)item.NumOwned : 0) == Ownership.Owned,
                (uint)item.PriceBuy, handler->Info.EventId.Id, sourceIndex, item.SubRowId));
            // A gil shop price is per item. StackSize is its inventory capacity, not a reward bundle.
            var price = merchants.NativePrice(0, 1, (uint)item.PriceBuy);
            if (merchants.NativeReward(rawId, 1, [price]) is { } reward)
                offers.Add(merchants.DisplayedOffer($"gil:{addon->Id}:{index}", [reward], [price], !list->GetItemDisabledState(index)));
        }
        lastReadFailure = null;
        merchants.PublishDisplayedShop(addon->Id, $"gil:{handler->Info.EventId.Id}", offers, balances, next.Where(r => r.OwnedAlready)
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
        if (!filteredListAttached)
        {
            filteredList.AttachNode(&addon->AtkUnitBase);
            filteredListAttached = true;
        }
        if (!originalInput.Suppress(list, out var inputChanged)) { RestoreList(addon); return; }
        filteredList.IsVisible = true;
        usingFilteredList = true;
        list->OwnerNode->SetAlpha(0);
        list->IsItemInteractionEnabled = false;
        if (changed || inputChanged) collisions.Request(&addon->AtkUnitBase);
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
        categories.OnOptionSelected = c =>
        {
            if (categoryId != c.Id) { RestoreList(addon); merchants.ResetFilters(addon->Id); }
            categoryId = c.Id; filteredList?.ResetScroll(); lastRead = 0;
        };
        categories.AttachNode(&addon->AtkUnitBase);
        calculate = new TextButtonNode { Position = new(444, 80), Size = new(110, 28), String = "필요 재화",
            OnClick = () => currency.OpenForShop(addon->Id) };
        calculate.AttachNode(&addon->AtkUnitBase);
        filteredList = new ListNode<NativeShopRow, NativeShopRowNode> { Position = new(list->OwnerNode->X, listY + 28),
            Size = new(list->OwnerNode->Width, listHeight - 28), OptionsList = [], IsVisible = false, AutoResetScroll = false, OnItemSelected = Select };
        filteredList.NoResultsTextNode.String = "해당하는 품목이 없습니다.";
        // Keep the entire unused subtree out of the addon's draw/collision lists.
        // Hiding its root alone can leave a list-sized mouse target over native rows.
        rows = []; lastRead = 0;
        collisions.Request(&addon->AtkUnitBase);
    }

    private void Select(NativeShopRow? row)
    {
        if (row is null || !merchants.SessionReady || boundAddon == 0 || !usingFilteredList || !rows.Contains(row)) return;
        var addon = (AddonShop*)boundAddon;
        if (!addon->IsVisible || !Shape(addon) || !TrySource(addon, out var handler, out _) ||
            (addon->BuyList->OwnerNode->NodeFlags & NodeFlags.Visible) == 0 ||
            handler->Info.EventId.Id != row.ShopId || row.OriginalIndex < 0 || row.OriginalIndex >= handler->VisibleItemsCount ||
            handler->VisibleItems[row.OriginalIndex] != row.SourceIndex) return;
        ref var item = ref handler->Items[row.SourceIndex];
        if (item.ItemId + (item.IsHQ ? 1_000_000u : 0u) != row.RawItemId || item.SubRowId != row.SubRowId ||
            item.PriceBuy <= 0 || (uint)item.PriceBuy != row.UnitPrice) return;
        // Only a real left-click reaches here. Forward its original index, never a filtered row number.
        var list = addon->BuyList;
        if (list->GetItemDisabledState(row.OriginalIndex)) return;
        list->IsItemInteractionEnabled = true;
        try
        {
            list->SelectItem(row.OriginalIndex, false);
            list->DispatchItemEvent(row.OriginalIndex, AtkEventType.ListItemClick);
        }
        finally
        {
            // The dispatched native callback can refresh/close the shop and restore input.
            if (boundAddon == (nint)addon && usingFilteredList && addon->BuyList == list)
                list->IsItemInteractionEnabled = false;
        }
    }

    private static bool TrySource(AddonShop* addon, out ShopEventHandler* handler, out string reason)
    {
        handler = null;
        reason = "gil shop binding unavailable";
        if (ShopEventHandler.AgentProxy.Addresses.Instance.Value == 0 || ShopEventHandler.StaticVirtualTablePointer is null) return false;
        var proxy = ShopEventHandler.AgentProxy.Instance();
        var agent = AgentShop.Instance();
        reason = "gil shop receiver unavailable";
        if (proxy is null || agent is null || agent->AddonId != addon->Id || proxy->AddonId != addon->Id ||
            agent->EventReceiver != &proxy->AtkEventInterface || proxy->Handler is null) return false;
        var source = proxy->Handler;
        reason = "gil shop handler type mismatch";
        if (source->VirtualTable != ShopEventHandler.StaticVirtualTablePointer ||
            source->Info.EventId.ContentId != EventHandlerContent.Shop) return false;
        // NeedSorting stays set for sorted shops and is also used when buying.
        // It is not a loading flag; validate the live row map and item data below.
        reason = $"gil shop not ready: mode={source->CurrentMode}; buyback={source->BuybackTabActive}";
        if (source->CurrentMode != 1 || source->BuybackTabActive) return false;
        reason = $"gil rows: list={addon->BuyList->ListLength}; visible={source->VisibleItemsCount}; items={source->ItemsCount}";
        if (source->ItemsCount < 0 || source->ItemsCount > source->Items.Length ||
            source->VisibleItemsCount < 0 || source->VisibleItemsCount > source->VisibleItems.Length ||
            source->VisibleItemsCount > source->ItemsCount || source->VisibleItemsCount != addon->BuyList->ListLength) return false;
        var seen = new HashSet<int>();
        for (var index = 0; index < source->VisibleItemsCount; index++)
        {
            var itemIndex = source->VisibleItems[index];
            if (itemIndex < 0 || itemIndex >= source->ItemsCount || !seen.Add(itemIndex))
            { reason = $"gil row mapping unavailable: row={index}"; return false; }
        }
        handler = source;
        reason = "";
        return true;
    }

    private void RestoreList(AddonShop* addon)
    {
        // Restore native input before touching any custom node.
        var changed = usingFilteredList || filteredListAttached;
        originalInput.Restore(addon->BuyList);
        if (usingFilteredList && addon->BuyList is not null && addon->BuyList->OwnerNode is not null)
        { addon->BuyList->OwnerNode->SetAlpha(originalAlpha); addon->BuyList->IsItemInteractionEnabled = originalInteraction; }
        usingFilteredList = false;
        lastApplied = null;
        if (filteredList is not null)
        {
            filteredList.IsVisible = false;
            if (filteredListAttached) filteredList.DetachNode();
        }
        filteredListAttached = false;
        if (changed) collisions.Request(&addon->AtkUnitBase);
    }

    private void ReadFailed(AddonShop* addon, string reason)
    {
        RestoreList(addon);
        merchants.ClearDisplayedShop(addon->Id);
        if (lastReadFailure != reason) log.Information("Shop snapshot waiting: {Reason}", reason);
        lastReadFailure = reason;
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
        collisions.Request(&addon->AtkUnitBase);
        collisions.Clear();
        boundAddon = 0; rows = []; lastRead = 0; renderFailed = false; lastReadFailure = null;
    }

    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AddonShop*)boundAddon); collisions.Dispose(); }
}
