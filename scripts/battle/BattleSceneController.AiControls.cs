namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool IsFieldBattleAiTest => ScenarioType == BattleScenarioType.FieldBattle;

    private void ConfigureFieldAiTestControls()
    {
        var isFieldBattle = IsFieldBattleAiTest;
        var isAiSide = IsCurrentTurnAiControlled();
        var allActed = HaveAllActingBattlePiecesActed();

        if (_enableAiButton != null)
        {
            _enableAiButton.Visible = isFieldBattle;
            _enableAiButton.Disabled = !isFieldBattle || _isFieldAiRoundStarted || isAiSide;
            _enableAiButton.Text = BattleText("ui.battle.enable_ai", "Enable AI");
        }

        if (_disableAiButton != null)
        {
            _disableAiButton.Visible = isFieldBattle;
            _disableAiButton.Disabled = !isFieldBattle || _isFieldAiRoundStarted || !isAiSide;
            _disableAiButton.Text = BattleText("ui.battle.disable_ai", "Disable AI");
        }

        if (_startRoundButton != null)
        {
            _startRoundButton.Visible = isFieldBattle;
            _startRoundButton.Disabled = !isFieldBattle || _isBattleFinished || _isFieldAiRoundStarted;
            _startRoundButton.Text = BattleText("ui.battle.start_round", "Start Round");
        }

        if (_nextAiButton != null)
        {
            _nextAiButton.Visible = isFieldBattle;
            _nextAiButton.Disabled = !isFieldBattle || !_isFieldAiRoundStarted || !isAiSide || allActed || _isBattleFinished;
            _nextAiButton.Text = BattleText("ui.battle.next_ai", "Next");
        }

        if (_attackerOneDayFoodButton != null)
        {
            _attackerOneDayFoodButton.Visible = isFieldBattle;
            _attackerOneDayFoodButton.Disabled = !isFieldBattle || _isBattleFinished;
            _attackerOneDayFoodButton.Text = BattleText("ui.battle.test_attacker_food_1_day", "Attacker Food: 1d");
        }

        if (_defenderOneDayFoodButton != null)
        {
            _defenderOneDayFoodButton.Visible = isFieldBattle;
            _defenderOneDayFoodButton.Disabled = !isFieldBattle || _isBattleFinished;
            _defenderOneDayFoodButton.Text = BattleText("ui.battle.test_defender_food_1_day", "Defender Food: 1d");
        }

        if (_endTurnButton != null && isFieldBattle)
        {
            _endTurnButton.Disabled = !_isFieldAiRoundStarted || _isBattleFinished;
        }

        if (_aiRoundStatusLabel == null)
        {
            return;
        }

        if (!isFieldBattle)
        {
            _aiRoundStatusLabel.Visible = false;
        }
        else if (!_isFieldAiRoundStarted)
        {
            _aiRoundStatusLabel.Visible = true;
            SetFieldAiRoundStatus(BattleText("ui.battle.ai_status_not_started", "Test round not started: click Start Round"));
        }
        else if (allActed)
        {
            _aiRoundStatusLabel.Visible = true;
            SetFieldAiRoundStatus(BattleText("ui.battle.ai_status_all_acted", "All battle teams have acted: click End Turn"));
        }
        else if (isAiSide)
        {
            _aiRoundStatusLabel.Visible = true;
            SetFieldAiRoundStatus(BattleText("ui.battle.ai_status_review", "AI waiting for review: click Next, or End Turn"));
        }
        else
        {
            _aiRoundStatusLabel.Visible = true;
            SetFieldAiRoundStatus(BattleText("ui.battle.ai_status_player", "Player turn: command battle teams, or End Turn"));
        }
    }

    private void SetFieldAiRoundStatus(string status)
    {
        if (_aiRoundStatusLabel == null)
        {
            return;
        }

        _aiRoundStatusLabel.Text = BattleFormat(
            "ui.battle.ai_status_active_turn",
            "Active turn: {0} — {1}",
            FormatTeamName(GetCurrentTurnSideName()),
            status);
    }

    private void OnEnableAiButtonPressed()
    {
        if (!IsFieldBattleAiTest || _isFieldAiRoundStarted)
        {
            return;
        }

        _aiControlledSides |= GetCurrentAiSideFlag();
        AppendBattleLog(GetCurrentTurnSideName(), "AI", "AI control enabled for this side.");
        ConfigureHud();
        RefreshBattleLogPanel();
    }

    private void OnDisableAiButtonPressed()
    {
        if (!IsFieldBattleAiTest || _isFieldAiRoundStarted)
        {
            return;
        }

        _aiControlledSides &= ~GetCurrentAiSideFlag();
        AppendBattleLog(GetCurrentTurnSideName(), "AI", "AI control disabled for this side.");
        ConfigureHud();
        RefreshBattleLogPanel();
    }

    private void OnStartRoundButtonPressed()
    {
        if (!IsFieldBattleAiTest || _isFieldAiRoundStarted || _isBattleFinished)
        {
            return;
        }

        _actedByMarkerThisRound.Clear();
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _isFieldAiRoundStarted = true;
        AppendBattleLog(GetCurrentTurnSideName(), "Round", $"Round started. Controller: {(IsCurrentTurnAiControlled() ? "AI (step review)" : "Player")}.");
        if (IsCurrentTurnAiControlled())
        {
            AppendBattleLog(GetCurrentTurnSideName(), "AI", BuildAiOpeningPlanLog());
        }
        ConfigureHud();
        RefreshBattleLogPanel();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnNextAiButtonPressed()
    {
        if (!IsFieldBattleAiTest || !_isFieldAiRoundStarted || !IsCurrentTurnAiControlled() || _isBattleFinished)
        {
            return;
        }

        ExecuteOneAiAction();
        if (HaveAllActingBattlePiecesActed())
        {
            AppendBattleLog(GetCurrentTurnSideName(), "AI", "All AI battle teams have acted. Player: click End Turn.");
        }

        ConfigureHud();
        RefreshBattleLogPanel();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnAttackerOneDayFoodButtonPressed()
    {
        SetTeamFoodForAiTest(TeamAInfo.Name);
    }

    private void OnDefenderOneDayFoodButtonPressed()
    {
        SetTeamFoodForAiTest(TeamBInfo.Name);
    }

    private void SetTeamFoodForAiTest(string teamName)
    {
        if (!IsFieldBattleAiTest || _isBattleFinished)
        {
            return;
        }

        var foodForOneDay = CalculateDailyFoodNeed(GetTeamActiveTroops(teamName));
        ApplyTeamResourceDelta(teamName, 0, foodForOneDay - GetTeamFood(teamName));
        AppendBattleLog(teamName, "AI Test", $"Food test: set {FormatTeamName(teamName)} food to {foodForOneDay:N0} (1 day of current active-troop upkeep).");
        ConfigureHud();
        RefreshBattleLogPanel();
    }
}
