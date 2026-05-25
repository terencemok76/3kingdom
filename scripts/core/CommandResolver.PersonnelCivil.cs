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
            if (giftedItem == null || !IsItemOwnedByFactionInventory(giftedItem, actorFactionId))
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
            AssignItemToOfficer(world, giftedItem, actorFactionId, officer.Id);
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

    public CommandResult ExecuteCivilRelief(int actorFactionId, int cityId, int officerId, int goldAmount, int foodAmount)
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
            return LocalizedResult(false, "cmd.civil_relief.officer_required");
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

        return Execute(new CommandRequest
        {
            Type = CommandType.CivilRelief,
            ActorFactionId = actorFactionId,
            SourceCityId = cityId,
            GoldToSend = goldAmount,
            FoodToSend = foodAmount,
            OfficerIds = new List<int> { officerId }
        });
    }

    public CommandResult ExecuteCivilInvestigation(int actorFactionId, int cityId)
    {
        return Execute(new CommandRequest
        {
            Type = CommandType.Search,
            ActorFactionId = actorFactionId,
            SourceCityId = cityId
        });
    }

    public CommandResult ExecuteAssignOfficerAppointment(int actorFactionId, int cityId, int officerId, string appointment)
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

        if (!IsValidOfficerAppointment(appointment))
        {
            return LocalizedResult(false, "cmd.assign_role.invalid_role");
        }

        if (appointment.Equals(OfficerAppointmentRules.Governor, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var cityOfficerId in city.OfficerIds)
            {
                if (cityOfficerId == officer.Id)
                {
                    continue;
                }

                var cityOfficer = world.GetOfficer(cityOfficerId);
                if (cityOfficer != null)
                {
                    ClearOfficerAppointment(cityOfficer, OfficerAppointmentRules.Governor);
                }
            }
        }

        AssignOfficerAppointment(officer, appointment);
        return LocalizedResult(
            true,
            "cmd.assign_role.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetAppointmentName(appointment, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetAppointmentName(appointment, GameLanguage.English) });
    }

    public CommandResult ExecuteClearOfficerAppointment(int actorFactionId, int cityId, int officerId, string appointment)
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

        if (!IsValidClearableAppointment(appointment))
        {
            return LocalizedResult(false, "cmd.assign_role.invalid_role");
        }

        if (!HasOfficerAppointment(officer, appointment))
        {
            return LocalizedResult(
                false,
                "cmd.assign_role.not_assigned",
                new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetAppointmentName(appointment, GameLanguage.TraditionalChinese) },
                new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetAppointmentName(appointment, GameLanguage.English) });
        }

        ClearOfficerAppointment(officer, appointment);
        if (appointment.Equals(OfficerAppointmentRules.Governor, StringComparison.OrdinalIgnoreCase))
        {
            ClearCityPrefectAuthorization(city);
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction != null)
        {
            if (appointment.Equals(OfficerAppointmentRules.Chancellor, StringComparison.OrdinalIgnoreCase) &&
                faction.ChancellorOfficerId == officer.Id)
            {
                faction.ChancellorOfficerId = 0;
            }

            if (appointment.Equals(OfficerAppointmentRules.ChiefStrategist, StringComparison.OrdinalIgnoreCase) &&
                faction.ChiefStrategistOfficerId == officer.Id)
            {
                faction.ChiefStrategistOfficerId = 0;
            }
        }

        return LocalizedResult(
            true,
            "cmd.assign_role.cleared",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetAppointmentName(appointment, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetAppointmentName(appointment, GameLanguage.English) });
    }

    public CommandResult ExecuteClearCityPrefect(int actorFactionId, int cityId)
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

        var prefect = GetCityPrefect(world, city);
        if (prefect == null)
        {
            return LocalizedResult(false, "cmd.assign_role.prefect_not_found");
        }

        return ExecuteClearOfficerAppointment(actorFactionId, cityId, prefect.Id, OfficerAppointmentRules.Governor);
    }

    public CommandResult ExecuteAuthorizePrefect(int actorFactionId, int cityId, PrefectAuthorizationType authorizationType)
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

        var prefect = GetCityPrefect(world, city);
        if (RequiresAppointedPrefect(authorizationType) && prefect == null)
        {
            return LocalizedResult(false, "cmd.authorize_prefect.prefect_required");
        }

        if (authorizationType == PrefectAuthorizationType.None)
        {
            RemoveAuthorizedPlanSchedule(world, city);
            ClearCityAuthorizedPlan(city);
        }
        else if (authorizationType == PrefectAuthorizationType.Full && city.PrefectPlanRemainingMonths <= 0)
        {
            var plannedJob = ChooseAuthorizedPlanJob(world, city, prefect);
            if (plannedJob.HasValue)
            {
                city.PrefectPlanJobType = plannedJob.Value;
                city.PrefectPlanTotalMonths = ChooseAuthorizedPlanDuration(city, plannedJob.Value, prefect);
                city.PrefectPlanRemainingMonths = city.PrefectPlanTotalMonths;
                city.PrefectPlanIsPlayerDirected = false;
            }
        }

        city.PrefectAuthorizationType = authorizationType;
        return LocalizedResult(
            true,
            "cmd.authorize_prefect.resolved",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                prefect != null ? GetOfficerDisplayName(prefect, GameLanguage.TraditionalChinese) : GetAppointmentName(OfficerAppointmentRules.Governor, GameLanguage.TraditionalChinese),
                GetPrefectAuthorizationTypeName(authorizationType, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                prefect != null ? GetOfficerDisplayName(prefect, GameLanguage.English) : GetAppointmentName(OfficerAppointmentRules.Governor, GameLanguage.English),
                GetPrefectAuthorizationTypeName(authorizationType, GameLanguage.English)
            });
    }

    public CommandResult ExecuteSetPrefectPlan(int actorFactionId, int cityId, InternalAffairsJobType jobType, int months)
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

        if (city.PrefectAuthorizationType == PrefectAuthorizationType.None)
        {
            return LocalizedResult(false, "cmd.prefect_plan.authorization_required");
        }

        if (months <= 0)
        {
            return LocalizedResult(false, "cmd.internal_affairs.invalid_duration");
        }

        var prefect = GetCityPrefect(world, city);
        if (prefect == null)
        {
            return LocalizedResult(false, "cmd.authorize_prefect.prefect_required");
        }

        RemoveAuthorizedPlanSchedule(world, city);
        city.PrefectPlanJobType = jobType;
        city.PrefectPlanTotalMonths = Math.Min(months, 24);
        city.PrefectPlanRemainingMonths = Math.Min(months, 24);
        city.PrefectPlanIsPlayerDirected = city.PrefectAuthorizationType == PrefectAuthorizationType.Half;

        return LocalizedResult(
            true,
            "cmd.prefect_plan.set",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetInternalAffairsJobName(jobType, GameLanguage.TraditionalChinese),
                city.PrefectPlanTotalMonths
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetInternalAffairsJobName(jobType, GameLanguage.English),
                city.PrefectPlanTotalMonths
            });
    }

    public CommandResult ExecuteAssignFactionAdvisor(int actorFactionId, int cityId, int officerId, string position)
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

        var faction = world.GetFaction(actorFactionId);
        if (faction == null)
        {
            return LocalizedResult(false, "cmd.actor_faction_not_found");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officerId))
        {
            return LocalizedResult(false, "cmd.assign_advisor.officer_required");
        }

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.assign_advisor.ruler_blocked");
        }

        if (!IsValidAdvisorPosition(position))
        {
            return LocalizedResult(false, "cmd.assign_advisor.invalid_position");
        }

        if (position.Equals("Chancellor", StringComparison.OrdinalIgnoreCase))
        {
            var previousHolder = world.GetOfficer(faction.ChancellorOfficerId);
            if (previousHolder != null && previousHolder.Id != officer.Id)
            {
                ClearOfficerAppointment(previousHolder, OfficerAppointmentRules.Chancellor);
            }
            faction.ChancellorOfficerId = officer.Id;
            AssignOfficerAppointment(officer, OfficerAppointmentRules.Chancellor);
        }
        else
        {
            var previousHolder = world.GetOfficer(faction.ChiefStrategistOfficerId);
            if (previousHolder != null && previousHolder.Id != officer.Id)
            {
                ClearOfficerAppointment(previousHolder, OfficerAppointmentRules.ChiefStrategist);
            }
            faction.ChiefStrategistOfficerId = officer.Id;
            AssignOfficerAppointment(officer, OfficerAppointmentRules.ChiefStrategist);
        }

        return LocalizedResult(
            true,
            "cmd.assign_advisor.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetAppointmentName(position, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetAppointmentName(position, GameLanguage.English) });
    }

    public CommandResult ExecuteFireOfficer(int actorFactionId, int cityId, int officerId)
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

        if (!city.OfficerIds.Contains(officerId))
        {
            return LocalizedResult(false, "cmd.fire_officer.officer_required");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || officer.CityId != city.Id)
        {
            return LocalizedResult(false, "cmd.fire_officer.officer_required");
        }

        var removedPrefect = HasOfficerAppointment(officer, OfficerAppointmentRules.Governor);

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.fire_officer.ruler_blocked");
        }

        if (IsOfficerAssignedThisMonth(world, officer) || HasActiveInternalAffairsSchedule(world, officer.Id))
        {
            return LocalizedResult(
                false,
                "cmd.fire_officer.officer_unavailable",
                new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese) },
                new object[] { GetOfficerDisplayName(officer, GameLanguage.English) });
        }

        city.OfficerIds.Remove(officer.Id);
        var faction = world.GetFaction(actorFactionId);
        faction?.OfficerIds.Remove(officer.Id);
        ClearFactionAdvisorPosts(world, officer.Id);
        ClearAllOfficerAppointments(officer);

        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officer.Id))
        {
            MoveItemToFactionInventory(item, actorFactionId);
        }

        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 2;
        officer.Loyalty = Math.Min(officer.Loyalty, HireOfficerDefaultLoyalty);
        PrefectAutoAppointmentOutcome? prefectOutcome = null;
        if (removedPrefect)
        {
            prefectOutcome = EnsureCityPrefectAppointment(world, city);
        }

        var result = LocalizedResult(
            true,
            "cmd.fire_officer.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English) });
        AppendPrefectAutoAppointmentOutcome(result, prefectOutcome);
        return result;
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

        var removedPrefect = HasOfficerAppointment(officer, OfficerAppointmentRules.Governor);

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
            if (giftedItem == null || !IsItemOwnedByFactionInventory(giftedItem, actorFactionId))
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
        ClearFactionAdvisorPosts(world, officer.Id);
        ClearAllOfficerAppointments(officer);
        var newFaction = world.GetFaction(actorFactionId);
        if (newFaction != null && !newFaction.OfficerIds.Contains(officer.Id))
        {
            newFaction.OfficerIds.Add(officer.Id);
        }

        TransferEquippedItemsToFaction(world, officer.Id, actorFactionId);
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 0;
        if (officer.Loyalty <= 0 || sourceFactionId == 0)
        {
            officer.Loyalty = HireOfficerDefaultLoyalty;
        }

        if (giftedItem != null)
        {
            AssignItemToOfficer(world, giftedItem, actorFactionId, officer.Id);
        }

        PrefectAutoAppointmentOutcome? prefectOutcome = null;
        if (removedPrefect && sourceCity != null && sourceFactionId > 0)
        {
            prefectOutcome = EnsureCityPrefectAppointment(world, sourceCity);
        }

        var result = LocalizedResult(
            true,
            giftedItem == null ? "cmd.hire_officer.resolved" : "cmd.hire_officer.resolved_with_item",
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese), HireOfficerGoldCost + goldOffer, foodOffer }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese), HireOfficerGoldCost + goldOffer, foodOffer, GetItemDisplayName(giftedItem, GameLanguage.TraditionalChinese) },
            giftedItem == null
                ? new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English), HireOfficerGoldCost + goldOffer, foodOffer }
                : new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English), HireOfficerGoldCost + goldOffer, foodOffer, GetItemDisplayName(giftedItem, GameLanguage.English) });
        AppendPrefectAutoAppointmentOutcome(result, prefectOutcome);
        return result;
    }

    public CommandResult ExecuteRecallOfficerItem(int actorFactionId, int cityId, int officerId, int itemId)
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
            return LocalizedResult(false, "cmd.request_item.officer_required");
        }

        var item = world.GetItem(itemId);
        if (item == null || item.EquippedOfficerId != officer.Id)
        {
            return LocalizedResult(false, "cmd.request_item.item_required");
        }

        MoveItemToFactionInventory(item, actorFactionId);
        return LocalizedResult(
            true,
            "cmd.request_item.resolved",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetItemDisplayName(item, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetItemDisplayName(item, GameLanguage.English) });
    }


}
