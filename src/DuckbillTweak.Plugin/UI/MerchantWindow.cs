using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DuckbillTweak.Core.Shops;
using DuckbillTweak.Plugin.Shops;

namespace DuckbillTweak.Plugin.UI;

internal sealed class MerchantWindow : Window
{
    private readonly MerchantService service;
    private readonly Configuration config;
    private readonly Action save;
    private readonly Dictionary<string, bool> choices = [];
    private IReadOnlyList<Offer>? previousOffers;
    private int manualId;
    private bool special = true;

    public MerchantWindow(MerchantService service, Configuration config, Action save) : base("상인 보조 · DuckbillTweak###DuckbillMerchant")
    {
        this.service = service; this.config = config; this.save = save;
        Size = new Vector2(1050, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(650, 420), MaximumSize = new Vector2(2200, 1600) };
    }

    public override void OnOpen() => service.Visible = true;
    public override void OnClose() => service.Visible = false;

    public override void Draw()
    {
        if (!config.MerchantTools) { ImGui.TextWrapped("설정에서 '상인 목록과 필요 재화'를 켜십시오."); return; }
        var view = service.View;
        if (!ReferenceEquals(previousOffers, view.Offers)) { previousOffers = view.Offers; choices.Clear(); }
        ImGui.TextUnformatted(string.IsNullOrEmpty(view.Name) ? "상인 NPC 선택" : $"{view.Name}  #{view.NpcId}");
        ImGui.SameLine();
        if (ImGui.Button("현재 대상 새로고침###Refresh")) service.RefreshTarget();
        if (view.Message is { } message) ImGui.TextWrapped(message);
        ImGui.TextDisabled("게임 목록을 변경하지 않는 보조 화면 · 소지품/장비함/착용 장비 + 수집 등록 기준");
        if (ImGui.CollapsingHeader("직접 상점 지정###Manual"))
        {
            ImGui.SetNextItemWidth(160);
            ImGui.InputInt("상점 ID###ShopId", ref manualId);
            ImGui.SameLine(); ImGui.Checkbox("교환 상점 (SpecialShop)###ShopType", ref special);
            ImGui.SameLine();
            if (ImGui.Button("열기###LoadShop") && manualId > 0) service.RequestShop((uint)manualId, special);
        }
        var filter = config.ShopFilter;
        var hideOwned = filter.HideOwned;
        if (ImGui.Checkbox("있는 것 제외###HideOwned", ref hideOwned))
        {
            filter.HideOwned = hideOwned; filter.CategoryHideOwned.Clear(); save();
        }
        ImGui.SameLine();
        if (ImGui.Button("전체 보기###ShowAll"))
        {
            filter.HideOwned = false; filter.CategoryHideOwned.Clear(); filter.Categories.Clear(); filter.FilterCategories = false;
            filter.Trade = TradeFilter.All; filter.Search = ""; choices.Clear(); save();
        }
        var search = filter.Search;
        ImGui.SetNextItemWidth(230);
        if (ImGui.InputText("품목 검색###Search", ref search, 200)) { filter.Search = search; save(); }
        ImGui.SameLine();
        var trade = (int)filter.Trade;
        ImGui.SetNextItemWidth(170);
        if (ImGui.Combo("거래 여부###Trade", ref trade, "전체\0거래 가능\0거래 불가\0")) { filter.Trade = (TradeFilter)trade; save(); }

        if (ImGui.CollapsingHeader("종류 및 종류별 '있는 것 제외'###Categories"))
        {
            if (ImGui.Button("모든 종류###AllCategories")) { filter.Categories.Clear(); filter.FilterCategories = false; save(); }
            if (ImGui.BeginTable("###CategoryTable", 2, ImGuiTableFlags.SizingStretchSame))
            {
                foreach (var category in view.Offers.SelectMany(x => x.Rewards).Select(r => r.Item)
                             .DistinctBy(i => i.CategoryId).OrderBy(i => i.Category))
                {
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    var included = !filter.FilterCategories || filter.Categories.Contains(category.CategoryId);
                    if (ImGui.Checkbox($"{category.Category}###Include:{category.CategoryId}", ref included))
                    {
                        if (!filter.FilterCategories)
                        {
                            filter.Categories = view.Offers.SelectMany(x => x.Rewards).Select(r => r.Item.CategoryId).ToHashSet();
                            filter.FilterCategories = true;
                        }
                        if (included) filter.Categories.Add(category.CategoryId); else filter.Categories.Remove(category.CategoryId);
                        save();
                    }
                    ImGui.TableNextColumn();
                    var hide = filter.CategoryHideOwned.GetValueOrDefault(category.CategoryId, filter.HideOwned);
                    if (ImGui.Checkbox($"있는 것 제외###CategoryHide:{category.CategoryId}", ref hide))
                    { filter.CategoryHideOwned[category.CategoryId] = hide; save(); }
                }
                ImGui.EndTable();
            }
        }
        if (!view.Inventory.OwnershipComplete)
            ImGui.TextColored(new Vector4(1, 0.78f, 0.35f, 1), "보유 정보를 전부 읽지 못했습니다. 확인하지 못한 품목은 제외하지 않습니다.");
        ImGui.TextDisabled("집사/초코보 가방/보관함 미포함 · 판매 조건과 할인은 실제 상점에서 확인");
        var filtered = view.Offers.Where(o => filter.Matches(o, id => view.Ownership.GetValueOrDefault(id))).ToArray();
        var defaults = PurchasePlanner.SelectDefaults(filtered.Where(o => o.ConditionsKnown));
        bool Selected(Offer offer) => choices.GetValueOrDefault(offer.Id, defaults.Contains(offer.Id));
        ImGui.TextUnformatted($"표시 {filtered.Length} / {view.Offers.Count} 교환식 · 계산 대상은 한 번씩 구매");
        if (ImGui.Button("계산 선택 초기화###ResetPlan")) choices.Clear();
        var tableHeight = Math.Max(140, ImGui.GetContentRegionAvail().Y - 155);
        if (ImGui.BeginTable("###Offers", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, tableHeight)))
        {
            ImGui.TableSetupColumn("계산", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("품목"); ImGui.TableSetupColumn("종류");
            ImGui.TableSetupColumn("보유"); ImGui.TableSetupColumn("1회 필요 재화"); ImGui.TableSetupColumn("상점/조건");
            ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
            foreach (var offer in filtered)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                var selected = Selected(offer);
                if (ImGui.Checkbox($"###Plan:{offer.Id}", ref selected))
                {
                    if (selected)
                    {
                        var rewardIds = offer.Rewards.Select(r => r.Item.Id).ToHashSet();
                        foreach (var other in view.Offers.Where(o => o.Rewards.Any(r => rewardIds.Contains(r.Item.Id)))) choices[other.Id] = false;
                    }
                    choices[offer.Id] = selected;
                }
                ImGui.TableNextColumn();
                foreach (var reward in offer.Rewards) ImGui.TextWrapped($"{reward.Item.Name}{(reward.Quality == ItemQuality.High ? " HQ" : "")} ×{reward.Quantity}");
                ImGui.TableNextColumn();
                foreach (var reward in offer.Rewards) ImGui.TextUnformatted(reward.Item.Category);
                ImGui.TableNextColumn();
                foreach (var reward in offer.Rewards) ImGui.TextUnformatted(view.Ownership.GetValueOrDefault(reward.Item.Id) switch
                { Ownership.Owned => "보유/등록", Ownership.Missing => "미보유", _ => "미확인" });
                ImGui.TableNextColumn();
                foreach (var price in offer.Prices) ImGui.TextWrapped($"{price.Name}{Quality(price.Key)} ×{price.Quantity:N0}");
                if (offer.Prices.Count == 0) ImGui.TextUnformatted("비용 없음/별도 조건");
                ImGui.TableNextColumn(); ImGui.TextWrapped(offer.Shop);
                if (offer.Limitation is { } limitation) ImGui.TextWrapped(limitation);
            }
            ImGui.EndTable();
        }
        var planned = filtered.Where(Selected).ToArray();
        var incomplete = planned.Count(o => o.Prices.Count == 0 || o.Prices.Any(p => !p.Resolved));
        ImGui.TextUnformatted($"필요 재화 · 선택 {planned.Length}개 교환식");
        if (incomplete > 0) ImGui.TextColored(new Vector4(1, 0.7f, 0.3f, 1), $"미확인 비용 {incomplete}건이 있어 아래 합계는 부분 합계입니다.");
        if (ImGui.BeginChild("###Totals", new Vector2(0, 0)))
        {
            foreach (var total in PurchasePlanner.Calculate(planned, view.Inventory.Quantity))
                ImGui.TextWrapped($"{total.Name}{Quality(total.Key)}: 필요 {total.Required:N0} · 보유 {Number(total.Owned)} · 부족 {Number(total.Shortfall)}");
        }
        ImGui.EndChild();
    }

    private static string Number(long? count) => count?.ToString("N0") ?? "미확인";
    private static string Quality(CurrencyKey key) => (key.Quality == ItemQuality.High ? " HQ" : "") +
        (key.Collectability > 0 ? $" (소장가치 {key.Collectability} 이상)" : "");
}
