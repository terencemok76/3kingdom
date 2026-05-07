using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class AiController
{
    private const int BlindAttackTroopThreshold = 3000;
    private const int BlindSpyTroopThreshold = 1800;
    private const int DiplomacyThreatTroopGap = 500;
    private const int DiplomacyGiftGoldThreshold = 500;
    private const int DiplomacyGiftAmount = 200;
    private const int DiplomacyAllianceRelationThreshold = 30;
    private const int SpySabotageDefenseThreshold = 65;
    private const int SpySabotageGoldThreshold = 260;
    private const int SpySabotageFoodThreshold = 700;
    private const int SpyInciteLoyaltyThreshold = 78;

    private CommandResolver? _commandResolver;
    private TurnManager? _turnManager;
    private LocalizationService? _localization;

    public void Initialize(CommandResolver commandResolver, TurnManager turnManager, LocalizationService localization)
    {
        _commandResolver = commandResolver;
        _turnManager = turnManager;
        _localization = localization;
    }

    public CommandResult RunSingleCityDecision(int factionId, int cityId)
    {
        if (_commandResolver == null || _turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.ai_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.ai_city_not_found");
        }

        var availableOfficerIds = GetAvailableOfficerIds(world, city);

        CommandResult? militaryResult = null;
        foreach (var targetId in city.ConnectedCityIds)
        {
            var target = world.GetCity(targetId);
            if (target == null)
            {
                continue;
            }

            if (target.OwnerFactionId == factionId)
            {
                continue;
            }

            if (availableOfficerIds.Count == 0)
            {
                continue;
            }

            var canInspectTarget = world.CanFactionViewCity(factionId, target.Id);
            var shouldAttack = canInspectTarget
                ? city.Troops > target.Troops + 300
                : city.Troops >= BlindAttackTroopThreshold;
            if (shouldAttack)
            {
                militaryResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = city.Troops / 2,
                    OfficerIds = new System.Collections.Generic.List<int>(availableOfficerIds)
                });
                break;
            }
        }

        if (militaryResult == null)
        {
            foreach (var targetId in city.ConnectedCityIds)
            {
                var target = world.GetCity(targetId);
                if (target == null || target.OwnerFactionId != factionId)
                {
                    continue;
                }

                if (city.Troops > target.Troops + 800)
                {
                    militaryResult = _commandResolver.Execute(new CommandRequest
                    {
                        Type = CommandType.Move,
                        ActorFactionId = factionId,
                        SourceCityId = cityId,
                        TargetCityId = targetId,
                        TroopsToSend = city.Troops / 2,
                        GoldToSend = city.Gold / 3,
                        FoodToSend = city.Food / 3,
                        HorsesToSend = city.Horses / 3
                    });
                    break;
                }
            }
        }

        CommandResult? diplomacyResult = null;
        if (militaryResult == null)
        {
            diplomacyResult = TryIssueDiplomacyCommand(world, city, factionId, availableOfficerIds, defensiveOnly: true);
        }

        CommandResult? spyResult = null;
        if (militaryResult == null)
        {
            spyResult = TryIssueSpyCommand(world, city, factionId, availableOfficerIds);
        }

        if (militaryResult == null && spyResult == null)
        {
            diplomacyResult ??= TryIssueDiplomacyCommand(world, city, factionId, availableOfficerIds, defensiveOnly: false);
        }

        var coreResults = new System.Collections.Generic.List<CommandResult>();
        var recruitOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Charm + officer.Leadership);
        var internalAffairsOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Politics + officer.Charm);
        var searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
        if (city.Troops < 2200 &&
            city.Gold >= 120 &&
            city.Food >= 80 &&
            recruitOfficerId > 0 &&
            !(city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                OfficerIds = new System.Collections.Generic.List<int> { recruitOfficerId }
            }));
            availableOfficerIds.Remove(recruitOfficerId);
            if (internalAffairsOfficerId == recruitOfficerId)
            {
                internalAffairsOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Politics + officer.Charm);
            }

            if (searchOfficerId == recruitOfficerId)
            {
                searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
            }
        }

        var internalAffairsJob = ChooseInternalAffairsJob(world, city);
        if (internalAffairsJob.HasValue && internalAffairsOfficerId > 0)
        {
            coreResults.Add(_commandResolver.ScheduleInternalAffairs(
                factionId,
                cityId,
                internalAffairsOfficerId,
                internalAffairsJob.Value,
                3));
            availableOfficerIds.Remove(internalAffairsOfficerId);
            if (searchOfficerId == internalAffairsOfficerId)
            {
                searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
            }
        }

        if (searchOfficerId > 0 &&
            !(city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Search,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                OfficerIds = new System.Collections.Generic.List<int> { searchOfficerId }
            }));
        }

        if (spyResult != null)
        {
            coreResults.Insert(0, spyResult);
        }

        if (diplomacyResult != null)
        {
            coreResults.Insert(0, diplomacyResult);
        }

        if (coreResults.Count == 0)
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Pass,
                ActorFactionId = factionId,
                SourceCityId = cityId
            }));
        }

        if (militaryResult == null)
        {
            return CombineResults(coreResults);
        }

        var messages = new System.Collections.Generic.List<string>();
        var messagesZh = new System.Collections.Generic.List<string>();
        var messagesEn = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(militaryResult.Message))
        {
            messages.Add(militaryResult.Message);
            if (!string.IsNullOrWhiteSpace(militaryResult.MessageZhHant))
            {
                messagesZh.Add(militaryResult.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(militaryResult.MessageEn))
            {
                messagesEn.Add(militaryResult.MessageEn);
            }
        }

        foreach (var coreResult in coreResults)
        {
            if (string.IsNullOrWhiteSpace(coreResult.Message) ||
                coreResult.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            messages.Add(coreResult.Message);
            if (!string.IsNullOrWhiteSpace(coreResult.MessageZhHant))
            {
                messagesZh.Add(coreResult.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(coreResult.MessageEn))
            {
                messagesEn.Add(coreResult.MessageEn);
            }
        }

        var anyCoreSuccess = false;
        foreach (var result in coreResults)
        {
            anyCoreSuccess |= result.Success;
        }

        return new CommandResult
        {
            Success = militaryResult.Success || anyCoreSuccess,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass"),
            MessageZhHant = messagesZh.Count > 0 ? string.Join(" | ", messagesZh) : (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass") ?? "Pass"),
            MessageEn = messagesEn.Count > 0 ? string.Join(" | ", messagesEn) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass")
        };
    }

    private CommandResult CombineResults(System.Collections.Generic.List<CommandResult> results)
    {
        if (results.Count == 0)
        {
            return LocalizedResult(true, "cmd.pass");
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        var messages = new System.Collections.Generic.List<string>();
        var messagesZh = new System.Collections.Generic.List<string>();
        var messagesEn = new System.Collections.Generic.List<string>();
        var anySuccess = false;

        foreach (var result in results)
        {
            anySuccess |= result.Success;
            if (!string.IsNullOrWhiteSpace(result.Message) &&
                !result.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(result.Message);
            }

            if (!string.IsNullOrWhiteSpace(result.MessageZhHant) &&
                !result.MessageZhHant.Equals(_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass"), System.StringComparison.OrdinalIgnoreCase))
            {
                messagesZh.Add(result.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(result.MessageEn) &&
                !result.MessageEn.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                messagesEn.Add(result.MessageEn);
            }
        }

        return new CommandResult
        {
            Success = anySuccess,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass"),
            MessageZhHant = messagesZh.Count > 0 ? string.Join(" | ", messagesZh) : (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass") ?? "Pass"),
            MessageEn = messagesEn.Count > 0 ? string.Join(" | ", messagesEn) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass")
        };
    }

    private CommandResult LocalizedResult(bool success, string key)
    {
        var zh = _localization?.TForLanguage(GameLanguage.TraditionalChinese, key) ?? key;
        var en = _localization?.TForLanguage(GameLanguage.English, key) ?? key;
        return new CommandResult
        {
            Success = success,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en
        };
    }

    private static System.Collections.Generic.List<int> GetAvailableOfficerIds(WorldState world, CityData city)
    {
        var result = new System.Collections.Generic.List<int>();
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month)
            {
                continue;
            }

            if (world.InternalAffairsSchedules.Any(schedule =>
                    schedule.State == InternalAffairsScheduleState.Active &&
                    schedule.OfficerId == officerId))
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    private static InternalAffairsJobType? ChooseInternalAffairsJob(WorldState world, CityData city)
    {
        var activeJobs = new System.Collections.Generic.HashSet<InternalAffairsJobType>(
            world.InternalAffairsSchedules
                .Where(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.CityId == city.Id)
                .Select(schedule => schedule.JobType));

        var candidates = new (InternalAffairsJobType JobType, int Score)[]
        {
            (InternalAffairsJobType.Farm, city.Farm),
            (InternalAffairsJobType.Commercial, city.Commercial),
            (InternalAffairsJobType.Defend, city.Defense),
            (InternalAffairsJobType.WaterControl, city.DisasterPrevention),
            (InternalAffairsJobType.Construction, city.Commercial + city.Defense)
        };

        return candidates
            .Where(candidate => !activeJobs.Contains(candidate.JobType))
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => (int)candidate.JobType)
            .Select(candidate => (InternalAffairsJobType?)candidate.JobType)
            .FirstOrDefault();
    }

    private static int GetBestOfficerId(
        WorldState world,
        CityData city,
        System.Collections.Generic.List<int> availableOfficerIds,
        System.Func<OfficerData, int> scoreSelector)
    {
        var bestOfficerId = -1;
        var bestScore = int.MinValue;

        foreach (var officerId in availableOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null || !city.OfficerIds.Contains(officerId))
            {
                continue;
            }

            var score = scoreSelector(officer);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestOfficerId = officerId;
        }

        return bestOfficerId;
    }

    private CommandResult? TryIssueDiplomacyCommand(
        WorldState world,
        CityData city,
        int factionId,
        System.Collections.Generic.List<int> availableOfficerIds,
        bool defensiveOnly)
    {
        if (_commandResolver == null || availableOfficerIds.Count == 0)
        {
            return null;
        }

        var diplomacyOfficerId = GetBestDiplomacyOfficerId(world, city, factionId, availableOfficerIds);
        if (diplomacyOfficerId <= 0)
        {
            return null;
        }

        var threatenedNeighbor = city.ConnectedCityIds
            .Select(world.GetCity)
            .Where(target => target != null && target.OwnerFactionId != factionId)
            .Cast<CityData>()
            .Where(target => !HasActiveDiplomacyBlock(world, factionId, target.OwnerFactionId))
            .OrderByDescending(target => target.Troops - city.Troops)
            .FirstOrDefault(target => target.Troops >= city.Troops + DiplomacyThreatTroopGap);
        if (threatenedNeighbor != null)
        {
            var truceResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Diplomacy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetFactionId = threatenedNeighbor.OwnerFactionId,
                DiplomacyActionType = DiplomacyActionType.Truce,
                DurationMonths = 3,
                OfficerIds = new System.Collections.Generic.List<int> { diplomacyOfficerId }
            });

            if (truceResult.Success)
            {
                availableOfficerIds.Remove(diplomacyOfficerId);
            }

            return truceResult;
        }

        if (defensiveOnly)
        {
            return null;
        }

        var allianceTargetFactionId = world.DiplomacyRelations
            .Where(relation =>
                relation.RelationScore >= DiplomacyAllianceRelationThreshold &&
                relation.RemainingMonths <= 0 &&
                relation.Status == DiplomacyStatusType.Neutral &&
                (relation.FactionAId == factionId || relation.FactionBId == factionId))
            .Select(relation => relation.FactionAId == factionId ? relation.FactionBId : relation.FactionAId)
            .FirstOrDefault(targetFactionId =>
                targetFactionId > 0 &&
                targetFactionId != factionId &&
                !HasActiveDiplomacyBlock(world, factionId, targetFactionId) &&
                world.GetFaction(targetFactionId) != null &&
                world.Cities.Any(targetCity => targetCity.OwnerFactionId == targetFactionId));
        if (allianceTargetFactionId > 0)
        {
            var allianceResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Diplomacy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetFactionId = allianceTargetFactionId,
                DiplomacyActionType = DiplomacyActionType.Alliance,
                DurationMonths = 4,
                OfficerIds = new System.Collections.Generic.List<int> { diplomacyOfficerId }
            });

            if (allianceResult.Success)
            {
                availableOfficerIds.Remove(diplomacyOfficerId);
            }

            return allianceResult;
        }

        if (city.Gold < DiplomacyGiftGoldThreshold)
        {
            return null;
        }

        var giftTargetFactionId = world.Factions
            .Where(faction => faction.Id != factionId && world.Cities.Any(targetCity => targetCity.OwnerFactionId == faction.Id))
            .Select(faction => new
            {
                FactionId = faction.Id,
                Relation = world.GetDiplomacyRelation(factionId, faction.Id)
            })
            .Where(item => item.Relation == null || item.Relation.RemainingMonths <= 0)
            .OrderBy(item => item.Relation?.RelationScore ?? 0)
            .ThenBy(item => item.FactionId)
            .FirstOrDefault();
        if (giftTargetFactionId == null)
        {
            return null;
        }

        var giftResult = _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = factionId,
            SourceCityId = city.Id,
            TargetFactionId = giftTargetFactionId.FactionId,
            DiplomacyActionType = DiplomacyActionType.Gift,
            GoldToSend = System.Math.Min(DiplomacyGiftAmount, city.Gold),
            OfficerIds = new System.Collections.Generic.List<int> { diplomacyOfficerId }
        });

        if (giftResult.Success)
        {
            availableOfficerIds.Remove(diplomacyOfficerId);
        }

        return giftResult;
    }

    private CommandResult? TryIssueSpyCommand(
        WorldState world,
        CityData city,
        int factionId,
        System.Collections.Generic.List<int> availableOfficerIds)
    {
        if (_commandResolver == null || availableOfficerIds.Count == 0)
        {
            return null;
        }

        var spyOfficerId = GetBestSpyOfficerId(world, city, availableOfficerIds);
        if (spyOfficerId <= 0)
        {
            return null;
        }

        var spyOfficer = world.GetOfficer(spyOfficerId);
        if (spyOfficer == null)
        {
            return null;
        }

        var hiddenTarget = city.ConnectedCityIds
            .Select(world.GetCity)
            .FirstOrDefault(target =>
                target != null &&
                target.OwnerFactionId != factionId &&
                !world.CanFactionViewCity(factionId, target.Id));
        if (hiddenTarget != null && city.Troops >= BlindSpyTroopThreshold)
        {
            var reconResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Spy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetCityId = hiddenTarget.Id,
                SpyActionType = SpyActionType.Reconnaissance,
                OfficerIds = new System.Collections.Generic.List<int> { spyOfficerId }
            });

            if (reconResult.Success)
            {
                availableOfficerIds.Remove(spyOfficerId);
            }

            return reconResult;
        }

        var visibleTargets = city.ConnectedCityIds
            .Select(world.GetCity)
            .Where(target =>
                target != null &&
                target.OwnerFactionId != factionId &&
                world.CanFactionViewCity(factionId, target.Id))
            .Cast<CityData>()
            .ToList();
        if (visibleTargets.Count == 0)
        {
            return null;
        }

        var sabotageTarget = visibleTargets
            .Where(target =>
                target.Defense >= SpySabotageDefenseThreshold ||
                target.Gold >= SpySabotageGoldThreshold ||
                target.Food >= SpySabotageFoodThreshold)
            .OrderByDescending(target => target.Defense + (target.Gold / 15) + (target.Food / 60))
            .FirstOrDefault();
        if (sabotageTarget != null)
        {
            var sabotageResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Spy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetCityId = sabotageTarget.Id,
                SpyActionType = SpyActionType.Sabotage,
                OfficerIds = new System.Collections.Generic.List<int> { spyOfficerId }
            });

            if (sabotageResult.Success)
            {
                availableOfficerIds.Remove(spyOfficerId);
            }

            return sabotageResult;
        }

        var inciteTarget = visibleTargets
            .Where(target => target.Loyalty >= SpyInciteLoyaltyThreshold)
            .OrderByDescending(target => target.Loyalty)
            .ThenByDescending(target => target.OfficerIds.Count)
            .FirstOrDefault();
        if (inciteTarget == null)
        {
            var expiringIntelTarget = visibleTargets
                .OrderBy(recordTarget => world.GetCityIntel(factionId, recordTarget.Id)?.RemainingMonths ?? 0)
                .FirstOrDefault();
            if (expiringIntelTarget == null)
            {
                return null;
            }

            var refreshResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Spy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetCityId = expiringIntelTarget.Id,
                SpyActionType = SpyActionType.Reconnaissance,
                OfficerIds = new System.Collections.Generic.List<int> { spyOfficerId }
            });

            if (refreshResult.Success)
            {
                availableOfficerIds.Remove(spyOfficerId);
            }

            return refreshResult;
        }

        var inciteResult = _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = factionId,
            SourceCityId = city.Id,
            TargetCityId = inciteTarget.Id,
            SpyActionType = SpyActionType.Incite,
            OfficerIds = new System.Collections.Generic.List<int> { spyOfficerId }
        });

        if (inciteResult.Success)
        {
            availableOfficerIds.Remove(spyOfficerId);
        }

        return inciteResult;
    }

    private static int GetBestSpyOfficerId(
        WorldState world,
        CityData city,
        System.Collections.Generic.List<int> availableOfficerIds)
    {
        return GetBestOfficerId(
            world,
            city,
            availableOfficerIds,
            officer => (officer.Intelligence * 2) + officer.Charm + officer.Politics + (officer.SpyRank * 20));
    }

    private static int GetBestDiplomacyOfficerId(
        WorldState world,
        CityData city,
        int factionId,
        System.Collections.Generic.List<int> availableOfficerIds)
    {
        var rulerOfficerId = world.GetFaction(factionId)?.RulerOfficerId ?? -1;
        var candidateIds = availableOfficerIds
            .Where(officerId => officerId != rulerOfficerId)
            .ToList();
        if (candidateIds.Count == 0)
        {
            return -1;
        }

        return GetBestOfficerId(
            world,
            city,
            candidateIds,
            officer => (officer.Charm * 2) + officer.Politics + officer.Intelligence + (officer.DiplomacyRank * 20));
    }

    private static bool HasActiveDiplomacyBlock(WorldState world, int factionAId, int factionBId)
    {
        var relation = world.GetDiplomacyRelation(factionAId, factionBId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }
}
