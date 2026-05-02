using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
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

        var duration = request.DiplomacyActionType == DiplomacyActionType.Gift
            ? 1
            : Math.Clamp(request.DurationMonths, 1, 12);
        if (request.DiplomacyActionType != DiplomacyActionType.Gift && request.DurationMonths <= 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.invalid_duration");
        }

        var reservedGold = request.DiplomacyActionType == DiplomacyActionType.Gift
            ? Math.Max(0, request.GoldToSend)
            : 0;
        if (request.DiplomacyActionType == DiplomacyActionType.Gift && reservedGold <= 0)
        {
            return LocalizedResult(false, "cmd.diplomacy.gift_gold_required");
        }

        if (reservedGold > sourceCity.Gold)
        {
            return LocalizedResult(false, "cmd.diplomacy.not_enough_gold", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        MarkOfficerAssigned(world, officer, CommandType.Diplomacy);
        if (reservedGold > 0)
        {
            sourceCity.Gold -= reservedGold;
        }

        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetFactionId = targetFaction.Id,
            GoldToSend = reservedGold,
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
            if (pendingCommand.GoldToSend > 0)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
            }

            return LocalizedResult(false, "cmd.diplomacy.cancelled");
        }

        var officer = world.GetOfficer(pendingCommand.OfficerIds.FirstOrDefault());
        if (officer == null)
        {
            if (pendingCommand.GoldToSend > 0)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
            }

            return LocalizedResult(false, "cmd.diplomacy.cancelled");
        }

        var relation = GetOrCreateDiplomacyRelation(world, pendingCommand.ActorFactionId, targetFaction.Id);
        relation.LastUpdatedYear = world.Year;
        relation.LastUpdatedMonth = world.Month;

        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift)
        {
            var gain = Math.Clamp(pendingCommand.GoldToSend / 100, 3, 25);
            relation.RelationScore = Math.Clamp(relation.RelationScore + gain, -100, 100);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, 12);
            return LocalizedResult(
                true,
                "cmd.diplomacy.gift_resolved",
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetFactionName(targetFaction, GameLanguage.TraditionalChinese),
                    pendingCommand.GoldToSend,
                    gain
                },
                new object[]
                {
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetFactionName(targetFaction, GameLanguage.English),
                    pendingCommand.GoldToSend,
                    gain
                });
        }

        var chance = CalculateDiplomacySuccessChance(world, sourceCity, officer, targetFaction, relation, pendingCommand.DiplomacyActionType);
        var success = _random.Next(100) < chance;
        if (!success)
        {
            relation.RelationScore = Math.Clamp(relation.RelationScore - 6, -100, 100);
            OfficerProgressionRules.AwardDiplomacyExperience(officer, 6);
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
        OfficerProgressionRules.AwardDiplomacyExperience(officer, pendingCommand.DiplomacyActionType == DiplomacyActionType.Alliance ? 24 : 18);

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
        var cityLoyaltyBonus = Math.Max(0, sourceCity.Loyalty - 70) / 10;
        var baseChance = actionType == DiplomacyActionType.Alliance ? 42 : 58;
        return Math.Clamp(baseChance + (officerScore - targetScore) / 4 + relationBonus + cityLoyaltyBonus, 10, 90);
    }
}
