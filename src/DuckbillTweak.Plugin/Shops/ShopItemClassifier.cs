using DuckbillTweak.Core.Shops;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Shops;

/// <summary>Sheet-only classification shared by native shop rows and the calculator.</summary>
internal sealed class ShopItemClassifier
{
    private readonly HashSet<uint> exchangeItems;

    public ShopItemClassifier(ExcelSheet<SpecialShop> shops)
    {
        // Build once from actual exchange inputs, not Korean name suffixes or a coin whitelist.
        // Type 2/3 are references to currencies, not ordinary item IDs.
        exchangeItems = shops.SelectMany(shop => shop.Item).SelectMany(offer => offer.ItemCosts)
            .Where(cost => cost.ItemCost.RowId != 0 && cost.CostType is 0 or 1)
            .Select(cost => cost.ItemCost.RowId).ToHashSet();
    }

    public ShopItemPurpose Classify(Item item) => ShopItemPolicy.Classify(item.ItemUICategory.RowId, item.EquipSlotCategory.RowId != 0,
            item.ItemAction.ValueNullable?.Action.RowId ?? 0, item.AdditionalData.RowId != 0, exchangeItems.Contains(item.RowId));
}
