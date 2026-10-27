using DuckbillTweak.Core.Shops;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Shops;

internal static class SheetExchangeCatalog
{
    public static ExchangeCatalog Load(ExcelSheet<SpecialShop> shops) => new(shops.SelectMany(shop =>
        shop.Item.Where(row => row.ReceiveItems.Any(reward => reward.Item.RowId != 0)).Select(row =>
            new ExchangeTemplate(shop.RowId, shop.Name.ExtractText(),
                row.ReceiveItems.Where(reward => reward.Item.RowId != 0).Select(reward =>
                    new ExchangeReward(reward.Item.RowId + (reward.ReceiveHq ? 1_000_000u : 0), reward.ReceiveCount)).ToArray(),
                row.ItemCosts.Where(cost => cost.ItemCost.RowId != 0).Select(cost =>
                    new ExchangeCost(cost.CostType, cost.ItemCost.RowId, cost.CurrencyCost, cost.CollectabilityCost)).ToArray()))));
}
