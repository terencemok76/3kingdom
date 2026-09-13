using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private const string GameplayScenePath = "res://scenes/main/Main.tscn";
    private ActiveBattleCampaignData? _activeCampaign;
    private bool _campaignResultHandled;
    private bool _campaignReturnQueued;
    private bool _campaignMonthLimitReached;
    private bool _standaloneMonthLimitReached;
    private int _debugMonthlyBattleDayLimit;
    private VBoxContainer? _campaignDecisionPanel;

    private int GetMonthlyBattleDayLimit() =>
        _debugMonthlyBattleDayLimit > 0
            ? _debugMonthlyBattleDayLimit
            : BattleCampaignService.MaximumBattleDaysPerMonth;

    private bool IsCampaignMonthLimitReached => _campaignMonthLimitReached && _activeCampaign != null;
    private bool IsStandaloneMonthLimitReached => _standaloneMonthLimitReached && _activeCampaign == null;

    private void InitializeCampaignRuntime()
    {
        _activeCampaign = CampaignRuntimeContext.GetActiveCampaign();
        if (_activeCampaign == null)
        {
            return;
        }

        _battleDateYear = _activeCampaign.CurrentYear;
        _battleDateMonth = _activeCampaign.CurrentMonth;
        _battleDateDay = Math.Clamp(_activeCampaign.BattleDaysThisMonth + 1, 1, DateTime.DaysInMonth(_battleDateYear, _battleDateMonth));
        _teamAGold = _activeCampaign.AttackerGold;
        _teamAFood = _activeCampaign.AttackerFood;
        _teamBGold = _activeCampaign.DefenderGold;
        _teamBFood = _activeCampaign.DefenderFood;

        var playerFactionId = CampaignRuntimeContext.World?.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? -1;
        _aiControlledSides = BattleAiControlledSides.None;
        if (_activeCampaign.AttackerFactionId != playerFactionId)
        {
            _aiControlledSides |= BattleAiControlledSides.Attacker;
        }
        if (_activeCampaign.DefenderFactionId != playerFactionId)
        {
            _aiControlledSides |= BattleAiControlledSides.Defender;
        }
    }

    private string GetCampaignHudScenarioName()
    {
        if (_activeCampaign == null || CampaignRuntimeContext.World == null)
        {
            return FormatScenarioName(ResolveScenarioDefinition().DisplayName);
        }

        var targetCity = CampaignRuntimeContext.World.GetCity(_activeCampaign.TargetCityId);
        var cityName = targetCity == null
            ? BattleText("ui.unknown", "Unknown")
            : _localization.GetCityName(targetCity);
        return _activeCampaign.Stage == CampaignStage.FieldBattle
            ? BattleFormat("ui.battle.campaign_field_title", "Field Battle ({0})", cityName)
            : BattleFormat("ui.battle.campaign_city_title", "Siege Battle ({0})", cityName);
    }

    private string GetCampaignFactionArmyName(CampaignBattleSide side)
    {
        if (_activeCampaign == null || CampaignRuntimeContext.World == null)
        {
            return string.Empty;
        }

        var factionId = side == CampaignBattleSide.Attacker
            ? _activeCampaign.AttackerFactionId
            : _activeCampaign.DefenderFactionId;
        return BattleFormat(
            "ui.battle.faction_army",
            "{0} Army",
            _localization.GetFactionName(CampaignRuntimeContext.World, factionId));
    }

    private bool TryPopulateCampaignMarkers()
    {
        if (_activeCampaign == null || _mapData == null)
        {
            return false;
        }

        var unitLayer = GetNodeOrNull<Node2D>("MapRoot/UnitLayer");
        if (unitLayer == null)
        {
            return false;
        }

        foreach (var child in unitLayer.GetChildren().OfType<BattlePieceMarker>())
        {
            child.Visible = false;
        }

        DeployCampaignTeams(unitLayer);
        return true;
    }

    private void RestoreCampaignSnapshotIfAvailable()
    {
        if (_activeCampaign == null || string.IsNullOrWhiteSpace(_activeCampaign.BattleSnapshotJson))
        {
            return;
        }

        if (TryApplyCampaignBattleSnapshot(_activeCampaign.BattleSnapshotJson))
        {
            if (IsCampaignSnapshotFromPreviousMonth())
            {
                RebaseCampaignSnapshotForNewMonth();
                _activeCampaign.BattleSnapshotJson = CreateCampaignBattleSnapshot();
            }

            AppendBattleLog("Battle", "Campaign", $"Resumed campaign {_activeCampaign.Id}, month day {_activeCampaign.BattleDaysThisMonth + 1}.");
        }
        else
        {
            _activeCampaign.BattleSnapshotJson = string.Empty;
        }
    }

    private bool IsCampaignSnapshotFromPreviousMonth()
    {
        return _activeCampaign != null &&
               (_battleDateYear != _activeCampaign.CurrentYear ||
                _battleDateMonth != _activeCampaign.CurrentMonth);
    }

    private void RebaseCampaignSnapshotForNewMonth()
    {
        if (_activeCampaign == null)
        {
            return;
        }

        // The snapshot is deliberately retained for terrain, gates, fires, positions, and unit state.
        // Only the temporal/action state starts the new strategic month again at its first dawn.
        _battleDateYear = _activeCampaign.CurrentYear;
        _battleDateMonth = _activeCampaign.CurrentMonth;
        _battleDateDay = 1;
        _currentBattleTimeOfDay = BattleTimeOfDay.Dawn;
        _currentTurnSide = BattleTurnSide.TeamA;
        _turnNumber = Math.Max(1, _turnNumber + 1);
        _isFieldAiRoundStarted = false;
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _actedByMarkerThisRound.Clear();

        _teamAGold = _activeCampaign.AttackerGold;
        _teamAFood = _activeCampaign.AttackerFood;
        _teamBGold = _activeCampaign.DefenderGold;
        _teamBFood = _activeCampaign.DefenderFood;
        ResetCampaignTeamDailyUpkeep(_state.TeamA);
        ResetCampaignTeamDailyUpkeep(_state.TeamB);
        ApplyCampaignMonthRecoveryToBattleUnits();
        RestoreTeamUnitEnergy(BattleTeamIdentity.AttackerName);
        RestoreTeamUnitEnergy(BattleTeamIdentity.DefenderName);
        RecalculateBattleHudTotals();
        ConfigureHud();
        ApplyTimeOfDayVisual(animate: false);
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private static void ResetCampaignTeamDailyUpkeep(BattleTeamState team)
    {
        team.FoodUpkeepRemainder = 0;
        team.GoldUpkeepRemainder = 0;
        team.HadFoodShortageThisDay = false;
    }

    private void ApplyCampaignMonthRecoveryToBattleUnits()
    {
        if (_activeCampaign == null)
        {
            return;
        }

        var teamsById = _activeCampaign.Teams.ToDictionary(team => team.Id);
        _occupantsByGrid.UpdateAll((_, occupant) =>
        {
            if (occupant.Category != CategoryUnit ||
                occupant.CampaignTeamId <= 0 ||
                !teamsById.TryGetValue(occupant.CampaignTeamId, out var team))
            {
                return occupant;
            }

            return occupant with
            {
                TroopCount = team.ActiveTroops,
                HitPoints = team.ActiveTroops,
                MaxHitPoints = Math.Max(team.MaximumTroops, team.ActiveTroops + team.WoundedTroops),
                WoundedTroops = team.WoundedTroops,
                Morale = team.Morale
            };
        });

        foreach (var occupant in _occupantsByGrid.Values.SelectMany(items => items))
        {
            if (occupant.Category == CategoryUnit && occupant.CampaignTeamId > 0)
            {
                UpdateMarkerStrengthBar(occupant);
            }
        }
    }

    private void DeployCampaignTeams(Node2D unitLayer)
    {
        if (_activeCampaign == null || _mapData == null)
        {
            return;
        }

        foreach (var side in Enum.GetValues<CampaignBattleSide>())
        {
            var alreadyDeployed = _occupantsByGrid
                .SelectMany(entry => entry.Value)
                .Count(occupant => occupant.CampaignTeamId > 0 &&
                                   (side == CampaignBattleSide.Attacker
                                       ? BattleTeamIdentity.IsAttacker(occupant.TeamName)
                                       : BattleTeamIdentity.IsDefender(occupant.TeamName)));
            var teams = _activeCampaign.Teams
                .Where(team => team.Side == side && team.ActiveTroops > 0 && IsCampaignTeamDeployable(team))
                .Where(team => !_occupantsByGrid.Values.SelectMany(items => items).Any(occupant => occupant.CampaignTeamId == team.Id))
                .Take(Math.Max(0, BattleCampaignService.MaximumActivePiecesPerSide - alreadyDeployed))
                .ToList();
            var occupied = _occupantsByGrid.Select(entry => entry.Key.Grid).ToHashSet();
            foreach (var team in teams)
            {
                var grid = GetCampaignSpawnGrids(team, occupied).FirstOrDefault();
                if (grid == default && !IsCampaignSpawnGridAvailable(grid, occupied))
                {
                    KeepBlockedReinforcementInReserve(team);
                    continue;
                }

                CreateCampaignMarker(unitLayer, grid, team);
                occupied.Add(grid);
            }
        }
    }

    private bool IsCampaignTeamDeployable(CampaignBattleTeamData team)
    {
        if (team.Location == CampaignTeamLocation.Field)
        {
            return _activeCampaign?.Stage != CampaignStage.FieldBattle ||
                   team.SiegeEngineType is not (SiegeEngineType.Ram or SiegeEngineType.Ladder);
        }

        return _activeCampaign?.Stage == CampaignStage.CityBattle && team.Location == CampaignTeamLocation.InnerCity;
    }

    private IEnumerable<Vector2I> GetCampaignSpawnGrids(CampaignBattleTeamData team, HashSet<Vector2I> occupied)
    {
        if (_mapData == null)
        {
            yield break;
        }

        var deploymentZone = team.Side == CampaignBattleSide.Attacker
            ? BattleDeploymentZone.Attacker
            : BattleDeploymentZone.Defender;
        if (team.ReinforcementOrderId > 0)
        {
            var configuredGrids = team.Side == CampaignBattleSide.Attacker
                ? _mapData.ScenarioDefinition.AttackerReinforcementEntranceGrids
                : _mapData.ScenarioDefinition.DefenderReinforcementEntranceGrids;
            foreach (var grid in configuredGrids)
            {
                // Scenario overrides remain constrained to this side's external
                // start zone: reinforcements cannot appear inside a city or beside
                // the enemy merely because a scenario entry was misconfigured.
                if (IsCampaignReinforcementEntranceGrid(grid, deploymentZone) &&
                    IsCampaignSpawnGridAvailable(grid, occupied))
                {
                    yield return grid;
                }
            }

            foreach (var grid in GetCampaignDeploymentZoneGrids(deploymentZone, occupied)
                         .Where(grid => !configuredGrids.Contains(grid)))
            {
                yield return grid;
            }
            yield break;
        }

        foreach (var grid in GetCampaignDeploymentZoneGrids(deploymentZone, occupied))
        {
            yield return grid;
        }
    }

    private IEnumerable<Vector2I> GetCampaignDeploymentZoneGrids(
        BattleDeploymentZone deploymentZone,
        HashSet<Vector2I> occupied)
    {
        if (_mapData == null)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (cell.DeploymentZone == deploymentZone && IsCampaignSpawnGridAvailable(cell.Grid, occupied))
                {
                    yield return cell.Grid;
                }
            }
        }
    }

    private bool IsCampaignReinforcementEntranceGrid(Vector2I grid, BattleDeploymentZone deploymentZone)
    {
        return _mapData != null &&
               grid.X >= 0 && grid.X < BattleMapData.Width &&
               grid.Y >= 0 && grid.Y < BattleMapData.Height &&
               _mapData.GetCell(grid.X, grid.Y).DeploymentZone == deploymentZone;
    }

    private bool IsCampaignSpawnGridAvailable(Vector2I grid, ISet<Vector2I> occupied)
    {
        return _mapData != null &&
               grid.X >= 0 && grid.X < BattleMapData.Width &&
               grid.Y >= 0 && grid.Y < BattleMapData.Height &&
               !_mapData.GetCell(grid.X, grid.Y).BlocksMovement &&
               !occupied.Contains(grid);
    }

    private void KeepBlockedReinforcementInReserve(CampaignBattleTeamData team)
    {
        if (_activeCampaign == null || team.ReinforcementOrderId <= 0)
        {
            return;
        }

        var order = _activeCampaign.Reinforcements.FirstOrDefault(item => item.Id == team.ReinforcementOrderId);
        if (order == null)
        {
            return;
        }

        var resourcesWereAvailable = order.Status == ReinforcementStatus.Deployed;
        team.Location = CampaignTeamLocation.Reserve;
        order.Status = ReinforcementStatus.Reserve;
        if (resourcesWereAvailable)
        {
            if (team.Side == CampaignBattleSide.Attacker)
            {
                _activeCampaign.AttackerGold = Math.Max(0, _activeCampaign.AttackerGold - order.Gold);
                _activeCampaign.AttackerFood = Math.Max(0, _activeCampaign.AttackerFood - order.Food);
            }
            else
            {
                _activeCampaign.DefenderGold = Math.Max(0, _activeCampaign.DefenderGold - order.Gold);
                _activeCampaign.DefenderFood = Math.Max(0, _activeCampaign.DefenderFood - order.Food);
            }
        }

        AppendBattleLog(
            team.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.AttackerName : BattleTeamIdentity.DefenderName,
            "Reinforcement",
            BattleFormat(
                "log.campaign.reinforcement_entrance_blocked",
                "Reinforcement entrance blocked; reserve retained: {0} ({1}).",
                GetCampaignReinforcementSideName(order),
                FormatCampaignReinforcementTeams(order)));
    }

    private void CreateCampaignMarker(Node2D unitLayer, Vector2I grid, CampaignBattleTeamData team)
    {
        var (troopType, category, label, moveRange, attackRange, hitPoints) = GetCampaignBattleProfile(team);
        var teamName = team.Side == CampaignBattleSide.Attacker
            ? BattleTeamIdentity.AttackerName
            : BattleTeamIdentity.DefenderName;
        var officer = CampaignRuntimeContext.World?.GetOfficer(team.OfficerId);
        var officerName = officer == null
            ? string.Empty
            : (!string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.NameZhHant : officer.Name);
        var marker = new BattlePieceMarker
        {
            Name = $"CampaignTeam{team.Id}"
        };
        unitLayer.AddChild(marker);
        var attacker = team.Side == CampaignBattleSide.Attacker;
        CreateMarker(
            marker,
            grid,
            label,
            $"{officerName} {troopType}".Trim(),
            category,
            teamName,
            officerName,
            troopType,
            hitPoints,
            attacker ? new Color("ad4832") : new Color("326b8d"),
            attacker ? new Color("f0d6a8") : new Color("e0f0ff"),
            moveRange: moveRange,
            attackRange: attackRange,
            campaignTeamId: team.Id,
            factionId: team.FactionId,
            controllerType: team.ControllerType);

        var gridKey = GetDefaultGridKey(grid);
        var occupant = _occupantsByGrid[gridKey].Last(item => item.CampaignTeamId == team.Id);
        var updated = occupant with
        {
            TroopCount = category == CategoryUnit ? team.ActiveTroops : occupant.TroopCount,
            HitPoints = category == CategoryUnit ? team.ActiveTroops : occupant.HitPoints,
            MaxHitPoints = category == CategoryUnit ? Math.Max(team.MaximumTroops, team.ActiveTroops + team.WoundedTroops) : occupant.MaxHitPoints,
            WoundedTroops = category == CategoryUnit ? team.WoundedTroops : 0,
            Morale = category == CategoryUnit ? team.Morale : occupant.Morale
        };
        ReplaceOccupantAtGrid(gridKey, occupant, updated);
        UpdateMarkerStrengthBar(updated);
    }

    private static (string TroopType, string Category, string Label, int Move, int Range, int HitPoints) GetCampaignBattleProfile(CampaignBattleTeamData team)
    {
        if (team.TroopType == TroopType.Siege)
        {
            return team.SiegeEngineType switch
            {
                SiegeEngineType.Ram => (TroopRam, CategorySiegeEngine, "R", 3, 1, RamMaxHitPoints),
                SiegeEngineType.Ladder => (TroopLadder, CategorySiegeEngine, "L", 3, 1, LadderMaxHitPoints),
                _ => (TroopCatapult, CategorySiegeEngine, "T", 2, 4, CatapultMaxHitPoints)
            };
        }

        return team.TroopType switch
        {
            TroopType.Spearman => (TroopSpearman, CategoryUnit, "S", 4, 1, team.ActiveTroops),
            TroopType.Cavalry => (TroopCavalry, CategoryUnit, "C", 6, 1, team.ActiveTroops),
            TroopType.Archer => (TroopArcher, CategoryUnit, "A", 4, 3, team.ActiveTroops),
            TroopType.Crossbow => (TroopCrossbow, CategoryUnit, "X", 4, 3, team.ActiveTroops),
            _ => (TroopInfantry, CategoryUnit, "I", 4, 1, team.ActiveTroops)
        };
    }

    private string GetCampaignReinforcementSideName(ReinforcementOrderData order)
    {
        var factionName = CampaignRuntimeContext.World == null
            ? FormatTeamName(order.Side == CampaignBattleSide.Attacker
                ? BattleTeamIdentity.AttackerName
                : BattleTeamIdentity.DefenderName)
            : _localization.GetFactionName(CampaignRuntimeContext.World, order.FactionId);
        return BattleFormat("ui.battle.reinforcement_side", "{0} reinforcements", factionName);
    }

    private string FormatCampaignReinforcementTeams(ReinforcementOrderData order)
    {
        return string.Join("、", order.Teams
            .Where(team => team.ActiveTroops > 0)
            .Select(team =>
            {
                var officer = CampaignRuntimeContext.World?.GetOfficer(team.OfficerId);
                var officerName = officer == null
                    ? BattleText("ui.unknown", "Unknown")
                    : FormatOfficerName(!string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.NameZhHant : officer.Name);
                var troopType = FormatTroopType(GetCampaignBattleProfile(team).TroopType);
                return BattleFormat("ui.battle.reinforcement_team", "{0}/{1} {2:N0}", officerName, troopType, team.ActiveTroops);
            }));
    }

    private void AnnounceCampaignReinforcementArrivals(ISet<int> arrivingOrderIds)
    {
        if (_activeCampaign == null || arrivingOrderIds.Count == 0)
        {
            return;
        }

        var arrivals = _activeCampaign.Reinforcements
            .Where(order => arrivingOrderIds.Contains(order.Id) &&
                            order.Status is ReinforcementStatus.Deployed or ReinforcementStatus.Reserve)
            .OrderBy(order => order.Id)
            .ToList();
        if (arrivals.Count == 0)
        {
            return;
        }

        // AdvanceCompletedBattleDay applies every newly-deployed order before this
        // presentation pass. Rebuild the per-side running balance so each log row
        // reports its own prior value, addition, and resulting value rather than
        // subtracting only one order from the already-final total.
        var attackerGold = _teamAGold - arrivals
            .Where(order => order.Status == ReinforcementStatus.Deployed && order.Side == CampaignBattleSide.Attacker)
            .Sum(order => order.Gold);
        var attackerFood = _teamAFood - arrivals
            .Where(order => order.Status == ReinforcementStatus.Deployed && order.Side == CampaignBattleSide.Attacker)
            .Sum(order => order.Food);
        var defenderGold = _teamBGold - arrivals
            .Where(order => order.Status == ReinforcementStatus.Deployed && order.Side == CampaignBattleSide.Defender)
            .Sum(order => order.Gold);
        var defenderFood = _teamBFood - arrivals
            .Where(order => order.Status == ReinforcementStatus.Deployed && order.Side == CampaignBattleSide.Defender)
            .Sum(order => order.Food);

        foreach (var order in arrivals)
        {
            var sideName = GetCampaignReinforcementSideName(order);
            var teams = FormatCampaignReinforcementTeams(order);
            AppendBattleLog(
                order.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.AttackerName : BattleTeamIdentity.DefenderName,
                "Reinforcement",
                BattleFormat(
                    "log.campaign.reinforcement_arrived",
                    "Reinforcements arrived: {0}; teams: {1}; gold {2:N0}, food {3:N0}.",
                    sideName,
                    teams,
                    order.Gold,
                    order.Food));
            if (order.Status == ReinforcementStatus.Deployed)
            {
                var previousGold = order.Side == CampaignBattleSide.Attacker ? attackerGold : defenderGold;
                var previousFood = order.Side == CampaignBattleSide.Attacker ? attackerFood : defenderFood;
                var finalGold = previousGold + order.Gold;
                var finalFood = previousFood + order.Food;
                AppendBattleLog(
                    order.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.AttackerName : BattleTeamIdentity.DefenderName,
                    "Reinforcement",
                    BattleFormat(
                        "log.campaign.reinforcement_resources_applied",
                        "Reinforcement supplies applied: gold {0:N0} + {1:N0} = {2:N0}; food {3:N0} + {4:N0} = {5:N0}.",
                        previousGold, order.Gold, finalGold,
                        previousFood, order.Food, finalFood));
                if (order.Side == CampaignBattleSide.Attacker)
                {
                    attackerGold = finalGold;
                    attackerFood = finalFood;
                }
                else
                {
                    defenderGold = finalGold;
                    defenderFood = finalFood;
                }
                ApplyTeamMoraleBonus(
                    order.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.AttackerName : BattleTeamIdentity.DefenderName,
                    ReinforcementArrivalFriendlyMoraleBonus,
                    BattleText("log.campaign.reinforcement_morale_friendly", "reinforcements arrived"));
                ApplyTeamMoralePenalty(
                    order.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.DefenderName : BattleTeamIdentity.AttackerName,
                    ReinforcementArrivalOpponentMoralePenalty,
                    BattleText("log.campaign.reinforcement_morale_opponent", "enemy reinforcements arrived"));
            }
        }

        ShowCampaignReinforcementArrivalNotice(FormatCampaignReinforcementArrivalNotice(arrivals));
    }

    private string FormatCampaignReinforcementArrivalNotice(IReadOnlyCollection<ReinforcementOrderData> arrivals)
    {
        var lines = arrivals
            .GroupBy(order => new { order.Side, order.FactionId })
            .OrderBy(group => group.Key.Side)
            .ThenBy(group => group.Key.FactionId)
            .Select(group =>
            {
                var representative = group.First();
                var teams = string.Join("、", group.SelectMany(order => order.Teams)
                    .Where(team => team.ActiveTroops > 0)
                    .Select(team =>
                    {
                        var officer = CampaignRuntimeContext.World?.GetOfficer(team.OfficerId);
                        var officerName = officer == null
                            ? BattleText("ui.unknown", "Unknown")
                            : FormatOfficerName(!string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.NameZhHant : officer.Name);
                        return BattleFormat(
                            "ui.battle.reinforcement_team",
                            "{0}/{1} {2:N0}",
                            officerName,
                            FormatTroopType(GetCampaignBattleProfile(team).TroopType),
                            team.ActiveTroops);
                    }));
                return BattleFormat(
                    "ui.battle.reinforcement_arrival_group",
                    "{0}: {1}",
                    GetCampaignReinforcementSideName(representative),
                    teams);
            });
        return BattleFormat(
            "ui.battle.reinforcement_arrival_batch",
            "Reinforcements arrived ({0})\n{1}",
            arrivals.Count,
            string.Join("\n", lines));
    }

    private void HandleCampaignCompletedDay()
    {
        if (_activeCampaign == null || _campaignReturnQueued)
        {
            return;
        }

        SyncCampaignFromBattle();
        var arrivingOrderIds = _activeCampaign.Reinforcements
            .Where(order => order.Status == ReinforcementStatus.Reserve ||
                            (order.Status == ReinforcementStatus.Traveling && order.RemainingBattleDays <= 1))
            .Select(order => order.Id)
            .ToHashSet();
        BattleCampaignService.AdvanceCompletedBattleDay(_activeCampaign);
        SyncCampaignResourcesToHud();
        if (GetNodeOrNull<Node2D>("MapRoot/UnitLayer") is { } unitLayer)
        {
            DeployCampaignTeams(unitLayer);
        }
        AnnounceCampaignReinforcementArrivals(arrivingOrderIds);
        ConfigureHud();
        if (!BattleCampaignService.HasReachedMonthlyBattleLimit(_activeCampaign, GetMonthlyBattleDayLimit()))
        {
            return;
        }

        _activeCampaign.BattleSnapshotJson = CreateCampaignBattleSnapshot();
        _campaignMonthLimitReached = true;
        if (CampaignRuntimeContext.World != null)
        {
            // A monthly pause returns control to Gameplay.  Do not resume the
            // original attack-resolution chain, or it would advance the month
            // and immediately launch this campaign again before the map is seen.
            CampaignRuntimeContext.World.ResumeAttackResolutionAfterCampaign = false;
            CampaignRuntimeContext.World.IsBattleResolutionPhase = true;
        }
        AppendBattleLog(
            "Battle",
            "Campaign",
            BattleFormat(
                "log.campaign.month_limit_reached",
                "Monthly battle limit reached: battle suspended after {0} days.",
                GetMonthlyBattleDayLimit()));
    }

    private void HandleStandaloneTestCompletedDay()
    {
        if (_activeCampaign != null ||
            _debugMonthlyBattleDayLimit <= 0 ||
            _standaloneMonthLimitReached ||
            _battleDateDay <= _debugMonthlyBattleDayLimit)
        {
            return;
        }

        _standaloneMonthLimitReached = true;
        AppendBattleLog(
            "Battle",
            "Debug",
            BattleText(
                "log.debug_month_limit_test_reached",
                "Debug: the standalone test reached its monthly battle limit."));
    }

    private void SyncCampaignFromBattle()
    {
        if (_activeCampaign == null)
        {
            return;
        }

        var occupants = _occupantsByGrid.Values.SelectMany(items => items)
            .Where(item => item.CampaignTeamId > 0)
            .ToDictionary(item => item.CampaignTeamId);
        foreach (var team in _activeCampaign.Teams.Where(team => team.Location == CampaignTeamLocation.Field ||
                                                                  (_activeCampaign.Stage == CampaignStage.CityBattle && team.Location == CampaignTeamLocation.InnerCity)))
        {
            if (!occupants.TryGetValue(team.Id, out var occupant))
            {
                team.ActiveTroops = 0;
                team.Location = CampaignTeamLocation.Eliminated;
                continue;
            }

            if (occupant.Category == CategoryUnit)
            {
                team.ActiveTroops = Math.Max(0, occupant.TroopCount);
                team.WoundedTroops = Math.Max(0, occupant.WoundedTroops);
                team.MaximumTroops = Math.Max(team.MaximumTroops, team.ActiveTroops + team.WoundedTroops);
                team.Morale = occupant.Morale ?? team.Morale;
            }
        }

        _activeCampaign.AttackerGold = _teamAGold;
        _activeCampaign.AttackerFood = _teamAFood;
        _activeCampaign.DefenderGold = _teamBGold;
        _activeCampaign.DefenderFood = _teamBFood;
    }

    private void SyncCampaignResourcesToHud()
    {
        if (_activeCampaign == null)
        {
            return;
        }

        _teamAGold = _activeCampaign.AttackerGold;
        _teamAFood = _activeCampaign.AttackerFood;
        _teamBGold = _activeCampaign.DefenderGold;
        _teamBFood = _activeCampaign.DefenderFood;
    }

    private bool HandleCampaignBattleFinished()
    {
        if (_activeCampaign == null || _campaignResultHandled || CampaignRuntimeContext.World == null)
        {
            return false;
        }

        _campaignResultHandled = true;
        SyncCampaignFromBattle();
        var attackerWon = HasActiveOfficerBattleTeam(isDefender: false) || _attackerOutpostVictorySecured;
        if (_activeCampaign.Stage == CampaignStage.FieldBattle && attackerWon)
        {
            _activeCampaign.AttackerWonFieldBattle = true;
            _activeCampaign.BattleSnapshotJson = string.Empty;
            if (BattleCampaignService.HasEffectiveInnerCityDefender(_activeCampaign))
            {
                _activeCampaign.IsAwaitingPlayerDecision = true;
                ShowPostFieldBattleDecision();
                return true;
            }

            _activeCampaign.Stage = CampaignStage.CityBattle;
        }

        BattleCampaignService.CompleteCampaign(
            CampaignRuntimeContext.World,
            _activeCampaign,
            attackerWon ? CampaignBattleSide.Attacker : CampaignBattleSide.Defender);
        QueueCampaignReturnToGameplay();
        return true;
    }

    private void ShowPostFieldBattleDecision()
    {
        if (_campaignDecisionPanel != null)
        {
            _campaignDecisionPanel.Visible = true;
            return;
        }

        var canvas = new CanvasLayer { Layer = 200 };
        AddChild(canvas);
        var panel = new PanelContainer
        {
            Position = new Vector2(420, 210),
            CustomMinimumSize = new Vector2(430, 250)
        };
        canvas.AddChild(panel);
        _campaignDecisionPanel = new VBoxContainer();
        panel.AddChild(_campaignDecisionPanel);
        _campaignDecisionPanel.AddChild(new Label
        {
            Text = BattleText("ui.battle.campaign_field_victory", "Field victory: choose the next operation"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        AddCampaignDecisionButton(BattleText("ui.battle.campaign_assault_now", "Assault now"), PostFieldBattleDecision.ImmediateAssault);
        AddCampaignDecisionButton(BattleText("ui.battle.campaign_prepare_next_month", "Prepare next month"), PostFieldBattleDecision.PrepareNextMonthSiege);
        AddCampaignDecisionButton(BattleText("ui.battle.campaign_withdraw", "Withdraw"), PostFieldBattleDecision.Withdraw);
    }

    private void AddCampaignDecisionButton(string text, PostFieldBattleDecision decision)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(390, 48) };
        button.Pressed += () => OnPostFieldBattleDecision(decision);
        _campaignDecisionPanel?.AddChild(button);
    }

    private void OnPostFieldBattleDecision(PostFieldBattleDecision decision)
    {
        if (_activeCampaign == null || CampaignRuntimeContext.World == null)
        {
            return;
        }

        BattleCampaignService.ApplyPostFieldDecision(_activeCampaign, decision);
        if (decision == PostFieldBattleDecision.ImmediateAssault)
        {
            _activeCampaign.BattleSnapshotJson = string.Empty;
            var targetCity = CampaignRuntimeContext.World.GetCity(_activeCampaign.TargetCityId);
            PendingLaunchOptions = new LaunchOptions(BattleScenarioType.SiegeAssault, true, _activeCampaign.Id);
            var scenePath = targetCity?.Id % 2 == 0
                ? "res://scenes/battle/BattleSceneNorthWest.tscn"
                : "res://scenes/battle/BattleScene.tscn";
            GetTree().ChangeSceneToFile(scenePath);
            return;
        }
        if (decision == PostFieldBattleDecision.Withdraw)
        {
            BattleCampaignService.CompleteCampaign(CampaignRuntimeContext.World, _activeCampaign, CampaignBattleSide.Defender);
        }
        QueueCampaignReturnToGameplay();
    }

    private void HandleCampaignTeamRetreat(BattleOccupantInfo unit)
    {
        if (_activeCampaign == null || unit.CampaignTeamId <= 0)
        {
            return;
        }

        var team = _activeCampaign.Teams.FirstOrDefault(item => item.Id == unit.CampaignTeamId);
        if (team == null)
        {
            return;
        }

        team.ActiveTroops = Math.Max(0, unit.TroopCount);
        team.WoundedTroops = Math.Max(0, unit.WoundedTroops);
        team.Location = team.Side == CampaignBattleSide.Defender && _activeCampaign.Stage == CampaignStage.FieldBattle
            ? CampaignTeamLocation.InnerCity
            : CampaignTeamLocation.NeighborCity;
    }

    private void HandleCampaignTeamRemoved(BattleOccupantInfo unit)
    {
        if (_activeCampaign == null || unit.CampaignTeamId <= 0)
        {
            return;
        }

        var team = _activeCampaign.Teams.FirstOrDefault(item => item.Id == unit.CampaignTeamId);
        if (team == null || team.Location is CampaignTeamLocation.InnerCity or CampaignTeamLocation.NeighborCity)
        {
            return;
        }

        team.ActiveTroops = 0;
        team.WoundedTroops = Math.Max(team.WoundedTroops, unit.WoundedTroops);
        team.Location = CampaignTeamLocation.Eliminated;
    }

    private void QueueCampaignReturnToGameplay()
    {
        if (_campaignReturnQueued)
        {
            return;
        }

        _campaignReturnQueued = true;
        if (CampaignRuntimeContext.World != null)
        {
            // A completed battle returns to the strategic battle-resolution phase.
            // The player acknowledges its report and explicitly advances to the next
            // same-month battle instead of immediately chaining scenes.
            CampaignRuntimeContext.World.ResumeAttackResolutionAfterCampaign = false;
            CampaignRuntimeContext.World.IsBattleResolutionPhase = true;
        }
        CampaignRuntimeContext.ReturnToGameplay();
        CallDeferred(nameof(ReturnCampaignToGameplay));
    }

    private async void QueueCampaignReturnToGameplayAfterMonthLimitResult()
    {
        if (_campaignReturnQueued)
        {
            return;
        }

        _campaignReturnQueued = true;
        CampaignRuntimeContext.ReturnToGameplay();
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        if (IsInsideTree())
        {
            ReturnCampaignToGameplay();
        }
    }

    private void ReturnCampaignToGameplay()
    {
        GetTree().ChangeSceneToFile(GameplayScenePath);
    }
}
