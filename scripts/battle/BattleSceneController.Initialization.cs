namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void InitializeBattleRuntimeServices()
    {
        InitializeBattleLocalization();
        LoadOfficerSpeechCatalog();
        ApplyPendingLaunchOptions();
        InitializeCampaignRuntime();
        StartBattleBgm();
    }

    private void ConnectBattleTurnAndDebugSignals()
    {
        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnButtonPressed;
        }

        if (_enableAiButton != null)
        {
            _enableAiButton.Pressed += OnEnableAiButtonPressed;
        }

        if (_disableAiButton != null)
        {
            _disableAiButton.Pressed += OnDisableAiButtonPressed;
        }

        if (_startRoundButton != null)
        {
            _startRoundButton.Pressed += OnStartRoundButtonPressed;
        }

        if (_nextAiButton != null)
        {
            _nextAiButton.Pressed += OnNextAiButtonPressed;
        }

        if (_attackerOneDayFoodButton != null)
        {
            _attackerOneDayFoodButton.Pressed += OnAttackerOneDayFoodButtonPressed;
        }

        if (_defenderOneDayFoodButton != null)
        {
            _defenderOneDayFoodButton.Pressed += OnDefenderOneDayFoodButtonPressed;
        }

        if (_monthlyBattleLimitOneDayButton != null)
        {
            _monthlyBattleLimitOneDayButton.Pressed += OnMonthlyBattleLimitOneDayButtonPressed;
        }

        if (_timeButton != null)
        {
            _timeButton.Pressed += OnTimeButtonPressed;
        }

        if (_weatherButton != null)
        {
            _weatherButton.Pressed += OnWeatherButtonPressed;
        }

        if (_windButton != null)
        {
            _windButton.Pressed += OnWindButtonPressed;
        }

        if (_windPowerButton != null)
        {
            _windPowerButton.Pressed += OnWindPowerButtonPressed;
        }
    }

    private void ConnectBattleOptionSignals()
    {
        if (_battleOptionButton != null)
        {
            _battleOptionButton.Pressed += OnBattleOptionButtonPressed;
        }

        if (_battleOptionDebugButton != null)
        {
            _battleOptionDebugButton.Pressed += OnBattleOptionDebugButtonPressed;
        }

        if (_battleOptionCloseButton != null)
        {
            _battleOptionCloseButton.Pressed += HideBattleOptionDialog;
        }

        if (_battleDebugCloseButton != null)
        {
            _battleDebugCloseButton.Pressed += HideBattleDebugDialog;
        }

        if (_battleOptionSaveLoadButton != null)
        {
            _battleOptionSaveLoadButton.Pressed += OnBattleOptionSaveLoadButtonPressed;
        }

        if (_battleOptionLanguageButton != null)
        {
            _battleOptionLanguageButton.Pressed += OnBattleOptionLanguageButtonPressed;
        }

        if (_battleBgmToggleButton != null)
        {
            _battleBgmToggleButton.Pressed += OnBattleBgmToggleButtonPressed;
        }

        if (_battleSfxToggleButton != null)
        {
            _battleSfxToggleButton.Pressed += OnBattleSfxToggleButtonPressed;
        }

        if (_battleBgmVolumeSlider != null)
        {
            _battleBgmVolumeSlider.ValueChanged += OnBattleBgmVolumeChanged;
        }

        if (_battleSfxVolumeSlider != null)
        {
            _battleSfxVolumeSlider.ValueChanged += OnBattleSfxVolumeChanged;
        }

        if (_battleSaveSettingsButton != null)
        {
            _battleSaveSettingsButton.Pressed += OnBattleSaveSettingsButtonPressed;
        }
    }

    private void ConnectBattleCommandSignals()
    {
        if (_moveButton != null)
        {
            _moveButton.Pressed += OnMoveButtonPressed;
        }

        if (_attackButton != null)
        {
            _attackButton.Pressed += OnAttackButtonPressed;
        }

        if (_unionAttackButton != null)
        {
            _unionAttackButton.Pressed += OnUnionAttackButtonPressed;
        }

        if (_guardButton != null)
        {
            _guardButton.Pressed += OnGuardActionRequested;
        }

        if (_chargeButton != null)
        {
            _chargeButton.Pressed += OnChargeButtonPressed;
        }

        if (_duelButton != null)
        {
            _duelButton.Pressed += OnDuelButtonPressed;
        }

        if (_retreatButton != null)
        {
            _retreatButton.Pressed += OnRetreatActionRequested;
        }

        if (_hideButton != null)
        {
            _hideButton.Pressed += OnHideActionRequested;
        }

        if (_dropStoneButton != null)
        {
            _dropStoneButton.Pressed += OnDropStoneButtonPressed;
        }

        if (_pourOilButton != null)
        {
            _pourOilButton.Pressed += OnPourOilButtonPressed;
        }

        if (_workButton != null)
        {
            _workButton.Pressed += OnWorkButtonPressed;
        }

        if (_installWoodFenceButton != null)
        {
            _installWoodFenceButton.Pressed += OnInstallWoodFenceButtonPressed;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Pressed += OnUninstallWoodFenceButtonPressed;
        }

        if (_supplyButton != null)
        {
            _supplyButton.Pressed += OnSupplyActionRequested;
        }

        if (_resupplyWeaponButton != null)
        {
            _resupplyWeaponButton.Pressed += OnResupplyWeaponButtonPressed;
        }

        if (_captureSupplyCartButton != null)
        {
            _captureSupplyCartButton.Pressed += OnCaptureSupplyCartButtonPressed;
        }

        if (_hireOfficerButton != null)
        {
            _hireOfficerButton.Pressed += OnHireOfficerButtonPressed;
        }

        if (_strategyButton != null)
        {
            _strategyButton.Pressed += OnStrategyButtonPressed;
        }

        if (_openGateButton != null)
        {
            _openGateButton.Pressed += OnOpenGateButtonPressed;
        }
    }

    private void ConnectBattlePanelSignals()
    {
        if (_allLogButton != null)
        {
            _allLogButton.Pressed += OnAllLogButtonPressed;
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Pressed += OnSelfLogButtonPressed;
        }

        if (_copyLogButton != null)
        {
            _copyLogButton.Pressed += OnCopyLogButtonPressed;
        }

        if (_minimizeLogButton != null)
        {
            _minimizeLogButton.Pressed += OnMinimizeLogButtonPressed;
        }

        if (_battleLogHeaderRow != null)
        {
            _battleLogHeaderRow.GuiInput += OnBattleLogHeaderGuiInput;
        }

        if (_battleLogTitleLabel != null)
        {
            _battleLogTitleLabel.GuiInput += OnBattleLogHeaderGuiInput;
        }

        if (_windowTitleLabel != null)
        {
            _windowTitleLabel.GuiInput += HandleCommandMenuDragInput;
        }
    }

    private void InitializeBattleSceneAndPresentation()
    {
        InitializeMapDataAndLayers();
        InitializeEnvironmentSimulation();
        BuildCastleDepthVisuals();
        BuildBuildingDepthVisuals();
        PopulateMarkers();
        RestoreCampaignSnapshotIfAvailable();
        InitializeFieldAiTestDefaults();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        RefreshHighlights();
        ConfigureHud();
        ApplyTimeOfDayVisual(animate: false);
        BuildWeatherVisuals();
        ApplyWeatherVisual(animate: false);
        ApplyBattleLogPanelStyle();
        ApplyBattleScrollBarStyles();
        ApplyBattleOptionDialogStyle();
        ApplyBattleHudPresentation();
        RefreshBattleLogPanel();
        DismissCampaignBattleLoadingOverlay();

        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }

        ShowOpeningOfficerSpeechAfterDelay();
    }
}
