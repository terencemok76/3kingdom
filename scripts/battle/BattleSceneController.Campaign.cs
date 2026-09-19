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
        var ruler = CampaignRuntimeContext.World.GetFaction(factionId) is { } faction
            ? CampaignRuntimeContext.World.GetOfficer(faction.RulerOfficerId)
            : null;
        var displayName = ruler == null
            ? _localization.GetFactionName(CampaignRuntimeContext.World, factionId)
            : _localization.GetOfficerName(ruler);
        return displayName;
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
                var spawn = GetCampaignSpawnGrids(team, occupied)
                    .Select(grid => (Found: true, Grid: grid))
                    .FirstOrDefault();
                if (!spawn.Found)
                {
                    KeepBlockedReinforcementInReserve(team);
                    continue;
                }

                CreateCampaignMarker(unitLayer, spawn.Grid, team);
                occupied.Add(spawn.Grid);
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
            if (_activeCampaign?.Stage == CampaignStage.FieldBattle)
            {
                foreach (var grid in configuredGrids
                             .Where(grid => IsFieldBattleReinforcementEdgeGrid(grid, team.Side))
                             .Where(grid => IsCampaignSpawnGridAvailable(grid, occupied)))
                {
                    yield return grid;
                }

                foreach (var grid in GetFieldBattleReinforcementEdgeGrids(team.Side)
                             .Where(grid => !configuredGrids.Contains(grid))
                             .Where(grid => IsCampaignSpawnGridAvailable(grid, occupied)))
                {
                    yield return grid;
                }
                yield break;
            }

            foreach (var grid in configuredGrids)
            {
                if (IsCampaignReinforcementEntranceGrid(grid, team.Side) &&
                    IsCampaignSpawnGridAvailable(grid, occupied))
                {
                    yield return grid;
                }
            }

            if (_activeCampaign?.Stage == CampaignStage.CityBattle &&
                team.Side == CampaignBattleSide.Defender)
            {
                foreach (var grid in GetCityBattleDefenderReinforcementEdgeGrids(occupied)
                             .Where(grid => !configuredGrids.Contains(grid)))
                {
                    yield return grid;
                }
                yield break;
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

    private static IEnumerable<Vector2I> GetDefaultFieldBattleReinforcementEdgeGrids(CampaignBattleSide side)
    {
        const int entranceDepth = 2;
        const int entranceWidth = 4;
        var startX = side == CampaignBattleSide.Attacker ? 0 : BattleMapData.Width - entranceWidth;
        var startY = side == CampaignBattleSide.Attacker ? 0 : BattleMapData.Height - entranceDepth;
        for (var y = startY; y < startY + entranceDepth; y += 1)
        {
            for (var x = startX; x < startX + entranceWidth; x += 1)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    private static IEnumerable<Vector2I> GetFieldBattleReinforcementEdgeGrids(CampaignBattleSide side)
    {
        var yielded = new HashSet<Vector2I>();
        foreach (var grid in GetDefaultFieldBattleReinforcementEdgeGrids(side))
        {
            if (yielded.Add(grid))
            {
                yield return grid;
            }
        }

        // The scene determines which edge cells are traversable.  After the preferred
        // corner entrance, scan the same outer two-cell border so mountains, water, or
        // authored obstacles never force a reinforcement beside the initial formation.
        if (side == CampaignBattleSide.Attacker)
        {
            for (var x = 0; x < BattleMapData.Width; x += 1)
            {
                for (var y = 0; y < 2; y += 1)
                {
                    var grid = new Vector2I(x, y);
                    if (yielded.Add(grid))
                    {
                        yield return grid;
                    }
                }
            }
            for (var y = 0; y < BattleMapData.Height; y += 1)
            {
                for (var x = 0; x < 2; x += 1)
                {
                    var grid = new Vector2I(x, y);
                    if (yielded.Add(grid))
                    {
                        yield return grid;
                    }
                }
            }
            yield break;
        }

        for (var x = BattleMapData.Width - 1; x >= 0; x -= 1)
        {
            for (var y = BattleMapData.Height - 1; y >= BattleMapData.Height - 2; y -= 1)
            {
                var grid = new Vector2I(x, y);
                if (yielded.Add(grid))
                {
                    yield return grid;
                }
            }
        }
        for (var y = BattleMapData.Height - 1; y >= 0; y -= 1)
        {
            for (var x = BattleMapData.Width - 1; x >= BattleMapData.Width - 2; x -= 1)
            {
                var grid = new Vector2I(x, y);
                if (yielded.Add(grid))
                {
                    yield return grid;
                }
            }
        }
    }

    private static bool IsFieldBattleReinforcementEdgeGrid(Vector2I grid, CampaignBattleSide side)
    {
        return side == CampaignBattleSide.Attacker
            ? grid.X is >= 0 and < 2 || grid.Y is >= 0 and < 2
            : grid.X is >= BattleMapData.Width - 2 and < BattleMapData.Width ||
              grid.Y is >= BattleMapData.Height - 2 and < BattleMapData.Height;
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

    private IEnumerable<Vector2I> GetCityBattleDefenderReinforcementEdgeGrids(HashSet<Vector2I> occupied)
    {
        if (_mapData == null)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y += 1)
        {
            for (var x = 0; x < BattleMapData.Width; x += 1)
            {
                var gate = _mapData.GetCell(x, y);
                if (gate.Structure != BattleStructureType.Gate)
                {
                    continue;
                }

                if (gate.StructureFacing == BattleStructureFacing.NorthWest)
                {
                    for (var edgeY = 0; edgeY < BattleMapData.Height; edgeY += 1)
                    {
                        var grid = new Vector2I(BattleMapData.Width - 1, edgeY);
                        if (IsCampaignReinforcementEntranceGrid(grid, CampaignBattleSide.Defender) &&
                            IsCampaignSpawnGridAvailable(grid, occupied))
                        {
                            yield return grid;
                        }
                    }
                    continue;
                }

                for (var edgeY = gate.Grid.Y + 1; edgeY < BattleMapData.Height; edgeY += 1)
                {
                    foreach (var edgeX in new[] { 0, BattleMapData.Width - 1 })
                    {
                        var grid = new Vector2I(edgeX, edgeY);
                        if (IsCampaignReinforcementEntranceGrid(grid, CampaignBattleSide.Defender) &&
                            IsCampaignSpawnGridAvailable(grid, occupied))
                        {
                            yield return grid;
                        }
                    }
                }
            }
        }
    }

    private bool IsCampaignReinforcementEntranceGrid(Vector2I grid, CampaignBattleSide side)
    {
        if (_mapData == null ||
            grid.X < 0 || grid.X >= BattleMapData.Width ||
            grid.Y < 0 || grid.Y >= BattleMapData.Height)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (side == CampaignBattleSide.Attacker)
        {
            return cell.DeploymentZone == BattleDeploymentZone.Attacker;
        }

        if (_activeCampaign?.Stage == CampaignStage.CityBattle)
        {
            // The defender's authored deployment zone is the inner courtyard.
            // Relief forces must be outside the wall, even when a scenario
            // supplies custom entrance coordinates.
            return cell.DeploymentZone == BattleDeploymentZone.None &&
                   !cell.HideGroundOccupantWithForeground &&
                   IsOutsideCityWall(grid);
        }

        return cell.DeploymentZone == BattleDeploymentZone.Defender;
    }

    private bool IsOutsideCityWall(Vector2I grid)
    {
        if (_mapData == null)
        {
            return false;
        }

        for (var y = 0; y < BattleMapData.Height; y += 1)
        {
            for (var x = 0; x < BattleMapData.Width; x += 1)
            {
                var gate = _mapData.GetCell(x, y);
                if (gate.Structure != BattleStructureType.Gate)
                {
                    continue;
                }

                if (gate.StructureFacing == BattleStructureFacing.NorthWest
                    ? grid.X > gate.Grid.X
                    : grid.Y > gate.Grid.Y)
                {
                    return true;
                }
            }
        }

        return false;
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

        var deferredMoralePopups = new List<(BattleGridKey Grid, int MoraleDelta)>();
        var deployedReinforcementOrderIds = arrivals
            .Where(order => order.Status == ReinforcementStatus.Deployed)
            .Select(order => order.Id)
            .ToHashSet();
        var deployedReinforcementTeamIds = _activeCampaign.Teams
            .Where(team => deployedReinforcementOrderIds.Contains(team.ReinforcementOrderId))
            .Select(team => team.Id)
            .ToHashSet();
        var arrivingOfficerSpeakers = _occupantsByGrid.Values
            .SelectMany(occupants => occupants)
            .Where(occupant => occupant.Category == CategoryUnit &&
                               !string.IsNullOrWhiteSpace(occupant.OfficerName) &&
                               deployedReinforcementTeamIds.Contains(occupant.CampaignTeamId))
            .ToList();
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
                    BattleText("log.campaign.reinforcement_morale_friendly", "reinforcements arrived"),
                    deferredPopups: deferredMoralePopups);
                ApplyTeamMoralePenalty(
                    order.Side == CampaignBattleSide.Attacker ? BattleTeamIdentity.DefenderName : BattleTeamIdentity.AttackerName,
                    ReinforcementArrivalOpponentMoralePenalty,
                    BattleText("log.campaign.reinforcement_morale_opponent", "enemy reinforcements arrived"),
                    deferredPopups: deferredMoralePopups);
            }
        }

        ShowCampaignReinforcementArrivalNotice(
            FormatCampaignReinforcementArrivalNotice(arrivals),
            deferredMoralePopups,
            arrivingOfficerSpeakers);
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

    // Returns true whenever this is a campaign retreat, including a blocked or
    // player-prompted retreat.  Standalone test battles keep their old behaviour.
    private bool TryHandleCampaignRetreat(BattleOccupantInfo unit, BattleGridKey grid)
    {
        if (_activeCampaign == null || CampaignRuntimeContext.World == null || unit.CampaignTeamId <= 0)
        {
            return false;
        }

        var team = _activeCampaign.Teams.FirstOrDefault(item => item.Id == unit.CampaignTeamId);
        if (team == null)
        {
            return false;
        }

        // Battle damage is held on the live occupant until the campaign snapshot
        // sync.  Copy it before a retreat writes the team back to a city.
        team.ActiveTroops = Math.Max(0, unit.TroopCount);
        team.WoundedTroops = Math.Max(0, unit.WoundedTroops);

        if (team.OfficerId <= 0)
        {
            if (!BattleCampaignService.TryReturnSupportTeamToOrigin(CampaignRuntimeContext.World, team))
            {
                ShowBattleEventNotice(BattleText(
                    "ui.battle.retreat_no_destination",
                    "No city is available for this retreat."));
                AppendBattleLog(unit, "Retreat", $"{FormatLogUnit(unit)} cannot retreat: no valid origin city.");
                MarkCampaignRetreatBlockedForAi(unit);
                return true;
            }

            CompleteSelectedRetreat(unit, grid, campaignReturnHandled: true);
            return true;
        }

        var destinations = BattleCampaignService.GetRetreatDestinations(CampaignRuntimeContext.World, _activeCampaign, team);
        if (destinations.Count == 0)
        {
            ShowBattleEventNotice(BattleText(
                "ui.battle.retreat_no_destination",
                "No city is available for this retreat."));
            AppendBattleLog(unit, "Retreat", $"{FormatLogUnit(unit)} cannot retreat: no adjacent friendly or neutral city.");
            MarkCampaignRetreatBlockedForAi(unit);
            return true;
        }

        if (team.ControllerType == CampaignControllerType.Player)
        {
            ShowCampaignRetreatDestinationDialog(unit, grid, team, destinations);
            return true;
        }

        // AI prefers an existing friendly city with the strongest garrison; it
        // only claims a neutral neighbour when no friendly refuge exists.
        var destination = destinations
            .OrderBy(city => city.OwnerFactionId == team.FactionId ? 0 : 1)
            .ThenByDescending(city => city.Troops + city.Defense * 10)
            .ThenBy(city => city.Id)
            .First();
        if (BattleCampaignService.TryRetreatTeamToCity(CampaignRuntimeContext.World, _activeCampaign, team, destination.Id))
        {
            AppendBattleLog(unit, "Retreat", $"{FormatLogUnit(unit)} retreats to {destination.NameZhHant}.");
            CompleteSelectedRetreat(unit, grid, campaignReturnHandled: true);
        }

        return true;
    }

    private void MarkCampaignRetreatBlockedForAi(BattleOccupantInfo unit)
    {
        if (IsCurrentTurnAiControlled())
        {
            MarkUnitActed(unit);
        }
    }

    private void ShowCampaignRetreatDestinationDialog(
        BattleOccupantInfo unit,
        BattleGridKey grid,
        CampaignBattleTeamData team,
        IReadOnlyList<CityData> destinations)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/main/RetreatDestinationDialog.tscn");
        var overlay = scene?.Instantiate<Control>();
        var uiLayer = GetNodeOrNull<CanvasLayer>("UiLayer");
        if (overlay == null || uiLayer == null)
        {
            ShowBattleEventNotice(BattleText("ui.battle.retreat_no_destination", "No city is available for this retreat."));
            return;
        }

        var root = overlay.GetNodeOrNull<VBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot");
        var panel = overlay.GetNodeOrNull<PanelContainer>("CenterContainer/AdvisorDialogPanel");
        var centerContainer = overlay.GetNodeOrNull<Control>("CenterContainer");
        var titleBar = root?.GetNodeOrNull<Control>("TitleBarPanel/TitleBar");
        var titleLabel = root?.GetNodeOrNull<Label>("TitleBarPanel/TitleBar/TitleLabel");
        var titleCloseButton = root?.GetNodeOrNull<Button>("TitleBarPanel/TitleBar/CloseButton");
        var promptLabel = root?.GetNodeOrNull<Label>("PromptLabel");
        var destinationContainer = root?.GetNodeOrNull<VBoxContainer>("DestinationScroll/Destinations");
        var noDestinationLabel = root?.GetNodeOrNull<Label>("DestinationScroll/Destinations/NoDestinationLabel");
        var cancelButton = root?.GetNodeOrNull<Button>("ButtonRow/CancelButton");
        if (root == null || panel == null || centerContainer == null || titleBar == null || promptLabel == null || destinationContainer == null || noDestinationLabel == null || cancelButton == null)
        {
            overlay.QueueFree();
            ShowBattleEventNotice(BattleText("ui.battle.retreat_no_destination", "No city is available for this retreat."));
            return;
        }

        uiLayer.AddChild(overlay);
        overlay.ZIndex = 220;
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        root.MouseFilter = Control.MouseFilterEnum.Stop;
        titleBar.MouseFilter = Control.MouseFilterEnum.Stop;
        titleBar.MouseDefaultCursorShape = Control.CursorShape.Drag;
        panel.Size = panel.CustomMinimumSize;
        var viewportSize = overlay.GetViewportRect().Size;
        panel.Position = new Vector2(
            Mathf.Max(0.0f, (viewportSize.X - panel.Size.X) * 0.5f),
            Mathf.Max(0.0f, (viewportSize.Y - panel.Size.Y) * 0.5f));

        var isDragging = false;
        var dragOffset = Vector2.Zero;
        titleBar.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    isDragging = true;
                    dragOffset = mouseButton.GlobalPosition - panel.GlobalPosition;
                }
                else
                {
                    isDragging = false;
                }

                titleBar.AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion mouseMotion && isDragging)
            {
                var maxX = Mathf.Max(0.0f, overlay.GetViewportRect().Size.X - panel.Size.X);
                var maxY = Mathf.Max(0.0f, overlay.GetViewportRect().Size.Y - panel.Size.Y);
                var target = mouseMotion.GlobalPosition - dragOffset;
                panel.Position = new Vector2(
                    Mathf.Clamp(target.X, 0.0f, maxX),
                    Mathf.Clamp(target.Y, 0.0f, maxY));
                titleBar.AcceptEvent();
            }
        };
        panel.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton or InputEventMouseMotion)
            {
                panel.AcceptEvent();
            }
        };

        if (titleLabel != null)
        {
            titleLabel.Text = BattleText("ui.battle.retreat_destination_title", "Choose retreat destination");
        }
        var retreatOriginCityId = BattleCampaignService.GetRetreatOriginCityId(_activeCampaign!, team);
        var retreatFactionId = CampaignRuntimeContext.World == null
            ? team.FactionId
            : BattleCampaignService.GetRetreatFactionId(CampaignRuntimeContext.World, team);
        var defenderAlreadyInOriginCity = team.Side == CampaignBattleSide.Defender &&
                                           retreatOriginCityId == _activeCampaign!.TargetCityId;
        promptLabel.Text = BattleText(
            defenderAlreadyInOriginCity
                ? "ui.battle.retreat_destination_prompt_defender"
                : "ui.battle.retreat_destination_prompt",
            "Choose an adjacent friendly or neutral city.");
        noDestinationLabel.Visible = destinations.Count == 0;
        noDestinationLabel.Text = BattleText("ui.battle.retreat_no_destination", "No city is available for this retreat.");
        cancelButton.Text = BattleText("ui.battle.retreat_cancel", "Cancel");
        ApplyBattleOptionButtonStyle(cancelButton);

        void Close()
        {
            overlay.QueueFree();
        }

        cancelButton.Pressed += Close;
        if (titleCloseButton != null)
        {
            titleCloseButton.Pressed += Close;
        }

        foreach (var city in destinations)
        {
            var destination = city;
            var button = new Button
            {
                CustomMinimumSize = new Vector2(0.0f, 42.0f),
                Text = destination.Id == retreatOriginCityId
                    ? $"{BattleText("ui.battle.retreat_return_origin", "Return to origin")}：{destination.NameZhHant}"
                    : destination.OwnerFactionId == retreatFactionId
                    ? $"{BattleText("ui.battle.retreat", "Retreat")}：{destination.NameZhHant}"
                    : $"{BattleText("ui.battle.retreat", "Retreat")}：{destination.NameZhHant}（{BattleText("ui.battle.retreat_neutral", "Neutral")}）"
            };
            ApplyBattleOptionButtonStyle(button);
            button.Pressed += () =>
            {
                if (CampaignRuntimeContext.World != null && _activeCampaign != null &&
                    BattleCampaignService.TryRetreatTeamToCity(CampaignRuntimeContext.World, _activeCampaign, team, destination.Id))
                {
                    AppendBattleLog(unit, "Retreat", $"{FormatLogUnit(unit)} retreats to {destination.NameZhHant}.");
                    Close();
                    CompleteSelectedRetreat(unit, grid, campaignReturnHandled: true);
                }
            };
            destinationContainer.AddChild(button);
        }
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
