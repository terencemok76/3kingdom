using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    public CommandResult ExecutePersonnelBonus(int actorFactionId, int cityId, int officerId, int goldAmount, int foodAmount)
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

        if (goldAmount <= 0 && foodAmount <= 0)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.empty");
        }

        if (goldAmount < 0 || foodAmount < 0 || city.Gold < goldAmount || city.Food < foodAmount)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var loyaltyGain = goldAmount / PersonnelBonusGoldPerLoyalty + foodAmount / PersonnelBonusFoodPerLoyalty;
        if (loyaltyGain <= 0)
        {
            return LocalizedResult(false, "cmd.personnel_bonus.too_small");
        }

        city.Gold -= goldAmount;
        city.Food -= foodAmount;
        officer.Loyalty = ClampStat(officer.Loyalty + loyaltyGain);
        return LocalizedResult(
            true,
            "cmd.personnel_bonus.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), goldAmount, foodAmount, loyaltyGain },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), goldAmount, foodAmount, loyaltyGain });
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

    public CommandResult ExecuteHireOfficer(int actorFactionId, int cityId, int officerId)
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
        var sourceFactionId = sourceCity?.OwnerFactionId ?? 0;
        if (sourceFactionId == actorFactionId)
        {
            return LocalizedResult(false, "cmd.hire_officer.same_faction");
        }

        if (sourceFactionId > 0 && officer.Loyalty > HireOfficerMaxLoyalty)
        {
            return LocalizedResult(
                false,
                "cmd.hire_officer.refused",
                new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), officer.Loyalty },
                new object[] { GetOfficerDisplayName(officer, GameLanguage.English), officer.Loyalty });
        }

        if (city.Gold < HireOfficerGoldCost)
        {
            return LocalizedResult(false, "cmd.hire_officer.not_enough_gold", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        city.Gold -= HireOfficerGoldCost;
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
        if (officer.Loyalty <= 0 || sourceFactionId == 0)
        {
            officer.Loyalty = HireOfficerDefaultLoyalty;
        }

        return LocalizedResult(
            true,
            "cmd.hire_officer.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese), HireOfficerGoldCost },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English), HireOfficerGoldCost });
    }


}
