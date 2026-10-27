using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
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
    private readonly ExchangeCatalog exchangeCatalog;
    private readonly NativeCurrencyReader wallet = new();
    private string? lastNativePage;
    private readonly ShopItemClassifier itemClassifier;
    private readonly NativeOwnership nativeOwnership;
    private readonly StoredInventoryBridge storedInventory;
    private ushort displayedAddon;
    private string displayedContext = "";
    private IReadOnlyList<Offer> displayedOffers = [];
    private IReadOnlyDictionary<CurrencyKey, long> displayedBalances = new Dictionary<CurrencyKey, long>();
    private IReadOnlySet<uint> displayedOwned = new HashSet<uint>();
    private (ushort Addon, int Offers, int Skipped, int Verified, bool Dresser, bool Cabinet, int MissingCosts, int EmptyCosts)? lastDisplayed;
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
    public long SessionGeneration { get; private set; }
    public bool SessionReady => config.MerchantTools && client.IsLoggedIn && player.IsLoaded &&
        character != 0 && character == player.ContentId && territory == client.TerritoryType &&
        !conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51];
    public bool NativeOwnershipEnabled { set { nativeOwnership.Enabled = value; wallet.Enabled = value; } }
    public bool DresserLoaded => nativeOwnership.DresserLoaded;
    public bool CabinetLoaded => nativeOwnership.CabinetLoaded;
    public string OwnershipHelp => ShopItemPolicy.OwnedFilterHelp;
    public bool IsShopOpen(ushort id) => id != 0 && SessionReady && new[] { "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop" }.Any(name =>
    {
        var addon = gameGui.GetAddonByName(name);
        return !addon.IsNull && addon.Id == id && addon.IsReady && addon.IsVisible;
    });
    public bool TryGetCalculation(ushort id, out MerchantView view, out string context)
    {
        view = MerchantView.Empty; context = "";
        if (!IsShopOpen(id) || displayedAddon != id) return false;
        context = displayedContext;
        view = View with { Name = "현재 상점 목록", Offers = displayedOffers, Message = null };
        return true;
    }
    public long? CalculationQuantity(CurrencyKey key)
    {
        if (wallet.Quantity(key) is { } walletCount) return walletCount;
        if (displayedAddon != 0 && displayedBalances.TryGetValue(key, out var count)) return count;
        // A wallet currency need not exist in ordinary inventory containers.
        if (key.ItemId != 1 && data.GetExcelSheet<Item>().TryGetRow(key.ItemId, out var item) && item.ItemUICategory.RowId == 100) return null;
        return View.Inventory.Quantity(key);
    }
    public StoredCurrency CalculationStorage(CurrencyKey key) => storedInventory.Currency(key);
    public string StorageHelp => storedInventory.Help;
    public Ownership CalculationOwnership(uint id) => displayedAddon != 0 && displayedOwned.Contains(id) ? Ownership.Owned : ItemOwnership(id);

    public MerchantService(IDataManager data, IUnlockState unlocks, IGameInventory inventory, IClientState client,
        IPlayerState player, ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, Configuration config, IDalamudPluginInterface pluginInterface)
    {
        this.data = data; this.unlocks = unlocks; this.inventory = inventory; this.client = client;
        this.player = player; this.conditions = conditions; this.target = target; this.gameGui = gameGui;
        this.framework = framework; this.log = log; this.config = config;
        currencyResolver = CurrencyData.Load(data);
        exchangeCatalog = SheetExchangeCatalog.Load(data.GetExcelSheet<SpecialShop>());
        itemClassifier = new(data.GetExcelSheet<SpecialShop>(), data.GetExcelSheet<Recipe>(), data.GetExcelSheet<RacingChocoboItem>());
        nativeOwnership = new(data, unlocks);
        storedInventory = new(pluginInterface, log);
        framework.Update += Update;
    }

    public void RefreshTarget() { manual = false; refreshRequested = true; }
    public void RequestShop(uint id, bool special) => manualRequest = (id, special);

    public string ItemName(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item)
        ? item.Name.ExtractText() : $"아이템 #{rawId}";
    public string ItemDisplayName(uint rawId) => ItemName(rawId) + (rawId >= 1_000_000 ? " HQ" : "");
    public ShopItem? ItemDetails(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item) ? Describe(item) : null;
    public (int Major, int Minor, uint Id) CategoryOrder(uint id) => id == ShopCategory.Equipment ? (0, 0, id) :
        data.GetExcelSheet<ItemUICategory>().TryGetRow(id, out var category)
            ? (category.OrderMajor, category.OrderMinor, id) : (int.MaxValue, int.MaxValue, id);
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
        if (storedInventory.Contains(id, !nativeOwnership.StorageKnown(item))) return Ownership.Owned;
        var registration = nativeOwnership.Registration(item);
        if (registration == Ownership.Owned) return Ownership.Owned;
        // The storage IPC cannot certify that all retainers/containers were scanned.
        // An absent record therefore does not establish absence across the requested scope.
        return Ownership.Unknown;
    }
    public bool IsExcludedSupply(uint rawId) => ItemDetails(rawId)?.ExcludedSupply == true;
    public bool CanHideOwned(uint rawId) => ItemDetails(rawId)?.CanHideOwned == true;
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
        var resolved = currencyResolver.Resolve(type, id, wallet.SpecialItem);
        var baseId = resolved ?? id;
        var valid = amount > 0 && resolved.HasValue && data.GetExcelSheet<Item>().TryGetRow(baseId, out var item) && !item.Name.IsEmpty;
        return new(new(baseId, type == 1 ? ItemQuality.High : ItemQuality.Normal, 0),
            valid ? ItemName(baseId) : $"미확인 재화 #{id}", amount, valid);
    }
    public IReadOnlyList<ResolvedExchange?> ResolveNativePage(string shopName, IReadOnlyList<ExchangeObservation> page)
    {
        var signature = shopName + ":" + string.Join(";", page.Select(row => $"{row.RawItemId}x{row.Quantity}[{string.Join(',', row.Prices.Select(price => $"{price.Key.ItemId}x{price.Quantity}/{price.Resolved}"))}]"));
        var matched = exchangeCatalog.Match(shopName, page, cost =>
        {
            var price = NativePrice(cost.Type, cost.Id, cost.Quantity);
            return price with { Key = price.Key with { Collectability = cost.Collectability } };
        });
        if (lastNativePage != signature)
        {
            lastNativePage = signature;
            log.Information("Exchange page trace: Shop={Shop}; Rows={Rows}; CatalogMatched={Matched}", shopName, page.Count, matched is not null);
            foreach (var row in matched is null ? page.Take(3) : [])
                log.Information("Exchange row trace: Reward={Item}; Quantity={Quantity}; Prices={Prices}", row.RawItemId, row.Quantity,
                    string.Join(',', row.Prices.Select(price => $"{price.Key.ItemId}x{price.Quantity}/{price.Resolved}")));
        }
        return page.Select((row, index) => matched?[index] ??
            (row.Quantity > 0 && row.Prices.Count > 0 ? new ResolvedExchange(row.Quantity, row.Prices) : null)).ToArray();
    }
    public void PublishDisplayedShop(ushort addon, string context, IReadOnlyList<Offer> offers, IReadOnlyDictionary<CurrencyKey, long> balances, IReadOnlySet<uint> owned, int total)
    {
        if (!SessionReady) return;
        var same = displayedAddon == addon && displayedContext == context && displayedOffers.Count == offers.Count && displayedOffers.Zip(offers).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.ConditionsKnown == pair.Second.ConditionsKnown &&
            pair.First.Rewards.Select(r => (r.Item, r.Quantity, r.Quality)).SequenceEqual(pair.Second.Rewards.Select(r => (r.Item, r.Quantity, r.Quality))) &&
            pair.First.Prices.SequenceEqual(pair.Second.Prices));
        displayedAddon = addon; displayedContext = context;
        if (!same) displayedOffers = offers;
        displayedBalances = balances; displayedOwned = owned; UnreadableDisplayedOffers = total - offers.Count;
        var state = (addon, offers.Count, UnreadableDisplayedOffers, offers.Count(o => o.ConditionsKnown), DresserLoaded, CabinetLoaded,
            offers.Count(o => o.Prices.Any(p => !p.Resolved)), offers.Count(o => o.Prices.Count == 0));
        if (lastDisplayed != state)
        {
            log.Information("Shop calculation snapshot: Addon={Addon}; Offers={Offers}; Unreadable={Unreadable}; ConditionsVerified={Verified}; DresserLoaded={Dresser}; CabinetLoaded={Cabinet}; UnknownCosts={Unknown}; EmptyCosts={Empty}",
                addon, offers.Count, UnreadableDisplayedOffers, state.Item4, DresserLoaded, CabinetLoaded, state.Item7, state.Item8);
            lastDisplayed = state;
        }
    }
    public void ClearDisplayedShop(ushort addon)
    { if (displayedAddon == addon) { displayedAddon = 0; displayedContext = ""; displayedOffers = []; displayedBalances = new Dictionary<CurrencyKey, long>(); displayedOwned = new HashSet<uint>(); UnreadableDisplayedOffers = 0; lastDisplayed = null; } }
    public uint Icon(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId >= 1_000_000 ? rawId - 1_000_000 : rawId, out var item) ? item.Icon : 0u;
    public string NativeCost(byte type, uint id, uint amount)
    {
        var item = currencyResolver.Resolve(type, id, wallet.SpecialItem);
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
            var shopOpen = new[] { "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop" }.Any(name =>
            {
                var addon = gameGui.GetAddonByName(name);
                return !addon.IsNull && addon.IsReady && addon.IsVisible;
            });
            if (!Visible && !shopOpen && !refreshRequested && manualRequest is null) return;
            nativeOwnership.Refresh(player.ContentId);
            storedInventory.Refresh(player.ContentId);

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
            nativeOwnership.Clear(); storedInventory.Clear(); ClearDisplayedShop(displayedAddon);
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

    private ShopItem Describe(Item item)
    {
        var purpose = itemClassifier.Classify(item);
        return new(item.RowId, item.Name.ExtractText(), ShopCategory.GroupId(item.ItemUICategory.RowId, purpose),
            ShopCategory.GroupName(item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? "기타", purpose), item.IsUntradable, purpose);
    }

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
                var resolvedId = currencyResolver.Resolve(cost.CostType, rawId, wallet.SpecialItem);
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
        lastNativePage = null;
        SessionGeneration++;
        View = MerchantView.Empty; character = 0; territory = 0; lastNpc = 0; manual = false; manualRequest = null;
        nativeOwnership.Clear(); storedInventory.Clear(); displayedAddon = 0; displayedContext = ""; displayedOffers = []; displayedBalances = new Dictionary<CurrencyKey, long>(); displayedOwned = new HashSet<uint>(); UnreadableDisplayedOffers = 0; lastDisplayed = null;
        lastRead = 0;
    }

    private bool QuestSatisfied(RowRef<Quest> quest) => quest.RowId == 0 || quest.IsValid && unlocks.IsQuestCompleted(quest.Value);
    private bool AchievementSatisfied(RowRef<Achievement> achievement) => achievement.RowId == 0 ||
        unlocks.IsAchievementListLoaded && achievement.IsValid && unlocks.IsAchievementComplete(achievement.Value);

    public void Dispose() => framework.Update -= Update;
}
