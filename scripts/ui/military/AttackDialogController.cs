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
    private string _lastSelectionSignature = string.Empty;
    private int _warningAcknowledgedTargetCityId = -1;
    private DialogMode _dialogMode = DialogMode.Attack;
    private CityData? _dialogContextCity;
    private PendingCommandData? _pendingDefenseCommand;

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
        SetOverlayTitleText(isDefenseMode
            ? (_context.Localization.T("ui.defense") ?? "Defense")
            : _context.Localization.T("ui.attack"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = isDefenseMode
                ? (_context.Localization.T("ui.confirm_defense") ?? "Confirm Defense")
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
        RefreshText();
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

            var label = _context.Localization?.GetCityName(city) ?? city.NameEn;
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
        RefreshText();
        SetWarning(string.Empty);
        _warningAcknowledgedTargetCityId = -1;

        _targetCityOption.Clear();
        var attackerLabel = _context.Localization?.GetCityName(attackingCity) ?? attackingCity.NameEn;
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
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
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
                    TroopCount = 0
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
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
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
            deployment.TroopCount = Mathf.Clamp(deployment.TroopCount, 0, GetAvailableTroopCount(deployment.TroopType));
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
        ConfigureSpinBox(troopCountSpinBox, GetAvailableTroopCount(deployment.TroopType), deployment.TroopCount);
        troopCountSpinBox.ValueChanged += value =>
        {
            deployment.TroopCount = (int)value;
            _deployments[officer.Id] = deployment;
            UpdateDeploymentSummary();
        };
        ApplyInputThemeToSubtree(troopCountSpinBox);
        row.AddChild(troopCountSpinBox);

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
        var summary = string.Join(" | ", new[]
        {
            FormatSummaryPart(TroopType.Infantry, allocation.Infantry, dialogCity.InfantryTroops),
            FormatSummaryPart(TroopType.Spearman, allocation.Spearman, dialogCity.SpearmanTroops),
            FormatSummaryPart(TroopType.Cavalry, allocation.Cavalry, dialogCity.CavalryTroops),
            FormatSummaryPart(TroopType.Archer, allocation.Archer, dialogCity.ArcherTroops),
            FormatSummaryPart(TroopType.Crossbow, allocation.Crossbow, dialogCity.CrossbowTroops),
            FormatSummaryPart(TroopType.Siege, allocation.Siege, dialogCity.SiegeTroops)
        });
        _deploymentSummaryLabel.Text = _context.Localization.Format("fmt.attack_deployment_summary", summary, allocation.Total);
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
                TroopCount = item.TroopCount
            })
            .ToList();

        if (attackDeployments.Count == 0)
        {
            SetWarning(_context.Localization?.T("ui.attack_deployment_required_warning") ?? "Configure troop type and count for each deployed officer.");
            ShowOverlay();
            return;
        }

        var allocation = BuildTroopAllocation(attackDeployments);
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

        if (_dialogMode == DialogMode.Defense)
        {
            if (_pendingDefenseCommand == null)
            {
                return;
            }

            _pendingDefenseCommand.DefenderOfficerDeployments = attackDeployments;
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
            attackDeployments.Select(item => item.OfficerId).Distinct().ToList());

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
}
