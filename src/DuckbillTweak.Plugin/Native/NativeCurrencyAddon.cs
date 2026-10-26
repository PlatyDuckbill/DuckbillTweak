using System.Diagnostics;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeCurrencyAddon(MerchantService merchants, Configuration config, Action save) : NativeAddon
{
    private CheckboxNode? missing;
    private CheckboxNode? consumables;
    private DropDownNode<CategoryOption>? categories;
    private StringDropDownNode? trade;
    private TextNode? summary;
    private TextNode? notice;
    private ListNode<TotalRow, TotalRowNode>? totals;
    private ListNode<CalculationRow, CalculationRowNode>? items;
    private IReadOnlyList<Offer>? previousOffers;
    private long lastRead;
    private uint categoryId;
    private bool syncing;
    private ushort sourceAddon;
    private long sourceSession;
    private string sourceContext = "";

    public void OpenForShop(ushort addon)
    {
        if (!merchants.IsShopOpen(addon)) return;
        if (sourceAddon != addon || sourceSession != merchants.SessionGeneration)
        { categoryId = 0; sourceContext = ""; previousOffers = null; }
        sourceAddon = addon; sourceSession = merchants.SessionGeneration; lastRead = 0;
        Open();
    }

    public void CloseForShop(ushort addon)
    {
        if (sourceAddon != addon) return;
        sourceAddon = 0;
        Close();
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        merchants.Visible = true;
        missing = new CheckboxNode { Position = new(28, 50), Size = new(200, 24), String = "없는 것만 계산", IsChecked = config.ShopFilter.HideOwned };
        missing.OnClick = value => { if (!syncing) { config.ShopFilter.HideOwned = value; save(); lastRead = 0; } };
        missing.AttachNode(this);
        consumables = new CheckboxNode { Position = new(260, 50), Size = new(220, 24), String = "소모품 제외", IsChecked = config.MerchantHideConsumables,
            TextTooltip = "약품·요리·염료·촉매·효과 설명서·PvP 소모품·폭죽을 제외합니다. 등록 수집품은 유지합니다." };
        consumables.OnClick = value => { if (!syncing) { config.MerchantHideConsumables = value; save(); lastRead = 0; } };
        consumables.AttachNode(this);
        categories = new DropDownNode<CategoryOption> { Position = new(28, 84), Size = new(290, 28),
            GetLabelFunction = item => item.Name, Options = [] };
        categories.OnOptionSelected = option => { if (!syncing) { categoryId = option.Id; lastRead = 0; } };
        categories.AttachNode(this);
        trade = new StringDropDownNode { Position = new(334, 84), Size = new(220, 28),
            Options = ["거래 여부 전체", "거래 가능", "거래 불가"] };
        trade.SelectedOption = trade.Options[(int)config.ShopFilter.Trade];
        trade.OnOptionSelected = option => { if (!syncing) { config.ShopFilter.Trade = (TradeFilter)trade.Options.IndexOf(option); save(); lastRead = 0; } };
        trade.AttachNode(this);
        new TextButtonNode { Position = new(588, 84), Size = new(132, 28), String = "전체 보기", OnClick = () =>
        {
            syncing = true;
            config.ShopFilter.HideOwned = false; config.ShopFilter.Trade = TradeFilter.All; categoryId = 0;
            config.MerchantHideConsumables = false; consumables.IsChecked = false;
            missing.IsChecked = false; trade.SelectedOption = trade.Options[0];
            categories.SelectedOption = categories.Options.FirstOrDefault(); syncing = false; save(); lastRead = 0;
        } }.AttachNode(this);
        summary = Label("", 28, 118, 692);
        notice = Label("", 28, 146, 692, fontSize: 13);
        Label("재화", 58, 180, 280);
        Label("필요", 350, 180, 112, AlignmentType.Right);
        Label("보유", 472, 180, 112, AlignmentType.Right);
        Label("부족", 594, 180, 112, AlignmentType.Right);
        new HorizontalLineNode { Position = new(28, 203), Size = new(692, 1) }.AttachNode(this);
        totals = new ListNode<TotalRow, TotalRowNode> { Position = new(28, 206), Size = new(704, 90), OptionsList = [], AutoResetScroll = false };
        totals.NoResultsTextNode.String = "계산할 품목이 없습니다.";
        totals.AttachNode(this);
        Label("계산 대상 품목", 28, 310, 300);
        Label("품목", 58, 338, 280);
        Label("받는 수", 344, 338, 52, AlignmentType.Right);
        Label("1회 교환 비용", 412, 338, 206);
        Label("보유 판정", 624, 338, 82, AlignmentType.Right);
        new HorizontalLineNode { Position = new(28, 361), Size = new(692, 1) }.AttachNode(this);
        items = new ListNode<CalculationRow, CalculationRowNode> { Position = new(28, 364), Size = new(704, 182), OptionsList = [], AutoResetScroll = false };
        items.NoResultsTextNode.String = "계산할 품목이 없습니다.";
        items.AttachNode(this);
        Label("각 행은 1회 교환 기준입니다. 묶음 품목의 비용은 한 번만 합산합니다.", 28, 552, 692, fontSize: 12);
        previousOffers = null; lastRead = 0;
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (totals is null || items is null || categories is null || summary is null || notice is null) return;
        if (sourceSession != merchants.SessionGeneration || !merchants.IsShopOpen(sourceAddon)) { Close(); return; }
        totals.Update(); items.Update();
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 500) return;
        lastRead = Stopwatch.GetTimestamp();
        if (!merchants.TryGetCalculation(sourceAddon, out var view, out var context))
        {
            summary.String = "현재 상점의 품목 정보를 확인하는 중입니다.";
            notice.String = "목록이 갱신되면 다시 계산합니다.";
            totals.NoResultsTextNode.String = "상점 정보 확인 대기";
            items.NoResultsTextNode.String = "상점 정보 확인 대기";
            if (totals.OptionsList.Count > 0 || items.OptionsList.Count > 0)
            { totals.OptionsList = []; items.OptionsList = []; addon->UpdateCollisionNodeList(false); }
            return;
        }
        if (sourceContext != context) { sourceContext = context; categoryId = 0; previousOffers = null; totals.ResetScroll(); items.ResetScroll(); }
        syncing = true;
        if (consumables is not null && consumables.IsChecked != config.MerchantHideConsumables) consumables.IsChecked = config.MerchantHideConsumables;
        syncing = false;
        if (missing is not null) missing.TextTooltip = merchants.OwnershipHelp;
        if (!ReferenceEquals(previousOffers, view.Offers))
        {
            previousOffers = view.Offers;
            var options = new[] { new CategoryOption(0, "모든 품목 종류") }.Concat(view.Offers.SelectMany(o => o.Rewards)
                .Select(r => new CategoryOption(r.Item.CategoryId, r.Item.Category)).DistinctBy(c => c.Id).OrderBy(c => c.Name)).ToList();
            syncing = true;
            if (!categories.Options.SequenceEqual(options)) categories.Options = options;
            categories.SelectedOption = categories.Options.FirstOrDefault(c => c.Id == categoryId) ?? categories.Options[0];
            categoryId = categories.SelectedOption.Id;
            syncing = false;
        }
        var filter = new ShopFilter { HideOwned = config.ShopFilter.HideOwned, Trade = config.ShopFilter.Trade,
            HideConsumables = config.MerchantHideConsumables,
            FilterCategories = categoryId != 0, Categories = categoryId == 0 ? [] : [categoryId] };
        var offers = view.Offers.Where(o => filter.Matches(o, merchants.CalculationOwnership)).ToArray();
        var ids = PurchasePlanner.SelectDefaults(offers.Where(o => o.ConditionsKnown));
        var selected = offers.Where(o => ids.Contains(o.Id)).ToArray();
        var unknown = selected.Count(o => o.Prices.Any(p => !p.Resolved));
        var conditions = offers.Count(o => !o.ConditionsKnown);
        var uncertainOwnership = filter.HideOwned ? selected.SelectMany(o => o.Rewards).Select(r => r.Item.Id).Distinct()
            .Count(id => merchants.CalculationOwnership(id) == Ownership.Unknown) : 0;
        summary.String = $"현재 상점 분류 · {selected.Length}개 교환을 각 1회 계산" + (unknown > 0 ? " · 부분 합계" : "");
        var messages = new List<string>();
        if (unknown > 0) messages.Add($"비용 미확인 {unknown}건");
        if (conditions > 0) messages.Add($"조건 미확인 {conditions}건 제외");
        if (merchants.UnreadableDisplayedOffers > 0) messages.Add($"수량 미확인 {merchants.UnreadableDisplayedOffers}건 제외");
        if (uncertainOwnership > 0) messages.Add($"보유 미확인 {uncertainOwnership}종 포함");
        notice.String = messages.Count == 0 ? "현재 상점에 표시된 교환 수량과 가격 기준입니다." : string.Join(" · ", messages);
        notice.TextTooltip = string.Join("\n", messages) + "\n" + merchants.OwnershipHelp;
        totals.NoResultsTextNode.String = view.Offers.Count == 0 ? "현재 상점의 계산 가능한 품목 정보를 확인하지 못했습니다." :
            offers.Length == 0 ? "선택한 필터에 해당하는 품목이 없습니다." :
            selected.Length == 0 ? "구매 조건을 확인한 교환식이 없습니다." : "계산할 재화가 없습니다.";
        items.NoResultsTextNode.String = totals.NoResultsTextNode.String;
        var next = PurchasePlanner.Calculate(selected, merchants.CalculationQuantity).Select(t => new TotalRow(merchants.Icon(t.Key.ItemId),
            t.Name + (t.Key.Quality == ItemQuality.High ? " HQ" : "") + (t.Key.Collectability > 0 ? $" (소장가치 {t.Key.Collectability} 이상)" : ""),
            t.Required.ToString("N0"), Count(t.Owned), Count(t.Shortfall))).ToList();
        if (!totals.OptionsList.SequenceEqual(next))
        {
            if (!totals.OptionsList.Select(r => r.Name).SequenceEqual(next.Select(r => r.Name))) totals.ResetScroll();
            totals.OptionsList = next;
            addon->UpdateCollisionNodeList(false);
        }
        var itemRows = selected.Select(offer =>
        {
            var reward = offer.Rewards[0];
            var states = offer.Rewards.Select(r => merchants.CalculationOwnership(r.Item.Id)).ToArray();
            var owned = states.All(s => s == Ownership.Owned) ? "보유" : states.Any(s => s == Ownership.Unknown) ? "미확인" : "미보유";
            return new CalculationRow(reward.Item.Id + (reward.Quality == ItemQuality.High ? 1_000_000u : 0), merchants.Icon(reward.Item.Id),
                string.Join(" / ", offer.Rewards.Select(r => r.Item.Name + (r.Quality == ItemQuality.High ? " HQ" : "") + (offer.Rewards.Count > 1 ? $" ×{r.Quantity:N0}" : ""))),
                offer.Rewards.Count == 1 ? reward.Quantity.ToString("N0") : "묶음",
                string.Join(" / ", offer.Prices.Select(p => $"{p.Name} ×{p.Quantity:N0}")), owned);
        }).ToList();
        if (!items.OptionsList.SequenceEqual(itemRows))
        {
            if (!items.OptionsList.Select(r => r.ItemId).SequenceEqual(itemRows.Select(r => r.ItemId))) items.ResetScroll();
            items.OptionsList = itemRows;
            addon->UpdateCollisionNodeList(false);
        }
    }

    private TextNode Label(string text, float x, float y, float width, AlignmentType alignment = AlignmentType.Left, byte fontSize = 14)
    {
        var label = new TextNode { Position = new(x, y), Size = new(width, 24), String = text,
            FontSize = fontSize, LineSpacing = 18, AlignmentType = alignment, TextFlags = TextFlags.Ellipsis };
        label.AttachNode(this);
        return label;
    }

    private static string Count(long? value) => value?.ToString("N0") ?? "미확인";
    protected override void OnFinalize(AtkUnitBase* addon)
    {
        sourceAddon = 0; sourceContext = ""; previousOffers = null;
        merchants.Visible = false; missing = null; consumables = null; categories = null; trade = null; summary = null; notice = null; totals = null; items = null;
    }
}
