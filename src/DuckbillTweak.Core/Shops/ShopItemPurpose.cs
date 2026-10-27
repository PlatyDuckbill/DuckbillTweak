namespace DuckbillTweak.Core.Shops;

public enum ShopItemPurpose { Other, Equipment, Collectible, Consumable, Material, Materia, Housing, ExchangeMaterial }

public static class ShopItemPolicy
{
    public const string OwnedFilterLabel = "보유 수집품 제외";
    public const string FilterLabel = "수집 위주 보기";
    public const string OwnedFilterHelp = "보유·등록한 장비와 수집품을 숨깁니다.\n소모품·재료·하우징은 그대로 표시합니다.";
    public const string FilterHelp = "장비와 등록 수집품 위주로 표시합니다.\n소모품·재료·마테리아·하우징 등을 숨깁니다.";

    public static bool CanHideOwned(ShopItemPurpose purpose) => purpose is ShopItemPurpose.Equipment or ShopItemPurpose.Collectible;

    public static ShopItemPurpose Classify(uint category, bool equipment, uint action, bool additionalData, bool exchangeCost,
        ShopItemPurpose linkedPurpose = ShopItemPurpose.Other, bool craftingIngredient = false)
    {
        if (equipment) return ShopItemPurpose.Equipment;
        if (linkedPurpose != ShopItemPurpose.Other) return linkedPurpose;
        if (category is 57 or >= 64 and <= 80 or 82 or >= 90 and <= 93 or 95 or >= 101 and <= 104)
            return ShopItemPurpose.Housing;
        if (category is 58 or 83) return ShopItemPurpose.Materia;
        if (category is 33 or 44 or 46 or 55 or 60 || IsSupplyAction(action)) return ShopItemPurpose.Consumable;
        if (category is 45 or 47 or >= 48 and <= 54 or 56 or 59) return ShopItemPurpose.Material;
        // Permanent unlock items can also appear as exchange costs: keep them.
        if (category is 81 or 86 or 94 || IsCollectionAction(action)) return ShopItemPurpose.Collectible;
        // An action/additional-data link alone is not proof of a permanent unlock.
        // Keep unfamiliar usable items and reward containers rather than hide them.
        if (action != 0 || additionalData) return ShopItemPurpose.Other;
        if (craftingIngredient && category is 61 or 63 or 85) return ShopItemPurpose.Material;
        if (category == 100 || exchangeCost && category is 61 or 63) return ShopItemPurpose.ExchangeMaterial;
        return ShopItemPurpose.Other;
    }

    public static bool IsSupplyAction(uint action) => action is 816 or 851 or 852 or 944 or 2483 or 2634 or 2645 or
        3800 or 5136 or 9994 or 17290 or 31315 or 32865 or 34493 or 36120 or 47023;

    public static bool IsCollectionAction(uint action) => action is 853 or 1013 or 1322 or 2136 or 2633 or 3357 or 4107 or
        20086 or 25183 or 29459 or 37312 or 43141 or 43142 or 50454;

    public static bool Excluded(ShopItemPurpose purpose) => purpose is ShopItemPurpose.Consumable or
        ShopItemPurpose.Material or ShopItemPurpose.Materia or ShopItemPurpose.Housing or ShopItemPurpose.ExchangeMaterial;
}
