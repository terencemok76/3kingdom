using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AttackDialogController : FloatingOverlayController
{
    private enum DialogMode
    {
        Attack,
        Defense
    }

    private readonly MilitaryUiContext _context;
    private readonly Dictionary<int, AttackOfficerDeploymentData> _deployments = new();
    private readonly List<int> _deploymentOfficerOrder = new();
    private OptionButton? _targetCityOption;
    private HBoxContainer? _defenderPlanRow;
    private Label? _defenderPlanLabel;
    private OptionButton? _defenderPlanOption;
    private HBoxContainer? _defenseSupportRow;
    private Label? _defenseSupportLabel;
    private OptionButton? _defenseSupportSource;
    private SpinBox? _defenseSupportTroops;
    private Button? _defenseSupportAddButton;
    private Label? _defenseSupportSummary;
    private HBoxContainer? _defenseMessengerRow;
    private Label? _defenseMessengerLabel;
    private OptionButton? _defenseMessengerOption;
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
    private string _lastSelectionSignature = string.Empty;
    private int _warningAcknowledgedTargetCityId = -1;
    private DialogMode _dialogMode = DialogMode.Attack;
    private CityData? _dialogContextCity;
    private PendingCommandData? _pendingDefenseCommand;
    private readonly Dictionary<int, AttackOfficerDeploymentData> _defenderDeployments = new();
    private bool _editingDomesticReinforcement;
    private int _domesticReinforcementSourceCityId;
    private int _selectedDefenseEnvoyOfficerId;

    protected override Vector2 MinimumOverlaySize => new(620.0f, 630.0f);

    public AttackDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/AttackDialog.tscn")
    {
        _context = context;
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
        SetLabelText("TroopsLabel", _context.Localization.T("ui.attack_troops"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.attack_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.attack_food"));
        SetLabelText("OfficerListLabel", isDefenseMode
            ? (_context.Localization.T("ui.defense_officers") ?? "Defending Officers")
            : _context.Localization.T("ui.attack_officers"));
        SetLabelText("DeploymentListLabel", isDefenseMode
            ? (_context.Localization.T("ui.defense_deployments") ?? "Defense Deployments")
            : _context.Localization.T("ui.attack_deployments"));
        SetFieldRowVisible("GoldRow", !isDefenseMode);
        SetFieldRowVisible("FoodRow", !isDefenseMode);
        if (_defenseSupportRow != null)
        {
            _defenseSupportRow.Visible = isDefenseMode;
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
        _defenderDeployments.Clear();
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
        _deployments.Clear();
        _deploymentOfficerOrder.Clear();
        _defenderDeployments.Clear();
        _editingDomesticReinforcement = false;
        _domesticReinforcementSourceCityId = 0;
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
        _defenderPlanRow = root.GetNodeOrNull<HBoxContainer>("DefenderPlanRow");
        _defenderPlanLabel = root.GetNodeOrNull<Label>("DefenderPlanRow/DefenderPlanLabel");
        _defenderPlanOption = root.GetNodeOrNull<OptionButton>("DefenderPlanRow/DefenderPlanOption");
        _defenseSupportRow = root.GetNodeOrNull<HBoxContainer>("DefenseSupportRow");
        _defenseSupportLabel = root.GetNodeOrNull<Label>("DefenseSupportRow/DefenseSupportLabel");
        _defenseSupportSource = root.GetNodeOrNull<OptionButton>("DefenseSupportRow/DefenseSupportSource");
        _defenseSupportTroops = root.GetNodeOrNull<SpinBox>("DefenseSupportRow/DefenseSupportTroops");
        _defenseSupportAddButton = root.GetNodeOrNull<Button>("DefenseSupportRow/DefenseSupportAddButton");
        _defenseSupportSummary = root.GetNodeOrNull<Label>("DefenseSupportSummary");
        _defenseMessengerRow = root.GetNodeOrNull<HBoxContainer>("DefenseMessengerRow");
        _defenseMessengerLabel = root.GetNodeOrNull<Label>("DefenseMessengerRow/DefenseMessengerLabel");
        _defenseMessengerOption = root.GetNodeOrNull<OptionButton>("DefenseMessengerRow/DefenseMessengerOption");
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
        if (_defenseMessengerOption != null)
        {
            _defenseMessengerOption.ItemSelected += OnDefenseMessengerSelected;
        }
        if (_defenseSupportSource != null)
        {
            _defenseSupportSource.ItemSelected += _ => RefreshDefenseMessengerOptions();
        }

        if (!_targetCitySignalsConnected && _targetCityOption != null)
        {
            _targetCityOption.ItemSelected += OnTargetCitySelected;
            _targetCitySignalsConnected = true;
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
        _context.ConfigureCompactOfficerTableColumns(_officerList, includeCheck: true);
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
            _context.PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
            if (_deployments.ContainsKey(officer.Id))
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
        var selectedOfficerSet = selectedOfficerIds.ToHashSet();

        foreach (var officerId in selectedOfficerIds)
        {
            if (!_deploymentOfficerOrder.Contains(officerId))
            {
                _deploymentOfficerOrder.Add(officerId);
            }
        }

        foreach (var officerId in _deployments.Keys.Where(id => !selectedOfficerSet.Contains(id)).ToList())
        {
            _deployments.Remove(officerId);
        }

        _deploymentOfficerOrder.RemoveAll(officerId => !selectedOfficerSet.Contains(officerId));

        foreach (var officerId in _deploymentOfficerOrder)
        {
            if (!_deployments.ContainsKey(officerId))
            {
                _deployments[officerId] = new AttackOfficerDeploymentData
                {
                    OfficerId = officerId,
                    TroopType = GetDefaultTroopType(),
                    TroopCount = 0,
                    SiegeEngineType = SiegeEngineType.None
                };
            }

            var officer = _context.TurnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            _deploymentList.AddChild(CreateDeploymentRow(officer));
        }

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

    private Control CreateDeploymentRow(OfficerData officer)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0.0f, 32.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);

        row.AddChild(new Label
        {
            Text = _context.Localization?.GetOfficerName(officer) ?? officer.Name,
            CustomMinimumSize = new Vector2(100.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        });

        var troopTypeOption = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(190.0f, 0.0f)
        };
        foreach (var troopType in GetAvailableTroopTypes())
        {
            troopTypeOption.AddItem(GetTroopTypeDisplayName(troopType));
            troopTypeOption.SetItemMetadata(troopTypeOption.ItemCount - 1, (int)troopType);
        }

        var deployment = _deployments[officer.Id];
        SelectTroopTypeOption(troopTypeOption, deployment.TroopType);
        troopTypeOption.ItemSelected += _ =>
        {
            deployment.TroopType = GetSelectedTroopType(troopTypeOption);
            if (deployment.TroopType != TroopType.Siege)
            {
                deployment.SiegeEngineType = SiegeEngineType.None;
            }

            deployment.TroopCount = Mathf.Clamp(deployment.TroopCount, 0, GetMaxDeployableTroopCount(officer.Id, deployment.TroopType));
            _deployments[officer.Id] = deployment;
            RefreshDeploymentEditor();
        };
        ApplyInputThemeToSubtree(troopTypeOption);
        row.AddChild(troopTypeOption);

        var troopCountSpinBox = new SpinBox
        {
            MinValue = 0,
            Step = 1,
            Rounded = true,
            CustomMinimumSize = new Vector2(90.0f, 0.0f)
        };
        ConfigureSpinBox(troopCountSpinBox, GetMaxDeployableTroopCount(officer.Id, deployment.TroopType), deployment.TroopCount);
        troopCountSpinBox.ValueChanged += value =>
        {
            deployment.TroopCount = Mathf.Clamp(
                (int)value,
                0,
                GetMaxDeployableTroopCount(officer.Id, deployment.TroopType));
            _deployments[officer.Id] = deployment;
            RefreshDeploymentEditor();
        };
        ApplyInputThemeToSubtree(troopCountSpinBox);
        row.AddChild(troopCountSpinBox);

        var maxTroopCountButton = new Button
        {
            Text = _context.Localization?.T("ui.max") ?? "Max",
            CustomMinimumSize = new Vector2(58.0f, 0.0f)
        };
        maxTroopCountButton.Pressed += () =>
        {
            deployment.TroopCount = GetMaxDeployableTroopCount(officer.Id, deployment.TroopType);
            _deployments[officer.Id] = deployment;
            RefreshDeploymentEditor();
        };
        _context.ApplyCommandButtonTheme(maxTroopCountButton);
        row.AddChild(maxTroopCountButton);

        var siegeEngineOption = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120.0f, 0.0f)
        };
        PopulateSiegeEngineOption(siegeEngineOption, deployment.SiegeEngineType);
        siegeEngineOption.Disabled = deployment.TroopType != TroopType.Siege;
        siegeEngineOption.Visible = deployment.TroopType == TroopType.Siege;
        siegeEngineOption.ItemSelected += _ =>
        {
            deployment.SiegeEngineType = deployment.TroopType == TroopType.Siege
                ? GetSelectedSiegeEngineType(siegeEngineOption)
                : SiegeEngineType.None;
            _deployments[officer.Id] = deployment;
            UpdateDeploymentSummary();
        };
        ApplyInputThemeToSubtree(siegeEngineOption);
        row.AddChild(siegeEngineOption);

        return row;
    }

    private void UpdateDeploymentSummary()
    {
        var dialogCity = GetDialogCityContext();
        if (_deploymentSummaryLabel == null || dialogCity == null || _context.Localization == null)
        {
            return;
        }

        var activeDeployments = _deployments.Values.Where(item => item.TroopCount > 0).ToList();
        var allocation = BuildTroopAllocation(activeDeployments);
        var siegeEngineAllocation = BuildSiegeEngineAllocation(activeDeployments);
        var summary = string.Join(" | ", new[]
        {
            FormatSummaryPart(TroopType.Infantry, allocation.Infantry, dialogCity.InfantryTroops),
            FormatSummaryPart(TroopType.Spearman, allocation.Spearman, dialogCity.SpearmanTroops),
            FormatSummaryPart(TroopType.Cavalry, allocation.Cavalry, dialogCity.CavalryTroops),
            FormatSummaryPart(TroopType.Archer, allocation.Archer, dialogCity.ArcherTroops),
            FormatSummaryPart(TroopType.Crossbow, allocation.Crossbow, dialogCity.CrossbowTroops),
            FormatSummaryPart(TroopType.Siege, allocation.Siege, dialogCity.SiegeTroops)
        });
        var siegeEngineSummary = string.Join(" | ", new[]
        {
            FormatSiegeEngineSummaryPart(SiegeEngineType.Ram, siegeEngineAllocation.Ram, dialogCity.RamCount),
            FormatSiegeEngineSummaryPart(SiegeEngineType.Catapult, siegeEngineAllocation.Catapult, dialogCity.CatapultCount),
            FormatSiegeEngineSummaryPart(SiegeEngineType.Ladder, siegeEngineAllocation.Ladder, dialogCity.LadderCount)
        });
        _deploymentSummaryLabel.Text = _context.Localization.Format("fmt.attack_deployment_summary", summary, allocation.Total);
        if (siegeEngineAllocation.Total > 0 || dialogCity.RamCount > 0 || dialogCity.CatapultCount > 0 || dialogCity.LadderCount > 0)
        {
            _deploymentSummaryLabel.Text += $"\n{siegeEngineSummary}";
        }
    }

    private void OnConfirmPressed()
    {
        var dialogCity = GetDialogCityContext();
        if (_targetCityOption == null || dialogCity == null)
        {
            return;
        }

        var attackDeployments = _deployments.Values
            .Where(item => item.TroopCount > 0)
            .Select(item => new AttackOfficerDeploymentData
            {
                OfficerId = item.OfficerId,
                TroopType = item.TroopType,
                TroopCount = item.TroopCount,
                SiegeEngineType = item.TroopType == TroopType.Siege ? item.SiegeEngineType : SiegeEngineType.None
            })
            .ToList();

        if (attackDeployments.Count == 0)
        {
            SetWarning(_context.Localization?.T("ui.attack_deployment_required_warning") ?? "Configure troop type and count for each deployed officer.");
            ShowOverlay();
            return;
        }

        var allocation = BuildTroopAllocation(attackDeployments);
        var siegeEngineAllocation = BuildSiegeEngineAllocation(attackDeployments);
        if (allocation.Total <= 0)
        {
            SetWarning(_context.Localization?.T("ui.attack_troops_required_warning") ?? "Enter the number of troops to deploy.");
            ShowOverlay();
            return;
        }

        if (allocation.Infantry > dialogCity.InfantryTroops ||
            allocation.Spearman > dialogCity.SpearmanTroops ||
            allocation.Cavalry > dialogCity.CavalryTroops ||
            allocation.Archer > dialogCity.ArcherTroops ||
            allocation.Crossbow > dialogCity.CrossbowTroops ||
            allocation.Siege > dialogCity.SiegeTroops)
        {
            SetWarning(_context.Localization?.T("ui.attack_deployment_exceed_warning") ?? "Troop deployment exceeds the city's available troop types.");
            ShowOverlay();
            return;
        }

        if (siegeEngineAllocation.Ram > dialogCity.RamCount ||
            siegeEngineAllocation.Catapult > dialogCity.CatapultCount ||
            siegeEngineAllocation.Ladder > dialogCity.LadderCount)
        {
            SetWarning(_context.Localization?.T("ui.attack_siege_engine_exceed_warning") ?? "Assigned siege engines exceed the city's available stock.");
            ShowOverlay();
            return;
        }

        if (_dialogMode == DialogMode.Defense)
        {
            if (_pendingDefenseCommand == null)
            {
                return;
            }

            if (_editingDomesticReinforcement)
            {
                SaveDomesticDefenseReinforcement(attackDeployments);
                return;
            }

            _pendingDefenseCommand.DefenderOfficerDeployments = attackDeployments;
            _pendingDefenseCommand.DefenderBattlePlan = GetSelectedDefenderBattlePlan();
            SetWarning(string.Empty);
            HideOverlay();
            _context.ContinuePendingAttackResolution();
            return;
        }

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

    private void RefreshDefenseSupportControls()
    {
        if (_dialogMode != DialogMode.Defense || _pendingDefenseCommand == null ||
            _context.TurnManager?.World == null || _dialogContextCity == null)
        {
            return;
        }

        var world = _context.TurnManager.World;
        var defendingCity = world.GetCity(_pendingDefenseCommand.TargetCityId) ?? _dialogContextCity;
        var defenderFactionId = defendingCity.OwnerFactionId;
        if (_defenseSupportLabel != null)
        {
            _defenseSupportLabel.Text = _context.Localization?.T("ui.defense_reinforcement") ?? "Reinforce";
        }
        if (_defenseSupportSource != null && !_editingDomesticReinforcement)
        {
            _defenseSupportSource.Clear();
            foreach (var city in defendingCity.ConnectedCityIds
                         .Select(world.GetCity)
                         .Where(city => city != null && city.OwnerFactionId > 0)
                         .Cast<CityData>()
                         .Where(city => city.OwnerFactionId == defenderFactionId || IsActiveAlly(defenderFactionId, city.OwnerFactionId))
                         .OrderBy(city => city.OwnerFactionId == defenderFactionId ? 0 : 1)
                         .ThenBy(city => city.Id))
            {
                var cityName = _context.Localization?.GetCityName(city) ?? city.Name;
                var factionName = _context.Localization?.GetFactionName(world, city.OwnerFactionId) ?? city.OwnerFactionId.ToString();
                _defenseSupportSource.AddItem($"{cityName} ({factionName})");
                _defenseSupportSource.SetItemMetadata(_defenseSupportSource.ItemCount - 1, city.Id);
            }
        }
        if (_defenseSupportTroops != null)
        {
            _defenseSupportTroops.Visible = !_editingDomesticReinforcement;
            _defenseSupportTroops.MinValue = 100;
            _defenseSupportTroops.MaxValue = 10000;
            _defenseSupportTroops.Step = 100;
            _defenseSupportTroops.Value = Math.Max(100, _defenseSupportTroops.Value);
        }
        if (_defenseSupportSource != null)
        {
            _defenseSupportSource.Visible = !_editingDomesticReinforcement;
        }
        if (_defenseSupportAddButton != null)
        {
            _defenseSupportAddButton.Text = _editingDomesticReinforcement
                ? (_context.Localization?.T("ui.defense_reinforcement_save") ?? "Save Reinforcement")
                : (_context.Localization?.T("ui.defense_reinforcement_add") ?? "Add Reinforcement");
        }
        if (_defenseSupportSummary != null)
        {
            _defenseSupportSummary.Visible = _pendingDefenseCommand.DefenseReinforcementRequests.Count > 0;
            _defenseSupportSummary.Text = string.Join("\n", _pendingDefenseCommand.DefenseReinforcementRequests.Select(request =>
            {
                var city = world.GetCity(request.SourceCityId);
                var name = city == null ? request.SourceCityId.ToString() : (_context.Localization?.GetCityName(city) ?? city.Name);
                return request.IsAllianceRequest
                    ? $"{name}: {_context.Localization?.T("ui.defense_reinforcement_ally") ?? "Ally request"} {request.RequestedTroops:N0}"
                    : $"{name}: {_context.Localization?.T("ui.defense_reinforcement_domestic") ?? "Domestic reinforcement"} {request.Deployments.Sum(item => item.TroopCount):N0}";
            }));
        }
    }

    private bool IsActiveAlly(int defenderFactionId, int otherFactionId)
    {
        if (defenderFactionId == otherFactionId || _context.TurnManager?.World == null)
        {
            return false;
        }
        var relation = _context.TurnManager.World.GetDiplomacyRelation(defenderFactionId, otherFactionId);
        return relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 };
    }

    private void RefreshDefenseMessengerOptions()
    {
        if (_defenseMessengerRow == null || _defenseMessengerLabel == null || _defenseMessengerOption == null ||
            _dialogMode != DialogMode.Defense || _pendingDefenseCommand == null || _context.TurnManager?.World == null)
        {
            return;
        }

        var world = _context.TurnManager.World;
        var defendingCity = world.GetCity(_pendingDefenseCommand.TargetCityId);
        var sourceCity = GetSelectedDefenseSupportSourceCity();
        var requiresEnvoy = defendingCity != null && sourceCity != null && sourceCity.OwnerFactionId != defendingCity.OwnerFactionId;
        _defenseMessengerRow.Visible = requiresEnvoy && !_editingDomesticReinforcement;
        if (!requiresEnvoy || defendingCity == null)
        {
            return;
        }

        _defenseMessengerLabel.Text = _context.Localization?.T("ui.defense_reinforcement_envoy") ?? "Envoy";
        var selectedDefenderIds = _context.GetCheckedTreeMetadataIds(_officerList).ToHashSet();
        _defenseMessengerOption.Clear();
        _defenseMessengerOption.AddItem(_context.Localization?.T("ui.defense_reinforcement_envoy_select") ?? "Select envoy");
        _defenseMessengerOption.SetItemMetadata(0, 0);
        var selectedIndex = 0;
        foreach (var officer in defendingCity.OfficerIds
                     .Where(id => !selectedDefenderIds.Contains(id) && !BattleCampaignService.IsOfficerCommitted(world, id))
                     .Select(world.GetOfficer)
                     .Where(officer => officer != null)
                     .Cast<OfficerData>()
                     .OrderByDescending(officer => officer.Intelligence + officer.Charm))
        {
            _defenseMessengerOption.AddItem($"{_context.Localization?.GetOfficerName(officer) ?? officer.Name}  智 {officer.Intelligence}／魅 {officer.Charm}");
            _defenseMessengerOption.SetItemMetadata(_defenseMessengerOption.ItemCount - 1, officer.Id);
            if (officer.Id == _selectedDefenseEnvoyOfficerId)
            {
                selectedIndex = _defenseMessengerOption.ItemCount - 1;
            }
        }
        _defenseMessengerOption.Select(selectedIndex);
    }

    private CityData? GetSelectedDefenseSupportSourceCity()
    {
        var sourceOption = _defenseSupportSource;
        if (sourceOption == null || sourceOption.Selected < 0 || _context.TurnManager?.World == null)
        {
            return null;
        }
        var metadata = sourceOption.GetItemMetadata(sourceOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? _context.TurnManager.World.GetCity(metadata.AsInt32())
            : null;
    }

    private void OnDefenseMessengerSelected(long index)
    {
        if (_defenseMessengerOption == null || index < 0 || index >= _defenseMessengerOption.ItemCount)
        {
            return;
        }
        var metadata = _defenseMessengerOption.GetItemMetadata((int)index);
        _selectedDefenseEnvoyOfficerId = metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : 0;
    }

    private void OnDefenseSupportAddPressed()
    {
        if (_editingDomesticReinforcement)
        {
            OnConfirmPressed();
            return;
        }
        var sourceOption = _defenseSupportSource;
        if (sourceOption == null || sourceOption.Selected < 0 || _context.TurnManager?.World == null || _pendingDefenseCommand == null)
        {
            return;
        }
        var metadata = sourceOption.GetItemMetadata(sourceOption.Selected);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }
        var source = _context.TurnManager.World.GetCity(metadata.AsInt32());
        if (source == null || _dialogContextCity == null)
        {
            return;
        }
        if (source.OwnerFactionId != _dialogContextCity.OwnerFactionId)
        {
            if (_selectedDefenseEnvoyOfficerId <= 0)
            {
                SetWarning(_context.Localization?.T("ui.defense_reinforcement_envoy_required") ?? "Select an envoy for this allied request.");
                return;
            }
            UpsertDefenseReinforcement(new DefenseReinforcementRequestData
            {
                SourceCityId = source.Id,
                IsAllianceRequest = true,
                EnvoyOfficerId = _selectedDefenseEnvoyOfficerId,
                RequestedTroops = _defenseSupportTroops == null ? 1000 : (int)_defenseSupportTroops.Value
            });
            RefreshDefenseSupportControls();
            return;
        }

        _defenderDeployments.Clear();
        foreach (var pair in _deployments)
        {
            _defenderDeployments[pair.Key] = CloneDeployment(pair.Value);
        }
        _deployments.Clear();
        _deploymentOfficerOrder.Clear();
        _dialogContextCity = source;
        _editingDomesticReinforcement = true;
        _domesticReinforcementSourceCityId = source.Id;
        PopulateOfficerList(source, source.OfficerIds.ToList());
        RefreshText();
        RefreshDeploymentEditor();
    }

    private void SaveDomesticDefenseReinforcement(List<AttackOfficerDeploymentData> deployments)
    {
        UpsertDefenseReinforcement(new DefenseReinforcementRequestData
        {
            SourceCityId = _domesticReinforcementSourceCityId > 0 ? _domesticReinforcementSourceCityId : GetDialogCityContext()?.Id ?? 0,
            Deployments = deployments.Select(CloneDeployment).ToList()
        });
        _deployments.Clear();
        foreach (var pair in _defenderDeployments)
        {
            _deployments[pair.Key] = CloneDeployment(pair.Value);
        }
        _deploymentOfficerOrder.Clear();
        _deploymentOfficerOrder.AddRange(_deployments.Keys);
        _dialogContextCity = _context.TurnManager?.World?.GetCity(_pendingDefenseCommand?.TargetCityId ?? 0);
        _editingDomesticReinforcement = false;
        _domesticReinforcementSourceCityId = 0;
        _selectedDefenseEnvoyOfficerId = 0;
        if (_dialogContextCity != null)
        {
            PopulateOfficerList(_dialogContextCity, _dialogContextCity.OfficerIds.ToList());
        }
        RefreshText();
        RefreshDeploymentEditor();
    }

    private void UpsertDefenseReinforcement(DefenseReinforcementRequestData request)
    {
        if (_pendingDefenseCommand == null || request.SourceCityId <= 0)
        {
            return;
        }
        _pendingDefenseCommand.DefenseReinforcementRequests.RemoveAll(item => item.SourceCityId == request.SourceCityId);
        _pendingDefenseCommand.DefenseReinforcementRequests.Add(request);
    }

    private static AttackOfficerDeploymentData CloneDeployment(AttackOfficerDeploymentData deployment) => new()
    {
        OfficerId = deployment.OfficerId,
        TroopType = deployment.TroopType,
        TroopCount = deployment.TroopCount,
        SiegeEngineType = deployment.SiegeEngineType
    };

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

    private List<TroopType> GetAvailableTroopTypes()
    {
        var result = new List<TroopType>();
        foreach (var troopType in Enum.GetValues<TroopType>())
        {
            if (GetAvailableTroopCount(troopType) > 0)
            {
                result.Add(troopType);
            }
        }

        return result.Count == 0 ? new List<TroopType> { TroopType.Infantry } : result;
    }

    private TroopType GetDefaultTroopType() => GetAvailableTroopTypes().FirstOrDefault();

    private int GetAvailableTroopCount(TroopType troopType) => GetDialogCityContext()?.GetTroops(troopType) ?? 0;

    private int GetMaxDeployableTroopCount(int officerId, TroopType troopType)
    {
        var alreadyAssigned = _deployments.Values
            .Where(deployment => deployment.OfficerId != officerId && deployment.TroopType == troopType)
            .Sum(deployment => deployment.TroopCount);
        return Math.Max(0, GetAvailableTroopCount(troopType) - alreadyAssigned);
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
            TroopType.Siege => "troop_type.siege",
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

    private static void SelectTroopTypeOption(OptionButton optionButton, TroopType troopType)
    {
        for (var index = 0; index < optionButton.ItemCount; index += 1)
        {
            var metadata = optionButton.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)troopType)
            {
                optionButton.Select(index);
                return;
            }
        }

        if (optionButton.ItemCount > 0)
        {
            optionButton.Select(0);
        }
    }

    private static TroopType GetSelectedTroopType(OptionButton optionButton)
    {
        if (optionButton.Selected < 0)
        {
            return TroopType.Infantry;
        }

        var metadata = optionButton.GetItemMetadata(optionButton.Selected);
        return metadata.VariantType == Variant.Type.Int ? (TroopType)metadata.AsInt32() : TroopType.Infantry;
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

    private static TroopAllocationData BuildTroopAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var deployment in deployments)
        {
            switch (deployment.TroopType)
            {
                case TroopType.Infantry:
                    allocation.Infantry += deployment.TroopCount;
                    break;
                case TroopType.Spearman:
                    allocation.Spearman += deployment.TroopCount;
                    break;
                case TroopType.Cavalry:
                    allocation.Cavalry += deployment.TroopCount;
                    break;
                case TroopType.Archer:
                    allocation.Archer += deployment.TroopCount;
                    break;
                case TroopType.Crossbow:
                    allocation.Crossbow += deployment.TroopCount;
                    break;
                case TroopType.Siege:
                    allocation.Siege += deployment.TroopCount;
                    break;
            }
        }

        return allocation;
    }

    private static SiegeEngineAllocationData BuildSiegeEngineAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new SiegeEngineAllocationData();
        foreach (var deployment in deployments)
        {
            if (deployment.TroopType != TroopType.Siege || deployment.TroopCount <= 0)
            {
                continue;
            }

            switch (deployment.SiegeEngineType)
            {
                case SiegeEngineType.Ram:
                    allocation.Ram += 1;
                    break;
                case SiegeEngineType.Catapult:
                    allocation.Catapult += 1;
                    break;
                case SiegeEngineType.Ladder:
                    allocation.Ladder += 1;
                    break;
            }
        }

        return allocation;
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

        return _dialogMode == DialogMode.Defense
            ? dialogCity.OfficerIds.ToList()
            : _context.GetAvailableOfficerIdsForOrder().ToList();
    }

    private void OnTargetCitySelected(long _)
    {
        if (_dialogMode == DialogMode.Attack)
        {
            RefreshText();
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

        return localization.Format(
            "fmt.attack_target_faction_city",
            localization.GetFactionName(world, city.OwnerFactionId),
            localization.GetCityName(city));
    }
}
