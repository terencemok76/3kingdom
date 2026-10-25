using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private const int SurrenderHighAmbitionThreshold = 86;
    private const int SurrenderHighAmbitionMaximumCityCount = 1;
    private const int SurrenderHighAmbitionTroopRatioPercent = 30;
    private const int SurrenderHighAmbitionMinimumThreatScore = 90;
    private const int SurrenderedOfficerStartingLoyalty = 60;
    private enum SurrenderEligibilityFailure
    {
        None,
        TargetMustBeAi,
        CityCount,
        ActiveCampaign,
        ActiveAlliance,
        TroopRatio,
        Threat,
        HighAmbitionCityCount,
        HighAmbitionTroopRatio,
        HighAmbitionThreat
    }
    private CommandResult ScheduleDiplomacy(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetFactionId.HasValue)
        {
            return LocalizedResult(false, "cmd.diplomacy.target_required");
        }

        var targetFaction = world.GetFaction(request.TargetFactionId.Value);
        if (targetFaction == null || !IsFactionAlive(world, targetFaction.Id))
        {
            return LocalizedResult(false, "cmd.diplomacy.target_not_found");
        }

        if (targetFaction.Id == request.ActorFactionId)
        {
            return LocalizedResult(false, "cmd.diplomacy.same_faction");
        }

        var officer = GetSingleAvailableOfficer(world, sourceCity, request.OfficerIds);
        if (officer == null)
        {
            return LocalizedResult(false, "cmd.diplomacy.officer_required", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.diplomacy.ruler_blocked");
        }

        var relation = FindDiplomacyRelation(world, request.ActorFactionId, targetFaction.Id);
        if (request.DiplomacyActionType == DiplomacyActionType.Alliance &&
            relation?.Status == DiplomacyStatusType.Alliance &&
            relation.RemainingMonths > 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.alliance_already_active", GetFactionArgs(targetFaction, GameLanguage.TraditionalChinese), GetFactionArgs(targetFaction, GameLanguage.English));
        }

        if (request.DiplomacyActionType == DiplomacyActionType.Truce &&
            relation?.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
            relation.RemainingMonths > 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.truce_already_active", GetFactionArgs(targetFaction, GameLanguage.TraditionalChinese), GetFactionArgs(targetFaction, GameLanguage.English));
        }

        if (request.DiplomacyActionType == DiplomacyActionType.BreakPact &&
            (relation == null || relation.Status == DiplomacyStatusType.Neutral || relation.RemainingMonths <= 0))
        {
            return LocalizedResult(false, "cmd.diplomacy.break_pact_not_active", GetFactionArgs(targetFaction, GameLanguage.TraditionalChinese), GetFactionArgs(targetFaction, GameLanguage.English));
        }

        var forceSurrenderSuccess = IsDebugSurrenderForceApplicable(world, request.ActorFactionId, targetFaction.Id);
        if (request.DiplomacyActionType == DiplomacyActionType.Surrender &&
            !forceSurrenderSuccess &&
            !IsSurrenderEligible(world, request.ActorFactionId, targetFaction.Id))
        {
            return GetSurrenderUnavailableResult(world, request.ActorFactionId, targetFaction.Id);
        }

        var duration = request.DiplomacyActionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand or DiplomacyActionType.BreakPact or DiplomacyActionType.Surrender
            ? 1
            : Math.Clamp(request.DurationMonths, 1, 12);
        if (request.DiplomacyActionType is not (DiplomacyActionType.Gift or DiplomacyActionType.Demand or DiplomacyActionType.BreakPact or DiplomacyActionType.Surrender) &&
            request.DurationMonths <= 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.invalid_duration");
        }

        var reservedGold = request.DiplomacyActionType == DiplomacyActionType.Gift
            ? Math.Max(0, request.GoldToSend)
            : 0;
        var reservedFood = request.DiplomacyActionType == DiplomacyActionType.Gift
            ? Math.Max(0, request.FoodToSend)
            : 0;
        var reservedHorses = request.DiplomacyActionType == DiplomacyActionType.Gift
            ? Math.Max(0, request.HorsesToSend)
            : 0;
        var demandGold = request.DiplomacyActionType == DiplomacyActionType.Demand ? Math.Max(0, request.GoldToSend) : 0;
        var demandFood = request.DiplomacyActionType == DiplomacyActionType.Demand ? Math.Max(0, request.FoodToSend) : 0;
        var demandHorses = request.DiplomacyActionType == DiplomacyActionType.Demand ? Math.Max(0, request.HorsesToSend) : 0;
        if (request.DiplomacyActionType == DiplomacyActionType.Gift &&
            reservedGold <= 0 &&
            reservedFood <= 0 &&
            reservedHorses <= 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.gift_resource_required");
        }

        if (request.DiplomacyActionType == DiplomacyActionType.Demand &&
            demandGold <= 0 &&
            demandFood <= 0 &&
            demandHorses <= 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.demand_resource_required");
        }

        if (request.DiplomacyActionType == DiplomacyActionType.Gift &&
            (reservedGold > sourceCity.Gold || reservedFood > sourceCity.Food || reservedHorses > sourceCity.Horses))
        {
            return LocalizedResult(false, "cmd.diplomacy.not_enough_resources", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        MarkOfficerAssigned(world, officer, CommandType.Diplomacy);
        if (request.DiplomacyActionType == DiplomacyActionType.Gift)
        {
            sourceCity.Gold -= reservedGold;
            sourceCity.Food -= reservedFood;
            sourceCity.Horses -= reservedHorses;
        }

        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetFactionId = targetFaction.Id,
            GoldToSend = request.DiplomacyActionType == DiplomacyActionType.Demand ? demandGold : reservedGold,
            FoodToSend = request.DiplomacyActionType == DiplomacyActionType.Demand ? demandFood : reservedFood,
            HorsesToSend = request.DiplomacyActionType == DiplomacyActionType.Demand ? demandHorses : reservedHorses,
            DurationMonths = duration,
            DiplomacyActionType = request.DiplomacyActionType,
            OfficerIds = new System.Collections.Generic.List<int> { officer.Id }
        });

        return LocalizedResult(
            true,
            "cmd.diplomacy.scheduled",
            new object[]
            {
                GetCityName(sourceCity, GameLanguage.TraditionalChinese),
                GetDiplomacyActionName(request.DiplomacyActionType, GameLanguage.TraditionalChinese),
                GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetCityName(sourceCity, GameLanguage.English),
                GetDiplomacyActionName(request.DiplomacyActionType, GameLanguage.English),
                GetFactionName(targetFaction, GameLanguage.English)
            });
    }

    private CommandResult ResolveDiplomacy(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        var targetFaction = world.GetFaction(pendingCommand.TargetFactionId);
        if (targetFaction == null || !IsFactionAlive(world, pendingCommand.TargetFactionId))
        {
            if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
                sourceCity.Food += pendingCommand.FoodToSend;
                sourceCity.Horses += pendingCommand.HorsesToSend;
            }

            return LocalizedResult(false, "cmd.diplomacy.cancelled");
        }

        var officer = world.GetOfficer(pendingCommand.OfficerIds.FirstOrDefault());
        if (officer == null)
        {
            if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
                sourceCity.Food += pendingCommand.FoodToSend;
                sourceCity.Horses += pendingCommand.HorsesToSend;
            }

            return LocalizedResult(false, "cmd.diplomacy.cancelled");
        }

        var relation = GetOrCreateDiplomacyRelation(world, pendingCommand.ActorFactionId, targetFaction.Id);
        relation.LastUpdatedYear = world.Year;
        relation.LastUpdatedMonth = world.Month;

        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift)
        {
            var giftValue = pendingCommand.GoldToSend +
                            pendingCommand.FoodToSend * MerchantGoldPerTrade / MerchantFoodPerTrade +
                            pendingCommand.HorsesToSend * MerchantGoldPerHorseTrade / MerchantHorsePerTrade;
            var gain = Math.Clamp(giftValue / 100, 3, 25);
            relation.RelationScore = Math.Clamp(relation.RelationScore + gain, -100, 100);
            DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, 3);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: true));
            return LocalizedResult(
                true,
                "cmd.diplomacy.gift_resolved",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese),
                    pendingCommand.GoldToSend,
                    pendingCommand.FoodToSend,
                    pendingCommand.HorsesToSend,
                    gain
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English),
                    pendingCommand.GoldToSend,
                    pendingCommand.FoodToSend,
                    pendingCommand.HorsesToSend,
                    gain
                });
        }

        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.BreakPact)
        {
            relation.Status = DiplomacyStatusType.Neutral;
            relation.RemainingMonths = 0;
            relation.RelationScore = Math.Clamp(relation.RelationScore - 18, -100, 100);
            DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, -35);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: true));
            return LocalizedResult(
                true,
                "cmd.diplomacy.break_pact_resolved",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English)
                });
        }

        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Demand)
        {
            var chance = CalculateDiplomacySuccessChance(world, sourceCity, officer, targetFaction, relation, pendingCommand.DiplomacyActionType);
            var success = _random.Next(100) < chance;
            if (!success)
            {
                relation.RelationScore = Math.Clamp(relation.RelationScore - 8, -100, 100);
                DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, -4);
                OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: false));
                return LocalizedResult(
                    false,
                    "cmd.diplomacy.failed",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.TraditionalChinese),
                        GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.English),
                        GetFactionName(targetFaction, GameLanguage.English)
                    });
            }

            var tributeGoldCity = world.Cities
                .Where(city => city.OwnerFactionId == targetFaction.Id)
                .OrderByDescending(city => city.Gold)
                .FirstOrDefault();
            var tributeFoodCity = world.Cities
                .Where(city => city.OwnerFactionId == targetFaction.Id)
                .OrderByDescending(city => city.Food)
                .FirstOrDefault();
            var tributeHorseCity = world.Cities
                .Where(city => city.OwnerFactionId == targetFaction.Id)
                .OrderByDescending(city => city.Horses)
                .FirstOrDefault();
            var tributeGold = tributeGoldCity == null ? 0 : Math.Min(tributeGoldCity.Gold, pendingCommand.GoldToSend);
            var tributeFood = tributeFoodCity == null ? 0 : Math.Min(tributeFoodCity.Food, pendingCommand.FoodToSend);
            var tributeHorses = tributeHorseCity == null ? 0 : Math.Min(tributeHorseCity.Horses, pendingCommand.HorsesToSend);
            if (tributeGold <= 0 && tributeFood <= 0 && tributeHorses <= 0)
            {
                relation.RelationScore = Math.Clamp(relation.RelationScore - 10, -100, 100);
                OfficerProgressionRules.AwardDiplomacyExperience(officer, 10);
                return LocalizedResult(
                    false,
                    "cmd.diplomacy.demand_no_resources",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetFactionName(targetFaction, GameLanguage.English)
                    });
            }

            if (tributeGold > 0 && tributeGoldCity != null)
            {
                tributeGoldCity.Gold -= tributeGold;
                sourceCity.Gold += tributeGold;
            }

            if (tributeFood > 0 && tributeFoodCity != null)
            {
                tributeFoodCity.Food -= tributeFood;
                sourceCity.Food += tributeFood;
            }

            if (tributeHorses > 0 && tributeHorseCity != null)
            {
                tributeHorseCity.Horses -= tributeHorses;
                sourceCity.Horses += tributeHorses;
            }

            relation.RelationScore = Math.Clamp(relation.RelationScore - 14, -100, 100);
            DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, -8);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: true));
            return LocalizedResult(
                true,
                "cmd.diplomacy.demand_resolved",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese),
                    tributeGold,
                    tributeFood,
                    tributeHorses
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English),
                    tributeGold,
                    tributeFood,
                    tributeHorses
                });
        }

        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Surrender)
        {
            var forceSurrenderSuccess = IsDebugSurrenderForceApplicable(world, pendingCommand.ActorFactionId, targetFaction.Id);
            if (!forceSurrenderSuccess && !IsSurrenderEligible(world, pendingCommand.ActorFactionId, targetFaction.Id))
            {
                return GetSurrenderUnavailableResult(world, pendingCommand.ActorFactionId, targetFaction.Id);
            }

            var surrenderChance = forceSurrenderSuccess
                ? 100
                : CalculateDiplomacySuccessChance(world, sourceCity, officer, targetFaction, relation, pendingCommand.DiplomacyActionType);
            if (_random.Next(100) >= surrenderChance)
            {
                relation.RelationScore = Math.Clamp(relation.RelationScore - 12, -100, 100);
                DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, -8);
                OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: false));
                return LocalizedResult(
                    false,
                    "cmd.diplomacy.failed",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.TraditionalChinese),
                        GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.English),
                        GetFactionName(targetFaction, GameLanguage.English)
                    });
            }

            var transferredCityCount = world.Cities.Count(city => city.OwnerFactionId == targetFaction.Id);
            var transferredOfficerCount = AbsorbFaction(world, pendingCommand.ActorFactionId, targetFaction.Id, pendingCommand);
            if (forceSurrenderSuccess)
            {
                world.ForceNextSurrenderSuccessForDebug = false;
            }
            OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: true));
            return LocalizedResult(
                true,
                "cmd.diplomacy.surrender_resolved",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese),
                    transferredCityCount,
                    transferredOfficerCount
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English),
                    transferredCityCount,
                    transferredOfficerCount
                });
        }

        var treatyChance = CalculateDiplomacySuccessChance(world, sourceCity, officer, targetFaction, relation, pendingCommand.DiplomacyActionType);
        var treatySuccess = _random.Next(100) < treatyChance;
        if (!treatySuccess)
        {
            relation.RelationScore = Math.Clamp(relation.RelationScore - 6, -100, 100);
            DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, -2);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: false));
            return LocalizedResult(
                false,
                "cmd.diplomacy.failed",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English)
                });
        }

        relation.Status = pendingCommand.DiplomacyActionType == DiplomacyActionType.Alliance
            ? DiplomacyStatusType.Alliance
            : DiplomacyStatusType.Truce;
        relation.RemainingMonths = Math.Max(relation.RemainingMonths, pendingCommand.DurationMonths + 1);
        relation.RelationScore = Math.Clamp(relation.RelationScore + (pendingCommand.DiplomacyActionType == DiplomacyActionType.Alliance ? 25 : 15), -100, 100);
        var treatyTrustGain = pendingCommand.DiplomacyActionType == DiplomacyActionType.Alliance ? 8 : 5;
        DiplomacyScoreRules.AdjustTrust(relation, pendingCommand.ActorFactionId, treatyTrustGain);
        DiplomacyScoreRules.AdjustTrust(relation, targetFaction.Id, treatyTrustGain);
        OfficerProgressionRules.AwardDiplomacyExperience(officer, GetDiplomacyExperienceReward(pendingCommand.DiplomacyActionType, success: true));

        return LocalizedResult(
            true,
            "cmd.diplomacy.resolved",
            new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese),
                    pendingCommand.DurationMonths
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetDiplomacyActionName(pendingCommand.DiplomacyActionType, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English),
                    pendingCommand.DurationMonths
                });
    }

    private static int CalculateDiplomacySuccessChance(
        WorldState world,
        CityData sourceCity,
        OfficerData officer,
        FactionData targetFaction,
        DiplomacyRelationData relation,
        DiplomacyActionType actionType)
    {
        var charm = GetEffectiveStat(world, officer, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var politics = GetEffectiveStat(world, officer, data => data.Politics, item => item.PoliticsBonus, OfficerProgressionStat.Politics);
        var intelligence = GetEffectiveStat(world, officer, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var ruler = world.GetOfficer(targetFaction.RulerOfficerId);
        var rulerCharm = ruler != null ? GetEffectiveStat(world, ruler, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm) : 50;
        var rulerPolitics = ruler != null ? GetEffectiveStat(world, ruler, data => data.Politics, item => item.PoliticsBonus, OfficerProgressionStat.Politics) : 50;
        var officerScore = (charm + politics + intelligence) / 3;
        var targetScore = (rulerCharm + rulerPolitics) / 2;
        var relationBonus = relation.RelationScore / 4;
        var targetTrust = DiplomacyScoreRules.GetTrustScore(relation, targetFaction.Id, sourceCity.OwnerFactionId);
        var targetThreat = DiplomacyScoreRules.GetThreatScore(relation, targetFaction.Id, sourceCity.OwnerFactionId);
        var ambitionPenalty = actionType == DiplomacyActionType.Surrender
            ? Math.Max(0, (ruler?.Ambition ?? 50) - 50) / 2
            : 0;
        var cityLoyaltyBonus = Math.Max(0, sourceCity.Loyalty - 70) / 10;
        var pressureBonus = GetFactionTroopTotal(world, sourceCity.OwnerFactionId) - GetFactionTroopTotal(world, targetFaction.Id);
        var rankBonus = OfficerProgressionRules.GetDiplomacySuccessBonus(officer);
        var baseChance = actionType switch
        {
            DiplomacyActionType.Alliance => 42,
            DiplomacyActionType.Truce => 58,
            DiplomacyActionType.Demand => 34,
            DiplomacyActionType.Surrender => 18,
            _ => 50
        };
        var extraPressure = actionType switch
        {
            DiplomacyActionType.Demand => pressureBonus / 120,
            DiplomacyActionType.Surrender => pressureBonus / 80,
            _ => 0
        };
        var trustBonus = actionType switch
        {
            DiplomacyActionType.Alliance => (targetTrust - 50) / 3,
            DiplomacyActionType.Truce => (targetTrust - 50) / 5,
            DiplomacyActionType.Surrender => (targetTrust - 50) / 4,
            _ => 0
        };
        var threatBonus = actionType switch
        {
            DiplomacyActionType.Demand => (targetThreat - 50) / 2,
            DiplomacyActionType.Surrender => (targetThreat - 50) * 2 / 3,
            DiplomacyActionType.Truce => (targetThreat - 50) / 4,
            DiplomacyActionType.Alliance => (50 - targetThreat) / 5,
            _ => 0
        };
        return Math.Clamp(baseChance + (officerScore - targetScore) / 4 + relationBonus + cityLoyaltyBonus + rankBonus + extraPressure + trustBonus + threatBonus - ambitionPenalty, 10, 90);
    }

    private static int GetFactionTroopTotal(WorldState world, int factionId)
    {
        return world.Cities
            .Where(city => city.OwnerFactionId == factionId)
            .Sum(city => city.Troops);
    }

    internal static bool IsSurrenderEligible(
        WorldState world,
        int actorFactionId,
        int targetFactionId,
        int maximumCityCount = 3,
        int maximumTroopRatioPercent = 60,
        int minimumThreatScore = 70)
    {
        return GetSurrenderEligibilityFailure(
                   world,
                   actorFactionId,
                   targetFactionId,
                   maximumCityCount,
                   maximumTroopRatioPercent,
                   minimumThreatScore) == SurrenderEligibilityFailure.None;
    }

    private static SurrenderEligibilityFailure GetSurrenderEligibilityFailure(
        WorldState world,
        int actorFactionId,
        int targetFactionId,
        int maximumCityCount = 3,
        int maximumTroopRatioPercent = 60,
        int minimumThreatScore = 70)
    {
        var actorFaction = world.GetFaction(actorFactionId);
        var targetFaction = world.GetFaction(targetFactionId);
        if (actorFaction == null || targetFaction == null || targetFaction.IsPlayer || actorFactionId == targetFactionId)
        {
            return SurrenderEligibilityFailure.TargetMustBeAi;
        }

        var targetCities = world.Cities.Where(city => city.OwnerFactionId == targetFactionId).ToList();
        if (targetCities.Count is < 1 or > 3 || targetCities.Count > maximumCityCount)
        {
            return SurrenderEligibilityFailure.CityCount;
        }

        if (world.ActiveBattleCampaigns.Any(campaign =>
                campaign.Stage != CampaignStage.Resolved &&
                (campaign.AttackerFactionId == targetFactionId || campaign.DefenderFactionId == targetFactionId)))
        {
            return SurrenderEligibilityFailure.ActiveCampaign;
        }

        if (world.DiplomacyRelations.Any(relation =>
                relation.Status == DiplomacyStatusType.Alliance &&
                relation.RemainingMonths > 0 &&
                (relation.FactionAId == targetFactionId || relation.FactionBId == targetFactionId)))
        {
            return SurrenderEligibilityFailure.ActiveAlliance;
        }

        var actorTroops = GetFactionTroopTotal(world, actorFactionId);
        var targetTroops = GetFactionTroopTotal(world, targetFactionId);
        if (actorTroops <= 0 || targetTroops * 100 > actorTroops * maximumTroopRatioPercent)
        {
            return SurrenderEligibilityFailure.TroopRatio;
        }

        var relation = world.GetDiplomacyRelation(actorFactionId, targetFactionId);
        var threatScore = DiplomacyScoreRules.CalculateThreatScore(world, targetFactionId, actorFactionId, relation);
        if (threatScore < minimumThreatScore)
        {
            return SurrenderEligibilityFailure.Threat;
        }

        var targetRuler = world.GetOfficer(targetFaction.RulerOfficerId);
        if (targetRuler == null || targetRuler.Ambition < SurrenderHighAmbitionThreshold)
        {
            return SurrenderEligibilityFailure.None;
        }

        if (targetCities.Count > SurrenderHighAmbitionMaximumCityCount)
        {
            return SurrenderEligibilityFailure.HighAmbitionCityCount;
        }

        if (targetTroops * 100 > actorTroops * SurrenderHighAmbitionTroopRatioPercent)
        {
            return SurrenderEligibilityFailure.HighAmbitionTroopRatio;
        }

        return threatScore < SurrenderHighAmbitionMinimumThreatScore
            ? SurrenderEligibilityFailure.HighAmbitionThreat
            : SurrenderEligibilityFailure.None;
    }

    private CommandResult GetSurrenderUnavailableResult(WorldState world, int actorFactionId, int targetFactionId)
    {
        var targetCityCount = world.Cities.Count(city => city.OwnerFactionId == targetFactionId);
        var actorTroops = GetFactionTroopTotal(world, actorFactionId);
        var targetTroops = GetFactionTroopTotal(world, targetFactionId);
        var relation = world.GetDiplomacyRelation(actorFactionId, targetFactionId);
        var threatScore = DiplomacyScoreRules.CalculateThreatScore(world, targetFactionId, actorFactionId, relation);
        var hasMilitaryIntel = HasSurrenderMilitaryIntel(world, actorFactionId, targetFactionId);
        var failure = GetSurrenderEligibilityFailure(world, actorFactionId, targetFactionId);
        return failure switch
        {
            SurrenderEligibilityFailure.CityCount => LocalizedResult(false, "cmd.diplomacy.surrender_city_count", new object[] { targetCityCount }, new object[] { targetCityCount }),
            SurrenderEligibilityFailure.ActiveCampaign => LocalizedResult(false, "cmd.diplomacy.surrender_active_campaign"),
            SurrenderEligibilityFailure.ActiveAlliance => LocalizedResult(false, "cmd.diplomacy.surrender_active_alliance"),
            SurrenderEligibilityFailure.TroopRatio when !hasMilitaryIntel => LocalizedResult(false, "cmd.diplomacy.surrender_troop_ratio_hidden"),
            SurrenderEligibilityFailure.TroopRatio => LocalizedResult(false, "cmd.diplomacy.surrender_troop_ratio", new object[] { targetTroops, actorTroops, 60 }, new object[] { targetTroops, actorTroops, 60 }),
            SurrenderEligibilityFailure.Threat => LocalizedResult(false, "cmd.diplomacy.surrender_threat", new object[] { threatScore, 70 }, new object[] { threatScore, 70 }),
            SurrenderEligibilityFailure.HighAmbitionCityCount => LocalizedResult(false, "cmd.diplomacy.surrender_high_ambition_city_count", new object[] { targetCityCount }, new object[] { targetCityCount }),
            SurrenderEligibilityFailure.HighAmbitionTroopRatio when !hasMilitaryIntel => LocalizedResult(false, "cmd.diplomacy.surrender_high_ambition_troop_ratio_hidden"),
            SurrenderEligibilityFailure.HighAmbitionTroopRatio => LocalizedResult(false, "cmd.diplomacy.surrender_high_ambition_troop_ratio", new object[] { targetTroops, actorTroops, SurrenderHighAmbitionTroopRatioPercent }, new object[] { targetTroops, actorTroops, SurrenderHighAmbitionTroopRatioPercent }),
            SurrenderEligibilityFailure.HighAmbitionThreat => LocalizedResult(false, "cmd.diplomacy.surrender_high_ambition_threat", new object[] { threatScore, SurrenderHighAmbitionMinimumThreatScore }, new object[] { threatScore, SurrenderHighAmbitionMinimumThreatScore }),
            _ => LocalizedResult(false, "cmd.diplomacy.surrender_unavailable")
        };
    }

    private static bool HasSurrenderMilitaryIntel(WorldState world, int actorFactionId, int targetFactionId)
    {
        var targetRuler = world.GetFaction(targetFactionId) is { RulerOfficerId: > 0 } faction
            ? world.GetOfficer(faction.RulerOfficerId)
            : null;
        return targetRuler != null &&
               targetRuler.CityId > 0 &&
               world.GetCity(targetRuler.CityId)?.OwnerFactionId == targetFactionId &&
               world.HasActiveCityIntel(actorFactionId, targetRuler.CityId);
    }

    private static bool IsDebugSurrenderForceApplicable(WorldState world, int actorFactionId, int targetFactionId)
    {
        var actorFaction = world.GetFaction(actorFactionId);
        var targetFaction = world.GetFaction(targetFactionId);
        return world.ForceNextSurrenderSuccessForDebug &&
               actorFaction?.IsPlayer == true &&
               targetFaction is { IsPlayer: false } &&
               actorFactionId != targetFactionId &&
               !world.ActiveBattleCampaigns.Any(campaign =>
                   campaign.Stage != CampaignStage.Resolved &&
                   (campaign.AttackerFactionId == targetFactionId || campaign.DefenderFactionId == targetFactionId));
    }

    private int AbsorbFaction(
        WorldState world,
        int receivingFactionId,
        int surrenderedFactionId,
        PendingCommandData resolvingCommand)
    {
        var receivingFaction = world.GetFaction(receivingFactionId);
        var surrenderedFaction = world.GetFaction(surrenderedFactionId);
        if (receivingFaction == null || surrenderedFaction == null)
        {
            return 0;
        }

        var cityIds = world.Cities
            .Where(city => city.OwnerFactionId == surrenderedFactionId)
            .Select(city => city.Id)
            .ToList();
        foreach (var city in world.Cities.Where(city => cityIds.Contains(city.Id)))
        {
            city.OwnerFactionId = receivingFactionId;
            city.Loyalty = Math.Max(city.Loyalty, 55);
            ClearCityPrefectAuthorization(city);
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => cityIds.Contains(schedule.CityId));
        var transferredOfficerCount = 0;
        var officerIds = new System.Collections.Generic.HashSet<int>(surrenderedFaction.OfficerIds);
        foreach (var officer in world.Officers.Where(candidate => candidate.Belongs == surrenderedFactionId.ToString()))
        {
            officerIds.Add(officer.Id);
        }

        foreach (var officerId in officerIds)
        {
            var surrenderedOfficer = world.GetOfficer(officerId);
            if (surrenderedOfficer == null || surrenderedOfficer.CaptiveFactionId > 0)
            {
                continue;
            }

            OfficerAppointmentRules.RemoveAppointment(surrenderedOfficer, OfficerAppointmentRules.Lord);
            OfficerAppointmentRules.RemoveAppointment(surrenderedOfficer, OfficerAppointmentRules.Chancellor);
            OfficerAppointmentRules.RemoveAppointment(surrenderedOfficer, OfficerAppointmentRules.ChiefStrategist);
            surrenderedOfficer.Belongs = receivingFactionId.ToString();
            // Loyalty belongs to the ruler, not to the city or faction label.
            // A surrendered officer begins serving the new ruler at the
            // standard recruit baseline instead of retaining old-ruler loyalty.
            surrenderedOfficer.Loyalty = SurrenderedOfficerStartingLoyalty;
            if (!receivingFaction.OfficerIds.Contains(surrenderedOfficer.Id))
            {
                receivingFaction.OfficerIds.Add(surrenderedOfficer.Id);
            }

            transferredOfficerCount += 1;
        }

        foreach (var item in world.Items.Where(item => item.OwnerFactionId == surrenderedFactionId))
        {
            item.OwnerFactionId = receivingFactionId;
        }

        surrenderedFaction.OfficerIds.Clear();
        surrenderedFaction.RulerOfficerId = 0;
        surrenderedFaction.ChancellorOfficerId = 0;
        surrenderedFaction.ChiefStrategistOfficerId = 0;
        surrenderedFaction.SurrenderedToFactionId = receivingFactionId;
        world.DiplomacyRelations.RemoveAll(relation => relation.FactionAId == surrenderedFactionId || relation.FactionBId == surrenderedFactionId);
        // TurnManager resolves commands by iterating this list.  Changing its
        // size here would invalidate that enumeration, so obsolete commands
        // are converted to Pass and discarded by the normal month-end cleanup.
        foreach (var command in world.PendingCommands)
        {
            if (!ReferenceEquals(command, resolvingCommand) &&
                (command.ActorFactionId == surrenderedFactionId || command.TargetFactionId == surrenderedFactionId))
            {
                command.Type = CommandType.Pass;
            }
        }
        world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == surrenderedFactionId);
        world.PendingCapturedOfficerRecords.RemoveAll(record => record.WinnerFactionId == surrenderedFactionId);
        world.CityIntelRecords.RemoveAll(record => record.ViewerFactionId == surrenderedFactionId);
        return transferredOfficerCount;
    }

    private static int GetDiplomacyExperienceReward(DiplomacyActionType actionType, bool success)
    {
        if (success)
        {
            return actionType switch
            {
                DiplomacyActionType.Alliance => 24,
                DiplomacyActionType.Truce => 18,
                DiplomacyActionType.Gift => 12,
                DiplomacyActionType.Demand => 16,
                DiplomacyActionType.Surrender => 32,
                DiplomacyActionType.BreakPact => 10,
                _ => 12
            };
        }

        return actionType switch
        {
            DiplomacyActionType.Demand => 8,
            DiplomacyActionType.Surrender => 12,
            DiplomacyActionType.Alliance => 6,
            DiplomacyActionType.Truce => 6,
            _ => 6
        };
    }
}
