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
    private long lastRead;
    private ulong character;
    private uint territory;
    private uint lastNpc;
    private bool manual;
    private bool refreshRequested;
    private (uint Id, bool Special)? manualRequest;
    public MerchantView View { get; private set; } = MerchantView.Empty;
    public bool Visible { get; set; }

    public MerchantService(IDataManager data, IUnlockState unlocks, IGameInventory inventory, IClientState client,
        IPlayerState player, ICondition conditions, ITargetManager target, IGameGui gameGui, IFramework framework,
        IPluginLog log, Configuration config)
    {
        this.data = data; this.unlocks = unlocks; this.inventory = inventory; this.client = client;
        this.player = player; this.conditions = conditions; this.target = target; this.gameGui = gameGui;
        this.framework = framework; this.log = log; this.config = config;
        currencyResolver = CurrencyData.Load(data);
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
        if (View.Ownership.TryGetValue(id, out var known)) return known;
        if (data.GetExcelSheet<Item>().TryGetRow(id, out var item) && unlocks.IsItemUnlockable(item) && unlocks.IsItemUnlocked(item))
            return Ownership.Owned;
        return View.Inventory.OwnershipComplete ? Ownership.Missing : Ownership.Unknown;
    }
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
                var registered = data.GetExcelSheet<Item>().TryGetRow(id, out var item) &&
                    unlocks.IsItemUnlockable(item) && unlocks.IsItemUnlocked(item);
                owned[id] = registered || stock.OwnedItems.GetValueOrDefault(id) > 0 ? Ownership.Owned :
                    stock.OwnershipComplete ? Ownership.Missing : Ownership.Unknown;
            }
            if (!client.IsLoggedIn || player.ContentId != character) { Reset(); return; }
            View = View with { Ownership = owned, Inventory = stock };
        }
        catch (Exception ex)
        {
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
        item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? "기타", item.IsUntradable);

    private void AddGil(uint id, List<Offer> offers, HashSet<string> visited)
    {
        if (!visited.Add($"gil:{id}")) return;
        if (!data.GetExcelSheet<GilShop>().TryGetRow(id, out var shop)) return;
        var items = data.GetSubrowExcelSheet<GilShopItem>();
        if (!items.TryGetRow(id, out var rows)) return;
        foreach (var row in rows)
        {
            if (!row.Item.IsValid || row.Item.RowId == 0) continue;
            var conditional = shop.Quest.RowId != 0 || row.QuestRequired.Any(q => q.RowId != 0) ||
                row.AchievementRequired.RowId != 0 || row.StateRequired != 0 || shop.FestivalId != 0;
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
            var conditional = shop.Quest.RowId != 0 || shop.RequiredFestival.RowId != 0 ||
                shop.RequiredContentFinderCondition.RowId != 0 || entry.Quest.RowId != 0 || entry.AchievementUnlock.RowId != 0;
            offers.Add(new($"special:{id}:{entryIndex}", shop.Name.ExtractText(), rewards, prices, !conditional,
                conditional ? "판매 조건 확인 필요" : null));
        }
    }

    private void Reset()
    {
        View = MerchantView.Empty; character = 0; territory = 0; lastNpc = 0; manual = false; manualRequest = null;
        lastRead = 0;
    }

    public void Dispose() => framework.Update -= Update;
}
