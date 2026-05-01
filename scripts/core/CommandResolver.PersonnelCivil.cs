using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    public CommandResult ExecutePersonnelBonus(int actorFactionId, int cityId, int officerId, int goldAmount, int foodAmount, int itemId = 0)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officerId))
        {
            return LocalizedResult(false, "cmd.personnel_bonus.officer_required");
        }

        ItemData? giftedItem = null;
        if (itemId > 0)
        {
            giftedItem = world.GetItem(itemId);
            if (giftedItem == null || !IsItemOwnedByFactionAtCity(giftedItem, actorFactionId, city.Id))
            {
                return LocalizedResult(false, "cmd.item.invalid_bonus_item");
            }
        }

        if (goldAmount <= 0 && foodAmount <= 0 && giftedItem == null)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.empty");
        }

        if (goldAmount < 0 || foodAmount < 0 || city.Gold < goldAmount || city.Food < foodAmount)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var loyaltyGain = goldAmount / PersonnelBonusGoldPerLoyalty + foodAmount / PersonnelBonusFoodPerLoyalty;
        var itemLoyaltyGain = giftedItem != null ? Math.Max(1, giftedItem.LoyaltyBonus) : 0;
        if (loyaltyGain <= 0 && itemLoyaltyGain <= 0)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.too_small");
        }

        city.Gold -= goldAmount;
        city.Food -= foodAmount;
        officer.Loyalty = ClampStat(officer.Loyalty + loyaltyGain + itemLoyaltyGain);
        if (giftedItem != null)
        {
            AssignItemToOfficer(world, giftedItem, actorFactionId, city.Id, officer.Id);
        }

        return LocalizedResult(
            true,
            giftedItem == null ? "cmd.personnel_bonus.resolved" : "cmd.personnel_bonus.resolved_with_item",
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), goldAmount, foodAmount, loyaltyGain }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), goldAmount, foodAmount, loyaltyGain, GetItemDisplayName(giftedItem, GameLanguage.TraditionalChinese) },
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.English), goldAmount, foodAmount, loyaltyGain }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.English), goldAmount, foodAmount, loyaltyGain, GetItemDisplayName(giftedItem, GameLanguage.English) });
    }

    public CommandResult ExecuteCivilRelief(int actorFactionId, int cityId, int goldAmount, int foodAmount)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        if (goldAmount <= 0 && foodAmount <= 0)
        {
            return LocalizedResult(false, "cmd.civil_relief.empty");
        }

        if (goldAmount < 0 || foodAmount < 0 || city.Gold < goldAmount || city.Food < foodAmount)
        {
            return LocalizedResult(false, "cmd.civil_relief.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var loyaltyGain = goldAmount / CivilReliefGoldPerTenLoyalty * 10 + foodAmount / CivilReliefFoodPerTenLoyalty * 10;
        if (loyaltyGain <= 0)
        {
            return LocalizedResult(false, "cmd.civil_relief.too_small");
        }

        city.Gold -= goldAmount;
        city.Food -= foodAmount;
        city.Loyalty = ClampStat(city.Loyalty + loyaltyGain);
        return LocalizedResult(
            true,
            "cmd.civil_relief.resolved",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), goldAmount, foodAmount, loyaltyGain },
            new object[] { GetCityName(city, GameLanguage.English), goldAmount, foodAmount, loyaltyGain });
    }

    public CommandResult ExecuteCivilInvestigation(int actorFactionId, int cityId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        var hiddenOfficer = TryFindDiscoverableOfficer(world, city.OwnerFactionId, city.Id);
        if (hiddenOfficer != null)
        {
            RevealFreeOfficerAtCity(city, hiddenOfficer);
            return LocalizedResult(
                true,
                "cmd.civil_investigate.officer_discovered",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetOfficerDisplayName(hiddenOfficer, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetOfficerDisplayName(hiddenOfficer, GameLanguage.English) });
        }

        var hiddenItem = TryFindDiscoverableItem(world, city.Id);
        if (hiddenItem != null)
        {
            MoveItemToCityInventory(hiddenItem, actorFactionId, city.Id);
            return LocalizedResult(
                true,
                "cmd.civil_investigate.item_discovered",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetItemDisplayName(hiddenItem, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetItemDisplayName(hiddenItem, GameLanguage.English) });
        }

        var roll = _random.Next(0, 100);
        if (roll < 55)
        {
            var loyaltyGain = _random.Next(1, 4);
            city.Loyalty = ClampStat(city.Loyalty + loyaltyGain);
            return LocalizedResult(
                true,
                "cmd.civil_investigate.loyalty",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), loyaltyGain },
                new object[] { GetCityName(city, GameLanguage.English), loyaltyGain });
        }

        if (roll < 78)
        {
            var foodGain = _random.Next(80, 181);
            city.Food += foodGain;
            return LocalizedResult(
                true,
                "cmd.civil_investigate.food",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foodGain },
                new object[] { GetCityName(city, GameLanguage.English), foodGain });
        }

        if (roll < 93)
        {
            var goldGain = _random.Next(40, 101);
            city.Gold += goldGain;
            return LocalizedResult(
                true,
                "cmd.civil_investigate.gold",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), goldGain },
                new object[] { GetCityName(city, GameLanguage.English), goldGain });
        }

        city.Loyalty = ClampStat(city.Loyalty + 1);
        city.Farm = ClampStat(city.Farm + 1);
        return LocalizedResult(
            true,
            "cmd.civil_investigate.farm_tip",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(city, GameLanguage.English) });
    }

    public CommandResult ExecuteAssignOfficerRole(int actorFactionId, int cityId, int officerId, string role)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officerId))
        {
            return LocalizedResult(false, "cmd.assign_role.officer_required");
        }

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.assign_role.ruler_blocked");
        }

        if (!IsAssignableRole(role))
        {
            return LocalizedResult(false, "cmd.assign_role.invalid_role");
        }

        officer.Role = role;
        return LocalizedResult(
            true,
            "cmd.assign_role.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetOfficerRoleName(role, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetOfficerRoleName(role, GameLanguage.English) });
    }

    public CommandResult ExecuteHireOfficer(int actorFactionId, int cityId, int officerId, int goldOffer = 0, int foodOffer = 0, int itemId = 0)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null)
        {
            return LocalizedResult(false, "cmd.hire_officer.officer_required");
        }

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.hire_officer.ruler_blocked");
        }

        if (!IsOfficerOldEnoughToJoin(world, officer))
        {
            return LocalizedResult(false, "cmd.hire_officer.too_young");
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        var isFreeOfficer = FreeOfficerMovement.IsFreeOfficer(world, officer);
        var sourceFactionId = isFreeOfficer ? 0 : sourceCity?.OwnerFactionId ?? 0;
        if (isFreeOfficer && !FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
        {
            return LocalizedResult(false, "cmd.hire_officer.not_visible");
        }

        if (sourceFactionId == actorFactionId)
        {
            return LocalizedResult(false, "cmd.hire_officer.same_faction");
        }

        ItemData? giftedItem = null;
        if (itemId > 0)
        {
            giftedItem = world.GetItem(itemId);
            if (giftedItem == null || !IsItemOwnedByFactionAtCity(giftedItem, actorFactionId, city.Id))
            {
                return LocalizedResult(false, "cmd.item.invalid_hire_item");
            }
        }

        if (goldOffer < 0 || foodOffer < 0 || city.Gold < HireOfficerGoldCost + goldOffer || city.Food < foodOffer)
        {
            return LocalizedResult(false, "cmd.hire_officer.not_enough_offer_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var rulerCharm = GetRulerCharm(world, actorFactionId);
        if (isFreeOfficer && !DoesFreeOfficerAcceptHire(city, officer, rulerCharm, goldOffer, foodOffer, giftedItem))
        {
            return LocalizedResult(
                false,
                "cmd.hire_officer.free_refused",
                new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese) },
                new object[] { GetOfficerDisplayName(officer, GameLanguage.English) });
        }

        if (sourceFactionId > 0 && !DoesEmployedOfficerAcceptHire(officer, rulerCharm, goldOffer, foodOffer, giftedItem))
        {
            return LocalizedResult(
                false,
                "cmd.hire_officer.refused",
                new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), officer.Loyalty },
                new object[] { GetOfficerDisplayName(officer, GameLanguage.English), officer.Loyalty });
        }

        city.Gold -= HireOfficerGoldCost + goldOffer;
        city.Food -= foodOffer;
        sourceCity?.OfficerIds.Remove(officer.Id);
        if (!city.OfficerIds.Contains(officer.Id))
        {
            city.OfficerIds.Add(officer.Id);
        }

        var oldFaction = sourceFactionId > 0 ? world.GetFaction(sourceFactionId) : null;
        oldFaction?.OfficerIds.Remove(officer.Id);
        var newFaction = world.GetFaction(actorFactionId);
        if (newFaction != null && !newFaction.OfficerIds.Contains(officer.Id))
        {
            newFaction.OfficerIds.Add(officer.Id);
        }

        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 0;
        if (officer.Loyalty <= 0 || sourceFactionId == 0)
        {
            officer.Loyalty = HireOfficerDefaultLoyalty;
        }

        if (giftedItem != null)
        {
            AssignItemToOfficer(world, giftedItem, actorFactionId, city.Id, officer.Id);
        }

        return LocalizedResult(
            true,
            giftedItem == null ? "cmd.hire_officer.resolved" : "cmd.hire_officer.resolved_with_item",
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese), HireOfficerGoldCost + goldOffer, foodOffer }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese), HireOfficerGoldCost + goldOffer, foodOffer, GetItemDisplayName(giftedItem, GameLanguage.TraditionalChinese) },
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English), HireOfficerGoldCost + goldOffer, foodOffer }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English), HireOfficerGoldCost + goldOffer, foodOffer, GetItemDisplayName(giftedItem, GameLanguage.English) });
    }


}
