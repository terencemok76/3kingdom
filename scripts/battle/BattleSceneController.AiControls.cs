using Godot;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool IsFieldBattleAiTest => ScenarioType == BattleScenarioType.FieldBattle && _activeCampaign == null;
    private bool IsBattleDebugAvailable => IsFieldBattleAiTest || _activeCampaign != null;
    private bool IsDebugAiStepMode => _isFieldAiRoundStarted && IsBattleDebugAvailable;

    private void ConfigureFieldAiTestControls()
    {
        var debugAvailable = IsBattleDebugAvailable;
        var isAiSide = IsCurrentTurnAiControlled();
        var allActed = HaveAllActingBattlePiecesActed();

        if (_battleDebugButton != null)
        {
            _battleDebugButton.Visible = debugAvailable;
            _battleDebugButton.Disabled = !debugAvailable;
            _battleDebugButton.Text = BattleText("ui.battle.debug", "Debug");
        }
        if (!debugAvailable && _battleDebugOverlay != null)
        {
            _battleDebugOverlay.Visible = false;
        }

        if (_enableAiButton != null)
        {
            _enableAiButton.Visible = debugAvailable;
            _enableAiButton.Disabled = !debugAvailable || _isBattleFinished || isAiSide;
            _enableAiButton.Text = BattleText("ui.battle.enable_ai", "Enable AI");
        }

        if (_disableAiButton != null)
        {
            _disableAiButton.Visible = debugAvailable;
            _disableAiButton.Disabled = !debugAvailable || _isBattleFinished || !isAiSide;
            _disableAiButton.Text = BattleText("ui.battle.disable_ai", "Disable AI");
        }

        if (_startRoundButton != null)
        {
            _startRoundButton.Visible = debugAvailable;
            _startRoundButton.Disabled = !debugAvailable || _isBattleFinished || _isFieldAiRoundStarted;
            _startRoundButton.Text = BattleText("ui.battle.start_round", "Start Round");
        }

        if (_nextAiButton != null)
        {
            _nextAiButton.Visible = debugAvailable;
            _nextAiButton.Disabled = !debugAvailable || !_isFieldAiRoundStarted || !isAiSide || allActed || _isBattleFinished;
            _nextAiButton.Text = BattleText("ui.battle.next_ai", "Next");
            _nextAiButton.TooltipText = !_isFieldAiRoundStarted
                ? BattleText("ui.battle.next_ai_start_round_hint", "Start the round first.")
                : !isAiSide
                    ? BattleText("ui.battle.next_ai_enable_hint", "Enable AI for the current side, then click Next.")
                    : string.Empty;
        }

        if (_attackerOneDayFoodButton != null)
        {
            _attackerOneDayFoodButton.Visible = debugAvailable;
            _attackerOneDayFoodButton.Disabled = !debugAvailable || _isBattleFinished;
            _attackerOneDayFoodButton.Text = BattleText("ui.battle.test_attacker_food_1_day", "Attacker Food: 1d");
        }

        if (_defenderOneDayFoodButton != null)
        {
            _defenderOneDayFoodButton.Visible = debugAvailable;
            _defenderOneDayFoodButton.Disabled = !debugAvailable || _isBattleFinished;
            _defenderOneDayFoodButton.Text = BattleText("ui.battle.test_defender_food_1_day", "Defender Food: 1d");
        }

        if (_endTurnButton != null && (IsFieldBattleAiTest || IsDebugAiStepMode))
        {
            _endTurnButton.Disabled = !_isFieldAiRoundStarted || _isBattleFinished;
        }

        if (_aiRoundStatusLabel == null)
        {
            return;
        }

        if (!IsFieldBattleAiTest)
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

    private void OnBattleDebugButtonPressed()
    {
        if (!IsBattleDebugAvailable)
        {
            return;
        }

        ConfigureFieldAiTestControls();
        RefreshBattleDebugDialogText();
        if (_battleDebugOverlay != null)
        {
            _battleDebugOverlay.Visible = true;
        }
    }

    private void HideBattleDebugDialog()
    {
        _isDraggingBattleDebugDialog = false;
        if (_battleDebugOverlay != null)
        {
            _battleDebugOverlay.Visible = false;
        }
    }

    private void HandleBattleDebugDialogInput(InputEvent @event)
    {
        if (_battleDebugOverlay?.Visible != true || _battleDebugPanel == null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    if (_battleDebugTitleBar?.GetGlobalRect().HasPoint(mouseButton.GlobalPosition) == true &&
                        !(_battleDebugCloseButton?.GetGlobalRect().HasPoint(mouseButton.GlobalPosition) ?? false))
                    {
                        _isDraggingBattleDebugDialog = true;
                        _battleDebugDialogDragOffset = mouseButton.GlobalPosition - _battleDebugPanel.Position;
                        GetViewport().SetInputAsHandled();
                    }
                }
                else
                {
                    _isDraggingBattleDebugDialog = false;
                }

                break;
            case InputEventMouseMotion mouseMotion when _isDraggingBattleDebugDialog:
                _battleDebugPanel.Position = ClampBattleDebugDialogPosition(
                    mouseMotion.GlobalPosition - _battleDebugDialogDragOffset,
                    _battleDebugPanel.Size);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private Vector2 ClampBattleDebugDialogPosition(Vector2 desiredPosition, Vector2 panelSize)
    {
        var viewportSize = GetViewportRect().Size;
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - panelSize.X)),
            Mathf.Clamp(desiredPosition.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - panelSize.Y)));
    }

    private void RefreshBattleDebugDialogText()
    {
        if (_battleDebugButton != null)
        {
            _battleDebugButton.Text = BattleText("ui.battle.debug", "Debug");
        }
        if (_battleDebugTitleLabel != null)
        {
            _battleDebugTitleLabel.Text = BattleText("ui.battle.debug_title", "Battle Debug");
        }
    }

    private void OnEnableAiButtonPressed()
    {
        if (!IsBattleDebugAvailable || _isBattleFinished)
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
        if (!IsBattleDebugAvailable || _isBattleFinished)
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
        if (!IsBattleDebugAvailable || _isFieldAiRoundStarted || _isBattleFinished)
        {
            return;
        }

        _actedByMarkerThisRound.Clear();
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _isFieldAiRoundStarted = true;
        if (_activeCampaign != null)
        {
            // Match the standalone field-battle test: Debug owns both sides until the
            // tester explicitly disables AI for the side currently taking its turn.
            _aiControlledSides = BattleAiControlledSides.Attacker | BattleAiControlledSides.Defender;
            AppendBattleLog(GetCurrentTurnSideName(), "AI", "Debug round started: AI control enabled for both sides.");
        }
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
        if (!IsBattleDebugAvailable || !_isFieldAiRoundStarted || !IsCurrentTurnAiControlled() || _isBattleFinished)
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
        if (!IsBattleDebugAvailable || _isBattleFinished)
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
