using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Shops;

internal sealed record MerchantView(string Name, uint NpcId, IReadOnlyList<Offer> Offers,
    IReadOnlyDictionary<uint, Ownership> Ownership, InventorySnapshot Inventory, string? Message)
{
    public static MerchantView Empty { get; } = new("", 0, [], new Dictionary<uint, Ownership>(), InventorySnapshot.Unknown, null);
}

internal sealed class MerchantService : IDisposable
{
    private readonly IDataManager data;
    private readonly IUnlockState unlocks;
    private readonly IGameInventory inventory;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly ITargetManager target;
    private readonly IGameGui gameGui;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly SpecialCurrencyResolver currencyResolver;
    private readonly NativeOwnership nativeOwnership;
    private ushort displayedAddon;
    private IReadOnlyList<Offer> displayedOffers = [];
    private IReadOnlyDictionary<CurrencyKey, long> displayedBalances = new Dictionary<CurrencyKey, long>();
    private IReadOnlySet<uint> displayedOwned = new HashSet<uint>();
    private (ushort Addon, int Offers, int Skipped, int Verified, bool Dresser, bool Cabinet)? lastDisplayed;
    public int UnreadableDisplayedOffers { get; private set; }
    private long lastRead;
    private ulong character;
    private uint territory;
    private uint lastNpc;
    private bool manual;
    private bool refreshRequested;
    private (uint Id, bool Special)? manualRequest;
    public MerchantView View { get; private set; } = MerchantView.Empty;
    public bool Visible { get; set; }
    public bool NativeOwnershipEnabled { set => nativeOwnership.Enabled = value; }
    public bool DresserLoaded => nativeOwnership.DresserLoaded;
    public bool CabinetLoaded => nativeOwnership.CabinetLoaded;
    public string OwnershipHelp => "등록한 수집품·소지품·장비함·착용 장비·환상투영함·추억의 보관함을 확인합니다.\n" +
        $"환상투영함: {nativeOwnership.DresserSource} / 추억의 보관함: {nativeOwnership.CabinetSource}\n" +
        "현재 캐릭터의 게임 저장 정보도 사용합니다. 미확인이면 보관함을 한 번 열어 주세요.\n낡은 악보는 제작 결과 악보가 모두 등록됐으면 제외합니다.";
    public MerchantView CalculationView => displayedAddon == 0 ? View : View with { Offers = displayedOffers, Message = "현재 상점 목록 기준" };
    public string CalculationScope => displayedAddon == 0 ? "NPC 시트 목록" : "현재 상점 목록";
    public long? CalculationQuantity(CurrencyKey key) => displayedAddon != 0 && displayedBalances.TryGetValue(key, out var count) ? count : View.Inventory.Quantity(key);
    public Ownership CalculationOwnership(uint id) => displayedAddon != 0 && displayedOwned.Contains(id) ? Ownership.Owned : ItemOwnership(id);

    public MerchantService(IDataManager data, IUnlockState unlocks, IGameInventory inventory, IClientState client,
        IPlayerState player, ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, Configuration config)
    {
        this.data = data; this.unlocks = unlocks; this.inventory = inventory; this.client = client;
        this.player = player; this.conditions = conditions; this.target = target; this.gameGui = gameGui;
        this.framework = framework; this.log = log; this.config = config;
        currencyResolver = CurrencyData.Load(data);
        nativeOwnership = new(data, unlocks);
        framework.Update += Update;
    }

    public void RefreshTarget() { manual = false; refreshRequested = true; }
    public void RequestShop(uint id, bool special) => manualRequest = (id, special);

    public string ItemName(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item)
        ? item.Name.ExtractText() : $"아이템 #{rawId}";
    public string ItemDisplayName(uint rawId) => ItemName(rawId) + (rawId >= 1_000_000 ? " HQ" : "");
    public ShopItem? ItemDetails(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item) ? Describe(item) : null;
    public Ownership ItemOwnership(uint rawId, uint nativeOwned = 0)
    {
        var id = rawId >= 1_000_000 ? rawId - 1_000_000 : rawId;
        if (nativeOwned > 0 || View.Inventory.OwnedItems.GetValueOrDefault(id) > 0) return Ownership.Owned;
        return ReadOwnership(id, View.Inventory);
    }
    private Ownership ReadOwnership(uint id, InventorySnapshot stock)
    {
        if (stock.OwnedItems.GetValueOrDefault(id) > 0 || nativeOwnership.Contains(id)) return Ownership.Owned;
        if (!data.GetExcelSheet<Item>().TryGetRow(id, out var item)) return Ownership.Unknown;
        var registration = nativeOwnership.Registration(item);
        if (registration == Ownership.Owned) return Ownership.Owned;
        if (registration == Ownership.Unknown || !nativeOwnership.StorageKnown(item)) return Ownership.Unknown;
        return stock.OwnershipComplete ? Ownership.Missing : Ownership.Unknown;
    }
    public bool IsConsumable(uint rawId) => ItemDetails(rawId)?.Consumable == true;
    public Reward? NativeReward(uint rawId, uint quantity, IReadOnlyList<Price>? prices = null)
    {
        var item = ItemDetails(rawId);
        if (item is null) return null;
        var quality = rawId >= 1_000_000 ? ItemQuality.High : ItemQuality.Normal;
        if (quantity > 0) return new(item, quantity, quality);
        if (prices is null || prices.Any(p => !p.Resolved)) return null;
        // A zero native quantity is unknown. Recover it only from a unique, exact sheet offer.
        var quantities = View.Offers.Where(o => o.Rewards.Count == 1 && o.Rewards[0].Item.Id == item.Id && o.Rewards[0].Quality == quality &&
            o.Prices.Select(p => (p.Key, p.Quantity, p.Resolved)).SequenceEqual(prices.Select(p => (p.Key, p.Quantity, p.Resolved))))
            .Select(o => o.Rewards[0].Quantity).Where(q => q > 0).Distinct().ToArray();
        return quantities is [var known] ? new(item, known, quality) : null;
    }
    public Offer DisplayedOffer(string key, IReadOnlyList<Reward> rewards, IReadOnlyList<Price> prices, bool nativeEnabled)
    {
        var known = nativeEnabled || View.Offers.Any(o => o.ConditionsKnown &&
            o.Rewards.Select(r => (r.Item.Id, r.Quantity, r.Quality)).SequenceEqual(rewards.Select(r => (r.Item.Id, r.Quantity, r.Quality))) &&
            o.Prices.Select(p => (p.Key, p.Quantity)).SequenceEqual(prices.Select(p => (p.Key, p.Quantity))));
        return new(key, "현재 상점 목록", rewards, prices, known, known ? null : "구매 조건 미확인");
    }
    public Price NativePrice(byte type, uint id, uint amount)
    {
        var resolved = currencyResolver.Resolve(type, id);
        var baseId = resolved ?? id;
        var valid = resolved.HasValue && data.GetExcelSheet<Item>().TryGetRow(baseId, out var item) && !item.Name.IsEmpty;
        return new(new(baseId, type == 1 ? ItemQuality.High : ItemQuality.Normal, 0),
            valid ? ItemName(baseId) : $"미확인 재화 #{id}", amount, valid);
    }
    public void PublishDisplayedShop(ushort addon, IReadOnlyList<Offer> offers, IReadOnlyDictionary<CurrencyKey, long> balances, IReadOnlySet<uint> owned, int total)
    {
        var same = displayedAddon == addon && displayedOffers.Count == offers.Count && displayedOffers.Zip(offers).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.ConditionsKnown == pair.Second.ConditionsKnown &&
            pair.First.Rewards.Select(r => (r.Item, r.Quantity, r.Quality)).SequenceEqual(pair.Second.Rewards.Select(r => (r.Item, r.Quantity, r.Quality))) &&
            pair.First.Prices.SequenceEqual(pair.Second.Prices));
        displayedAddon = addon;
        if (!same) displayedOffers = offers;
        displayedBalances = balances; displayedOwned = owned; UnreadableDisplayedOffers = total - offers.Count;
        var state = (addon, offers.Count, UnreadableDisplayedOffers, offers.Count(o => o.ConditionsKnown), DresserLoaded, CabinetLoaded);
        if (lastDisplayed != state)
        {
            log.Information("Shop calculation snapshot: Addon={Addon}; Offers={Offers}; Unreadable={Unreadable}; ConditionsVerified={Verified}; DresserLoaded={Dresser}; CabinetLoaded={Cabinet}",
                addon, offers.Count, UnreadableDisplayedOffers, state.Item4, DresserLoaded, CabinetLoaded);
            lastDisplayed = state;
        }
    }
    public void ClearDisplayedShop(ushort addon)
    { if (displayedAddon == addon) { displayedAddon = 0; displayedOffers = []; displayedBalances = new Dictionary<CurrencyKey, long>(); displayedOwned = new HashSet<uint>(); UnreadableDisplayedOffers = 0; lastDisplayed = null; } }
    public uint Icon(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item) ? item.Icon : 0u;
    public string NativeCost(byte type, uint id, uint amount)
    {
        var item = currencyResolver.Resolve(type, id);
        return item.HasValue ? $"{ItemName(item.Value)} ×{amount:N0}" : $"미확인 재화 ×{amount:N0}";
    }

    private void Update(IFramework _)
    {
        if (!config.MerchantTools || !client.IsLoggedIn || !player.IsLoaded ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
        {
            Reset(); return;
        }
        if (character != player.ContentId || territory != client.TerritoryType)
        { Reset(); character = player.ContentId; territory = client.TerritoryType; }
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 500) return;
        lastRead = Stopwatch.GetTimestamp();
        try
        {
            var shopOpen = new[] { "Shop", "ShopExchangeItem", "InclusionShop" }.Any(name =>
            {
                var addon = gameGui.GetAddonByName(name);
                return !addon.IsNull && addon.IsReady && addon.IsVisible;
            });
            if (!Visible && !shopOpen && !refreshRequested && manualRequest is null) return;
            nativeOwnership.Refresh(player.ContentId);

            var currentTarget = target.Target;
            var npc = currentTarget?.ObjectKind == ObjectKind.EventNpc ? currentTarget.BaseId : 0;
            if (manualRequest is { } request)
            {
                manualRequest = null; manual = true;
                var offers = new List<Offer>();
                var visited = new HashSet<string>();
                if (request.Special) AddSpecial(request.Id, offers, visited);
                else AddGil(request.Id, offers, visited);
                View = new($"{(request.Special ? "SpecialShop" : "GilShop")} #{request.Id}", 0, offers,
                    new Dictionary<uint, Ownership>(), InventorySnapshot.Unknown, "수동 상점: NPC의 실제 판매/해금 상태를 확인하십시오.");
            }
            else if (!manual && (refreshRequested || npc != lastNpc))
            {
                refreshRequested = false; lastNpc = npc;
                var offers = new List<Offer>();
                if (npc != 0 && data.GetExcelSheet<ENpcBase>().TryGetRow(npc, out var npcRow))
                {
                    var visited = new HashSet<string>();
                    foreach (var entry in npcRow.ENpcData) Resolve(entry, offers, visited, 0);
                }
                View = new(currentTarget?.Name.TextValue ?? "", npc, offers, new Dictionary<uint, Ownership>(),
                    InventorySnapshot.Unknown, npc == 0 ? "상인 NPC를 대상으로 선택한 뒤 새로고침하십시오." :
                    offers.Count == 0 ? "직접 연결된 상점이 없습니다. 특수 대화형 상점은 수동 상점 ID로 확인할 수 있습니다." : null);
            }
            var stock = InventorySnapshot.Read(inventory);
            var owned = new Dictionary<uint, Ownership>();
            foreach (var reward in View.Offers.SelectMany(o => o.Rewards).DistinctBy(x => x.Item.Id))
            {
                var id = reward.Item.Id;
                owned[id] = ReadOwnership(id, stock);
            }
            if (!client.IsLoggedIn || player.ContentId != character) { Reset(); return; }
            View = View with { Ownership = owned, Inventory = stock };
        }
        catch (Exception ex)
        {
            nativeOwnership.Clear(); ClearDisplayedShop(displayedAddon);
            View = MerchantView.Empty with { Message = "상점 데이터를 읽지 못했습니다. 로그와 참조 버전을 확인하십시오." };
            log.Error(ex, "Merchant snapshot failed");
            // Avoid retrying a failing sheet query every half second.
            lastRead = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 4;
        }
    }

    private void Resolve(RowRef reference, List<Offer> offers, HashSet<string> visited, int depth)
    {
        if (depth > 8 || reference.RowId == 0) return;
        if (reference.Is<GilShop>()) AddGil(reference.RowId, offers, visited);
        else if (reference.Is<SpecialShop>()) AddSpecial(reference.RowId, offers, visited);
        else if (!visited.Add($"{reference.RowType}:{reference.RowId}")) return;
        else if (reference.TryGetValue<TopicSelect>(out var topic))
            foreach (var shop in topic.Shop) Resolve(shop, offers, visited, depth + 1);
        else if (reference.TryGetValue<PreHandler>(out var pre)) Resolve(pre.Target, offers, visited, depth + 1);
        else if (reference.TryGetValue<InclusionShop>(out var inclusion))
            foreach (var category in inclusion.Category)
                if (category.IsValid && category.Value.InclusionShopSeries.IsValid)
                    foreach (var series in category.Value.InclusionShopSeries.Value)
                        AddSpecial(series.SpecialShop.RowId, offers, visited);
    }

    private ShopItem Describe(Item item) => new(item.RowId, item.Name.ExtractText(), item.ItemUICategory.RowId,
        item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? "기타", item.IsUntradable,
        item.ItemUICategory.RowId is 44 or 46 or 55 or 60 && item.EquipSlotCategory.RowId == 0);

    private void AddGil(uint id, List<Offer> offers, HashSet<string> visited)
    {
        if (!visited.Add($"gil:{id}")) return;
        if (!data.GetExcelSheet<GilShop>().TryGetRow(id, out var shop)) return;
        var items = data.GetSubrowExcelSheet<GilShopItem>();
        if (!items.TryGetRow(id, out var rows)) return;
        foreach (var row in rows)
        {
            if (!row.Item.IsValid || row.Item.RowId == 0) continue;
            var conditional = !QuestSatisfied(shop.Quest) || row.QuestRequired.Any(q => !QuestSatisfied(q)) ||
                !AchievementSatisfied(row.AchievementRequired) || row.StateRequired != 0 || shop.FestivalId != 0;
            var item = row.Item.Value;
            offers.Add(new($"gil:{id}:{row.SubrowId}", shop.Name.ExtractText(),
                [new(Describe(item), 1, row.IsHQ ? ItemQuality.High : ItemQuality.Normal)],
                [new(new(1, ItemQuality.Normal, 0), "길", item.PriceMid)], !conditional,
                conditional ? "판매 조건 확인 필요 · 기본 단가" : "기본 단가: 할인/세금 미반영"));
        }
    }

    private void AddSpecial(uint id, List<Offer> offers, HashSet<string> visited)
    {
        if (!visited.Add($"special:{id}") || !data.GetExcelSheet<SpecialShop>().TryGetRow(id, out var shop)) return;
        var index = 0;
        foreach (var entry in shop.Item)
        {
            var entryIndex = index++;
            var rewards = entry.ReceiveItems.Where(x => x.Item.RowId != 0 && x.Item.IsValid)
                .Select(x => new Reward(Describe(x.Item.Value), x.ReceiveCount, x.ReceiveHq ? ItemQuality.High : ItemQuality.Normal)).ToArray();
            if (rewards.Length == 0) continue;
            var prices = new List<Price>();
            foreach (var cost in entry.ItemCosts.Where(x => x.ItemCost.RowId != 0))
            {
                var rawId = cost.ItemCost.RowId;
                var resolvedId = currencyResolver.Resolve(cost.CostType, rawId);
                var itemId = resolvedId ?? rawId;
                var resolved = resolvedId.HasValue &&
                    data.GetExcelSheet<Item>().TryGetRow(itemId, out var currency) && !currency.Name.IsEmpty;
                var priceName = resolved ? data.GetExcelSheet<Item>().GetRow(itemId).Name.ExtractText() :
                    $"미확인 재화 #{rawId} (방식 {shop.UseCurrencyType}/{cost.CostType})";
                prices.Add(new(new(itemId, cost.CostType == 1 ? ItemQuality.High : ItemQuality.Normal, cost.CollectabilityCost),
                    priceName, cost.CurrencyCost, resolved));
            }
            var conditional = !QuestSatisfied(shop.Quest) || shop.RequiredFestival.RowId != 0 ||
                shop.RequiredContentFinderCondition.RowId != 0 || !QuestSatisfied(entry.Quest) || !AchievementSatisfied(entry.AchievementUnlock);
            offers.Add(new($"special:{id}:{entryIndex}", shop.Name.ExtractText(), rewards, prices, !conditional,
                conditional ? "판매 조건 확인 필요" : null));
        }
    }

    private void Reset()
    {
        View = MerchantView.Empty; character = 0; territory = 0; lastNpc = 0; manual = false; manualRequest = null;
        nativeOwnership.Clear(); displayedAddon = 0; displayedOffers = []; displayedBalances = new Dictionary<CurrencyKey, long>(); displayedOwned = new HashSet<uint>(); UnreadableDisplayedOffers = 0; lastDisplayed = null;
        lastRead = 0;
    }

    private bool QuestSatisfied(RowRef<Quest> quest) => quest.RowId == 0 || quest.IsValid && unlocks.IsQuestCompleted(quest.Value);
    private bool AchievementSatisfied(RowRef<Achievement> achievement) => achievement.RowId == 0 ||
        unlocks.IsAchievementListLoaded && achievement.IsValid && unlocks.IsAchievementComplete(achievement.Value);

    public void Dispose() => framework.Update -= Update;
}
