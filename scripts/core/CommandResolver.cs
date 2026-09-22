using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private const int DevelopGoldCost = 100;
    private const int MerchantFoodPerTrade = 100;
    private const int MerchantGoldPerTrade = 10;
    private const int MerchantHorsePerTrade = 10;
    private const int MerchantGoldPerHorseTrade = 20;
    private const int PersonnelBonusGoldPerLoyalty = 100;
    private const int PersonnelBonusFoodPerLoyalty = 500;
    private const int HireOfficerGoldCost = 200;
    private const int HireOfficerMaxLoyalty = 70;
    private const int HireOfficerDefaultLoyalty = 60;
    private const double CapturedOfficerRecruitBaseChance = 0.30;
    private const double CapturedOfficerRecruitRulerFamilyBonus = 0.35;
    private const double CapturedOfficerRecruitFactionFamilyBonus = 0.18;
    private const double CapturedOfficerRecruitRulerCharmFactor = 0.0015;
    private const double CapturedOfficerRecruitAmbitionPenaltyFactor = 0.0025;
    private const double CapturedOfficerRecruitMinimumChance = 0.05;
    private const double CapturedOfficerRecruitMaximumChance = 0.95;
    private const int CivilReliefGoldPerTenLoyalty = 100;
    private const int CivilReliefFoodPerTenLoyalty = 1000;
    private const float FailedAttackSupplyReturnRatio = 0.5f;

    private Random _random = new();

    private TurnManager? _turnManager;
    private CombatResolver? _combatResolver;
    private LocalizationService? _localization;

    public void Initialize(TurnManager turnManager, CombatResolver combatResolver, LocalizationService localization)
    {
        _turnManager = turnManager;
        _combatResolver = combatResolver;
        _localization = localization;
        ConfigureRandom(turnManager.World?.RandomSeed ?? 0);
    }

    public CommandResult Execute(CommandRequest request)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(request.SourceCityId);
        if (sourceCity == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (request.Type != CommandType.Pass && sourceCity.OwnerFactionId != request.ActorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        return request.Type switch
        {
            CommandType.Develop => ScheduleDevelop(world, sourceCity, request),
            CommandType.Recruit => ScheduleRecruit(world, sourceCity, request),
            CommandType.Move => ScheduleMove(world, sourceCity, request),
            CommandType.Search => ScheduleSearch(world, sourceCity, request),
            CommandType.CivilRelief => ScheduleCivilRelief(world, sourceCity, request),
            CommandType.Merchant => ExecuteMerchant(world, sourceCity, request),
            CommandType.Diplomacy => ScheduleDiplomacy(world, sourceCity, request),
            CommandType.Spy => ScheduleSpy(world, sourceCity, request),
            CommandType.Attack => ScheduleAttack(world, sourceCity, request),
            CommandType.HireOfficer => ExecuteHireOfficer(request.ActorFactionId, sourceCity.Id, request.TargetOfficerId ?? 0, request.GoldToSend, request.FoodToSend, request.ItemId, request.OfficerIds.FirstOrDefault()),
            CommandType.Pass => LocalizedResult(true, "cmd.pass"),
            _ => LocalizedResult(false, "cmd.unknown_command")
        };
    }

    public CommandResult ResolvePendingCommand(PendingCommandData pendingCommand)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        if (sourceCity == null)
        {
            return LocalizedResult(false, "cmd.pending_source_city_not_found");
        }

        return pendingCommand.Type switch
        {
            CommandType.Develop => ResolveDevelop(world, sourceCity, pendingCommand),
            CommandType.Recruit => ResolveRecruit(world, sourceCity, pendingCommand),
            CommandType.Search => ResolveSearch(world, sourceCity, pendingCommand),
            CommandType.CivilRelief => ResolveCivilRelief(world, sourceCity, pendingCommand),
            CommandType.Move => ResolveMove(world, sourceCity, pendingCommand),
            CommandType.Diplomacy => ResolveDiplomacy(world, sourceCity, pendingCommand),
            CommandType.Spy => ResolveSpy(world, sourceCity, pendingCommand),
            CommandType.Attack => ResolveAttack(world, sourceCity, pendingCommand),
            CommandType.HireOfficer => ExecuteHireOfficer(pendingCommand.ActorFactionId, sourceCity.Id, pendingCommand.TargetOfficerId, pendingCommand.GoldToSend, pendingCommand.FoodToSend, pendingCommand.ItemId),
            _ => LocalizedResult(false, "cmd.unsupported_pending_command")
        };
    }

    public CommandResult ResolvePlayerSuccession(int factionId, int successorOfficerId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return LocalizedResult(false, "cmd.succession.invalid_faction");
        }

        var pendingSuccession = world.GetPendingSuccession(factionId);
        if (pendingSuccession == null)
        {
            return LocalizedResult(false, "cmd.succession.not_pending");
        }

        if (!pendingSuccession.CandidateOfficerIds.Contains(successorOfficerId))
        {
            return LocalizedResult(false, "cmd.succession.invalid_successor");
        }

        var successor = world.GetOfficer(successorOfficerId);
        if (successor == null)
        {
            return LocalizedResult(false, "cmd.succession.invalid_successor");
        }

        ApplyFactionSuccessor(world, faction, successor);

        world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == factionId);
        return LocalizedResult(
            true,
            "cmd.succession.player_resolved",
            new object[]
            {
                GetFactionName(faction, GameLanguage.TraditionalChinese),
                GetOfficerDisplayName(successor, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetFactionName(faction, GameLanguage.English),
                GetOfficerDisplayName(successor, GameLanguage.English)
            });
    }

    public CommandResult ResolvePlayerRulerChange(int factionId, int successorOfficerId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var faction = world.GetFaction(factionId);
        if (faction == null || !faction.IsPlayer || faction.RulerOfficerId <= 0)
        {
            return LocalizedResult(false, "cmd.ruler_change.invalid_faction");
        }

        if (world.GetPendingSuccession(factionId) != null)
        {
            return LocalizedResult(false, "cmd.ruler_change.pending_succession");
        }

        if (successorOfficerId == faction.RulerOfficerId)
        {
            return LocalizedResult(false, "cmd.ruler_change.already_ruler");
        }

        var successor = world.GetOfficer(successorOfficerId);
        if (successor == null ||
            !faction.OfficerIds.Contains(successorOfficerId) ||
            successor.CityId <= 0 ||
            successor.CaptiveFactionId > 0 ||
            (successor.DeathYear > 0 && world.Year > successor.DeathYear) ||
            BattleCampaignService.IsOfficerCommitted(world, successorOfficerId))
        {
            return LocalizedResult(false, "cmd.ruler_change.invalid_successor");
        }

        var previousRuler = world.GetOfficer(faction.RulerOfficerId);
        var previousNameZh = previousRuler == null ? string.Empty : GetOfficerDisplayName(previousRuler, GameLanguage.TraditionalChinese);
        var previousNameEn = previousRuler == null ? string.Empty : GetOfficerDisplayName(previousRuler, GameLanguage.English);
        ApplyFactionSuccessor(world, faction, successor);
        return LocalizedResult(
            true,
            "cmd.ruler_change.resolved",
            new object[] { previousNameZh, GetOfficerDisplayName(successor, GameLanguage.TraditionalChinese) },
            new object[] { previousNameEn, GetOfficerDisplayName(successor, GameLanguage.English) });
    }

    public CommandResult ResolveAiRulerChange(int factionId, int successorOfficerId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var faction = world.GetFaction(factionId);
        if (faction == null || faction.IsPlayer || faction.RulerOfficerId <= 0 ||
            world.GetPendingSuccession(factionId) != null)
        {
            return LocalizedResult(false, "cmd.ai_ruler_change.invalid");
        }

        var previousRuler = world.GetOfficer(faction.RulerOfficerId);
        var successor = world.GetOfficer(successorOfficerId);
        if (previousRuler == null || successor == null || successor.Id == previousRuler.Id ||
            !faction.OfficerIds.Contains(successor.Id) || successor.CityId <= 0 ||
            successor.CaptiveFactionId > 0 ||
            (successor.DeathYear > 0 && world.Year > successor.DeathYear) ||
            BattleCampaignService.IsOfficerCommitted(world, successor.Id))
        {
            return LocalizedResult(false, "cmd.ai_ruler_change.invalid");
        }

        var previousNameZh = GetOfficerDisplayName(previousRuler, GameLanguage.TraditionalChinese);
        var previousNameEn = GetOfficerDisplayName(previousRuler, GameLanguage.English);
        ApplyFactionSuccessor(world, faction, successor);
        previousRuler.Loyalty = System.Math.Min(100, previousRuler.Loyalty + 5);
        var highAmbitionLoyaltyPenalty = successor.Ambition > 70
            ? System.Math.Max(1, (successor.Ambition - 60) / 15)
            : 0;
        if (highAmbitionLoyaltyPenalty > 0)
        {
            foreach (var officerId in faction.OfficerIds)
            {
                if (officerId == successor.Id || officerId == previousRuler.Id)
                {
                    continue;
                }

                var factionOfficer = world.GetOfficer(officerId);
                if (factionOfficer != null)
                {
                    factionOfficer.Loyalty = System.Math.Max(0, factionOfficer.Loyalty - highAmbitionLoyaltyPenalty);
                }
            }
        }
        faction.LastRulerChangeYear = world.Year;
        faction.LastRulerChangeMonth = world.Month;

        var result = LocalizedResult(
            true,
            "cmd.ai_ruler_change.resolved",
            new object[]
            {
                previousNameZh,
                GetOfficerDisplayName(successor, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                previousNameEn,
                GetOfficerDisplayName(successor, GameLanguage.English)
            });
        if (highAmbitionLoyaltyPenalty > 0)
        {
            AppendLocalizedText(
                result,
                _localization?.FormatForLanguage(
                    GameLanguage.TraditionalChinese,
                    "cmd.ai_ruler_change.high_ambition_suffix",
                    highAmbitionLoyaltyPenalty) ?? string.Empty,
                _localization?.FormatForLanguage(
                    GameLanguage.English,
                    "cmd.ai_ruler_change.high_ambition_suffix",
                    highAmbitionLoyaltyPenalty) ?? string.Empty);
        }
        result.IsRulerChange = true;
        result.IsPlayerRelated = true;
        return result;
    }


    private void ConfigureRandom(int seed)
    {
        _random = seed != 0
            ? new Random(HashCode.Combine(seed, 701))
            : new Random();
    }
}
