using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class OfficerItemRules
{
    internal static IEnumerable<ItemData> GetEquippedItems(WorldState world, int officerId)
    {
        return world.Items.Where(item => item.EquippedOfficerId == officerId);
    }

    private static ItemData? GetEquippedItemInSlot(WorldState world, int officerId, ItemType itemType)
    {
        return world.Items.FirstOrDefault(item =>
            item.EquippedOfficerId == officerId &&
            AreItemsInSameSlot(item.ItemType, itemType));
    }

    private static bool AreItemsInSameSlot(ItemType a, ItemType b)
    {
        return GetItemSlotKey(a) == GetItemSlotKey(b);
    }

    private static string GetItemSlotKey(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "weapon",
            ItemType.Horse => "horse",
            ItemType.Book => "special",
            ItemType.Treasure => "special",
            _ => "special"
        };
    }

    internal static bool IsItemOwnedByFactionInventory(ItemData item, int factionId)
    {
        return item.OwnerFactionId == factionId &&
               item.EquippedOfficerId <= 0;
    }

    internal static void MoveItemToFactionInventory(ItemData item, int factionId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = 0;
    }

    private static void EquipItemToOfficer(ItemData item, int factionId, int officerId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = officerId;
    }

    internal static void AssignItemToOfficer(WorldState world, ItemData item, int factionId, int officerId)
    {
        var existingItem = GetEquippedItemInSlot(world, officerId, item.ItemType);
        if (existingItem != null && existingItem.Id != item.Id)
        {
            MoveItemToFactionInventory(existingItem, factionId);
        }

        EquipItemToOfficer(item, factionId, officerId);
    }

    internal static void TransferEquippedItemsToFaction(WorldState world, int officerId, int factionId)
    {
        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officerId))
        {
            item.OwnerFactionId = factionId;
            item.OwnerCityId = 0;
        }
    }

}
