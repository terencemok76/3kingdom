using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MoveDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private OptionButton? _targetCityOption;
    private Button? _confirmButton;
    private SpinBox? _troopsSpinBox;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private SpinBox? _horseSpinBox;
    private SpinBox? _ramSpinBox;
    private SpinBox? _catapultSpinBox;
    private SpinBox? _ladderSpinBox;
    private Tree? _officerList;
    private Tree? _prisonerList;
    private bool _signalsConnected;
    private bool _officerListSignalsConnected;
    private bool _officerListGuiInputConnected;
    private bool _prisonerListSignalsConnected;
    private bool _prisonerListGuiInputConnected;
    protected override Vector2 MinimumOverlaySize => new(520.0f, 860.0f);

    public MoveDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/MoveDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show(List<int> candidateIds)
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || !EnsureOverlayReady() || _targetCityOption == null)
        {
            return;
        }

        RefreshText();

        _targetCityOption.Clear();
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

        ConfigureSpinBox(_troopsSpinBox, _context.SelectedCity.Troops, _context.SelectedCity.Troops / 2);
        ConfigureSpinBox(_goldSpinBox, _context.SelectedCity.Gold, _context.SelectedCity.Gold / 2);
        ConfigureSpinBox(_foodSpinBox, _context.SelectedCity.Food, _context.SelectedCity.Food / 2);
        ConfigureSpinBox(_horseSpinBox, _context.SelectedCity.Horses, _context.SelectedCity.Horses / 2);
        ConfigureSpinBox(_ramSpinBox, _context.SelectedCity.RamCount, 0);
        ConfigureSpinBox(_catapultSpinBox, _context.SelectedCity.CatapultCount, 0);
        ConfigureSpinBox(_ladderSpinBox, _context.SelectedCity.LadderCount, 0);

        PopulateOfficerList();
        PopulatePrisonerList();

        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.move"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_move");
        }

        SetLabelText("TargetCityLabel", _context.Localization.T("ui.target_city"));
        SetLabelText("TroopsLabel", _context.Localization.T("ui.transfer_troops"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.transfer_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.transfer_food"));
        SetLabelText("HorseLabel", _context.Localization.T("ui.transfer_horse"));
        SetLabelText("RamLabel", _context.Localization.T("siege_engine.ram"));
        SetLabelText("CatapultLabel", _context.Localization.T("siege_engine.catapult"));
        SetLabelText("LadderLabel", _context.Localization.T("siege_engine.ladder"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.transfer_officers"));
        SetLabelText("PrisonerListLabel", _context.Localization.T("ui.transfer_prisoners"));
        RefreshTargetCityOptionTexts();
        RefreshOfficerTableText();
        RefreshPrisonerTableText();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityOption");
        _troopsSpinBox = root.GetNodeOrNull<SpinBox>("TroopsSpinBox");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodSpinBox");
        _horseSpinBox = root.GetNodeOrNull<SpinBox>("HorseSpinBox");
        _ramSpinBox = root.GetNodeOrNull<SpinBox>("RamSpinBox");
        _catapultSpinBox = root.GetNodeOrNull<SpinBox>("CatapultSpinBox");
        _ladderSpinBox = root.GetNodeOrNull<SpinBox>("LadderSpinBox");
        _officerList = root.GetNodeOrNull<Tree>("OfficerTable");
        _prisonerList = root.GetNodeOrNull<Tree>("PrisonerTable");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
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

        if (!_prisonerListSignalsConnected && _prisonerList != null)
        {
            _prisonerList.ItemSelected += UpdatePrisonerCheckHighlights;
            _prisonerListSignalsConnected = true;
        }

        if (!_prisonerListGuiInputConnected && _prisonerList != null)
        {
            _prisonerList.GuiInput += OnPrisonerListGuiInput;
            _prisonerListGuiInputConnected = true;
        }

        if (_signalsConnected || _confirmButton == null)
        {
            return;
        }

        _confirmButton.Pressed += OnConfirmPressed;
        _signalsConnected = true;
    }

    private void OnConfirmPressed()
    {
        var sourceCity = _context.SelectedCity;
        if (_targetCityOption == null || sourceCity == null)
        {
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
        var movedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        var movedCaptiveOfficerIds = _context.GetCheckedTreeMetadataIds(_prisonerList);
        var siegeEngineAllocation = new SiegeEngineAllocationData
        {
            Ram = _ramSpinBox != null ? (int)_ramSpinBox.Value : 0,
            Catapult = _catapultSpinBox != null ? (int)_catapultSpinBox.Value : 0,
            Ladder = _ladderSpinBox != null ? (int)_ladderSpinBox.Value : 0
        };
        var result = _context.ExecuteMoveCommand(
            targetCityId,
            _troopsSpinBox != null ? (int)_troopsSpinBox.Value : 0,
            _goldSpinBox != null ? (int)_goldSpinBox.Value : 0,
            _foodSpinBox != null ? (int)_foodSpinBox.Value : 0,
            _horseSpinBox != null ? (int)_horseSpinBox.Value : 0,
            siegeEngineAllocation,
            movedOfficerIds,
            movedCaptiveOfficerIds);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(sourceCity.Id, sourceCity.OwnerFactionId);
            _context.UiEventHub.PublishCityStateChanged(targetCityId, sourceCity.OwnerFactionId);
            foreach (var officerId in movedOfficerIds)
            {
                _context.UiEventHub.PublishOfficerStateChanged(officerId, targetCityId, sourceCity.OwnerFactionId);
            }
            HideOverlay();
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

    private void PopulateOfficerList()
    {
        if (_officerList == null || _context.SelectedCity == null || _context.TurnManager?.World == null)
        {
            return;
        }

        var availableOfficerIds = _context.GetAvailableOfficerIdsForOrder();
        _officerList.Clear();
        _context.ConfigureCompactOfficerTableColumns(_officerList, includeCheck: true);
        var tableRoot = _officerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in _context.SelectedCity.OfficerIds)
        {
            if (!availableOfficerIds.Contains(officerId))
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

    private void PopulatePrisonerList()
    {
        if (_prisonerList == null || _context.SelectedCity == null || _context.TurnManager?.World == null)
        {
            return;
        }

        _prisonerList.Clear();
        _context.ConfigureCompactOfficerTableColumns(_prisonerList, includeCheck: true);
        var tableRoot = _prisonerList.CreateItem();
        var rowIndex = 0;
        foreach (var officer in _context.TurnManager.World.Officers
                     .Where(officer => officer.CaptiveFactionId == _context.SelectedCity.OwnerFactionId && officer.JailedCityId == _context.SelectedCity.Id)
                     .OrderBy(officer => officer.NameZhHant)
                     .ThenBy(officer => officer.Name))
        {
            var row = _prisonerList.CreateItem(tableRoot);
            _context.PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
            rowIndex += 1;
        }

        UpdatePrisonerCheckHighlights();
    }

    private void RefreshOfficerTableText()
    {
        if (_officerList == null || _context.SelectedCity == null)
        {
            return;
        }

        var checkedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        var checkedOfficerSet = new HashSet<int>(checkedOfficerIds);
        PopulateOfficerList();

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

    private void RefreshPrisonerTableText()
    {
        if (_prisonerList == null || _context.SelectedCity == null)
        {
            return;
        }

        var checkedOfficerIds = _context.GetCheckedTreeMetadataIds(_prisonerList);
        var checkedOfficerSet = new HashSet<int>(checkedOfficerIds);
        PopulatePrisonerList();

        var root = _prisonerList.GetRoot();
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

        UpdatePrisonerCheckHighlights();
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

    private void UpdatePrisonerCheckHighlights()
    {
        if (_prisonerList == null)
        {
            return;
        }

        var root = _prisonerList.GetRoot();
        var row = root?.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            ApplyOfficerRowVisualState(row, rowIndex, _prisonerList.Columns, IsOfficerRowChecked(row));
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

    private static void ToggleOfficerRow(TreeItem row)
    {
        var current = row.GetMetadata(0).VariantType == Variant.Type.Bool && row.GetMetadata(0).AsBool();
        row.SetMetadata(0, !current);
    }

    private static bool IsOfficerRowChecked(TreeItem row)
    {
        return row.GetMetadata(0).VariantType == Variant.Type.Bool && row.GetMetadata(0).AsBool();
    }

    private void OnOfficerListGuiInput(InputEvent @event)
    {
        ToggleTreeRowFromMouseInput(_officerList, @event, UpdateOfficerCheckHighlights);
    }

    private void OnPrisonerListGuiInput(InputEvent @event)
    {
        ToggleTreeRowFromMouseInput(_prisonerList, @event, UpdatePrisonerCheckHighlights);
    }

    private static void ToggleTreeRowFromMouseInput(Tree? tree, InputEvent @event, System.Action refreshAction)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            tree == null)
        {
            return;
        }

        var clickedRow = tree.GetItemAtPosition(mouseButton.Position);
        if (clickedRow == null)
        {
            return;
        }

        ToggleOfficerRow(clickedRow);
        refreshAction();
        tree.AcceptEvent();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
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
                _targetCityOption.SetItemText(index, localization.GetCityName(city));
            }
        }
    }
}
