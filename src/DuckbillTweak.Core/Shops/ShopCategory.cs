namespace DuckbillTweak.Core.Shops;

public static class ShopCategory
{
    // Plugin filter key; never passed to the game's category or purchase callbacks.
    public const uint Equipment = uint.MaxValue;
    public static uint GroupId(uint nativeId, ShopItemPurpose purpose) => purpose == ShopItemPurpose.Equipment ? Equipment : nativeId;
    public static string GroupName(string nativeName, ShopItemPurpose purpose) => purpose == ShopItemPurpose.Equipment ? "장비" : nativeName;
}
