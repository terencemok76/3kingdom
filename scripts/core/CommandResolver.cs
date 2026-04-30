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
    private const int PersonnelBonusGoldPerLoyalty = 100;
    private const int PersonnelBonusFoodPerLoyalty = 500;
    private const int HireOfficerGoldCost = 200;
    private const int HireOfficerMaxLoyalty = 70;
    private const int HireOfficerDefaultLoyalty = 60;
    private const int CivilReliefGoldPerTenLoyalty = 100;
    private const int CivilReliefFoodPerTenLoyalty = 1000;
    private const float FailedAttackSupplyReturnRatio = 0.5f;

    private readonly Random _random = new();

    private TurnManager? _turnManager;
    private CombatResolver? _combatResolver;
    private LocalizationService? _localization;

    public void Initialize(TurnManager turnManager, CombatResolver combatResolver, LocalizationService localization)
    {
        _turnManager = turnManager;
        _combatResolver = combatResolver;
        _localization = localization;
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
            CommandType.Merchant => ExecuteMerchant(world, sourceCity, request),
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
            CommandType.Move => ResolveMove(world, sourceCity, pendingCommand),
            CommandType.Attack => ResolveAttack(world, sourceCity, pendingCommand),
            _ => LocalizedResult(false, "cmd.unsupported_pending_command")
        };
    }


}
