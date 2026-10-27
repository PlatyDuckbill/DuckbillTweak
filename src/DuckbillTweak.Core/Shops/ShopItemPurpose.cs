namespace DuckbillTweak.Core.Shops;

public enum ShopItemPurpose { Other, Equipment, Collectible, Consumable, Material, Materia, Housing, ExchangeMaterial }

public static class ShopItemPolicy
{
    public const string FilterLabel = "재료 등 제외";
    public const string FilterHelp = "소모품·제작/강화/교환 재료·마테리아·하우징 용품을 제외합니다.\n" +
        "장비·등록 수집품·악보는 유지합니다. 기타/잡화 전체를 제외하지 않습니다.\n" +
        "제외한 아이템도 지불 재화로 쓰이면 필요량에 포함합니다. 체크를 해제하면 다시 표시합니다.";

    public static ShopItemPurpose Classify(uint category, bool equipment, uint action, bool additionalData, bool exchangeCost)
    {
        if (equipment) return ShopItemPurpose.Equipment;
        if (category is 57 or >= 64 and <= 80 or 82 or >= 90 and <= 93 or 95 or >= 101 and <= 104)
            return ShopItemPurpose.Housing;
        if (category is 58 or 83) return ShopItemPurpose.Materia;
        if (category is 33 or 44 or 46 or 55 or 60 || action is 816 or 944) return ShopItemPurpose.Consumable;
        if (category is 45 or 47 or >= 48 and <= 54 or 56 or 59) return ShopItemPurpose.Material;
        // Permanent unlock items can also appear as exchange costs: keep them.
        if (action != 0 || additionalData || category is 81 or 86 or 94) return ShopItemPurpose.Collectible;
        if (category == 100 || exchangeCost && category is 61 or 63) return ShopItemPurpose.ExchangeMaterial;
        return ShopItemPurpose.Other;
    }

    public static bool Excluded(ShopItemPurpose purpose) => purpose is ShopItemPurpose.Consumable or
        ShopItemPurpose.Material or ShopItemPurpose.Materia or ShopItemPurpose.Housing or ShopItemPurpose.ExchangeMaterial;
}
