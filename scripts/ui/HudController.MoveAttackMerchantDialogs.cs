using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void EnsureMoveDialogWidgets()
    {
        if (_moveDialog == null)
        {
            return;
        }

        var existingRoot = _moveDialog.GetNodeOrNull<VBoxContainer>("MoveDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("MoveDialogRoot not found in MoveDialog.tscn.");
            return;
        }

        _moveTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityOption");
        _moveTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsSpinBox");
        _moveGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
        _moveFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
        _moveHorseSpinBox = existingRoot.GetNodeOrNull<SpinBox>("HorseSpinBox");
        _moveOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
        _moveConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_moveDialogSignalsConnected && _moveConfirmButton != null)
        {
            _moveConfirmButton.Pressed += OnMoveDialogConfirmed;
            _moveDialogSignalsConnected = true;
        }
    }

    private void EnsureMerchantDialogWidgets()
    {
        if (_merchantDialog == null)
        {
            return;
        }

        var existingRoot = _merchantDialog.GetNodeOrNull<VBoxContainer>("MerchantDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("MerchantDialogRoot not found in MerchantDialog.tscn.");
            return;
        }

        _merchantModeOption = existingRoot.GetNodeOrNull<OptionButton>("TradeModeRow/TradeModeOption");
        _merchantFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _merchantSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
        _merchantConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        ConnectMerchantDialogSignals();
    }

    private Label CreateMoveFieldLabel(string name)
    {
        return new Label
        {
            Name = name
        };
    }

    private SpinBox CreateMoveSpinBox(string name)
    {
        return new SpinBox
        {
            Name = name,
            MinValue = 0,
            Step = 1,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
    }

    private void ShowMoveDialog(List<int> candidateIds)
    {
        if (_turnManager?.World == null || _selectedCity == null || _moveDialog == null || _moveTargetCityOption == null)
        {
            return;
        }

        EnsureMoveDialogWidgets();
        UpdateMoveDialogText();

        _moveTargetCityOption.Clear();
        foreach (var cityId in candidateIds)
        {
            var city = _turnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            var label = _localization?.GetCityName(city) ?? city.NameEn;
            _moveTargetCityOption.AddItem(label);
            _moveTargetCityOption.SetItemMetadata(_moveTargetCityOption.ItemCount - 1, city.Id);
        }

        if (_moveTargetCityOption.ItemCount > 0)
        {
            _moveTargetCityOption.Select(0);
        }

        ConfigureMoveSpinBox(_moveTroopsSpinBox, _selectedCity.Troops, _selectedCity.Troops / 2);
        ConfigureMoveSpinBox(_moveGoldSpinBox, _selectedCity.Gold, _selectedCity.Gold / 2);
        ConfigureMoveSpinBox(_moveFoodSpinBox, _selectedCity.Food, _selectedCity.Food / 2);
        ConfigureMoveSpinBox(_moveHorseSpinBox, _selectedCity.Horses, _selectedCity.Horses / 2);

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (_moveOfficerList != null)
        {
            _moveOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_moveOfficerList, includeCheck: true);
            var tableRoot = _moveOfficerList.CreateItem();
            var rowIndex = 0;
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var row = _moveOfficerList.CreateItem(tableRoot);
                PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
                rowIndex += 1;
            }
        }

        PopupDialogUsingSceneSize(_moveDialog);
    }

    private void EnsureAttackDialogWidgets()
    {
        if (_attackDialog == null)
        {
            return;
        }

        var existingRoot = _attackDialog.GetNodeOrNull<VBoxContainer>("AttackDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("AttackDialogRoot not found in AttackDialog.tscn.");
            return;
        }

        _attackTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
        _attackTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsRow/TroopsSpinBox");
        _attackGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _attackFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _attackOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
        _attackDeploymentScroll = existingRoot.GetNodeOrNull<ScrollContainer>("DeploymentScroll");
        _attackDeploymentList = existingRoot.GetNodeOrNull<VBoxContainer>("DeploymentScroll/DeploymentList");
        _attackDeploymentSummaryLabel = existingRoot.GetNodeOrNull<Label>("DeploymentSummaryLabel");
        _attackWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
        _attackConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_attackOfficerList != null)
        {
            _attackOfficerList.CustomMinimumSize = new Vector2(0.0f, 150.0f);
            _attackOfficerList.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        }

        if (_attackDeploymentScroll != null)
        {
            _attackDeploymentScroll.CustomMinimumSize = new Vector2(0.0f, 160.0f);
            _attackDeploymentScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _attackDeploymentScroll.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        }

        if (_attackDeploymentList != null)
        {
            _attackDeploymentList.CustomMinimumSize = new Vector2(0.0f, 0.0f);
            _attackDeploymentList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _attackDeploymentList.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        }

        if (_attackWarningLabel != null)
        {
            _attackWarningLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.52f, 0.45f, 1.0f));
        }

        ConnectAttackDialogSignals();
    }

    private void ConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int defaultValue)
    {
        if (spinBox == null)
        {
            return;
        }

        spinBox.MaxValue = maxValue;
        spinBox.Value = maxValue <= 0 ? 0 : Mathf.Clamp(defaultValue, 0, maxValue);
    }

    private static void ConfigureAttackTroopsSpinBox(SpinBox? spinBox, int availableTroops)
    {
        if (spinBox == null)
        {
            return;
        }

        // Allow over-typing here so confirm-time validation can show a real warning instead of silent clamping.
        spinBox.MinValue = 0;
        spinBox.MaxValue = Mathf.Max(availableTroops * 10, 99999);
        spinBox.Value = availableTroops <= 0 ? 0 : availableTroops / 2;
    }

    private static int GetRequestedSpinBoxValue(SpinBox? spinBox)
    {
        if (spinBox == null)
        {
            return 0;
        }

        // Read the raw text first because SpinBox.Value may already be clamped to MaxValue.
        var lineEdit = spinBox.GetLineEdit();
        if (lineEdit != null)
        {
            var rawText = lineEdit.Text?.Trim();
            if (!string.IsNullOrEmpty(rawText) && int.TryParse(rawText, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return (int)spinBox.Value;
    }

    private void ShowAttackDialog(List<int> candidateIds)
    {
        if (_turnManager?.World == null || _selectedCity == null || _attackDialog == null)
        {
            return;
        }

        _attackDialogMode = AttackDialogMode.Attack;
        _attackDialogContextCity = _selectedCity;
        _pendingDefenseCommand = null;
        EnsureAttackDialogWidgets();
        if (_attackTargetCityOption == null)
        {
            return;
        }

        UpdateAttackDialogText();
        SetAttackDialogWarning(string.Empty);
        _attackDiplomacyWarningAcknowledgedTargetCityId = -1;

        _attackTargetCityOption.Clear();
        _attackTargetCityOption.Disabled = false;
        foreach (var cityId in candidateIds)
        {
            var city = _turnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            var label = _localization?.GetCityName(city) ?? city.NameEn;
            _attackTargetCityOption.AddItem(label);
            _attackTargetCityOption.SetItemMetadata(_attackTargetCityOption.ItemCount - 1, city.Id);
        }

        if (_attackTargetCityOption.ItemCount > 0)
        {
            _attackTargetCityOption.Select(0);
        }

        ConfigureAttackTroopsSpinBox(_attackTroopsSpinBox, _selectedCity.Troops);
        ConfigureMoveSpinBox(_attackGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_attackFoodSpinBox, _selectedCity.Food, 0);
        _lastAttackDeploymentSelectionSignature = string.Empty;
        _attackOfficerDeployments.Clear();
        _attackDeploymentOfficerOrder.Clear();

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (_attackOfficerList != null)
        {
            _attackOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_attackOfficerList, includeCheck: true);
            _attackOfficerList.CustomMinimumSize = new Vector2(0.0f, 150.0f);
            _attackOfficerList.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            var tableRoot = _attackOfficerList.CreateItem();
            var rowIndex = 0;
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var row = _attackOfficerList.CreateItem(tableRoot);
                PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
                rowIndex += 1;
            }
        }

        RefreshAttackDeploymentEditor();
        PopupDialogUsingSceneSize(_attackDialog);
    }

    private void ShowDefenseAttackDialog(PendingCommandData pendingCommand, CityData defendingCity, CityData attackingCity)
    {
        if (_attackDialog == null)
        {
            return;
        }

        _attackDialogMode = AttackDialogMode.Defense;
        _attackDialogContextCity = defendingCity;
        _pendingDefenseCommand = pendingCommand;
        EnsureAttackDialogWidgets();
        if (_attackTargetCityOption == null)
        {
            return;
        }

        UpdateAttackDialogText();
        SetAttackDialogWarning(string.Empty);
        _attackDiplomacyWarningAcknowledgedTargetCityId = -1;

        _attackTargetCityOption.Clear();
        var attackerLabel = _localization?.GetCityName(attackingCity) ?? attackingCity.NameEn;
        _attackTargetCityOption.AddItem(attackerLabel);
        _attackTargetCityOption.SetItemMetadata(0, attackingCity.Id);
        _attackTargetCityOption.Select(0);
        _attackTargetCityOption.Disabled = true;

        ConfigureAttackTroopsSpinBox(_attackTroopsSpinBox, defendingCity.Troops);
        ConfigureMoveSpinBox(_attackGoldSpinBox, 0, 0);
        ConfigureMoveSpinBox(_attackFoodSpinBox, 0, 0);
        _lastAttackDeploymentSelectionSignature = string.Empty;
        _attackOfficerDeployments.Clear();
        _attackDeploymentOfficerOrder.Clear();

        var defendingOfficerIds = new HashSet<int>(defendingCity.OfficerIds);
        if (_attackOfficerList != null)
        {
            _attackOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_attackOfficerList, includeCheck: true);
            _attackOfficerList.CustomMinimumSize = new Vector2(0.0f, 150.0f);
            _attackOfficerList.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            var tableRoot = _attackOfficerList.CreateItem();
            var rowIndex = 0;
            foreach (var officerId in defendingCity.OfficerIds)
            {
                if (!defendingOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager?.World?.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var row = _attackOfficerList.CreateItem(tableRoot);
                PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
                rowIndex += 1;
            }
        }

        RefreshAttackDeploymentEditor();
        PopupDialogUsingSceneSize(_attackDialog);
    }

    private void ShowMerchantDialog()
    {
        if (_merchantDialog == null || _merchantModeOption == null)
        {
            return;
        }

        EnsureMerchantDialogWidgets();
        UpdateMerchantDialogText();

        _merchantModeOption.Clear();
        _merchantModeOption.AddItem(_localization?.T("ui.buy_food") ?? "Buy Food");
        _merchantModeOption.SetItemMetadata(0, (int)MerchantTradeMode.BuyFood);
        _merchantModeOption.AddItem(_localization?.T("ui.sell_food") ?? "Sell Food");
        _merchantModeOption.SetItemMetadata(1, (int)MerchantTradeMode.SellFood);
        _merchantModeOption.AddItem(_localization?.T("ui.buy_horse") ?? "Buy Horse");
        _merchantModeOption.SetItemMetadata(2, (int)MerchantTradeMode.BuyHorse);
        _merchantModeOption.Select(0);

        UpdateMerchantFoodSpinBoxRange();
        UpdateMerchantTradeSummary();
        PopupDialogUsingSceneSize(_merchantDialog);
    }

    private void UpdateMoveDialogText()
    {
        if (_moveDialog == null || _localization == null)
        {
            return;
        }

        _moveDialog.Title = _localization.T("ui.move");
        if (_moveConfirmButton != null)
        {
            _moveConfirmButton.Text = _localization.T("ui.confirm_move");
        }

        SetMoveDialogLabelText("TargetCityLabel", _localization.T("ui.target_city"));
        SetMoveDialogLabelText("TroopsLabel", _localization.T("ui.transfer_troops"));
        SetMoveDialogLabelText("GoldLabel", _localization.T("ui.transfer_gold"));
        SetMoveDialogLabelText("FoodLabel", _localization.T("ui.transfer_food"));
        SetMoveDialogLabelText("HorseLabel", _localization.T("ui.transfer_horse"));
        SetMoveDialogLabelText("OfficerListLabel", _localization.T("ui.transfer_officers"));
    }

    private void UpdateMerchantDialogText()
    {
        if (_merchantDialog == null || _localization == null)
        {
            return;
        }

        _merchantDialog.Title = _localization.T("ui.merchant");
        SetMerchantDialogLabelText("TradeModeLabel", _localization.T("ui.trade_mode"));
        SetMerchantDialogLabelText("FoodLabel", _localization.T("ui.trade_amount"));
        if (_merchantConfirmButton != null)
        {
            _merchantConfirmButton.Text = _localization.T("ui.confirm_merchant");
        }
        UpdateMerchantTradeSummary();
    }

    private void UpdateAttackDialogText()
    {
        if (_attackDialog == null || _localization == null)
        {
            return;
        }

        var isDefenseMode = _attackDialogMode == AttackDialogMode.Defense;
        _attackDialog.Title = isDefenseMode
            ? (_localization.T("ui.defense") ?? "Defense")
            : _localization.T("ui.attack");
        if (_attackConfirmButton != null)
        {
            _attackConfirmButton.Text = isDefenseMode
                ? (_localization.T("ui.confirm_defense") ?? "Confirm Defense")
                : _localization.T("ui.confirm_attack");
        }

        SetAttackDialogLabelText("TargetCityLabel", isDefenseMode ? _localization.T("ui.attack") : _localization.T("ui.target_city"));
        SetAttackDialogLabelText("TroopsLabel", _localization.T("ui.attack_troops"));
        SetAttackDialogLabelText("GoldLabel", _localization.T("ui.attack_gold"));
        SetAttackDialogLabelText("FoodLabel", _localization.T("ui.attack_food"));
        SetAttackDialogLabelText("OfficerListLabel", isDefenseMode
            ? (_localization.T("ui.defense_officers") ?? "Defending Officers")
            : _localization.T("ui.attack_officers"));
        SetAttackDialogLabelText("DeploymentListLabel", isDefenseMode
            ? (_localization.T("ui.defense_deployments") ?? "Defense Deployments")
            : _localization.T("ui.attack_deployments"));
        SetAttackFieldRowVisible("GoldRow", !isDefenseMode);
        SetAttackFieldRowVisible("FoodRow", !isDefenseMode);
        UpdateAttackDeploymentSummary();
    }

    private void SetMoveDialogLabelText(string nodeName, string text)
    {
        var label = _moveDialog?.GetNodeOrNull<Label>($"MoveDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SetAttackDialogLabelText(string nodeName, string text)
    {
        var root = _attackDialog?.GetNodeOrNull<Control>("AttackDialogRoot");
        var label = root?.FindChild(nodeName, recursive: true, owned: false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SetAttackDialogWarning(string text)
    {
        if (_attackWarningLabel == null)
        {
            return;
        }

        _attackWarningLabel.Text = text;
        _attackWarningLabel.Visible = !string.IsNullOrWhiteSpace(text);
    }

    private void SetAttackFieldRowVisible(string rowName, bool visible)
    {
        var root = _attackDialog?.GetNodeOrNull<Control>("AttackDialogRoot");
        var row = root?.FindChild(rowName, recursive: true, owned: false) as Control;
        if (row != null)
        {
            row.Visible = visible;
        }
    }

    private void ConnectAttackDialogSignals()
    {
        if (_attackOfficerListSignalsConnected || _attackOfficerList == null)
        {
            return;
        }

        _attackOfficerList.ItemEdited += OnAttackOfficerListEdited;
        _attackOfficerListSignalsConnected = true;
    }

    private void OnAttackOfficerListEdited()
    {
        RefreshAttackDeploymentEditor();
    }

    private void SyncAttackDeploymentEditorSelection()
    {
        if (_attackDialog == null || !_attackDialog.Visible)
        {
            _lastAttackDeploymentSelectionSignature = string.Empty;
            return;
        }

        var selectedOfficerIds = GetCheckedTreeMetadataIds(_attackOfficerList);
        selectedOfficerIds.Sort();
        var selectionSignature = string.Join(",", selectedOfficerIds);
        if (_lastAttackDeploymentSelectionSignature == selectionSignature)
        {
            return;
        }

        _lastAttackDeploymentSelectionSignature = selectionSignature;
        RefreshAttackDeploymentEditor();
    }

    private void RefreshAttackDeploymentEditor()
    {
        if (_attackDeploymentList == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        foreach (var child in _attackDeploymentList.GetChildren())
        {
            child.QueueFree();
        }

        var selectedOfficerIds = GetCheckedTreeMetadataIds(_attackOfficerList);
        var selectedOfficerSet = selectedOfficerIds.ToHashSet();

        foreach (var officerId in selectedOfficerIds)
        {
            if (!_attackDeploymentOfficerOrder.Contains(officerId))
            {
                _attackDeploymentOfficerOrder.Add(officerId);
            }
        }

        foreach (var officerId in _attackOfficerDeployments.Keys.Where(id => !selectedOfficerSet.Contains(id)).ToList())
        {
            _attackOfficerDeployments.Remove(officerId);
        }

        _attackDeploymentOfficerOrder.RemoveAll(officerId => !selectedOfficerSet.Contains(officerId));

        foreach (var officerId in _attackDeploymentOfficerOrder)
        {
            if (!_attackOfficerDeployments.ContainsKey(officerId))
            {
                _attackOfficerDeployments[officerId] = new AttackOfficerDeploymentData
                {
                    OfficerId = officerId,
                    TroopType = GetDefaultAttackTroopType(),
                    TroopCount = 0
                };
            }

            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            _attackDeploymentList.AddChild(CreateAttackDeploymentRow(officer));
        }

        if (selectedOfficerIds.Count == 0)
        {
            _attackDeploymentList.AddChild(new Label
            {
                Text = _localization.T("ui.attack_select_officers_hint"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        UpdateAttackDeploymentSummary();
    }

    private Control CreateAttackDeploymentRow(OfficerData officer)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0.0f, 32.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);

        var officerLabel = new Label
        {
            Text = _localization?.GetOfficerName(officer) ?? officer.Name,
            CustomMinimumSize = new Vector2(100.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(officerLabel);

        var troopTypeLabel = new Label
        {
            Text = _localization?.T("ui.attack_troop_type") ?? "Troop Type",
            CustomMinimumSize = new Vector2(62.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(troopTypeLabel);

        var troopTypeOption = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        foreach (var troopType in GetAvailableAttackTroopTypes())
        {
            troopTypeOption.AddItem(GetAttackTroopTypeDisplayName(troopType));
            troopTypeOption.SetItemMetadata(troopTypeOption.ItemCount - 1, (int)troopType);
        }

        var deployment = _attackOfficerDeployments[officer.Id];
        SelectAttackTroopTypeOption(troopTypeOption, deployment.TroopType);
        troopTypeOption.ItemSelected += _ =>
        {
            var selectedType = GetSelectedAttackTroopType(troopTypeOption);
            deployment.TroopType = selectedType;
            deployment.TroopCount = Mathf.Clamp(deployment.TroopCount, 0, GetAttackTroopTypeAvailableCount(selectedType));
            _attackOfficerDeployments[officer.Id] = deployment;
            RefreshAttackDeploymentEditor();
        };
        row.AddChild(troopTypeOption);

        var troopCountLabel = new Label
        {
            Text = _localization?.T("ui.attack_troops") ?? "Troops",
            CustomMinimumSize = new Vector2(48.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(troopCountLabel);

        var troopCountSpinBox = CreateMoveSpinBox($"AttackTroopsSpinBox_{officer.Id}");
        troopCountSpinBox.CustomMinimumSize = new Vector2(90.0f, 0.0f);
        var maxTroops = GetAttackTroopTypeAvailableCount(deployment.TroopType);
        ConfigureMoveSpinBox(troopCountSpinBox, maxTroops, deployment.TroopCount);
        troopCountSpinBox.ValueChanged += value =>
        {
            deployment.TroopCount = (int)value;
            _attackOfficerDeployments[officer.Id] = deployment;
            UpdateAttackDeploymentSummary();
        };
        row.AddChild(troopCountSpinBox);

        var availableLabel = new Label
        {
            Text = string.Format("{0} {1}", _localization?.T("ui.available_short") ?? "Avail", maxTroops),
            CustomMinimumSize = new Vector2(72.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(availableLabel);

        return row;
    }

    private void UpdateAttackDeploymentSummary()
    {
        var dialogCity = GetAttackDialogCityContext();
        if (_attackDeploymentSummaryLabel == null || dialogCity == null || _localization == null)
        {
            return;
        }

        var activeDeployments = _attackOfficerDeployments.Values.Where(item => item.TroopCount > 0).ToList();
        var allocation = BuildTroopAllocationFromAttackDeployments(activeDeployments);
        var summary = string.Join(" | ", new[]
        {
            FormatAttackDeploymentSummaryPart(TroopType.Infantry, allocation.Infantry, dialogCity.InfantryTroops),
            FormatAttackDeploymentSummaryPart(TroopType.Spearman, allocation.Spearman, dialogCity.SpearmanTroops),
            FormatAttackDeploymentSummaryPart(TroopType.Cavalry, allocation.Cavalry, dialogCity.CavalryTroops),
            FormatAttackDeploymentSummaryPart(TroopType.Archer, allocation.Archer, dialogCity.ArcherTroops),
            FormatAttackDeploymentSummaryPart(TroopType.Crossbow, allocation.Crossbow, dialogCity.CrossbowTroops),
            FormatAttackDeploymentSummaryPart(TroopType.Siege, allocation.Siege, dialogCity.SiegeTroops)
        });
        _attackDeploymentSummaryLabel.Text = _localization.Format("fmt.attack_deployment_summary", summary, allocation.Total);
    }

    private string FormatAttackDeploymentSummaryPart(TroopType troopType, int assigned, int available)
    {
        return string.Format("{0} {1}/{2}", GetAttackTroopTypeDisplayName(troopType), assigned, available);
    }

    private static TroopAllocationData BuildTroopAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
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

    private List<TroopType> GetAvailableAttackTroopTypes()
    {
        var dialogCity = GetAttackDialogCityContext();
        if (dialogCity == null)
        {
            return new List<TroopType> { TroopType.Infantry };
        }

        var result = new List<TroopType>();
        foreach (var troopType in Enum.GetValues<TroopType>())
        {
            if (GetAttackTroopTypeAvailableCount(troopType) > 0)
            {
                result.Add(troopType);
            }
        }

        return result.Count == 0 ? new List<TroopType> { TroopType.Infantry } : result;
    }

    private TroopType GetDefaultAttackTroopType()
    {
        return GetAvailableAttackTroopTypes().FirstOrDefault();
    }

    private int GetDefaultAttackTroopCount(TroopType troopType)
    {
        var available = GetAttackTroopTypeAvailableCount(troopType);
        if (available <= 0)
        {
            return 0;
        }

        return Math.Min(available, 100);
    }

    private int GetAttackTroopTypeAvailableCount(TroopType troopType)
    {
        return GetAttackDialogCityContext()?.GetTroops(troopType) ?? 0;
    }

    private string GetAttackTroopTypeDisplayName(TroopType troopType)
    {
        return _localization?.T(troopType switch
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

    private static void SelectAttackTroopTypeOption(OptionButton optionButton, TroopType troopType)
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

    private static TroopType GetSelectedAttackTroopType(OptionButton optionButton)
    {
        if (optionButton.Selected < 0)
        {
            return TroopType.Infantry;
        }

        var metadata = optionButton.GetItemMetadata(optionButton.Selected);
        return metadata.VariantType == Variant.Type.Int ? (TroopType)metadata.AsInt32() : TroopType.Infantry;
    }

    private void ReopenAttackDialog()
    {
        if (_attackDialog == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenAttackDialogDeferred));
    }

    private void ReopenAttackDialogDeferred()
    {
        if (_attackDialog == null)
        {
            return;
        }

        PopupDialogUsingSceneSize(_attackDialog);
    }

    private bool ShouldWarnAttackBreakPact(int targetCityId)
    {
        if (_attackDialogMode != AttackDialogMode.Attack || _turnManager?.World == null)
        {
            return false;
        }

        var sourceCity = GetAttackDialogCityContext();
        var targetCity = _turnManager.World.GetCity(targetCityId);
        if (sourceCity == null || targetCity == null || sourceCity.OwnerFactionId == targetCity.OwnerFactionId)
        {
            return false;
        }

        var relation = _turnManager.World.GetDiplomacyRelation(sourceCity.OwnerFactionId, targetCity.OwnerFactionId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }

    private CityData? GetAttackDialogCityContext()
    {
        return _attackDialogContextCity ?? _selectedCity;
    }

    private void SetMerchantDialogLabelText(string nodeName, string text)
    {
        var root = _merchantDialog?.GetNodeOrNull<Control>("MerchantDialogRoot");
        var label = root?.FindChild(nodeName, recursive: true, owned: false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ConnectMerchantDialogSignals()
    {
        if (_merchantDialogSignalsConnected)
        {
            return;
        }

        if (_merchantModeOption != null)
        {
            _merchantModeOption.ItemSelected += OnMerchantModeSelected;
        }

        if (_merchantFoodSpinBox != null)
        {
            _merchantFoodSpinBox.ValueChanged += OnMerchantFoodValueChanged;
        }

        if (_merchantConfirmButton != null)
        {
            _merchantConfirmButton.Pressed += OnMerchantDialogConfirmed;
        }

        _merchantDialogSignalsConnected = true;
    }

    private void OnMerchantModeSelected(long index)
    {
        UpdateMerchantFoodSpinBoxRange();
        UpdateMerchantTradeSummary();
    }

    private void OnMerchantFoodValueChanged(double value)
    {
        UpdateMerchantTradeSummary();
    }

    private void UpdateMerchantFoodSpinBoxRange()
    {
        if (_merchantFoodSpinBox == null || _merchantModeOption == null || _selectedCity == null)
        {
            return;
        }

        var tradeMode = GetSelectedMerchantTradeMode();
        var maxAmount = tradeMode switch
        {
            MerchantTradeMode.SellFood => _selectedCity.Food,
            MerchantTradeMode.BuyHorse => (_selectedCity.Gold / 20) * 10,
            _ => (_selectedCity.Gold / 10) * 100
        };
        ConfigureMoveSpinBox(_merchantFoodSpinBox, maxAmount, maxAmount > 0 ? (tradeMode == MerchantTradeMode.BuyHorse ? 10 : 100) : 0);
        _merchantFoodSpinBox.Step = tradeMode == MerchantTradeMode.BuyHorse ? 10 : 100;
    }

    private void UpdateMerchantTradeSummary()
    {
        if (_merchantSummaryLabel == null || _merchantFoodSpinBox == null || _merchantModeOption == null || _localization == null)
        {
            return;
        }

        var amount = (int)_merchantFoodSpinBox.Value;
        var tradeMode = GetSelectedMerchantTradeMode();
        if (tradeMode == MerchantTradeMode.SellFood)
        {
            var goldAmount = amount / 100 * 10;
            _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_sell_preview", amount, goldAmount);
            return;
        }

        if (tradeMode == MerchantTradeMode.BuyHorse)
        {
            var goldCost = amount / 10 * 20;
            _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_buy_horse_preview", goldCost, amount);
            return;
        }

        var buyGoldAmount = amount / 100 * 10;
        _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_buy_preview", buyGoldAmount, amount);
    }

    private MerchantTradeMode GetSelectedMerchantTradeMode()
    {
        if (_merchantModeOption == null)
        {
            return MerchantTradeMode.BuyFood;
        }

        if (_merchantModeOption.ItemCount == 0 || _merchantModeOption.Selected < 0)
        {
            return MerchantTradeMode.BuyFood;
        }

        var metadata = _merchantModeOption.GetItemMetadata(_merchantModeOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (MerchantTradeMode)metadata.AsInt32()
            : MerchantTradeMode.BuyFood;
    }


}
