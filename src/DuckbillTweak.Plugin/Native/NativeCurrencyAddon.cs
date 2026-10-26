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
    private ListNode<TotalRow, TotalRowNode>? totals;
    private IReadOnlyList<Offer>? previousOffers;
    private long lastRead;
    private uint categoryId;
    private bool syncing;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        merchants.Visible = true;
        missing = new CheckboxNode { Position = new(28, 50), Size = new(200, 24), String = "없는 것만 계산", IsChecked = config.ShopFilter.HideOwned };
        missing.OnClick = value => { config.ShopFilter.HideOwned = value; save(); lastRead = 0; };
        missing.AttachNode(this);
        consumables = new CheckboxNode { Position = new(260, 50), Size = new(220, 24), String = "소모품 제외", IsChecked = config.MerchantHideConsumables,
            TextTooltip = "약품·요리·염료·촉매를 제외합니다. 등록 수집품은 유지합니다." };
        consumables.OnClick = value => { if (!syncing) { config.MerchantHideConsumables = value; save(); lastRead = 0; } };
        consumables.AttachNode(this);
        categories = new DropDownNode<CategoryOption> { Position = new(28, 84), Size = new(230, 28),
            GetLabelFunction = item => item.Name, Options = [] };
        categories.OnOptionSelected = option => { categoryId = option.Id; lastRead = 0; };
        categories.AttachNode(this);
        trade = new StringDropDownNode { Position = new(276, 84), Size = new(170, 28),
            Options = ["거래 여부 전체", "거래 가능", "거래 불가"] };
        trade.SelectedOption = trade.Options[(int)config.ShopFilter.Trade];
        trade.OnOptionSelected = option => { config.ShopFilter.Trade = (TradeFilter)trade.Options.IndexOf(option); save(); lastRead = 0; };
        trade.AttachNode(this);
        new TextButtonNode { Position = new(466, 84), Size = new(110, 28), String = "전체 보기", OnClick = () =>
        {
            config.ShopFilter.HideOwned = false; config.ShopFilter.Trade = TradeFilter.All; categoryId = 0;
            config.MerchantHideConsumables = false; consumables.IsChecked = false;
            missing.IsChecked = false; trade.SelectedOption = trade.Options[0];
            categories.SelectedOption = categories.Options.FirstOrDefault(); save(); lastRead = 0;
        } }.AttachNode(this);
        summary = new TextNode { Position = new(28, 124), Size = new(548, 62), FontSize = 13,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap };
        summary.AttachNode(this);
        totals = new ListNode<TotalRow, TotalRowNode> { Position = new(28, 194), Size = new(548, 280), OptionsList = [], AutoResetScroll = false };
        totals.NoResultsTextNode.String = "계산할 품목이 없습니다.";
        totals.AttachNode(this);
        previousOffers = null; lastRead = 0;
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (totals is null || categories is null || summary is null) return;
        if (!config.MerchantTools) { Close(); return; }
        totals.Update();
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 500) return;
        lastRead = Stopwatch.GetTimestamp();
        var view = merchants.CalculationView;
        syncing = true;
        if (consumables is not null && consumables.IsChecked != config.MerchantHideConsumables) consumables.IsChecked = config.MerchantHideConsumables;
        syncing = false;
        if (missing is not null) missing.TextTooltip = merchants.OwnershipHelp;
        if (!ReferenceEquals(previousOffers, view.Offers))
        {
            previousOffers = view.Offers;
            categories.Options = new[] { new CategoryOption(0, "모든 품목 종류") }.Concat(view.Offers.SelectMany(o => o.Rewards)
                .Select(r => new CategoryOption(r.Item.CategoryId, r.Item.Category)).DistinctBy(c => c.Id).OrderBy(c => c.Name)).ToList();
            categories.SelectedOption = categories.Options.FirstOrDefault(c => c.Id == categoryId) ?? categories.Options[0];
            categoryId = categories.SelectedOption.Id;
        }
        var filter = new ShopFilter { HideOwned = config.ShopFilter.HideOwned, Trade = config.ShopFilter.Trade,
            HideConsumables = config.MerchantHideConsumables,
            FilterCategories = categoryId != 0, Categories = categoryId == 0 ? [] : [categoryId] };
        var offers = view.Offers.Where(o => filter.Matches(o, merchants.CalculationOwnership)).ToArray();
        var ids = PurchasePlanner.SelectDefaults(offers.Where(o => o.ConditionsKnown));
        var selected = offers.Where(o => ids.Contains(o.Id)).ToArray();
        var unknown = selected.Count(o => o.Prices.Any(p => !p.Resolved));
        var conditions = offers.Count(o => !o.ConditionsKnown);
        summary.String = $"{view.Name} · {merchants.CalculationScope} / 교환식 {selected.Length}개를 한 번씩 구매\n" +
            (unknown > 0 ? $"미확인 비용 {unknown}건 · 아래는 부분 합계입니다." : "게임 상점의 판매 조건과 할인은 별도 확인이 필요합니다.") +
            (conditions > 0 ? $" 판매 조건 미확인 {conditions}건 제외." : "") +
            (merchants.UnreadableDisplayedOffers > 0 ? $" 품목 수량 미확인 {merchants.UnreadableDisplayedOffers}건 제외." : "");
        var next = PurchasePlanner.Calculate(selected, merchants.CalculationQuantity).Select(t => new TotalRow(
            t.Name + (t.Key.Quality == ItemQuality.High ? " HQ" : "") + (t.Key.Collectability > 0 ? $" (소장가치 {t.Key.Collectability} 이상)" : ""),
            $"필요 {t.Required:N0}   보유 {Count(t.Owned)}   부족 {Count(t.Shortfall)}")).ToList();
        if (!totals.OptionsList.SequenceEqual(next))
        {
            if (!totals.OptionsList.Select(r => r.Name).SequenceEqual(next.Select(r => r.Name))) totals.ResetScroll();
            totals.OptionsList = next;
            addon->UpdateCollisionNodeList(false);
        }
    }

    private static string Count(long? value) => value?.ToString("N0") ?? "미확인";
    protected override void OnFinalize(AtkUnitBase* addon)
    {
        merchants.Visible = false; missing = null; consumables = null; categories = null; trade = null; summary = null; totals = null;
    }
}
