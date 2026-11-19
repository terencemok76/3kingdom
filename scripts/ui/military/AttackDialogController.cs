using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

internal sealed partial class AttackDialogController : FloatingOverlayController
{
    private enum DialogMode
    {
        Attack,
        Defense
    }

    private readonly MilitaryUiContext _context;
    private readonly DeploymentDraft _draft = new();
    private readonly DeploymentEditorController _deploymentEditor;
    private OptionButton? _targetCityOption;
    private Button? _targetCityMapButton;
    private Button? _attackAdviceButton;
    private HBoxContainer? _defenderPlanRow;
    private Label? _defenderPlanLabel;
    private OptionButton? _defenderPlanOption;
    private HBoxContainer? _defenseSupportRow;
    private Label? _defenseSupportLabel;
    private OptionButton? _defenseSupportSource;
    private SpinBox? _defenseSupportTroops;
    private HBoxContainer? _defenseRequestResources;
    private SpinBox? _defenseRequestGold;
    private SpinBox? _defenseRequestFood;
    private Button? _defenseSupportAddButton;
    private Button? _defenseSupportRemoveButton;
    private Label? _defenseSupportSummary;
    private HBoxContainer? _defensePromiseRow;
    private Label? _defensePromiseLabel;
    private SpinBox? _defensePromiseGold;
    private SpinBox? _defensePromiseFood;
    private HBoxContainer? _defenseMessengerRow;
    private Label? _defenseMessengerLabel;
    private OptionButton? _defenseMessengerOption;
    private Label? _defenseResponseEstimate;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private Tree? _officerList;
    private VBoxContainer? _deploymentList;
    private Label? _deploymentSummaryLabel;
    private Label? _warningLabel;
    private Button? _confirmButton;
    private bool _officerListSignalsConnected;
    private bool _officerListGuiInputConnected;
    private bool _confirmButtonSignalsConnected;
    private bool _targetCitySignalsConnected;
    private bool _targetCityMapSignalsConnected;
    private bool _attackAdviceSignalsConnected;
    private string _lastSelectionSignature = string.Empty;
    private int _warningAcknowledgedTargetCityId = -1;
    private DialogMode _dialogMode = DialogMode.Attack;
    private CityData? _dialogContextCity;
    private PendingCommandData? _pendingDefenseCommand;
    private bool _editingDomesticReinforcement;
    private int _domesticReinforcementSourceCityId;
    private int _selectedDefenseEnvoyOfficerId;

    protected override Vector2 MinimumOverlaySize => new(620.0f, 630.0f);

    public AttackDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/AttackDialog.tscn")
    {
        _context = context;
        _deploymentEditor = new DeploymentEditorController(context, _draft, GetDialogCityContext,
            ApplyInputThemeToSubtree, RefreshDeploymentEditor, SetWarning, ShowOverlay, GetTroopTypeDisplayName);
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        var isDefenseMode = _dialogMode == DialogMode.Defense;
        var isReinforcement = !isDefenseMode && IsReinforcementTarget();
        SetOverlayTitleText(isDefenseMode
            ? (_context.Localization.T("ui.defense") ?? "Defense")
            : isReinforcement
                ? _context.Localization.T("ui.campaign.reinforce") ?? "Reinforce"
                : _context.Localization.T("ui.attack"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = isDefenseMode
                ? (_context.Localization.T("ui.confirm_defense") ?? "Confirm Defense")
                : isReinforcement
                    ? _context.Localization.T("ui.campaign.confirm_reinforce") ?? "Confirm Reinforcement"
                    : _context.Localization.T("ui.confirm_attack");
        }

        SetLabelText("TargetCityLabel", isDefenseMode ? _context.Localization.T("ui.attack") : _context.Localization.T("ui.target_city"));
        if (_targetCityMapButton != null)
        {
            _targetCityMapButton.Text = _context.Localization.T("ui.strategic_map.open_selector");
            _targetCityMapButton.Visible = !isDefenseMode;
            _targetCityMapButton.Disabled = isDefenseMode || _targetCityOption?.ItemCount == 0;
        }
        SetLabelText("TroopsLabel", _context.Localization.T("ui.attack_troops"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.attack_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.attack_food"));
        SetLabelText("OfficerListLabel", isDefenseMode
            ? (_context.Localization.T("ui.defense_officers") ?? "Defending Officers")
            : _context.Localization.T("ui.attack_officers"));
        SetLabelText("DeploymentListLabel", isDefenseMode
            ? (_context.Localization.T("ui.defense_deployments") ?? "Defense Deployments")
            : _context.Localization.T("ui.attack_deployments"));
        SetFieldRowVisible("GoldRow", !isDefenseMode || _editingDomesticReinforcement);
        SetFieldRowVisible("FoodRow", !isDefenseMode || _editingDomesticReinforcement);
        if (_attackAdviceButton != null)
        {
            _attackAdviceButton.Visible = !isDefenseMode && !isReinforcement;
            _attackAdviceButton.Text = _context.Localization.T("ui.attack_advice_button") ?? "Ask Strategist";
        }
        if (_defenseSupportRow != null)
        {
            _defenseSupportRow.Visible = isDefenseMode;
        }
        if (!isDefenseMode)
        {
            if (_defenseRequestResources != null) _defenseRequestResources.Visible = false;
            if (_defensePromiseRow != null) _defensePromiseRow.Visible = false;
            if (_defenseResponseEstimate != null) _defenseResponseEstimate.Visible = false;
        }
        if (_defenseMessengerRow != null)
        {
            _defenseMessengerRow.Visible = isDefenseMode;
        }
        if (_defenderPlanRow != null)
        {
            _defenderPlanRow.Visible = isDefenseMode || _context.IsGodModeEnabled();
        }
        if (_defenderPlanLabel != null)
        {
            _defenderPlanLabel.Text = isDefenseMode
                ? _context.Localization.T("ui.defender_battle_plan")
                : _context.Localization.T("ui.debug_defender_plan_override");
        }
        RefreshDefenderPlanOptions();
        RefreshDefenseSupportControls();
        RefreshDefenseMessengerOptions();
        RefreshTargetCityOptionTexts();
        RefreshOfficerTableText();
        RefreshDeploymentEditor();
        UpdateDeploymentSummary();
    }

    public void ShowAttack(List<int> candidateIds)
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || !EnsureOverlayReady() || _targetCityOption == null)
        {
            return;
        }

        _dialogMode = DialogMode.Attack;
        _dialogContextCity = _context.SelectedCity;
        _pendingDefenseCommand = null;
        if (_defenderPlanRow != null)
        {
            _defenderPlanRow.Visible = _context.IsGodModeEnabled();
        }
        RefreshText();
        if (_context.IsGodModeEnabled() && _defenderPlanOption?.ItemCount > 0)
        {
            _defenderPlanOption.Select(0);
        }
        SetWarning(string.Empty);
        _warningAcknowledgedTargetCityId = -1;

        _targetCityOption.Clear();
        _targetCityOption.Disabled = false;
        foreach (var cityId in candidateIds)
        {
            var city = _context.TurnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            var label = GetAttackTargetLabel(city);
            _targetCityOption.AddItem(label);
            _targetCityOption.SetItemMetadata(_targetCityOption.ItemCount - 1, city.Id);
        }

        if (_targetCityOption.ItemCount > 0)
        {
            _targetCityOption.Select(0);
        }

        ConfigureSpinBox(_goldSpinBox, _context.SelectedCity.Gold, 0);
        ConfigureSpinBox(_foodSpinBox, _context.SelectedCity.Food, 0);
        ResetState();
        _dialogMode = DialogMode.Attack;
        _dialogContextCity = _context.SelectedCity;
        PopulateOfficerList(_context.SelectedCity, _context.GetAvailableOfficerIdsForOrder());
        RefreshText();
        RefreshDeploymentEditor();
        ShowOverlay();
    }

    public void ShowDefense(PendingCommandData pendingCommand, CityData defendingCity, CityData attackingCity)
    {
        if (!EnsureOverlayReady() || _targetCityOption == null)
        {
            return;
        }

        ResetState();
        _dialogMode = DialogMode.Defense;
        _dialogContextCity = defendingCity;
        _pendingDefenseCommand = pendingCommand;
        _editingDomesticReinforcement = false;
        _domesticReinforcementSourceCityId = 0;
        _selectedDefenseEnvoyOfficerId = 0;
        if (_defenderPlanRow != null)
        {
            _defenderPlanRow.Visible = true;
        }
        RefreshText();
        SetWarning(string.Empty);
        _warningAcknowledgedTargetCityId = -1;

        _targetCityOption.Clear();
        var attackerLabel = GetAttackTargetLabel(attackingCity);
        _targetCityOption.AddItem(attackerLabel);
        _targetCityOption.SetItemMetadata(0, attackingCity.Id);
        _targetCityOption.Select(0);
        _targetCityOption.Disabled = true;

        ConfigureSpinBox(_goldSpinBox, 0, 0);
        ConfigureSpinBox(_foodSpinBox, 0, 0);
        PopulateOfficerList(defendingCity, defendingCity.OfficerIds.ToList());
        RefreshDeploymentEditor();
        ShowOverlay();
    }

    public void Process()
    {
        if (OverlayRoot == null || !OverlayRoot.Visible)
        {
            _lastSelectionSignature = string.Empty;
            return;
        }

        var selectedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        selectedOfficerIds.Sort();
        var signature = string.Join(",", selectedOfficerIds);
        if (_lastSelectionSignature == signature)
        {
            return;
        }

        _lastSelectionSignature = signature;
        RefreshDeploymentEditor();
    }

    public void ResetState()
    {
        _pendingDefenseCommand = null;
        _warningAcknowledgedTargetCityId = -1;
        _dialogContextCity = null;
        _dialogMode = DialogMode.Attack;
        _lastSelectionSignature = string.Empty;
        _draft.Reset();
        _editingDomesticReinforcement = false;
        _domesticReinforcementSourceCityId = 0;
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
        _targetCityMapButton = root.GetNodeOrNull<Button>("TargetCityRow/TargetCityMapButton");
        _attackAdviceButton = root.GetNodeOrNull<Button>("ConfirmRow/AttackAdviceButton");
        _defenderPlanRow = root.GetNodeOrNull<HBoxContainer>("DefenderPlanRow");
        _defenderPlanLabel = root.GetNodeOrNull<Label>("DefenderPlanRow/DefenderPlanLabel");
        _defenderPlanOption = root.GetNodeOrNull<OptionButton>("DefenderPlanRow/DefenderPlanOption");
        _defenseSupportRow = root.GetNodeOrNull<HBoxContainer>("DefenseSupportRow");
        _defenseSupportLabel = root.GetNodeOrNull<Label>("DefenseSupportRow/DefenseSupportLabel");
        _defenseSupportSource = root.GetNodeOrNull<OptionButton>("DefenseSupportRow/DefenseSupportSource");
        _defenseSupportTroops = root.GetNodeOrNull<SpinBox>("DefenseSupportRow/DefenseSupportTroops");
        _defenseRequestResources = root.GetNodeOrNull<HBoxContainer>("DefenseRequestResources");
        _defenseRequestGold = root.GetNodeOrNull<SpinBox>("DefenseRequestResources/Gold");
        _defenseRequestFood = root.GetNodeOrNull<SpinBox>("DefenseRequestResources/Food");
        _defenseSupportAddButton = root.GetNodeOrNull<Button>("DefenseSupportRow/DefenseSupportAddButton");
        _defenseSupportRemoveButton = root.GetNodeOrNull<Button>("DefenseSupportRow/DefenseSupportRemoveButton");
        _defenseSupportSummary = root.GetNodeOrNull<Label>("DefenseSupportSummary");
        _defensePromiseRow = root.GetNodeOrNull<HBoxContainer>("DefensePromiseRow");
        _defensePromiseLabel = root.GetNodeOrNull<Label>("DefensePromiseRow/DefensePromiseLabel");
        _defensePromiseGold = root.GetNodeOrNull<SpinBox>("DefensePromiseRow/DefensePromiseGold");
        _defensePromiseFood = root.GetNodeOrNull<SpinBox>("DefensePromiseRow/DefensePromiseFood");
        _defenseMessengerRow = root.GetNodeOrNull<HBoxContainer>("DefenseMessengerRow");
        _defenseMessengerLabel = root.GetNodeOrNull<Label>("DefenseMessengerRow/DefenseMessengerLabel");
        _defenseMessengerOption = root.GetNodeOrNull<OptionButton>("DefenseMessengerRow/DefenseMessengerOption");
        _defenseResponseEstimate = root.GetNodeOrNull<Label>("DefenseResponseEstimate");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _officerList = root.GetNodeOrNull<Tree>("OfficerTable");
        _deploymentList = root.GetNodeOrNull<VBoxContainer>("DeploymentScroll/DeploymentList");
        _deploymentSummaryLabel = root.GetNodeOrNull<Label>("DeploymentSummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");

        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }

        if (_attackAdviceButton != null)
        {
            _context.ApplyCommandButtonTheme(_attackAdviceButton);
        }

        if (_targetCityMapButton != null)
        {
            _context.ApplyCommandButtonTheme(_targetCityMapButton);
        }

        if (_officerList != null)
        {
            _officerList.CustomMinimumSize = new Vector2(0.0f, 150.0f);
            _officerList.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        }

        if (_warningLabel != null)
        {
            _warningLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.52f, 0.45f, 1.0f));
        }

        if (!_officerListSignalsConnected && _officerList != null)
        {
            _officerList.ItemSelected += UpdateOfficerCheckHighlights;
            _officerListSignalsConnected = true;
        }

        if (!_officerListGuiInputConnected && _officerList != null)
        {
            _officerList.GuiInput += OnOfficerListGuiInput;
            _officerListGuiInputConnected = true;
        }

        if (!_confirmButtonSignalsConnected && _confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
            _confirmButtonSignalsConnected = true;
        }
        if (_defenseSupportAddButton != null)
        {
            _context.ApplyCommandButtonTheme(_defenseSupportAddButton);
            _defenseSupportAddButton.Pressed += OnDefenseSupportAddPressed;
        }
        if (_defenseSupportRemoveButton != null)
        {
            _context.ApplyCommandButtonTheme(_defenseSupportRemoveButton);
            _defenseSupportRemoveButton.Pressed += OnDefenseSupportRemovePressed;
        }
        if (_defenseMessengerOption != null)
        {
            _defenseMessengerOption.ItemSelected += OnDefenseMessengerSelected;
        }
        if (_defenseSupportSource != null)
        {
            _defenseSupportSource.ItemSelected += _ => OnDefenseSupportSourceSelected();
        }

        if (!_targetCitySignalsConnected && _targetCityOption != null)
        {
            _targetCityOption.ItemSelected += OnTargetCitySelected;
            _targetCitySignalsConnected = true;
        }
        if (!_targetCityMapSignalsConnected && _targetCityMapButton != null)
        {
            _targetCityMapButton.Pressed += OnTargetCityMapPressed;
            _targetCityMapSignalsConnected = true;
        }
        if (!_attackAdviceSignalsConnected && _attackAdviceButton != null)
        {
            _attackAdviceButton.Pressed += OnAttackAdvicePressed;
            _attackAdviceSignalsConnected = true;
        }
    }

    protected override void OnOverlayCloseRequested()
    {
        if (_dialogMode == DialogMode.Defense)
        {
            ShowOverlay();
            return;
        }

        HideOverlay();
        ResetState();
    }

    private void PopulateOfficerList(CityData city, List<int> candidateOfficerIds)
    {
        if (_officerList == null || _context.TurnManager?.World == null)
        {
            return;
        }

        var candidateSet = candidateOfficerIds.ToHashSet();
        _officerList.Clear();
        _context.ConfigureCompactOfficerTableColumns(_officerList, includeCheck: true, includeAppointments: true);
        var tableRoot = _officerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in city.OfficerIds)
        {
            if (!candidateSet.Contains(officerId))
            {
                continue;
            }

            var officer = _context.TurnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var row = _officerList.CreateItem(tableRoot);
            _context.PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true, includeAppointments: true);
            if (_draft.ContainsOfficer(officer.Id))
            {
                row.SetMetadata(0, true);
            }
            rowIndex += 1;
        }

        UpdateOfficerCheckHighlights();
    }

    private void RefreshDeploymentEditor()
    {
        if (_deploymentList == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        foreach (var child in _deploymentList.GetChildren())
        {
            child.QueueFree();
        }

        var selectedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        _draft.SynchronizeSelection(selectedOfficerIds, _deploymentEditor.AvailableTroopTypes()[0]);

        foreach (var officerId in _draft.OfficerOrder)
        {
            var officer = _context.TurnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            _deploymentList.AddChild(_deploymentEditor.CreateOfficerRow(officer));
        }

        _deploymentList.AddChild(_deploymentEditor.CreateSupportRow());

        if (selectedOfficerIds.Count == 0)
        {
            _deploymentList.AddChild(new Label
            {
                Text = _context.Localization.T("ui.attack_select_officers_hint"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        UpdateOfficerCheckHighlights();
        UpdateDeploymentSummary();
        if (_dialogMode == DialogMode.Defense && !_editingDomesticReinforcement)
        {
            RefreshDefenseMessengerOptions();
        }
    }

    private void UpdateOfficerCheckHighlights()
    {
        if (_officerList == null)
        {
            return;
        }

        var root = _officerList.GetRoot();
        var row = root?.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            ApplyOfficerRowVisualState(row, rowIndex, _officerList.Columns, IsOfficerRowChecked(row));
            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private static void ApplyOfficerRowVisualState(TreeItem row, int rowIndex, int columnCount, bool isChecked)
    {
        var background = isChecked
            ? new Color(0.33f, 0.27f, 0.16f, 0.78f)
            : (rowIndex % 2 == 0
                ? new Color(0.12f, 0.12f, 0.14f, 0.84f)
                : new Color(0.16f, 0.16f, 0.18f, 0.8f));
        var textColor = isChecked
            ? new Color(0.94f, 0.91f, 0.84f, 1.0f)
            : new Color(0.92f, 0.89f, 0.82f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, column == 0
                ? (isChecked ? new Color(0.88f, 0.79f, 0.52f, 1.0f) : new Color(0.60f, 0.57f, 0.52f, 0.92f))
                : textColor);
        }

        row.SetText(0, isChecked ? "●" : "○");
    }

    private void ToggleOfficerRow(TreeItem row)
    {
        if (row == null)
        {
            return;
        }

        var current = row.GetMetadata(0).VariantType == Variant.Type.Bool && row.GetMetadata(0).AsBool();
        row.SetMetadata(0, !current);
    }

    private static bool IsOfficerRowChecked(TreeItem row)
    {
        return row.GetMetadata(0).VariantType == Variant.Type.Bool && row.GetMetadata(0).AsBool();
    }

    private void OnOfficerListGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            _officerList == null)
        {
            return;
        }

        var clickedRow = _officerList.GetItemAtPosition(mouseButton.Position);
        if (clickedRow == null)
        {
            return;
        }

        ToggleOfficerRow(clickedRow);
        UpdateOfficerCheckHighlights();
        RefreshDeploymentEditor();
        _officerList.AcceptEvent();
    }

    private int GetMaxEngineerTeamCount(CityData? city) => _draft.MaxEngineerTeams(city?.EngineerTroops ?? 0);

    private void UpdateDeploymentSummary()
    {
        var dialogCity = GetDialogCityContext();
        if (_deploymentSummaryLabel == null || dialogCity == null || _context.Localization == null)
        {
            return;
        }

        var snapshot = _draft.CreateSnapshot();
        var allocation = snapshot.Troops;
        var siegeEngineAllocation = snapshot.Equipment;
        var support = snapshot.Support;
        var summary = string.Join(" | ", new[]
        {
            FormatSummaryPart(TroopType.Infantry, allocation.Infantry, dialogCity.InfantryTroops),
            FormatSummaryPart(TroopType.Spearman, allocation.Spearman, dialogCity.SpearmanTroops),
            FormatSummaryPart(TroopType.Cavalry, allocation.Cavalry, dialogCity.CavalryTroops),
            FormatSummaryPart(TroopType.Archer, allocation.Archer, dialogCity.ArcherTroops),
            FormatSummaryPart(TroopType.Crossbow, allocation.Crossbow, dialogCity.CrossbowTroops),
            $"工兵隊 {support.EngineerTeamCount}/{GetMaxEngineerTeamCount(dialogCity)}"
        });
        var siegeEngineSummary = string.Join(" | ", new[]
        {
            FormatSiegeEngineSummaryPart(SiegeEngineType.Ram, siegeEngineAllocation.Ram, dialogCity.RamCount),
            FormatSiegeEngineSummaryPart(SiegeEngineType.Catapult, siegeEngineAllocation.Catapult, dialogCity.CatapultCount),
            FormatSiegeEngineSummaryPart(SiegeEngineType.Ladder, siegeEngineAllocation.Ladder, dialogCity.LadderCount)
        });
        _deploymentSummaryLabel.Text = _context.Localization.Format("fmt.attack_deployment_summary", summary, allocation.Total + support.TotalEngineerCount);
        var requiredEngineers = BattleSupportRules.GetRequiredEngineerCount(support);
        if (requiredEngineers > 0)
        {
            _deploymentSummaryLabel.Text += $"\n工兵總數：工兵隊 {support.EngineerTeamCount} + 裝備操作 {requiredEngineers} = {support.TotalEngineerCount}/{dialogCity.EngineerTroops}";
        }
        if (support.EquipmentCount > 0 || dialogCity.RamCount > 0 || dialogCity.CatapultCount > 0 || dialogCity.LadderCount > 0)
        {
            _deploymentSummaryLabel.Text += $"\n{siegeEngineSummary}" + (support.SupplyCart ? $" | 補給車 1/{dialogCity.SupplyCartCount}" : string.Empty);
        }
    }

    private void OnConfirmPressed()
    {
        var dialogCity = GetDialogCityContext();
        if (_targetCityOption == null || dialogCity == null)
        {
            return;
        }

        var snapshot = _draft.CreateSnapshot();
        var attackDeployments = snapshot.Officers;
        var allocation = snapshot.Troops;
        var battleSupport = snapshot.Support;
        var validation = DeploymentValidation.Validate(snapshot, dialogCity);
        if (validation != DeploymentValidationError.None)
        {
            var warning = validation switch
            {
                DeploymentValidationError.NoOfficers => _context.Localization?.T("ui.attack_deployment_required_warning") ?? "Configure troop type and count for each deployed officer.",
                DeploymentValidationError.NoTroops => _context.Localization?.T("ui.attack_troops_required_warning") ?? "Enter the number of troops to deploy.",
                DeploymentValidationError.TroopsExceeded => _context.Localization?.T("ui.attack_deployment_exceed_warning") ?? "Troop deployment exceeds the city's available troop types.",
                DeploymentValidationError.EngineersExceeded => $"工兵不足：工兵隊與裝備操作共需 {battleSupport.TotalEngineerCount}，本城只有 {dialogCity.EngineerTroops}。",
                DeploymentValidationError.EquipmentExceeded => _context.Localization?.T("ui.attack_siege_engine_exceed_warning") ?? "Assigned siege engines exceed the city's available stock.",
                _ => _context.Localization?.T("ui.attack_siege_engine_exceed_warning") ?? "Assigned battle equipment exceeds the city's available stock."
            };
            SetWarning(warning);
            ShowOverlay();
            return;
        }
        if (_dialogMode == DialogMode.Defense)
        {
            ConfirmDefense(attackDeployments, battleSupport);
            return;
        }

        ConfirmAttack(attackDeployments, battleSupport, allocation);
    }

    private void ConfirmDefense(List<AttackOfficerDeploymentData> deployments, BattleSupportDeploymentData support)
    {
        if (_pendingDefenseCommand == null) return;
        if (_editingDomesticReinforcement)
        {
            SaveDomesticDefenseReinforcement(deployments);
            return;
        }

        _pendingDefenseCommand.DefenderOfficerDeployments = deployments;
        _pendingDefenseCommand.DefenderBattleSupport = support;
        _pendingDefenseCommand.DefenderBattlePlan = GetSelectedDefenderBattlePlan();
        SetWarning(string.Empty);
        HideOverlay();
        _context.ContinuePendingAttackResolution();
    }

    private void ConfirmAttack(List<AttackOfficerDeploymentData> attackDeployments, BattleSupportDeploymentData battleSupport, TroopAllocationData allocation)
    {
        if (_targetCityOption == null) return;
        var selectedIndex = _targetCityOption.Selected;
        if (selectedIndex < 0)
        {
            return;
        }

        var targetMetadata = _targetCityOption.GetItemMetadata(selectedIndex);
        if (targetMetadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var targetCityId = targetMetadata.AsInt32();
        if (ShouldWarnBreakPact(targetCityId) && _warningAcknowledgedTargetCityId != targetCityId)
        {
            _warningAcknowledgedTargetCityId = targetCityId;
            SetWarning(_context.Localization?.T("ui.attack_break_pact_warning") ?? "This attack will automatically break the current alliance or truce. Confirm again to proceed.");
            ShowOverlay();
            return;
        }

        var result = _context.ExecuteAttackCommand(
            targetCityId,
            allocation.Total,
            _goldSpinBox != null ? (int)_goldSpinBox.Value : 0,
            _foodSpinBox != null ? (int)_foodSpinBox.Value : 0,
            attackDeployments,
            battleSupport,
            attackDeployments.Select(item => item.OfficerId).Distinct().ToList(),
            _context.IsGodModeEnabled() ? GetSelectedDefenderBattlePlanOverride() : null);

        if (result.Success)
        {
            var sourceCity = GetDialogCityContext();
            if (sourceCity != null)
            {
                _context.UiEventHub.PublishCityStateChanged(sourceCity.Id, sourceCity.OwnerFactionId);
                foreach (var officerId in attackDeployments.Select(item => item.OfficerId).Distinct())
                {
                    _context.UiEventHub.PublishOfficerStateChanged(officerId, sourceCity.Id, sourceCity.OwnerFactionId);
                }
            }

            if (_context.TurnManager?.World?.GetCity(targetCityId) is { } targetCity)
            {
                _context.UiEventHub.PublishCityStateChanged(targetCity.Id, targetCity.OwnerFactionId);
            }
            SetWarning(string.Empty);
            HideOverlay();
            ResetState();
            return;
        }

        SetWarning(_context.GetLocalizedResultMessage(result));
        ShowOverlay();
    }

    private bool ShouldWarnBreakPact(int targetCityId)
    {
        if (_dialogMode != DialogMode.Attack || _context.TurnManager?.World == null)
        {
            return false;
        }

        var sourceCity = GetDialogCityContext();
        var targetCity = _context.TurnManager.World.GetCity(targetCityId);
        if (sourceCity == null || targetCity == null || sourceCity.OwnerFactionId == targetCity.OwnerFactionId)
        {
            return false;
        }

        var relation = _context.TurnManager.World.GetDiplomacyRelation(sourceCity.OwnerFactionId, targetCity.OwnerFactionId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }

    private CityData? GetDialogCityContext() => _dialogContextCity ?? _context.SelectedCity;

    private void OnAttackAdvicePressed()
    {
        var world = _context.TurnManager?.World;
        var sourceCity = GetDialogCityContext();
        var targetCity = GetSelectedTargetCity();
        var localization = _context.Localization;
        if (_dialogMode != DialogMode.Attack || world == null || sourceCity == null || targetCity == null || localization == null)
        {
            return;
        }

        var advisor = AttackAdviceRules.SelectAdvisor(world, sourceCity);
        if (advisor == null)
        {
            _context.ShowAdvisorMessage(
                null,
                localization.T("ui.local_place") ?? "Local",
                localization.T("ui.attack_advice_no_advisor") ?? "No strategist or local officer is available to advise.");
            return;
        }

        var advisorRole = world.GetFaction(sourceCity.OwnerFactionId)?.ChiefStrategistOfficerId == advisor.Id
            ? localization.T("ui.chief_strategist") ?? "Chief Strategist"
            : localization.T("ui.local_place") ?? "Local";
        _context.ShowAdvisorMessage(advisor, advisorRole, BuildAttackAdvice(world, sourceCity, targetCity, localization));
    }

    private string BuildAttackAdvice(WorldState world, CityData sourceCity, CityData targetCity, LocalizationService localization)
    {
        var deployedTroops = _draft.TotalTroops;
        var kind = AttackAdviceRules.Evaluate(world, sourceCity, targetCity, deployedTroops, _foodSpinBox != null ? (int)_foodSpinBox.Value : 0);
        if (kind == AttackAdviceKind.Prepare)
        {
            return localization.T("ui.attack_advice_prepare") ?? "Choose officers and assign troops before deciding whether to attack.";
        }

        if (kind is AttackAdviceKind.BreakAlliance or AttackAdviceKind.BreakTruce)
        {
            var relationName = kind == AttackAdviceKind.BreakAlliance
                ? localization.T("ui.diplomacy_alliance") ?? "Alliance"
                : localization.T("ui.diplomacy_truce") ?? "Truce";
            return localization.Format("fmt.attack_advice_break_pact", relationName);
        }

        if (kind == AttackAdviceKind.LowFood)
        {
            return localization.T("ui.attack_advice_low_food") ?? "The expedition has no food. Carry supplies before marching.";
        }

        if (kind == AttackAdviceKind.NoIntel)
        {
            return localization.T("ui.attack_advice_no_intel") ?? "Enemy strength and defenses are unknown. Scout before committing to an attack.";
        }

        if (kind == AttackAdviceKind.Unfavorable)
        {
            return localization.Format("fmt.attack_advice_unfavorable", deployedTroops, targetCity.Troops, targetCity.Defense);
        }

        if (kind == AttackAdviceKind.Cautious)
        {
            return localization.Format("fmt.attack_advice_cautious", deployedTroops, targetCity.Troops, targetCity.Defense);
        }

        return localization.Format("fmt.attack_advice_favorable", deployedTroops, targetCity.Troops, targetCity.Defense);
    }

    private CityData? GetSelectedTargetCity()
    {
        if (_targetCityOption == null || _targetCityOption.Selected < 0 || _context.TurnManager?.World == null)
        {
            return null;
        }

        var metadata = _targetCityOption.GetItemMetadata(_targetCityOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? _context.TurnManager.World.GetCity(metadata.AsInt32())
            : null;
    }

    private void RefreshDefenderPlanOptions()
    {
        if (_defenderPlanOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedPlan = _dialogMode == DialogMode.Defense
            ? _pendingDefenseCommand?.DefenderBattlePlan ?? DefenderBattlePlan.CityDefense
            : GetSelectedDefenderBattlePlanOverride();
        _defenderPlanOption.Clear();
        if (_dialogMode == DialogMode.Attack)
        {
            _defenderPlanOption.AddItem(_context.Localization.T("ui.defender_plan_ai_auto"));
            _defenderPlanOption.SetItemMetadata(0, -1);
        }
        var fieldIndex = _defenderPlanOption.ItemCount;
        _defenderPlanOption.AddItem(_context.Localization.T("ui.defender_plan_field_intercept"));
        _defenderPlanOption.SetItemMetadata(fieldIndex, (int)DefenderBattlePlan.FieldIntercept);
        var cityIndex = _defenderPlanOption.ItemCount;
        _defenderPlanOption.AddItem(_context.Localization.T("ui.defender_plan_city_defense"));
        _defenderPlanOption.SetItemMetadata(cityIndex, (int)DefenderBattlePlan.CityDefense);
        _defenderPlanOption.Select(selectedPlan switch
        {
            DefenderBattlePlan.FieldIntercept => fieldIndex,
            DefenderBattlePlan.CityDefense => cityIndex,
            _ => 0
        });
    }

    private DefenderBattlePlan GetSelectedDefenderBattlePlan()
        => GetSelectedDefenderBattlePlanOverride() ?? DefenderBattlePlan.CityDefense;

    private DefenderBattlePlan? GetSelectedDefenderBattlePlanOverride()
    {
        if (_defenderPlanOption == null || _defenderPlanOption.Selected < 0)
        {
            return null;
        }

        var metadata = _defenderPlanOption.GetItemMetadata(_defenderPlanOption.Selected);
        if (metadata.VariantType != Variant.Type.Int || metadata.AsInt32() < 0)
        {
            return null;
        }

        return metadata.AsInt32() == (int)DefenderBattlePlan.FieldIntercept
            ? DefenderBattlePlan.FieldIntercept
            : DefenderBattlePlan.CityDefense;
    }

    private int GetAvailableSiegeEngineCount(SiegeEngineType siegeEngineType) => GetDialogCityContext()?.GetSiegeEngineCount(siegeEngineType) ?? 0;

    private string GetTroopTypeDisplayName(TroopType troopType)
    {
        return _context.Localization?.T(troopType switch
        {
            TroopType.Infantry => "troop_type.infantry",
            TroopType.Spearman => "troop_type.spearman",
            TroopType.Cavalry => "troop_type.cavalry",
            TroopType.Archer => "troop_type.archer",
            TroopType.Crossbow => "troop_type.crossbow",
            TroopType.Engineer => "troop_type.engineer",
            _ => "troop_type.infantry"
        }) ?? troopType.ToString();
    }

    private string FormatSummaryPart(TroopType troopType, int assigned, int available)
    {
        return string.Format("{0} {1}/{2}", GetTroopTypeDisplayName(troopType), assigned, available);
    }

    private string FormatSiegeEngineSummaryPart(SiegeEngineType siegeEngineType, int assigned, int available)
    {
        return string.Format("{0} {1}/{2}", GetSiegeEngineDisplayName(siegeEngineType), assigned, available);
    }

    private void SetWarning(string text)
    {
        if (_warningLabel == null)
        {
            return;
        }

        _warningLabel.Text = text;
        _warningLabel.Visible = !string.IsNullOrWhiteSpace(text);
    }

    private void SetFieldRowVisible(string rowName, bool visible)
    {
        var root = OverlayContentRoot as Control;
        var row = root?.FindChild(rowName, recursive: true, owned: false) as Control;
        if (row != null)
        {
            row.Visible = visible;
        }
    }

    private void SetLabelText(string nodeName, string text)
    {
        var root = OverlayContentRoot as Control;
        var label = root?.FindChild(nodeName, recursive: true, owned: false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private static void ConfigureSpinBox(SpinBox? spinBox, int maxValue, int defaultValue)
    {
        if (spinBox == null)
        {
            return;
        }

        spinBox.MinValue = 0;
        spinBox.MaxValue = maxValue;
        spinBox.Value = maxValue <= 0 ? 0 : Mathf.Clamp(defaultValue, 0, maxValue);
    }

    private void PopulateSiegeEngineOption(OptionButton optionButton, SiegeEngineType selectedType)
    {
        optionButton.Clear();
        optionButton.AddItem(_context.Localization?.T("ui.none") ?? "None");
        optionButton.SetItemMetadata(0, (int)SiegeEngineType.None);
        foreach (var siegeEngineType in Enum.GetValues<SiegeEngineType>())
        {
            if (siegeEngineType == SiegeEngineType.None || GetAvailableSiegeEngineCount(siegeEngineType) <= 0)
            {
                continue;
            }

            optionButton.AddItem(GetSiegeEngineDisplayName(siegeEngineType));
            optionButton.SetItemMetadata(optionButton.ItemCount - 1, (int)siegeEngineType);
        }

        SelectSiegeEngineTypeOption(optionButton, selectedType);
    }

    private string GetSiegeEngineDisplayName(SiegeEngineType siegeEngineType)
    {
        return _context.Localization?.T(siegeEngineType switch
        {
            SiegeEngineType.Ram => "siege_engine.ram",
            SiegeEngineType.Catapult => "siege_engine.catapult",
            SiegeEngineType.Ladder => "siege_engine.ladder",
            _ => "ui.none"
        }) ?? siegeEngineType.ToString();
    }

    private static void SelectSiegeEngineTypeOption(OptionButton optionButton, SiegeEngineType siegeEngineType)
    {
        for (var index = 0; index < optionButton.ItemCount; index += 1)
        {
            var metadata = optionButton.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)siegeEngineType)
            {
                optionButton.Select(index);
                return;
            }
        }

        optionButton.Select(0);
    }

    private static SiegeEngineType GetSelectedSiegeEngineType(OptionButton optionButton)
    {
        if (optionButton.Selected < 0)
        {
            return SiegeEngineType.None;
        }

        var metadata = optionButton.GetItemMetadata(optionButton.Selected);
        return metadata.VariantType == Variant.Type.Int ? (SiegeEngineType)metadata.AsInt32() : SiegeEngineType.None;
    }

    private void RefreshOfficerTableText()
    {
        var dialogCity = GetDialogCityContext();
        if (_officerList == null || dialogCity == null)
        {
            return;
        }

        var checkedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        var checkedOfficerSet = checkedOfficerIds.ToHashSet();
        PopulateOfficerList(dialogCity, GetCurrentOfficerCandidateIds());
        var root = _officerList.GetRoot();
        var row = root?.GetFirstChild();
        while (row != null)
        {
            var metadata = row.GetMetadata(1);
            if (metadata.VariantType == Variant.Type.Int && checkedOfficerSet.Contains(metadata.AsInt32()))
            {
                row.SetMetadata(0, true);
            }

            row = row.GetNext();
        }

        UpdateOfficerCheckHighlights();
    }

    private List<int> GetCurrentOfficerCandidateIds()
    {
        var dialogCity = GetDialogCityContext();
        if (dialogCity == null)
        {
            return new List<int>();
        }

        if (_dialogMode != DialogMode.Defense || _editingDomesticReinforcement)
        {
            return _dialogMode == DialogMode.Defense
                ? dialogCity.OfficerIds.ToList()
                : _context.GetAvailableOfficerIdsForOrder().ToList();
        }

        var reservedOfficerIds = GetReservedDefenseOfficerIds();
        return dialogCity.OfficerIds
            .Where(officerId => !reservedOfficerIds.Contains(officerId))
            .ToList();
    }

    private HashSet<int> GetReservedDefenseOfficerIds()
    {
        var reservedOfficerIds = _pendingDefenseCommand?.DefenseReinforcementRequests
            .Where(request => request.IsAllianceRequest && request.EnvoyOfficerId > 0)
            .Select(request => request.EnvoyOfficerId)
            .ToHashSet() ?? new HashSet<int>();
        var sourceCity = GetSelectedDefenseSupportSourceCity();
        var defendingCity = _context.TurnManager?.World?.GetCity(_pendingDefenseCommand?.TargetCityId ?? 0);
        if (_selectedDefenseEnvoyOfficerId > 0 && sourceCity != null && defendingCity != null &&
            sourceCity.OwnerFactionId != defendingCity.OwnerFactionId)
        {
            reservedOfficerIds.Add(_selectedDefenseEnvoyOfficerId);
        }

        return reservedOfficerIds;
    }

    private void OnTargetCitySelected(long _)
    {
        if (_dialogMode == DialogMode.Attack)
        {
            RefreshText();
        }
    }

    private void OnTargetCityMapPressed()
    {
        if (_dialogMode != DialogMode.Attack || _targetCityOption == null)
        {
            return;
        }

        var candidateIds = Enumerable.Range(0, _targetCityOption.ItemCount)
            .Select(index => _targetCityOption.GetItemMetadata(index))
            .Where(metadata => metadata.VariantType == Variant.Type.Int)
            .Select(metadata => metadata.AsInt32())
            .ToList();
        if (candidateIds.Count == 0)
        {
            return;
        }

        _context.ShowStrategicMapSelection(new StrategicMapSelectionRequest
        {
            TitleKey = "ui.strategic_map.select_attack_title",
            PromptKey = "ui.strategic_map.select_attack_prompt",
            Layer = StrategicMapLayer.Military,
            FactionFilter = StrategicMapFactionFilter.Enemy,
            SelectableCityIds = candidateIds,
            SourceCityId = _dialogContextCity?.Id ?? _context.SelectedCity?.Id ?? 0,
            InitialCityId = GetSelectedTargetCity()?.Id ?? candidateIds[0],
            UseAttackTooltip = true,
            Confirmed = SelectTargetCityFromMap
        });
    }

    private void SelectTargetCityFromMap(int cityId)
    {
        if (_targetCityOption == null)
        {
            return;
        }

        for (var index = 0; index < _targetCityOption.ItemCount; index += 1)
        {
            var metadata = _targetCityOption.GetItemMetadata(index);
            if (metadata.VariantType != Variant.Type.Int || metadata.AsInt32() != cityId)
            {
                continue;
            }

            _targetCityOption.Select(index);
            OnTargetCitySelected(index);
            RefreshText();
            BringOverlayToFront();
            return;
        }
    }

    private bool IsReinforcementTarget()
    {
        var sourceCity = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (sourceCity == null || world == null || _targetCityOption == null || _targetCityOption.Selected < 0)
        {
            return false;
        }

        var metadata = _targetCityOption.GetItemMetadata(_targetCityOption.Selected);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return false;
        }

        return world.ActiveBattleCampaigns.Any(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            campaign.TargetCityId == metadata.AsInt32() &&
            campaign.AttackerFactionId == sourceCity.OwnerFactionId);
    }

    private void RefreshTargetCityOptionTexts()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (_targetCityOption == null || world == null || localization == null)
        {
            return;
        }

        for (var index = 0; index < _targetCityOption.ItemCount; index += 1)
        {
            var metadata = _targetCityOption.GetItemMetadata(index);
            if (metadata.VariantType != Variant.Type.Int)
            {
                continue;
            }

            var city = world.GetCity(metadata.AsInt32());
            if (city != null)
            {
                _targetCityOption.SetItemText(index, GetAttackTargetLabel(city));
            }
        }
    }

    private string GetAttackTargetLabel(CityData city)
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            return city.NameEn;
        }

        var ruler = world.GetFaction(city.OwnerFactionId) is { } faction
            ? world.GetOfficer(faction.RulerOfficerId)
            : null;
        return localization.Format(
            "fmt.attack_target_faction_city",
            ruler != null
                ? localization.GetOfficerName(ruler)
                : localization.GetFactionName(world, city.OwnerFactionId),
            localization.GetCityName(city));
    }
}
