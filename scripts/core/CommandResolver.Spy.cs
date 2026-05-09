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

        if (request.SpyActionType == SpyActionType.Assassination && request.TargetOfficerId.HasValue)
        {
            var targetOfficer = world.GetOfficer(request.TargetOfficerId.Value);
            if (targetOfficer == null ||
                targetOfficer.CityId != targetCity.Id ||
                !targetCity.OfficerIds.Contains(targetOfficer.Id) ||
                !IsOfficerAlive(world, targetOfficer))
            {
                return LocalizedResult(false, "cmd.spy.assassination_invalid_target");
            }
        }

        var officer = GetSingleAvailableOfficer(world, sourceCity, request.OfficerIds);
        if (officer == null)
        {
            return LocalizedResult(false, "cmd.spy.officer_required", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        MarkOfficerAssigned(world, officer, CommandType.Spy);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Spy,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TargetFactionId = targetCity.OwnerFactionId,
            TargetOfficerId = request.SpyActionType == SpyActionType.Assassination
                ? request.TargetOfficerId ?? 0
                : 0,
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
            var result = ResolveSuccessfulSpyAction(world, pendingCommand.ActorFactionId, targetCity, officer, pendingCommand.SpyActionType, pendingCommand.TargetOfficerId);
            if (exposed)
            {
                ApplySpyExposurePenalty(world, pendingCommand.ActorFactionId, officer, targetCity.OwnerFactionId, 6, 3);
                result.MessageZhHant = $"{result.MessageZhHant} {_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.spy.exposed_suffix") ?? string.Empty}".Trim();
                result.MessageEn = $"{result.MessageEn} {_localization?.TForLanguage(GameLanguage.English, "cmd.spy.exposed_suffix") ?? string.Empty}".Trim();
                result.Message = result.MessageEn;
            }

            return result;
        }

        OfficerProgressionRules.AwardSpyExperience(officer, GetSpyExperienceReward(pendingCommand.SpyActionType, success: false));
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

    private CommandResult ResolveSuccessfulSpyAction(WorldState world, int actorFactionId, CityData targetCity, OfficerData officer, SpyActionType actionType)
    {
        return ResolveSuccessfulSpyAction(world, actorFactionId, targetCity, officer, actionType, 0);
    }

    private CommandResult ResolveSuccessfulSpyAction(WorldState world, int actorFactionId, CityData targetCity, OfficerData officer, SpyActionType actionType, int targetOfficerId)
    {
        switch (actionType)
        {
            case SpyActionType.Reconnaissance:
            {
                // The turn advances immediately after month-end resolution, so store one extra month here.
                world.UpsertCityIntel(actorFactionId, targetCity.Id, 4);
                OfficerProgressionRules.AwardSpyExperience(officer, GetSpyExperienceReward(actionType, success: true));
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
                OfficerProgressionRules.AwardSpyExperience(officer, GetSpyExperienceReward(actionType, success: true));
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
            case SpyActionType.Assassination:
            {
                var targetOfficer = SelectAssassinationTarget(world, targetCity, targetOfficerId);
                if (targetOfficer == null)
                {
                    OfficerProgressionRules.AwardSpyExperience(officer, 10);
                    return LocalizedResult(
                        false,
                        "cmd.spy.assassination_no_target",
                        new object[]
                        {
                            GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                            GetCityName(targetCity, GameLanguage.TraditionalChinese)
                        },
                        new object[]
                        {
                            GetOfficerDisplayName(officer, GameLanguage.English),
                            GetCityName(targetCity, GameLanguage.English)
                        });
                }
                var targetFactionId = targetCity.OwnerFactionId;
                var wasRuler = IsFactionRuler(world, targetOfficer.Id);
                var targetFactionBeforeDeath = world.GetFaction(targetFactionId);
                var targetFactionNameZh = targetFactionBeforeDeath != null
                    ? GetFactionName(targetFactionBeforeDeath, GameLanguage.TraditionalChinese)
                    : targetFactionId.ToString();
                var targetFactionNameEn = targetFactionBeforeDeath != null
                    ? GetFactionName(targetFactionBeforeDeath, GameLanguage.English)
                    : targetFactionId.ToString();
                EliminateOfficer(world, targetOfficer);
                if (wasRuler && targetFactionId > 0)
                {
                    ResolveRulerDeath(world, targetFactionId);
                }

                var targetFaction = world.GetFaction(targetFactionId);
                OfficerProgressionRules.AwardSpyExperience(officer, GetSpyExperienceReward(actionType, success: true));
                if (wasRuler && targetFactionId > 0)
                {
                    if (targetFaction == null || !IsFactionAlive(world, targetFactionId))
                    {
                        return LocalizedResult(
                            true,
                            "cmd.spy.assassination_success_faction_destroyed",
                            new object[]
                            {
                                GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                                GetOfficerDisplayName(targetOfficer, GameLanguage.TraditionalChinese),
                                targetFactionNameZh
                            },
                            new object[]
                            {
                                GetOfficerDisplayName(officer, GameLanguage.English),
                                GetOfficerDisplayName(targetOfficer, GameLanguage.English),
                                targetFactionNameEn
                            });
                    }

                    if (!targetFaction.IsPlayer && targetFaction.RulerOfficerId > 0)
                    {
                        var successor = world.GetOfficer(targetFaction.RulerOfficerId);
                        if (successor != null)
                        {
                            return LocalizedResult(
                                true,
                                "cmd.spy.assassination_success_ruler_succeeded",
                                new object[]
                                {
                                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                                    GetOfficerDisplayName(targetOfficer, GameLanguage.TraditionalChinese),
                                    GetOfficerDisplayName(successor, GameLanguage.TraditionalChinese),
                                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese)
                                },
                                new object[]
                                {
                                    GetOfficerDisplayName(officer, GameLanguage.English),
                                    GetOfficerDisplayName(targetOfficer, GameLanguage.English),
                                    GetOfficerDisplayName(successor, GameLanguage.English),
                                    GetFactionName(targetFaction, GameLanguage.English)
                                });
                        }
                    }
                }

                return LocalizedResult(
                    true,
                    "cmd.spy.assassination_success",
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                        GetOfficerDisplayName(targetOfficer, GameLanguage.TraditionalChinese),
                        GetCityName(targetCity, GameLanguage.TraditionalChinese)
                    },
                    new object[]
                    {
                        GetOfficerDisplayName(officer, GameLanguage.English),
                        GetOfficerDisplayName(targetOfficer, GameLanguage.English),
                        GetCityName(targetCity, GameLanguage.English)
                    });
            }
            default:
            {
                var loyaltyLoss = Math.Min(targetCity.Loyalty, 5 + _random.Next(2, 7));
                targetCity.Loyalty = Math.Max(0, targetCity.Loyalty - loyaltyLoss);
                OfficerProgressionRules.AwardSpyExperience(officer, GetSpyExperienceReward(actionType, success: true));
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
        var rankBonus = OfficerProgressionRules.GetSpySuccessBonus(officer);
        var baseChance = actionType switch
        {
            SpyActionType.Reconnaissance => 62,
            SpyActionType.Sabotage => 48,
            SpyActionType.Incite => 44,
            SpyActionType.Assassination => 28,
            _ => 50
        };

        return Math.Clamp(baseChance + (actingScore - defenseScore) / 4 + sourceLoyaltyBonus + rankBonus - targetDefensePenalty, 10, 92);
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
            SpyActionType.Assassination => 42,
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

    private static int GetSpyExperienceReward(SpyActionType actionType, bool success)
    {
        if (!success)
        {
            return 6;
        }

        return actionType switch
        {
            SpyActionType.Reconnaissance => 18,
            SpyActionType.Sabotage => 22,
            SpyActionType.Incite => 20,
            SpyActionType.Assassination => 28,
            _ => 18
        };
    }

    private static OfficerData? SelectAssassinationTarget(WorldState world, CityData targetCity, int targetOfficerId = 0)
    {
        if (targetOfficerId > 0)
        {
            var designatedOfficer = world.GetOfficer(targetOfficerId);
            if (designatedOfficer != null &&
                designatedOfficer.CityId == targetCity.Id &&
                targetCity.OfficerIds.Contains(designatedOfficer.Id) &&
                IsOfficerAlive(world, designatedOfficer))
            {
                return designatedOfficer;
            }

            return null;
        }

        return targetCity.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => IsOfficerAlive(world, officer))
            .Cast<OfficerData>()
            .OrderByDescending(officer => IsFactionRuler(world, officer.Id))
            .ThenBy(officer => officer.Loyalty)
            .ThenByDescending(officer => officer.Combat + officer.Leadership + officer.Intelligence + officer.Politics + officer.Charm)
            .FirstOrDefault();
    }
}
