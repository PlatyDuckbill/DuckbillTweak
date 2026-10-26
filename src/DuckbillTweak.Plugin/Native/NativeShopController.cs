using System.Diagnostics;
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
    private readonly Configuration config;
    private readonly Action save;
    private readonly NativeCurrencyAddon currency;
    private readonly AddonController<AddonShop> controller;
    private CheckboxNode? missing;
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

    public NativeShopController(MerchantService merchants, Configuration config, Action save, NativeCurrencyAddon currency)
    {
        this.merchants = merchants; this.config = config; this.save = save; this.currency = currency;
        controller = new AddonController<AddonShop> { AddonName = "Shop", OnUpdate = Update, OnFinalize = Detach };
        controller.Enable();
    }

    private static bool Shape(AddonShop* addon) => addon->RootNode is not null &&
        addon->RootNode->Width == 584 && addon->RootNode->Height == 512 && addon->BuyList is not null &&
        addon->BuyList->OwnerNode is not null && addon->BuyList->OwnerNode->NodeId == 16 &&
        addon->GetNodeById(6) is not null && addon->GetNodeById(7) is not null &&
        addon->GetNodeById(12) is not null && addon->GetNodeById(13) is not null;

    private void Update(AddonShop* addon)
    {
        if (!config.MerchantTools || !Shape(addon)) { if (boundAddon == (nint)addon) Detach(addon); return; }
        var list = addon->BuyList;
        var selling = (list->OwnerNode->NodeFlags & NodeFlags.Visible) != 0;
        if (!selling) { if (boundAddon == (nint)addon) Detach(addon); return; }
        if (boundAddon == 0) Attach(addon);
        if (filteredList is null || missing is null || categories is null) return;
        filteredList.Update();
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 300) return;
        lastRead = Stopwatch.GetTimestamp();
        missing.IsEnabled = false;
        var agent = AgentShop.Instance();
        if (agent is null || agent->AddonId != addon->Id || agent->ItemReceive is null ||
            agent->ItemReceiveCount is < 0 or > 1000 || agent->ItemReceiveCount != list->ListLength ||
            agent->ItemCost is null || agent->ItemCostCount != agent->ItemReceiveCount * 3)
        { RestoreList(addon); return; }
        var view = merchants.View;
        var next = new List<NativeShopRow>();
        var itemInfo = view.Offers.SelectMany(o => o.Rewards).Select(r => r.Item).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        for (var index = 0; index < agent->ItemReceiveCount; index++)
        {
            ref var item = ref agent->ItemReceive[index];
            var baseId = item.ItemId >= 1_000_000 ? item.ItemId - 1_000_000 : item.ItemId;
            if (baseId == 0 || !itemInfo.TryGetValue(baseId, out var detail)) { RestoreList(addon); return; }
            ref var cost = ref agent->ItemCost[index * 3];
            if (cost.ItemId != 1) { RestoreList(addon); return; }
            next.Add(new(index, item.ItemId, item.ItemIconId, item.ItemName.ToString(), cost.ItemCount.ToString("N0"),
                item.OwnedItemCount.ToString("N0"), detail.CategoryId, !list->GetItemDisabledState(index)));
        }
        var options = new[] { new CategoryOption(0, "모든 종류") }.Concat(next.Select(r =>
            new CategoryOption(r.CategoryId, itemInfo[r.RawItemId >= 1_000_000 ? r.RawItemId - 1_000_000 : r.RawItemId].Category))
            .DistinctBy(c => c.Id).OrderBy(c => c.Name)).ToList();
        missing.IsEnabled = true;
        if (!categories.Options.SequenceEqual(options))
        {
            categories.Options = options;
            categories.SelectedOption = options.FirstOrDefault(o => o.Id == categoryId) ?? options[0];
            categoryId = categories.SelectedOption.Id;
        }
        var hide = config.MerchantCategoryHideOwned.GetValueOrDefault(categoryId, config.MerchantHideOwned);
        syncing = true; missing.IsChecked = hide; syncing = false;
        var filtered = next.Where(r => (categoryId == 0 || r.CategoryId == categoryId) &&
            (!hide || view.Ownership.GetValueOrDefault(r.RawItemId >= 1_000_000 ? r.RawItemId - 1_000_000 : r.RawItemId) != Ownership.Owned)).ToArray();
        if (!hide && categoryId == 0) { RestoreList(addon); return; }
        if (!rows.SequenceEqual(filtered)) { rows = filtered; filteredList.OptionsList = filtered.ToList(); }
        list->OwnerNode->SetAlpha(0);
        list->IsItemInteractionEnabled = false;
        filteredList.IsVisible = true;
        usingFilteredList = true;
    }

    private void Attach(AddonShop* addon)
    {
        boundAddon = (nint)addon;
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
        missing = new CheckboxNode { Position = new(28, 84), Size = new(152, 24), String = "없는 것만 보기" };
        missing.OnClick = value =>
        {
            if (syncing) return;
            if (categoryId == 0) { config.MerchantHideOwned = value; config.MerchantCategoryHideOwned.Clear(); }
            else config.MerchantCategoryHideOwned[categoryId] = value;
            save(); lastRead = 0;
        };
        missing.AttachNode(&addon->AtkUnitBase);
        categories = new DropDownNode<CategoryOption> { Position = new(190, 80), Size = new(182, 28),
            GetLabelFunction = c => c.Name, Options = [new(0, "모든 종류")] };
        categories.SelectedOption = categories.Options[0];
        categories.OnOptionSelected = c => { categoryId = c.Id; lastRead = 0; };
        categories.AttachNode(&addon->AtkUnitBase);
        calculate = new TextButtonNode { Position = new(390, 80), Size = new(164, 28), String = "필요 재화",
            OnClick = () => currency.Open() };
        calculate.AttachNode(&addon->AtkUnitBase);
        filteredList = new ListNode<NativeShopRow, NativeShopRowNode> { Position = new(list->OwnerNode->X, listY + 28),
            Size = new(list->OwnerNode->Width, listHeight - 28), OptionsList = [], IsVisible = false, OnItemSelected = Select };
        filteredList.NoResultsTextNode.String = "해당하는 품목이 없습니다.";
        filteredList.AttachNode(&addon->AtkUnitBase);
        rows = []; lastRead = 0;
    }

    private void Select(NativeShopRow? row)
    {
        if (row is null || !config.MerchantTools || boundAddon == 0 || !usingFilteredList) return;
        var addon = (AddonShop*)boundAddon;
        var agent = AgentShop.Instance();
        if (!Shape(addon) || agent is null || agent->AddonId != addon->Id || agent->ItemReceive is null ||
            row.OriginalIndex < 0 || row.OriginalIndex >= agent->ItemReceiveCount ||
            row.OriginalIndex >= addon->BuyList->ListLength || agent->ItemReceive[row.OriginalIndex].ItemId != row.RawItemId) return;
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
        if (filteredList is not null) filteredList.IsVisible = false;
        if (usingFilteredList && addon->BuyList is not null && addon->BuyList->OwnerNode is not null)
        { addon->BuyList->OwnerNode->SetAlpha(originalAlpha); addon->BuyList->IsItemInteractionEnabled = originalInteraction; }
        usingFilteredList = false;
    }

    private void Detach(AddonShop* addon)
    {
        if (boundAddon != (nint)addon) return;
        RestoreList(addon);
        filteredList?.Dispose(); filteredList = null;
        missing?.Dispose(); missing = null; categories?.Dispose(); categories = null; calculate?.Dispose(); calculate = null;
        foreach (var (id, y) in shifted) { var node = addon->GetNodeById(id); if (node is not null) node->SetYFloat(y); }
        shifted.Clear();
        if (addon->BuyList is not null && addon->BuyList->OwnerNode is not null)
        { addon->BuyList->OwnerNode->SetYFloat(listY); addon->BuyList->SetSize(addon->BuyList->OwnerNode->Width, listHeight); }
        boundAddon = 0; rows = []; lastRead = 0;
    }

    public void Dispose() { controller.Dispose(); if (boundAddon != 0) Detach((AddonShop*)boundAddon); }
}
