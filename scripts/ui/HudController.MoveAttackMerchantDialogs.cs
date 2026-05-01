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
        if (existingRoot != null)
        {
            _moveTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityOption");
            _moveTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsSpinBox");
            _moveGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _moveFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _moveOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MoveDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 420.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _moveDialog.AddChild(root);

        root.AddChild(CreateMoveFieldLabel("TargetCityLabel"));
        _moveTargetCityOption = new OptionButton
        {
            Name = "TargetCityOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_moveTargetCityOption);

        root.AddChild(CreateMoveFieldLabel("TroopsLabel"));
        _moveTroopsSpinBox = CreateMoveSpinBox("TroopsSpinBox");
        root.AddChild(_moveTroopsSpinBox);

        root.AddChild(CreateMoveFieldLabel("GoldLabel"));
        _moveGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        root.AddChild(_moveGoldSpinBox);

        root.AddChild(CreateMoveFieldLabel("FoodLabel"));
        _moveFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        root.AddChild(_moveFoodSpinBox);

        root.AddChild(CreateMoveFieldLabel("OfficerListLabel"));
        _moveOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 180.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_moveOfficerList);
    }

    private void EnsureMerchantDialogWidgets()
    {
        if (_merchantDialog == null)
        {
            return;
        }

        var existingRoot = _merchantDialog.GetNodeOrNull<VBoxContainer>("MerchantDialogRoot");
        if (existingRoot != null)
        {
            _merchantModeOption = existingRoot.GetNodeOrNull<OptionButton>("TradeModeRow/TradeModeOption");
            _merchantFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
            _merchantSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            ConnectMerchantDialogSignals();
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MerchantDialogRoot",
            CustomMinimumSize = new Vector2(380.0f, 180.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _merchantDialog.AddChild(root);

        var tradeModeRow = CreateMerchantFieldRow("TradeModeRow", "TradeModeLabel");
        _merchantModeOption = new OptionButton
        {
            Name = "TradeModeOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tradeModeRow.AddChild(_merchantModeOption);
        root.AddChild(tradeModeRow);

        var foodRow = CreateMerchantFieldRow("FoodRow", "FoodLabel");
        _merchantFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _merchantFoodSpinBox.Step = 100;
        foodRow.AddChild(_merchantFoodSpinBox);
        root.AddChild(foodRow);

        _merchantSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_merchantSummaryLabel);

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

        _moveDialog.PopupCentered(new Vector2I(460, 520));
    }

    private void EnsureAttackDialogWidgets()
    {
        if (_attackDialog == null)
        {
            return;
        }

        var existingRoot = _attackDialog.GetNodeOrNull<VBoxContainer>("AttackDialogRoot");
        if (existingRoot != null)
        {
            _attackTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
            _attackTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsRow/TroopsSpinBox");
            _attackGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
            _attackFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
            _attackOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _attackWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AttackDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 460.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _attackDialog.AddChild(root);

        var targetCityRow = CreateAttackFieldRow("TargetCityRow", "TargetCityLabel");
        _attackTargetCityOption = new OptionButton
        {
            Name = "TargetCityOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        targetCityRow.AddChild(_attackTargetCityOption);
        root.AddChild(targetCityRow);

        var troopsRow = CreateAttackFieldRow("TroopsRow", "TroopsLabel");
        _attackTroopsSpinBox = CreateMoveSpinBox("TroopsSpinBox");
        troopsRow.AddChild(_attackTroopsSpinBox);
        root.AddChild(troopsRow);

        var goldRow = CreateAttackFieldRow("GoldRow", "GoldLabel");
        _attackGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        goldRow.AddChild(_attackGoldSpinBox);
        root.AddChild(goldRow);

        var foodRow = CreateAttackFieldRow("FoodRow", "FoodLabel");
        _attackFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        foodRow.AddChild(_attackFoodSpinBox);
        root.AddChild(foodRow);

        root.AddChild(CreateMoveFieldLabel("OfficerListLabel"));
        _attackOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 180.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_attackOfficerList);

        _attackWarningLabel = new Label
        {
            Name = "WarningLabel",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            CustomMinimumSize = new Vector2(0.0f, 24.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _attackWarningLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.52f, 0.45f, 1.0f));
        root.AddChild(_attackWarningLabel);
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
        if (_turnManager?.World == null || _selectedCity == null || _attackDialog == null || _attackTargetCityOption == null)
        {
            return;
        }

        EnsureAttackDialogWidgets();
        UpdateAttackDialogText();
        SetAttackDialogWarning(string.Empty);

        _attackTargetCityOption.Clear();
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

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (_attackOfficerList != null)
        {
            _attackOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_attackOfficerList, includeCheck: true);
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

        _attackDialog.PopupCentered(new Vector2I(460, 560));
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
        _merchantModeOption.AddItem(_localization?.T("ui.sell_food") ?? "Sell Food");
        _merchantModeOption.Select(0);

        UpdateMerchantFoodSpinBoxRange();
        UpdateMerchantTradeSummary();
        _merchantDialog.PopupCentered(new Vector2I(400, 220));
    }

    private void UpdateMoveDialogText()
    {
        if (_moveDialog == null || _localization == null)
        {
            return;
        }

        _moveDialog.Title = _localization.T("ui.move");
        _moveDialog.OkButtonText = _localization.T("ui.confirm_move");

        SetMoveDialogLabelText("TargetCityLabel", _localization.T("ui.target_city"));
        SetMoveDialogLabelText("TroopsLabel", _localization.T("ui.transfer_troops"));
        SetMoveDialogLabelText("GoldLabel", _localization.T("ui.transfer_gold"));
        SetMoveDialogLabelText("FoodLabel", _localization.T("ui.transfer_food"));
        SetMoveDialogLabelText("OfficerListLabel", _localization.T("ui.transfer_officers"));
    }

    private void UpdateMerchantDialogText()
    {
        if (_merchantDialog == null || _localization == null)
        {
            return;
        }

        _merchantDialog.Title = _localization.T("ui.merchant");
        _merchantDialog.OkButtonText = _localization.T("ui.confirm_merchant");
        SetMerchantDialogLabelText("TradeModeLabel", _localization.T("ui.trade_mode"));
        SetMerchantDialogLabelText("FoodLabel", _localization.T("ui.food_amount"));
        UpdateMerchantTradeSummary();
    }

    private void UpdateAttackDialogText()
    {
        if (_attackDialog == null || _localization == null)
        {
            return;
        }

        _attackDialog.Title = _localization.T("ui.attack");
        _attackDialog.OkButtonText = _localization.T("ui.confirm_attack");

        SetAttackDialogLabelText("TargetCityLabel", _localization.T("ui.target_city"));
        SetAttackDialogLabelText("TroopsLabel", _localization.T("ui.attack_troops"));
        SetAttackDialogLabelText("GoldLabel", _localization.T("ui.attack_gold"));
        SetAttackDialogLabelText("FoodLabel", _localization.T("ui.attack_food"));
        SetAttackDialogLabelText("OfficerListLabel", _localization.T("ui.attack_officers"));
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

    private static HBoxContainer CreateAttackFieldRow(string rowName, string labelName)
    {
        var row = new HBoxContainer
        {
            Name = rowName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);

        var label = new Label
        {
            Name = labelName,
            CustomMinimumSize = new Vector2(84.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(label);
        return row;
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

        var size = _attackDialog.Size;
        if (size == Vector2I.Zero)
        {
            size = new Vector2I(460, 560);
        }

        _attackDialog.PopupCentered(size);
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

    private static HBoxContainer CreateMerchantFieldRow(string rowName, string labelName)
    {
        var row = new HBoxContainer
        {
            Name = rowName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);

        var label = new Label
        {
            Name = labelName,
            CustomMinimumSize = new Vector2(84.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(label);
        return row;
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

        var isSell = _merchantModeOption.Selected == 1;
        var maxFood = isSell ? _selectedCity.Food : (_selectedCity.Gold / 10) * 100;
        ConfigureMoveSpinBox(_merchantFoodSpinBox, maxFood, maxFood > 0 ? 100 : 0);
        _merchantFoodSpinBox.Step = 100;
    }

    private void UpdateMerchantTradeSummary()
    {
        if (_merchantSummaryLabel == null || _merchantFoodSpinBox == null || _merchantModeOption == null || _localization == null)
        {
            return;
        }

        var foodAmount = (int)_merchantFoodSpinBox.Value;
        var goldAmount = foodAmount / 100 * 10;
        if (_merchantModeOption.Selected == 1)
        {
            _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_sell_preview", foodAmount, goldAmount);
            return;
        }

        _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_buy_preview", goldAmount, foodAmount);
    }


}
