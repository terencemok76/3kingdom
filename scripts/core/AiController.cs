using System;
using System.Linq;
using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class AiController
{
    private const int BlindAttackTroopThreshold = 3000;
    private const int BaseAttackTroopAdvantageThreshold = 300;
    private const int MinimumAttackTroopAdvantageThreshold = 50;
    private const int MaximumAttackTroopAdvantageThreshold = 450;
    private const int MinimumBlindAttackTroopThreshold = 2600;
    private const int MaximumBlindAttackTroopThreshold = 3400;
    private const int BlindSpyTroopThreshold = 1800;
    private const int DiplomacyThreatTroopGap = 500;
    private const int DiplomacyGiftGoldThreshold = 500;
    private const int DiplomacyGiftAmount = 200;
    private const int DiplomacyAllianceRelationThreshold = 30;
    private const int DiplomacyDemandGoldThreshold = 300;
    private const int DiplomacyDemandAmount = 200;
    private const int DiplomacyDemandTroopAdvantage = 1000;
    private const int DiplomacyDemandMaxRelationScore = 15;
    private const int DiplomacyBreakPactTroopAdvantage = 700;
    private const int DiplomacyBreakPactMaxRelationScore = 10;
    private const int SpySabotageDefenseThreshold = 65;
    private const int SpySabotageGoldThreshold = 260;
    private const int SpySabotageFoodThreshold = 700;
    private const int SpyAssassinationDefenseThreshold = 58;
    private const int SpyAssassinationTargetValueThreshold = 320;
    private const int SpyInciteLoyaltyThreshold = 78;
    private const int AiReinforcementMinimumTroops = 500;
    private const int AiReinforcementFoodDays = 2;
    private const int AiRulerChangeMinimumAge = 65;
    private const int AiRulerChangeMinimumCandidateLoyalty = 70;
    private const int AiRulerChangeMinimumScoreMargin = 30;
    private const int AiRulerChangeCooldownMonths = 6;

    private CommandResolver? _commandResolver;
    private TurnManager? _turnManager;
    private LocalizationService? _localization;

    public string LastDecisionDebugDetail { get; private set; } = string.Empty;
    public int LastDiplomacyDecisionTargetFactionId { get; private set; } = -1;
    public string LastAttackDecisionDetail { get; private set; } = string.Empty;
    public int LastAttackDecisionTargetFactionId { get; private set; } = -1;

    public void Initialize(CommandResolver commandResolver, TurnManager turnManager, LocalizationService localization)
    {
        _commandResolver = commandResolver;
        _turnManager = turnManager;
        _localization = localization;
    }

    public List<CommandResult> RunFactionAppointmentDecisions(int factionId)
    {
        var results = new List<CommandResult>();
        if (_commandResolver == null || _turnManager?.World == null)
        {
            return results;
        }

        var world = _turnManager.World;
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return results;
        }

        var rulerChangeResult = TryResolveAiRulerChange(world, faction);
        if (rulerChangeResult != null)
        {
            results.Add(rulerChangeResult);
        }

        var reservedOfficerIds = new HashSet<int>();
        if (faction.ChancellorOfficerId > 0)
        {
            reservedOfficerIds.Add(faction.ChancellorOfficerId);
        }

        if (faction.ChiefStrategistOfficerId > 0)
        {
            reservedOfficerIds.Add(faction.ChiefStrategistOfficerId);
        }

        if (faction.ChancellorOfficerId <= 0)
        {
            var chancellorCandidate = GetBestFactionAdvisorCandidate(
                world,
                faction,
                reservedOfficerIds,
                officer => officer.Politics * 3 + officer.Intelligence * 2 + officer.Charm);
            var chancellorResult = TryAssignFactionAdvisor(factionId, chancellorCandidate, "Chancellor");
            if (chancellorResult != null)
            {
                results.Add(chancellorResult);
                if (chancellorResult.Success && chancellorCandidate != null)
                {
                    reservedOfficerIds.Add(chancellorCandidate.Id);
                }
            }
        }

        if (faction.ChiefStrategistOfficerId <= 0)
        {
            var strategistCandidate = GetBestFactionAdvisorCandidate(
                world,
                faction,
                reservedOfficerIds,
                officer => officer.Intelligence * 3 + officer.Leadership * 2 + officer.Charm);
            var strategistResult = TryAssignFactionAdvisor(factionId, strategistCandidate, "ChiefStrategist");
            if (strategistResult != null)
            {
                results.Add(strategistResult);
            }
        }

        foreach (var city in world.Cities.Where(city => city.OwnerFactionId == factionId))
        {
            if (HasCityPrefect(world, city))
            {
                continue;
            }

            var prefectCandidate = GetBestCityPrefectCandidate(world, faction, city, reservedOfficerIds);
            var prefectResult = TryAssignCityPrefect(factionId, city, prefectCandidate);
            if (prefectResult == null)
            {
                continue;
            }

            results.Add(prefectResult);
        }

        return results;
    }

    private CommandResult? TryResolveAiRulerChange(WorldState world, FactionData faction)
    {
        if (_commandResolver == null || faction.IsPlayer || faction.RulerOfficerId <= 0 ||
            world.GetPendingSuccession(faction.Id) != null ||
            HasActiveCampaign(world, faction.Id) ||
            world.PendingCommands.Any(command =>
                command.ActorFactionId == faction.Id && command.Type == CommandType.Diplomacy) ||
            !HasRulerChangeCooldownElapsed(world, faction))
        {
            return null;
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        if (ruler == null || ruler.CaptiveFactionId > 0 || BattleCampaignService.IsOfficerCommitted(world, ruler.Id))
        {
            return null;
        }

        var rulerAge = ruler.BirthYear > 0 ? world.Year - ruler.BirthYear : 0;
        var isNearKnownDeath = ruler.DeathYear > 0 && ruler.DeathYear <= world.Year + 2;
        if (rulerAge < AiRulerChangeMinimumAge && !isNearKnownDeath)
        {
            return null;
        }

        var candidate = faction.OfficerIds
            .Where(officerId => officerId != ruler.Id)
            .Select(world.GetOfficer)
            .Where(officer => officer != null &&
                              officer.CityId > 0 &&
                              officer.CaptiveFactionId <= 0 &&
                              (officer.DeathYear <= 0 || world.Year <= officer.DeathYear) &&
                              officer.Loyalty >= AiRulerChangeMinimumCandidateLoyalty &&
                              !BattleCampaignService.IsOfficerCommitted(world, officer.Id))
            .Cast<OfficerData>()
            .OrderByDescending(officer => ScoreAiRulerCandidate(ruler, officer))
            .ThenByDescending(officer => officer.Loyalty)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
        if (candidate == null)
        {
            return null;
        }

        var scoreMargin = ScoreAiRulerCandidate(ruler, candidate) - ScoreAiRulerCandidate(ruler, ruler);
        var currentRulerIsWeak = ruler.Loyalty < 55;
        if (!currentRulerIsWeak && scoreMargin < AiRulerChangeMinimumScoreMargin)
        {
            return null;
        }

        return _commandResolver.ResolveAiRulerChange(faction.Id, candidate.Id);
    }

    private static bool HasActiveCampaign(WorldState world, int factionId)
    {
        return world.ActiveBattleCampaigns.Any(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            (campaign.AttackerFactionId == factionId || campaign.DefenderFactionId == factionId));
    }

    private static bool HasRulerChangeCooldownElapsed(WorldState world, FactionData faction)
    {
        if (faction.LastRulerChangeYear <= 0 || faction.LastRulerChangeMonth <= 0)
        {
            return true;
        }

        var monthsSinceLastChange = (world.Year - faction.LastRulerChangeYear) * 12 +
                                    world.Month - faction.LastRulerChangeMonth;
        return monthsSinceLastChange >= AiRulerChangeCooldownMonths;
    }

    private static int ScoreAiRulerCandidate(OfficerData currentRuler, OfficerData candidate)
    {
        var relationshipBonus = OfficerRelationshipRules.GetSuccessionRelationshipPriority(currentRuler, candidate) switch
        {
            2 => 35,
            1 => 15,
            _ => 0
        };
        return candidate.Leadership * 3 + candidate.Intelligence * 2 + candidate.Politics * 2 +
               candidate.Charm + candidate.Loyalty + candidate.Ambition / 2 + relationshipBonus;
    }

    public CommandResult RunSingleCityDecision(int factionId, int cityId)
    {
        LastDecisionDebugDetail = string.Empty;
        LastDiplomacyDecisionTargetFactionId = -1;
        LastAttackDecisionDetail = string.Empty;
        LastAttackDecisionTargetFactionId = -1;

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
        militaryResult = TryDispatchCampaignReinforcement(world, city, factionId, availableOfficerIds);
        foreach (var targetId in city.ConnectedCityIds)
        {
            if (militaryResult != null)
            {
                break;
            }
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
            var attackTroopAdvantageThreshold = GetAttackTroopAdvantageThreshold(world, factionId);
            var blindAttackTroopThreshold = GetBlindAttackTroopThreshold(world, factionId);
            var shouldAttack = canInspectTarget
                ? city.Troops > target.Troops + attackTroopAdvantageThreshold
                : city.Troops >= blindAttackTroopThreshold;
            if (shouldAttack)
            {
                var deployments = CreateAiAttackDeployments(world, city, availableOfficerIds, city.Troops / 2);
                if (deployments.Count == 0)
                {
                    continue;
                }
                SetAttackDecisionDebug(
                    world,
                    city,
                    factionId,
                    target,
                    canInspectTarget,
                    deployments.Sum(item => item.TroopCount),
                    attackTroopAdvantageThreshold,
                    blindAttackTroopThreshold);
                militaryResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = deployments.Sum(item => item.TroopCount),
                    AttackOfficerDeployments = deployments,
                    OfficerIds = deployments.Select(item => item.OfficerId).ToList()
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
            recruitOfficerId > 0 &&
            !(city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month))
        {
            var recruitTroopType = TroopType.Infantry;
            var recruitCount = Math.Min(200, RecruitRules.GetMaxRecruitableCount(city, recruitTroopType));
            if (recruitCount > 0)
            {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                TroopsToSend = recruitCount,
                RecruitTroopType = recruitTroopType,
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
        }

        var internalAffairsJob = ChooseInternalAffairsJob(world, city);
        if (internalAffairsJob.HasValue && internalAffairsOfficerId > 0)
        {
            var constructionProjectType = internalAffairsJob.Value == InternalAffairsJobType.Construction
                ? AiConstructionRules.ChooseConstructionProjectType(world, city)
                : ConstructionProjectType.None;
            coreResults.Add(_commandResolver.ScheduleInternalAffairs(
                factionId,
                cityId,
                internalAffairsOfficerId,
                internalAffairsJob.Value,
                3,
                constructionProjectType));
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

    private CommandResult? TryDispatchCampaignReinforcement(
        WorldState world,
        CityData sourceCity,
        int factionId,
        IReadOnlyList<int> availableOfficerIds)
    {
        if (_commandResolver == null || availableOfficerIds.Count == 0 || sourceCity.Troops - BattleCampaignService.MinimumCityGarrison < AiReinforcementMinimumTroops)
        {
            return null;
        }

        var candidates = world.ActiveBattleCampaigns
            .Where(campaign => campaign.Stage != CampaignStage.Resolved)
            .Select(campaign => new
            {
                Campaign = campaign,
                Side = campaign.AttackerFactionId == factionId
                    ? CampaignBattleSide.Attacker
                    : campaign.DefenderFactionId == factionId
                        ? CampaignBattleSide.Defender
                        : (CampaignBattleSide?)null
            })
            .Where(item => item.Side.HasValue &&
                           item.Campaign.TargetCityId != sourceCity.Id &&
                           item.Campaign.SourceCityId != sourceCity.Id)
            .Select(item => new
            {
                item.Campaign,
                Side = item.Side!.Value,
                RouteLinks = BattleCampaignService.GetFriendlyRouteLinks(world, sourceCity.Id, item.Campaign.TargetCityId, factionId)
            })
            .Where(item => item.RouteLinks > 0)
            .OrderByDescending(item => GetCampaignReinforcementUrgency(world, item.Campaign, item.Side) - item.RouteLinks * 8)
            .ThenBy(item => item.RouteLinks)
            .ToList();
        foreach (var candidate in candidates)
        {
            if (candidate.Campaign.Reinforcements.Any(order =>
                    order.SourceCityId == sourceCity.Id &&
                    order.DispatchYear == world.Year &&
                    order.DispatchMonth == world.Month &&
                    order.Status != ReinforcementStatus.Cancelled))
            {
                continue;
            }

            var reserveRatio = candidate.Side == CampaignBattleSide.Defender
                ? GetDefenderReinforcementRatio(candidate.Campaign)
                : 35;
            var troopBudget = Math.Min(
                sourceCity.Troops - BattleCampaignService.MinimumCityGarrison,
                Math.Max(AiReinforcementMinimumTroops, sourceCity.Troops * reserveRatio / 100));
            var deployments = CreateAiAttackDeployments(world, sourceCity, availableOfficerIds, troopBudget);
            if (deployments.Count == 0)
            {
                continue;
            }

            var food = Math.Min(sourceCity.Food / 4, Math.Max(0, deployments.Sum(item => item.TroopCount) * AiReinforcementFoodDays / 100));
            try
            {
                BattleCampaignService.DispatchReinforcement(
                    world, candidate.Campaign, sourceCity.Id, candidate.Side, deployments, 0, food);
                return LocalizedResult(true, "cmd.attack.reinforcement_scheduled");
            }
            catch (ReinforcementDispatchException)
            {
                // A second candidate may still be reachable and safe to reinforce.
            }
        }

        return null;
    }

    private static int GetCampaignReinforcementUrgency(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleSide side)
    {
        var friendlyTroops = campaign.Teams.Where(team => team.Side == side).Sum(team => Math.Max(0, team.ActiveTroops));
        var enemyTroops = campaign.Teams.Where(team => team.Side != side).Sum(team => Math.Max(0, team.ActiveTroops));
        var food = side == CampaignBattleSide.Attacker ? campaign.AttackerFood : campaign.DefenderFood;
        var officers = campaign.Teams
            .Where(team => team.Side == side)
            .Select(team => world.GetOfficer(team.OfficerId))
            .Where(officer => officer != null)
            .ToList();
        var intelligence = officers.Count == 0 ? 50 : officers.Average(officer => officer!.Intelligence);
        var combat = officers.Count == 0 ? 50 : officers.Average(officer => officer!.Combat);
        var cityDefense = world.GetCity(campaign.TargetCityId)?.Defense ?? 0;
        // Intelligence weighs food, ETA and city-risk awareness. Combat expresses
        // the willingness to commit against an adverse troop gap.
        return (enemyTroops - friendlyTroops) / 100 +
               (food < friendlyTroops ? 20 : 0) +
               (side == CampaignBattleSide.Defender ? Math.Max(0, 100 - cityDefense) / 5 : 0) +
               (int)intelligence / 8 +
               (enemyTroops > friendlyTroops ? (int)combat / 10 : 0);
    }

    private static int GetDefenderReinforcementRatio(ActiveBattleCampaignData campaign)
    {
        var defenders = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => Math.Max(0, team.ActiveTroops));
        var attackers = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).Sum(team => Math.Max(0, team.ActiveTroops));
        return attackers > defenders ? 50 : 30;
    }

    private static List<AttackOfficerDeploymentData> CreateAiAttackDeployments(
        WorldState world,
        CityData city,
        IReadOnlyList<int> officerIds,
        int troopBudget)
    {
        var usableOfficers = officerIds
            .Where(id => city.OfficerIds.Contains(id) && !BattleCampaignService.IsOfficerCommitted(world, id))
            .Take(BattleCampaignService.MaximumActivePiecesPerSide)
            .ToList();
        var pools = new List<(TroopType Type, int Count)>
        {
            (TroopType.Infantry, city.InfantryTroops), (TroopType.Spearman, city.SpearmanTroops),
            (TroopType.Cavalry, city.CavalryTroops), (TroopType.Archer, city.ArcherTroops),
            (TroopType.Crossbow, city.CrossbowTroops), (TroopType.Siege, city.SiegeTroops)
        };
        var remaining = Math.Min(Math.Max(0, troopBudget), pools.Sum(pool => pool.Count));
        var result = new List<AttackOfficerDeploymentData>();
        foreach (var officerId in usableOfficers)
        {
            var poolIndex = pools.FindIndex(pool => pool.Count > 0);
            if (poolIndex < 0 || remaining <= 0)
            {
                break;
            }
            var remainingSlots = Math.Max(1, usableOfficers.Count - result.Count);
            var count = Math.Min(pools[poolIndex].Count, Math.Max(1, remaining / remainingSlots));
            result.Add(new AttackOfficerDeploymentData { OfficerId = officerId, TroopType = pools[poolIndex].Type, TroopCount = count });
            pools[poolIndex] = (pools[poolIndex].Type, pools[poolIndex].Count - count);
            remaining -= count;
        }
        return result;
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

            if (BattleCampaignService.IsOfficerCommitted(world, officerId))
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

    private CommandResult? TryAssignFactionAdvisor(int factionId, OfficerData? officer, string position)
    {
        if (_commandResolver == null || _turnManager?.World == null || officer == null)
        {
            return null;
        }

        var city = _turnManager.World.GetCity(officer.CityId);
        if (city == null || city.OwnerFactionId != factionId || !city.OfficerIds.Contains(officer.Id))
        {
            return null;
        }

        return _commandResolver.ExecuteAssignFactionAdvisor(factionId, city.Id, officer.Id, position);
    }

    private CommandResult? TryAssignCityPrefect(int factionId, CityData city, OfficerData? officer)
    {
        if (_commandResolver == null || officer == null)
        {
            return null;
        }

        return _commandResolver.ExecuteAssignOfficerAppointment(
            factionId,
            city.Id,
            officer.Id,
            OfficerAppointmentRules.Governor);
    }

    private static OfficerData? GetBestFactionAdvisorCandidate(
        WorldState world,
        FactionData faction,
        HashSet<int> reservedOfficerIds,
        System.Func<OfficerData, int> scoreSelector)
    {
        return faction.OfficerIds
            .Where(officerId => officerId != faction.RulerOfficerId && !reservedOfficerIds.Contains(officerId))
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .Where(officer => officer.CityId > 0)
            .OrderByDescending(scoreSelector)
            .ThenByDescending(officer => officer.Loyalty)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static bool HasCityPrefect(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Any(officer => officer != null && OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor));
    }

    private static OfficerData? GetBestCityPrefectCandidate(
        WorldState world,
        FactionData faction,
        CityData city,
        HashSet<int> reservedOfficerIds)
    {
        var primaryPool = city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .Where(officer =>
                officer.CityId == city.Id &&
                faction.OfficerIds.Contains(officer.Id) &&
                officer.Id != faction.RulerOfficerId &&
                !reservedOfficerIds.Contains(officer.Id))
            .OrderByDescending(ScoreCityPrefectCandidate)
            .ThenByDescending(officer => officer.Loyalty)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();

        if (primaryPool != null)
        {
            return primaryPool;
        }

        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .Where(officer =>
                officer.CityId == city.Id &&
                faction.OfficerIds.Contains(officer.Id) &&
                officer.Id != faction.RulerOfficerId)
            .OrderByDescending(ScoreCityPrefectCandidate)
            .ThenByDescending(officer => officer.Loyalty)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static int ScoreCityPrefectCandidate(OfficerData officer)
    {
        return officer.Politics * 3 +
               officer.Intelligence * 2 +
               officer.Charm +
               officer.Leadership +
               (officer.FarmRank + officer.CommercialRank) * 15 +
               (officer.DefendRank + officer.DisasterPreventionRank + officer.ConstructionRank) * 10;
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
            SetDiplomacyDecisionDebug(
                world,
                city,
                factionId,
                threatenedNeighbor.OwnerFactionId,
                DiplomacyActionType.Truce,
                "fmt.ai_debug_diplomacy_reason_truce",
                threatenedNeighbor.NameZhHant,
                threatenedNeighbor.NameEn,
                threatenedNeighbor.Troops,
                city.Troops,
                threatenedNeighbor.Troops - city.Troops,
                DiplomacyThreatTroopGap,
                3);
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

        var breakPactTargetFactionId = world.DiplomacyRelations
            .Where(relation =>
                relation.RemainingMonths > 0 &&
                relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
                relation.RelationScore <= DiplomacyBreakPactMaxRelationScore &&
                (relation.FactionAId == factionId || relation.FactionBId == factionId))
            .Select(relation => relation.FactionAId == factionId ? relation.FactionBId : relation.FactionAId)
            .FirstOrDefault(targetFactionId =>
                targetFactionId > 0 &&
                targetFactionId != factionId &&
                world.GetFaction(targetFactionId) != null &&
                world.Cities.Any(targetCity => targetCity.OwnerFactionId == targetFactionId) &&
                GetFactionTroopTotal(world, factionId) >= GetFactionTroopTotal(world, targetFactionId) + DiplomacyBreakPactTroopAdvantage);
        if (breakPactTargetFactionId > 0)
        {
            var relation = world.GetDiplomacyRelation(factionId, breakPactTargetFactionId);
            var ownTroops = GetFactionTroopTotal(world, factionId);
            var targetTroops = GetFactionTroopTotal(world, breakPactTargetFactionId);
            SetDiplomacyDecisionDebug(
                world,
                city,
                factionId,
                breakPactTargetFactionId,
                DiplomacyActionType.BreakPact,
                "fmt.ai_debug_diplomacy_reason_break_pact",
                relation?.RelationScore ?? 0,
                DiplomacyBreakPactMaxRelationScore,
                ownTroops,
                targetTroops,
                ownTroops - targetTroops,
                DiplomacyBreakPactTroopAdvantage);
            var breakPactResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Diplomacy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetFactionId = breakPactTargetFactionId,
                DiplomacyActionType = DiplomacyActionType.BreakPact,
                OfficerIds = new System.Collections.Generic.List<int> { diplomacyOfficerId }
            });

            if (breakPactResult.Success)
            {
                availableOfficerIds.Remove(diplomacyOfficerId);
            }

            return breakPactResult;
        }

        if (city.Gold >= DiplomacyDemandGoldThreshold)
        {
            var demandTargetFactionId = world.Factions
                .Where(faction => faction.Id != factionId && world.Cities.Any(targetCity => targetCity.OwnerFactionId == faction.Id))
                .Select(faction => new
                {
                    FactionId = faction.Id,
                    Relation = world.GetDiplomacyRelation(factionId, faction.Id),
                    Troops = GetFactionTroopTotal(world, faction.Id)
                })
                .Where(item =>
                    (item.Relation == null || item.Relation.RemainingMonths <= 0) &&
                    (item.Relation?.RelationScore ?? 0) <= DiplomacyDemandMaxRelationScore &&
                    GetFactionTroopTotal(world, factionId) >= item.Troops + DiplomacyDemandTroopAdvantage)
                .OrderBy(item => item.Relation?.RelationScore ?? 0)
                .ThenBy(item => item.Troops)
                .Select(item => item.FactionId)
                .FirstOrDefault();
            if (demandTargetFactionId > 0)
            {
                var relation = world.GetDiplomacyRelation(factionId, demandTargetFactionId);
                var ownTroops = GetFactionTroopTotal(world, factionId);
                var targetTroops = GetFactionTroopTotal(world, demandTargetFactionId);
                SetDiplomacyDecisionDebug(
                    world,
                    city,
                    factionId,
                    demandTargetFactionId,
                    DiplomacyActionType.Demand,
                    "fmt.ai_debug_diplomacy_reason_demand",
                    city.Gold,
                    DiplomacyDemandGoldThreshold,
                    relation?.RelationScore ?? 0,
                    DiplomacyDemandMaxRelationScore,
                    ownTroops - targetTroops,
                    DiplomacyDemandTroopAdvantage,
                    Math.Min(DiplomacyDemandAmount, city.Gold));
                var demandResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Diplomacy,
                    ActorFactionId = factionId,
                    SourceCityId = city.Id,
                    TargetFactionId = demandTargetFactionId,
                    DiplomacyActionType = DiplomacyActionType.Demand,
                    GoldToSend = System.Math.Min(DiplomacyDemandAmount, city.Gold),
                    OfficerIds = new System.Collections.Generic.List<int> { diplomacyOfficerId }
                });

                if (demandResult.Success)
                {
                    availableOfficerIds.Remove(diplomacyOfficerId);
                }

                return demandResult;
            }
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
            var relation = world.GetDiplomacyRelation(factionId, allianceTargetFactionId);
            SetDiplomacyDecisionDebug(
                world,
                city,
                factionId,
                allianceTargetFactionId,
                DiplomacyActionType.Alliance,
                "fmt.ai_debug_diplomacy_reason_alliance",
                relation?.RelationScore ?? 0,
                DiplomacyAllianceRelationThreshold,
                4);
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

        var giftRelation = world.GetDiplomacyRelation(factionId, giftTargetFactionId.FactionId);
        SetDiplomacyDecisionDebug(
            world,
            city,
            factionId,
            giftTargetFactionId.FactionId,
            DiplomacyActionType.Gift,
            "fmt.ai_debug_diplomacy_reason_gift",
            city.Gold,
            DiplomacyGiftGoldThreshold,
            giftRelation?.RelationScore ?? 0,
            Math.Min(DiplomacyGiftAmount, city.Gold));
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

    private void SetDiplomacyDecisionDebug(
        WorldState world,
        CityData city,
        int actorFactionId,
        int targetFactionId,
        DiplomacyActionType actionType,
        string reasonKey,
        params object[] reasonArguments)
    {
        var useChinese = _localization?.IsTraditionalChinese != false;
        var actorFaction = world.GetFaction(actorFactionId);
        var targetFaction = world.GetFaction(targetFactionId);
        var actorName = useChinese
            ? actorFaction?.NameZhHant ?? actorFaction?.NameEn ?? actorFactionId.ToString()
            : actorFaction?.NameEn ?? actorFaction?.NameZhHant ?? actorFactionId.ToString();
        var targetName = useChinese
            ? targetFaction?.NameZhHant ?? targetFaction?.NameEn ?? targetFactionId.ToString()
            : targetFaction?.NameEn ?? targetFaction?.NameZhHant ?? targetFactionId.ToString();
        var cityName = useChinese
            ? city.NameZhHant
            : city.NameEn;
        if (string.IsNullOrWhiteSpace(cityName))
        {
            cityName = city.Name;
        }

        var language = useChinese ? GameLanguage.TraditionalChinese : GameLanguage.English;
        var actionName = _localization?.TForLanguage(language, GetDiplomacyActionLocaleKey(actionType)) ?? actionType.ToString();
        var reason = _localization?.FormatForLanguage(language, reasonKey, reasonArguments) ?? reasonKey;
        LastDiplomacyDecisionTargetFactionId = targetFactionId;
        LastDecisionDebugDetail = _localization?.FormatForLanguage(
            language,
            "fmt.ai_diplomacy_decision_reason",
            actorName,
            cityName,
            targetName,
            actionName,
            reason) ?? $"[AI Debug] Diplomacy decision: {actorName}/{cityName} -> {targetName}: {reason}";
    }

    private static string GetDiplomacyActionLocaleKey(DiplomacyActionType actionType)
    {
        return actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        };
    }

    private void SetAttackDecisionDebug(
        WorldState world,
        CityData sourceCity,
        int actorFactionId,
        CityData targetCity,
        bool canInspectTarget,
        int deployedTroops,
        int attackTroopAdvantageThreshold,
        int blindAttackTroopThreshold)
    {
        var useChinese = _localization?.IsTraditionalChinese != false;
        var language = useChinese ? GameLanguage.TraditionalChinese : GameLanguage.English;
        var actorFaction = world.GetFaction(actorFactionId);
        var actorName = useChinese
            ? actorFaction?.NameZhHant ?? actorFaction?.NameEn ?? actorFactionId.ToString()
            : actorFaction?.NameEn ?? actorFaction?.NameZhHant ?? actorFactionId.ToString();
        var sourceCityName = useChinese ? sourceCity.NameZhHant : sourceCity.NameEn;
        var targetCityName = useChinese ? targetCity.NameZhHant : targetCity.NameEn;
        sourceCityName = string.IsNullOrWhiteSpace(sourceCityName) ? sourceCity.Name : sourceCityName;
        targetCityName = string.IsNullOrWhiteSpace(targetCityName) ? targetCity.Name : targetCityName;
        var reasonKey = canInspectTarget
            ? "fmt.ai_attack_reason_visible"
            : "fmt.ai_attack_reason_blind";
        var reasonArguments = canInspectTarget
            ? new object[]
            {
                targetCity.Troops,
                sourceCity.Troops,
                sourceCity.Troops - targetCity.Troops,
                GetRulerAmbition(world, actorFactionId),
                attackTroopAdvantageThreshold
            }
            : new object[]
            {
                sourceCity.Troops,
                GetRulerAmbition(world, actorFactionId),
                blindAttackTroopThreshold
            };
        var reason = _localization?.FormatForLanguage(language, reasonKey, reasonArguments) ?? reasonKey;

        LastAttackDecisionTargetFactionId = targetCity.OwnerFactionId;
        LastAttackDecisionDetail = _localization?.FormatForLanguage(
            language,
            "fmt.ai_attack_decision_reason",
            actorName,
            sourceCityName,
            targetCityName,
            deployedTroops,
            reason) ?? $"AI attack decision: {actorName}/{sourceCityName} -> {targetCityName}: {reason}";
    }

    private static int GetRulerAmbition(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        var ruler = faction == null ? null : world.GetOfficer(faction.RulerOfficerId);
        return System.Math.Clamp(ruler?.Ambition ?? 50, 0, 100);
    }

    private static int GetAttackTroopAdvantageThreshold(WorldState world, int factionId)
    {
        var ambition = GetRulerAmbition(world, factionId);
        return System.Math.Clamp(
            BaseAttackTroopAdvantageThreshold + 250 - ambition * 5,
            MinimumAttackTroopAdvantageThreshold,
            MaximumAttackTroopAdvantageThreshold);
    }

    private static int GetBlindAttackTroopThreshold(WorldState world, int factionId)
    {
        var ambition = GetRulerAmbition(world, factionId);
        return System.Math.Clamp(
            BlindAttackTroopThreshold + (50 - ambition) * 8,
            MinimumBlindAttackTroopThreshold,
            MaximumBlindAttackTroopThreshold);
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

        var assassinationTarget = visibleTargets
            .Where(target => target.Defense <= SpyAssassinationDefenseThreshold)
            .Where(target => GetBestAssassinationTargetValue(world, target) >= SpyAssassinationTargetValueThreshold)
            .OrderByDescending(target => GetBestAssassinationTargetValue(world, target))
            .ThenBy(target => target.Defense)
            .FirstOrDefault();
        if (assassinationTarget != null)
        {
            var assassinationTargetOfficerId = GetBestAssassinationTargetOfficerId(world, assassinationTarget);
            var assassinationResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Spy,
                ActorFactionId = factionId,
                SourceCityId = city.Id,
                TargetCityId = assassinationTarget.Id,
                TargetOfficerId = assassinationTargetOfficerId > 0 ? assassinationTargetOfficerId : null,
                SpyActionType = SpyActionType.Assassination,
                OfficerIds = new System.Collections.Generic.List<int> { spyOfficerId }
            });

            if (assassinationResult.Success)
            {
                availableOfficerIds.Remove(spyOfficerId);
            }

            return assassinationResult;
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

    private static int GetBestAssassinationTargetValue(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .Select(officer =>
                officer.Combat + officer.Leadership + officer.Intelligence + officer.Politics + officer.Charm - (officer.Loyalty / 2) +
                (world.Factions.Any(faction => faction.RulerOfficerId == officer.Id) ? 1000 : 0))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int GetBestAssassinationTargetOfficerId(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .OrderByDescending(officer => world.Factions.Any(faction => faction.RulerOfficerId == officer.Id))
            .ThenBy(officer => officer.Loyalty)
            .ThenByDescending(officer => officer.Combat + officer.Leadership + officer.Intelligence + officer.Politics + officer.Charm)
            .Select(officer => officer.Id)
            .FirstOrDefault();
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

    private static int GetFactionTroopTotal(WorldState world, int factionId)
    {
        return world.Cities
            .Where(city => city.OwnerFactionId == factionId)
            .Sum(city => city.Troops);
    }
}
