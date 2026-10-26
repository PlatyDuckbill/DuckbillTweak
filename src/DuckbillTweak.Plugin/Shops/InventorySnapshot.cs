using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Shops;

namespace DuckbillTweak.Plugin.Shops;

internal sealed record InventorySnapshot(bool OwnershipComplete, bool BagsComplete, bool CurrencyComplete,
    IReadOnlyDictionary<uint, long> OwnedItems, IReadOnlyDictionary<CurrencyKey, long> Spendable)
{
    public static InventorySnapshot Unknown { get; } = new(false, false, false,
        new Dictionary<uint, long>(), new Dictionary<CurrencyKey, long>());

    public long? Quantity(CurrencyKey key)
    {
        if (key.Collectability != 0 || !BagsComplete || !CurrencyComplete) return null;
        return Spendable.GetValueOrDefault(key);
    }

    public static InventorySnapshot Read(IGameInventory inventory)
    {
        GameInventoryType[] bags = [GameInventoryType.Inventory1, GameInventoryType.Inventory2,
            GameInventoryType.Inventory3, GameInventoryType.Inventory4];
        GameInventoryType[] equipment = [GameInventoryType.EquippedItems, GameInventoryType.ArmoryMainHand,
            GameInventoryType.ArmoryOffHand, GameInventoryType.ArmoryHead, GameInventoryType.ArmoryBody,
            GameInventoryType.ArmoryHands, GameInventoryType.ArmoryLegs, GameInventoryType.ArmoryFeets,
            GameInventoryType.ArmoryEar, GameInventoryType.ArmoryNeck, GameInventoryType.ArmoryWrist,
            GameInventoryType.ArmoryRings, GameInventoryType.ArmorySoulCrystal];
        var owned = new Dictionary<uint, long>();
        var spendable = new Dictionary<CurrencyKey, long>();
        var bagsComplete = true;
        var ownershipComplete = true;
        var currencyComplete = true;

        foreach (var container in bags.Concat(equipment).Concat([GameInventoryType.Currency, GameInventoryType.Crystals]))
        {
            var slots = inventory.GetInventoryItems(container);
            var valid = !slots.IsEmpty && slots.Length <= 1000;
            var containerOwned = new Dictionary<uint, long>();
            var containerSpendable = new Dictionary<CurrencyKey, long>();
            if (valid)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    ref readonly var slot = ref slots[i];
                    if (slot.IsEmpty) continue;
                    if (slot.ContainerType != container || slot.InventorySlot != i) { valid = false; break; }
                    var id = slot.BaseItemId;
                    if (id == 0 || slot.Quantity <= 0) { valid = false; break; }
                    containerOwned[id] = containerOwned.GetValueOrDefault(id) + slot.Quantity;
                    if (bags.Contains(container) || container is GameInventoryType.Currency or GameInventoryType.Crystals)
                    {
                        var key = new CurrencyKey(id, slot.IsHq ? ItemQuality.High : ItemQuality.Normal, 0);
                        containerSpendable[key] = containerSpendable.GetValueOrDefault(key) + slot.Quantity;
                    }
                }
            }
            if (!valid)
            {
                if (bags.Contains(container)) bagsComplete = false;
                if (container is GameInventoryType.Currency or GameInventoryType.Crystals) currencyComplete = false;
                else ownershipComplete = false;
            }
            else
            {
                foreach (var (id, count) in containerOwned) owned[id] = owned.GetValueOrDefault(id) + count;
                foreach (var (key, count) in containerSpendable) spendable[key] = spendable.GetValueOrDefault(key) + count;
            }
        }
        // Partial containers are not used as proof of absence or as spendable balances.
        if (!bagsComplete) spendable.Clear();
        return new(ownershipComplete && bagsComplete, bagsComplete, currencyComplete, owned, spendable);
    }
}
