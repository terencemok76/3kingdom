using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private CommandResult ScheduleSpy(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetCityId.HasValue)
        {
            return LocalizedResult(false, "cmd.spy.target_required");
        }

        var targetCity = world.GetCity(request.TargetCityId.Value);
        if (targetCity == null)
        {
            return LocalizedResult(false, "cmd.target_city_not_found");
        }

        if (targetCity.OwnerFactionId == request.ActorFactionId)
        {
            return LocalizedResult(false, "cmd.spy.same_faction");
        }

        var officer = GetSingleAvailableOfficer(world, sourceCity, request.OfficerIds);
        if (officer == null)
        {
            return LocalizedResult(false, "cmd.spy.officer_required", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        if (IsFactionRuler(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.spy.ruler_blocked");
        }

        MarkOfficerAssigned(world, officer, CommandType.Spy);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Spy,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TargetFactionId = targetCity.OwnerFactionId,
            SpyActionType = request.SpyActionType,
            OfficerIds = new System.Collections.Generic.List<int> { officer.Id }
        });

        return LocalizedResult(
            true,
            "cmd.spy.scheduled",
            new object[]
            {
                GetCityName(sourceCity, GameLanguage.TraditionalChinese),
                GetSpyActionName(request.SpyActionType, GameLanguage.TraditionalChinese),
                GetCityName(targetCity, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetCityName(sourceCity, GameLanguage.English),
                GetSpyActionName(request.SpyActionType, GameLanguage.English),
                GetCityName(targetCity, GameLanguage.English)
            });
    }

    private CommandResult ResolveSpy(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        var targetCity = world.GetCity(pendingCommand.TargetCityId);
        if (targetCity == null || targetCity.OwnerFactionId == pendingCommand.ActorFactionId)
        {
            return LocalizedResult(false, "cmd.spy.cancelled");
        }

        var officer = world.GetOfficer(pendingCommand.OfficerIds.FirstOrDefault());
        if (officer == null)
        {
            return LocalizedResult(false, "cmd.spy.cancelled");
        }

        var successChance = CalculateSpySuccessChance(world, sourceCity, targetCity, officer, pendingCommand.SpyActionType);
        var exposedChance = CalculateSpyExposureChance(world, sourceCity, targetCity, officer, pendingCommand.SpyActionType);
        var success = _random.Next(100) < successChance;
        var exposed = _random.Next(100) < exposedChance;

        if (success)
        {
            var result = ResolveSuccessfulSpyAction(world, targetCity, officer, pendingCommand.SpyActionType);
            if (exposed)
            {
                ApplySpyExposurePenalty(world, pendingCommand.ActorFactionId, officer, targetCity.OwnerFactionId, 6, 3);
                result.MessageZhHant = $"{result.MessageZhHant} {_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.spy.exposed_suffix") ?? string.Empty}".Trim();
                result.MessageEn = $"{result.MessageEn} {_localization?.TForLanguage(GameLanguage.English, "cmd.spy.exposed_suffix") ?? string.Empty}".Trim();
                result.Message = result.MessageEn;
            }

            return result;
        }

        OfficerProgressionRules.AwardSpyExperience(officer, 6);
        if (exposed)
        {
            ApplySpyExposurePenalty(world, pendingCommand.ActorFactionId, officer, targetCity.OwnerFactionId, 10, 6);
            return LocalizedResult(
                false,
                "cmd.spy.failed_exposed",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetSpyActionName(pendingCommand.SpyActionType, GameLanguage.TraditionalChinese),
                    GetCityName(targetCity, GameLanguage.TraditionalChinese)
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetSpyActionName(pendingCommand.SpyActionType, GameLanguage.English),
                    GetCityName(targetCity, GameLanguage.English)
                });
        }

        return LocalizedResult(
            false,
            "cmd.spy.failed",
            new object[]
            {
                GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                GetSpyActionName(pendingCommand.SpyActionType, GameLanguage.TraditionalChinese),
                GetCityName(targetCity, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetOfficerDisplayName(officer, GameLanguage.English),
                GetSpyActionName(pendingCommand.SpyActionType, GameLanguage.English),
                GetCityName(targetCity, GameLanguage.English)
            });
    }

    private CommandResult ResolveSuccessfulSpyAction(WorldState world, CityData targetCity, OfficerData officer, SpyActionType actionType)
    {
        switch (actionType)
        {
            case SpyActionType.Reconnaissance:
            {
                OfficerProgressionRules.AwardSpyExperience(officer, 18);
                return LocalizedResult(
                    true,
                    "cmd.spy.recon_success",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetCityName(targetCity, GameLanguage.TraditionalChinese),
                        GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.TraditionalChinese),
                        targetCity.Gold,
                        targetCity.Food,
                        targetCity.Troops,
                        targetCity.Defense,
                        targetCity.Loyalty,
                        targetCity.OfficerIds.Count
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetCityName(targetCity, GameLanguage.English),
                        GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.English),
                        targetCity.Gold,
                        targetCity.Food,
                        targetCity.Troops,
                        targetCity.Defense,
                        targetCity.Loyalty,
                        targetCity.OfficerIds.Count
                    });
            }
            case SpyActionType.Sabotage:
            {
                var goldLoss = Math.Min(targetCity.Gold, 40 + _random.Next(20, 81));
                var foodLoss = Math.Min(targetCity.Food, 80 + _random.Next(40, 161));
                var defenseLoss = Math.Min(targetCity.Defense, 4 + _random.Next(2, 8));
                targetCity.Gold -= goldLoss;
                targetCity.Food -= foodLoss;
                targetCity.Defense = Math.Max(0, targetCity.Defense - defenseLoss);
                OfficerProgressionRules.AwardSpyExperience(officer, 22);
                return LocalizedResult(
                    true,
                    "cmd.spy.sabotage_success",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetCityName(targetCity, GameLanguage.TraditionalChinese),
                        goldLoss,
                        foodLoss,
                        defenseLoss
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetCityName(targetCity, GameLanguage.English),
                        goldLoss,
                        foodLoss,
                        defenseLoss
                    });
            }
            default:
            {
                var loyaltyLoss = Math.Min(targetCity.Loyalty, 5 + _random.Next(2, 7));
                targetCity.Loyalty = Math.Max(0, targetCity.Loyalty - loyaltyLoss);
                OfficerProgressionRules.AwardSpyExperience(officer, 20);
                return LocalizedResult(
                    true,
                    "cmd.spy.incite_success",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetCityName(targetCity, GameLanguage.TraditionalChinese),
                        loyaltyLoss
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetCityName(targetCity, GameLanguage.English),
                        loyaltyLoss
                    });
            }
        }
    }

    private static int CalculateSpySuccessChance(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        OfficerData officer,
        SpyActionType actionType)
    {
        var intelligence = GetEffectiveStat(world, officer, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var charm = GetEffectiveStat(world, officer, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var actingScore = (intelligence * 2 + charm) / 3;
        var targetIntelligence = GetAverageEffectiveStat(world, targetCity, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var targetCharm = GetAverageEffectiveStat(world, targetCity, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var defenseScore = (targetIntelligence * 2 + targetCharm) / 3;
        var sourceLoyaltyBonus = Math.Max(0, sourceCity.Loyalty - 60) / 8;
        var targetDefensePenalty = Math.Max(0, targetCity.Defense - 50) / 10;
        var baseChance = actionType switch
        {
            SpyActionType.Reconnaissance => 62,
            SpyActionType.Sabotage => 48,
            SpyActionType.Incite => 44,
            _ => 50
        };

        return Math.Clamp(baseChance + (actingScore - defenseScore) / 4 + sourceLoyaltyBonus - targetDefensePenalty, 10, 92);
    }

    private static int CalculateSpyExposureChance(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        OfficerData officer,
        SpyActionType actionType)
    {
        var intelligence = GetEffectiveStat(world, officer, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var targetIntelligence = GetAverageEffectiveStat(world, targetCity, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var defensePenalty = Math.Max(0, targetCity.Defense - 40) / 8;
        var sourceBonus = Math.Max(0, sourceCity.Defense - 40) / 20;
        var baseChance = actionType switch
        {
            SpyActionType.Reconnaissance => 18,
            SpyActionType.Sabotage => 32,
            SpyActionType.Incite => 28,
            _ => 25
        };

        return Math.Clamp(baseChance + (targetIntelligence - intelligence) / 5 + defensePenalty - sourceBonus, 5, 75);
    }

    private static void ApplySpyExposurePenalty(WorldState world, int actorFactionId, OfficerData officer, int targetFactionId, int relationPenalty, int loyaltyPenalty)
    {
        officer.Loyalty = Math.Max(0, officer.Loyalty - loyaltyPenalty);
        if (actorFactionId <= 0 || targetFactionId <= 0)
        {
            return;
        }

        var relation = GetOrCreateDiplomacyRelation(world, actorFactionId, targetFactionId);
        relation.RelationScore = Math.Clamp(relation.RelationScore - relationPenalty, -100, 100);
        relation.LastUpdatedYear = world.Year;
        relation.LastUpdatedMonth = world.Month;
    }
}
