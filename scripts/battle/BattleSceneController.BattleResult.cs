using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void RefreshBattleResultState()
    {
        if (!TryBuildBattleResultMessage(out var resultMessage))
        {
            _isBattleFinished = false;
            if (_battleResultOverlay != null)
            {
                _battleResultOverlay.Visible = false;
            }
            if (_endTurnButton != null)
            {
                _endTurnButton.Disabled = false;
            }
            return;
        }

        var wasFinished = _isBattleFinished;
        _isBattleFinished = true;
        HideCommandMenu();
        CancelCommandAction(clearSelection: false);
        if (_battleResultLabel != null)
        {
            _battleResultLabel.Text = resultMessage;
        }
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.Visible = true;
        }
        if (_endTurnButton != null)
        {
            _endTurnButton.Disabled = true;
        }
        if (!wasFinished)
        {
            AppendBattleLog("Battle", "Result", resultMessage.Replace('\n', ' '));
        }
    }

    private bool TryBuildBattleResultMessage(out string resultMessage)
    {
        resultMessage = string.Empty;
        if (_mapData == null)
        {
            return false;
        }

        var teamAHasOfficerBattleTeam = HasActiveOfficerBattleTeam(isDefender: false);
        var teamBHasOfficerBattleTeam = HasActiveOfficerBattleTeam(isDefender: true);
        if (!teamAHasOfficerBattleTeam && !teamBHasOfficerBattleTeam)
        {
            resultMessage = "Battle Finished\nDraw\nBoth sides have no officer-led battle teams.";
            return true;
        }

        if (!teamAHasOfficerBattleTeam || !teamBHasOfficerBattleTeam)
        {
            var winnerName = teamAHasOfficerBattleTeam ? TeamAInfo.Name : TeamBInfo.Name;
            var defeatedName = teamAHasOfficerBattleTeam ? TeamBInfo.Name : TeamAInfo.Name;
            resultMessage = $"Battle Finished\n{winnerName} Victory\n{defeatedName} has no officer-led battle teams remaining.";
            return true;
        }

        if (_attackerOutpostVictorySecured)
        {
            resultMessage = $"Battle Finished\n{TeamAInfo.Name} Victory\nAll defense outposts are occupied and held.";
            return true;
        }

        return false;
    }

    private bool HasActiveOfficerBattleTeam(bool isDefender)
    {
        return _occupantsByGrid.Values
            .SelectMany(static occupants => occupants)
            .Any(occupant => IsDefenderTeam(occupant) == isDefender &&
                             IsGeneralCountedPiece(occupant.Category, occupant.OfficerName));
    }

    private bool DoesAttackerHoldAllDefenseOutposts()
    {
        if (_mapData?.ScenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle)
        {
            return false;
        }

        var outpostGrids = new List<Vector2I>();
        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (cell.IsDefenseOutpost)
                {
                    outpostGrids.Add(cell.Grid);
                }
            }
        }

        return outpostGrids.Count > 0 && outpostGrids.All(grid =>
            _mapData.GetCell(grid.X, grid.Y).DefenseOutpostOwner == BattleOutpostOwner.Attacker);
    }

    private static bool IsDefenderTeam(BattleOccupantInfo occupant)
    {
        return IsDefenderTeamName(occupant.TeamName);
    }

    private static bool IsDefenderTeamName(string teamName)
    {
        return BattleTeamIdentity.IsDefender(teamName);
    }

    private void ConfirmAttackerOutpostVictoryAtTurnEnd()
    {
        if (_currentTurnSide == BattleTurnSide.TeamB && DoesAttackerHoldAllDefenseOutposts())
        {
            _attackerOutpostVictorySecured = true;
        }
    }





}
