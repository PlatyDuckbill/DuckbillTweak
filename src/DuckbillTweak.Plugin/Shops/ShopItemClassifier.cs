using DuckbillTweak.Core.Shops;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Shops;

/// <summary>Sheet-only classification shared by native shop rows and the calculator.</summary>
internal sealed class ShopItemClassifier
{
    private readonly HashSet<uint> exchangeItems;
    private readonly HashSet<uint> craftingIngredients;
    private readonly HashSet<uint> racingSupplies;

    public ShopItemClassifier(ExcelSheet<SpecialShop> shops, ExcelSheet<Recipe> recipes, ExcelSheet<RacingChocoboItem> racingItems)
    {
        // Build once from actual exchange inputs, not Korean name suffixes or a coin whitelist.
        // Type 2/3 are references to currencies, not ordinary item IDs.
        exchangeItems = shops.SelectMany(shop => shop.Item).SelectMany(offer => offer.ItemCosts)
            .Where(cost => cost.ItemCost.RowId != 0 && cost.CostType is 0 or 1)
            .Select(cost => cost.ItemCost.RowId).ToHashSet();
        craftingIngredients = recipes.Where(recipe => recipe.ItemResult.RowId != 0 && recipe.ItemResult.IsValid && recipe.AmountResult > 0)
            .SelectMany(recipe => recipe.Ingredient.Where((item, index) => item.RowId != 0 && recipe.AmountIngredient[index] > 0))
            .Select(item => item.RowId).ToHashSet();
        racingSupplies = racingItems.Where(row => row.Item.RowId != 0 && row.Category is >= 1 and <= 7)
            .Select(row => row.Item.RowId).ToHashSet();
    }

    public ShopItemPurpose Classify(Item item)
    {
        // Contracts are Miscellany but point to actual furniture/yard objects.
        // Treasure rank 0 is a real map, so inspect the reference type rather than its numeric value.
        var linked = item.AdditionalData.Is<HousingFurniture>() || item.AdditionalData.Is<HousingYardObject>()
            ? ShopItemPurpose.Housing
            : item.AdditionalData.Is<TreasureHuntRank>() || racingSupplies.Contains(item.RowId)
                ? ShopItemPurpose.Consumable : ShopItemPurpose.Other;
        return ShopItemPolicy.Classify(item.ItemUICategory.RowId, item.EquipSlotCategory.RowId != 0,
            item.ItemAction.ValueNullable?.Action.RowId ?? 0, item.AdditionalData.RowId != 0, exchangeItems.Contains(item.RowId),
            linked, craftingIngredients.Contains(item.RowId));
    }
}
