using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private const int DevelopGoldCost = 100;
    private const int RecruitGoldCost = 120;
    private const int RecruitFoodCost = 80;
    private const int MerchantFoodPerTrade = 100;
    private const int MerchantGoldPerTrade = 10;
    private const int MerchantHorsePerTrade = 10;
    private const int MerchantGoldPerHorseTrade = 20;
    private const int PersonnelBonusGoldPerLoyalty = 100;
    private const int PersonnelBonusFoodPerLoyalty = 500;
    private const int HireOfficerGoldCost = 200;
    private const int HireOfficerMaxLoyalty = 70;
    private const int HireOfficerDefaultLoyalty = 60;
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

        faction.RulerOfficerId = successorOfficerId;
        if (!faction.OfficerIds.Contains(successorOfficerId))
        {
            faction.OfficerIds.Add(successorOfficerId);
        }

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


    private void ConfigureRandom(int seed)
    {
        _random = seed != 0
            ? new Random(HashCode.Combine(seed, 701))
            : new Random();
    }
}
