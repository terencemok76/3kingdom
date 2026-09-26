using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

internal sealed class MoveDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private OptionButton? _targetCityOption;
    private Button? _targetCityMapButton;
    private Button? _confirmButton;
    private ScrollContainer? _contentScroll;
    private VBoxContainer? _content;
    private SpinBox? _infantrySpinBox;
    private SpinBox? _spearmanSpinBox;
    private SpinBox? _cavalrySpinBox;
    private SpinBox? _archerSpinBox;
    private SpinBox? _crossbowSpinBox;
    private SpinBox? _engineerSpinBox;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private SpinBox? _horseSpinBox;
    private SpinBox? _supplyCartSpinBox;
    private SpinBox? _ramSpinBox;
    private SpinBox? _catapultSpinBox;
    private SpinBox? _ladderSpinBox;
    private Button? _supplyCartMaxButton;
    private Button? _ramMaxButton;
    private Button? _catapultMaxButton;
    private Button? _ladderMaxButton;
    private Button? _infantryMaxButton;
    private Button? _spearmanMaxButton;
    private Button? _cavalryMaxButton;
    private Button? _archerMaxButton;
    private Button? _crossbowMaxButton;
    private Button? _engineerMaxButton;
    private Button? _goldMaxButton;
    private Button? _foodMaxButton;
    private Button? _horseMaxButton;
    private Tree? _officerList;
    private bool _signalsConnected;
    private bool _officerListSignalsConnected;
    private bool _officerListGuiInputConnected;
    protected override Vector2 MinimumOverlaySize => new(520.0f, 720.0f);

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
        if (_targetCityMapButton != null) _targetCityMapButton.Disabled = _targetCityOption.ItemCount == 0;

        ConfigureTroopRows(_context.SelectedCity);
        ConfigureSpinBox(_goldSpinBox, _context.SelectedCity.Gold, 0);
        ConfigureSpinBox(_foodSpinBox, _context.SelectedCity.Food, 0);
        ConfigureSpinBox(_horseSpinBox, _context.SelectedCity.Horses, 0);
        ConfigureSpinBox(_supplyCartSpinBox, _context.SelectedCity.SupplyCartCount, 0);
        ConfigureSpinBox(_ramSpinBox, _context.SelectedCity.RamCount, 0);
        ConfigureSpinBox(_catapultSpinBox, _context.SelectedCity.CatapultCount, 0);
        ConfigureSpinBox(_ladderSpinBox, _context.SelectedCity.LadderCount, 0);
        ConfigureEquipmentRows(_context.SelectedCity);

        PopulateOfficerList();

        ShowOverlay();
        ResetContentScrollPosition();
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
        if (_targetCityMapButton != null)
        {
            _targetCityMapButton.Text = _context.Localization.T("ui.strategic_map.open_selector");
            _targetCityMapButton.Disabled = _targetCityOption?.ItemCount == 0;
        }
        SetLabelText("GoldLabel", _context.Localization.T("ui.transfer_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.transfer_food"));
        SetLabelText("HorseLabel", _context.Localization.T("ui.transfer_horse"));
        SetMaxButtonText(_goldMaxButton);
        SetMaxButtonText(_foodMaxButton);
        SetMaxButtonText(_horseMaxButton);
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.transfer_officers"));
        RefreshTargetCityOptionTexts();
        RefreshOfficerTableText();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _contentScroll = root.GetNodeOrNull<ScrollContainer>("ContentScroll");
        _content = root.GetNodeOrNull<VBoxContainer>("ContentScroll/Content");
        _targetCityOption = root.GetNodeOrNull<OptionButton>("ContentScroll/Content/TargetCityRow/TargetCityOption");
        _targetCityMapButton = root.GetNodeOrNull<Button>("ContentScroll/Content/TargetCityRow/TargetCityMapButton");
        _infantrySpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/InfantryRow/InfantrySpinBox");
        _spearmanSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/SpearmanRow/SpearmanSpinBox");
        _cavalrySpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/CavalryRow/CavalrySpinBox");
        _archerSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/ArcherRow/ArcherSpinBox");
        _crossbowSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/CrossbowRow/CrossbowSpinBox");
        _engineerSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/EngineerRow/EngineerSpinBox");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/FoodRow/FoodSpinBox");
        _horseSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/HorseRow/HorseSpinBox");
        _supplyCartSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/SupplyCartRow/SupplyCartSpinBox");
        _ramSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/RamRow/RamSpinBox");
        _catapultSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/CatapultRow/CatapultSpinBox");
        _ladderSpinBox = root.GetNodeOrNull<SpinBox>("ContentScroll/Content/LadderRow/LadderSpinBox");
        _supplyCartMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/SupplyCartRow/SupplyCartMaxButton");
        _ramMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/RamRow/RamMaxButton");
        _catapultMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/CatapultRow/CatapultMaxButton");
        _ladderMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/LadderRow/LadderMaxButton");
        _infantryMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/InfantryRow/InfantryMaxButton");
        _spearmanMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/SpearmanRow/SpearmanMaxButton");
        _cavalryMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/CavalryRow/CavalryMaxButton");
        _archerMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/ArcherRow/ArcherMaxButton");
        _crossbowMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/CrossbowRow/CrossbowMaxButton");
        _engineerMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/EngineerRow/EngineerMaxButton");
        _goldMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/GoldRow/GoldMaxButton");
        _foodMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/FoodRow/FoodMaxButton");
        _horseMaxButton = root.GetNodeOrNull<Button>("ContentScroll/Content/HorseRow/HorseMaxButton");
        _officerList = root.GetNodeOrNull<Tree>("ContentScroll/Content/OfficerTable");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (_targetCityMapButton != null) _context.ApplyCommandButtonTheme(_targetCityMapButton);
        ApplyMaxButtonTheme(_supplyCartMaxButton);
        ApplyMaxButtonTheme(_ramMaxButton);
        ApplyMaxButtonTheme(_catapultMaxButton);
        ApplyMaxButtonTheme(_ladderMaxButton);
        ApplyMaxButtonTheme(_infantryMaxButton);
        ApplyMaxButtonTheme(_spearmanMaxButton);
        ApplyMaxButtonTheme(_cavalryMaxButton);
        ApplyMaxButtonTheme(_archerMaxButton);
        ApplyMaxButtonTheme(_crossbowMaxButton);
        ApplyMaxButtonTheme(_engineerMaxButton);
        ApplyMaxButtonTheme(_goldMaxButton);
        ApplyMaxButtonTheme(_foodMaxButton);
        ApplyMaxButtonTheme(_horseMaxButton);

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

        if (_signalsConnected || _confirmButton == null)
        {
            return;
        }

        _confirmButton.Pressed += OnConfirmPressed;
        if (_targetCityMapButton != null) _targetCityMapButton.Pressed += OnTargetCityMapPressed;
        ConnectMaxButton(_supplyCartMaxButton, _supplyCartSpinBox);
        ConnectMaxButton(_ramMaxButton, _ramSpinBox);
        ConnectMaxButton(_catapultMaxButton, _catapultSpinBox);
        ConnectMaxButton(_ladderMaxButton, _ladderSpinBox);
        ConnectMaxButton(_infantryMaxButton, _infantrySpinBox);
        ConnectMaxButton(_spearmanMaxButton, _spearmanSpinBox);
        ConnectMaxButton(_cavalryMaxButton, _cavalrySpinBox);
        ConnectMaxButton(_archerMaxButton, _archerSpinBox);
        ConnectMaxButton(_crossbowMaxButton, _crossbowSpinBox);
        ConnectMaxButton(_engineerMaxButton, _engineerSpinBox);
        ConnectMaxButton(_goldMaxButton, _goldSpinBox);
        ConnectMaxButton(_foodMaxButton, _foodSpinBox);
        ConnectMaxButton(_horseMaxButton, _horseSpinBox);
        _signalsConnected = true;
    }

    private void OnTargetCityMapPressed()
    {
        if (_targetCityOption == null)
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
            TitleKey = "ui.strategic_map.select_move_target_title",
            PromptKey = "ui.strategic_map.select_move_target_prompt",
            Layer = StrategicMapLayer.Faction,
            FactionFilter = StrategicMapFactionFilter.Self,
            SelectableCityIds = candidateIds,
            SourceCityId = _context.SelectedCity?.Id ?? 0,
            InitialCityId = GetSelectedTargetCityId(),
            Confirmed = cityId =>
            {
                SelectTargetCityOption(cityId);
                BringOverlayToFront();
            }
        });
    }

    private int GetSelectedTargetCityId()
    {
        if (_targetCityOption == null || _targetCityOption.Selected < 0)
        {
            return 0;
        }

        var metadata = _targetCityOption.GetItemMetadata(_targetCityOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : 0;
    }

    private void SelectTargetCityOption(int cityId)
    {
        if (_targetCityOption == null)
        {
            return;
        }

        for (var index = 0; index < _targetCityOption.ItemCount; index += 1)
        {
            var metadata = _targetCityOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == cityId)
            {
                _targetCityOption.Select(index);
                return;
            }
        }
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
        var troopAllocation = BuildTroopAllocation();
        var selectedOfficerIds = _context.GetCheckedTreeMetadataIds(_officerList);
        var movedCaptiveOfficerIds = GetSelectedCaptiveOfficerIds(selectedOfficerIds);
        var movedCaptiveOfficerSet = movedCaptiveOfficerIds.ToHashSet();
        var movedOfficerIds = selectedOfficerIds.Where(id => !movedCaptiveOfficerSet.Contains(id)).ToList();
        var siegeEngineAllocation = new SiegeEngineAllocationData
        {
            SupplyCart = _supplyCartSpinBox != null ? (int)_supplyCartSpinBox.Value : 0,
            Ram = _ramSpinBox != null ? (int)_ramSpinBox.Value : 0,
            Catapult = _catapultSpinBox != null ? (int)_catapultSpinBox.Value : 0,
            Ladder = _ladderSpinBox != null ? (int)_ladderSpinBox.Value : 0
        };
        var result = _context.ExecuteMoveCommand(
            targetCityId,
            troopAllocation,
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

    private void ConfigureTroopRows(CityData city)
    {
        var rows = new (string RowName, string LabelName, TroopType TroopType, SpinBox? Input, Button? MaxButton)[]
        {
            ("InfantryRow", "InfantryLabel", TroopType.Infantry, _infantrySpinBox, _infantryMaxButton),
            ("SpearmanRow", "SpearmanLabel", TroopType.Spearman, _spearmanSpinBox, _spearmanMaxButton),
            ("CavalryRow", "CavalryLabel", TroopType.Cavalry, _cavalrySpinBox, _cavalryMaxButton),
            ("ArcherRow", "ArcherLabel", TroopType.Archer, _archerSpinBox, _archerMaxButton),
            ("CrossbowRow", "CrossbowLabel", TroopType.Crossbow, _crossbowSpinBox, _crossbowMaxButton),
            ("EngineerRow", "EngineerLabel", TroopType.Engineer, _engineerSpinBox, _engineerMaxButton)
        };

        foreach (var row in rows)
        {
            var available = city.GetTroops(row.TroopType);
            var rowControl = GetOverlayContentNode<Control>($"ContentScroll/Content/{row.RowName}");
            var label = GetOverlayContentNode<Label>($"ContentScroll/Content/{row.RowName}/{row.LabelName}");
            var visible = available > 0 && rowControl != null && label != null && row.Input != null && row.MaxButton != null;
            if (rowControl != null)
            {
                rowControl.Visible = visible;
            }
            if (label != null && _context.Localization != null)
            {
                label.Text = _context.Localization.Format("fmt.move_troop_city_available", _context.GetTroopTypeDisplayName(row.TroopType), available);
            }
            if (row.MaxButton != null && _context.Localization != null)
            {
                row.MaxButton.Text = _context.Localization.T("ui.max");
            }
            ConfigureSpinBox(row.Input, available, 0);
        }
    }

    private TroopAllocationData BuildTroopAllocation()
    {
        return new TroopAllocationData
        {
            Infantry = _infantrySpinBox != null ? (int)_infantrySpinBox.Value : 0,
            Spearman = _spearmanSpinBox != null ? (int)_spearmanSpinBox.Value : 0,
            Cavalry = _cavalrySpinBox != null ? (int)_cavalrySpinBox.Value : 0,
            Archer = _archerSpinBox != null ? (int)_archerSpinBox.Value : 0,
            Crossbow = _crossbowSpinBox != null ? (int)_crossbowSpinBox.Value : 0,
            Siege = _engineerSpinBox != null ? (int)_engineerSpinBox.Value : 0
        };
    }

    private void ConfigureEquipmentRows(CityData city)
    {
        if (_content == null)
        {
            return;
        }

        var rows = new (string RowName, string LabelName, string LabelKey, SpinBox? Input, Button? MaxButton, int Count)[]
        {
            ("SupplyCartRow", "SupplyCartLabel", "ui.supply_cart", _supplyCartSpinBox, _supplyCartMaxButton, city.SupplyCartCount),
            ("RamRow", "RamLabel", "siege_engine.ram", _ramSpinBox, _ramMaxButton, city.RamCount),
            ("LadderRow", "LadderLabel", "siege_engine.ladder", _ladderSpinBox, _ladderMaxButton, city.LadderCount),
            ("CatapultRow", "CatapultLabel", "siege_engine.catapult", _catapultSpinBox, _catapultMaxButton, city.CatapultCount)
        };
        var insertionIndex = 1;
        foreach (var row in rows)
        {
            var rowControl = GetOverlayContentNode<Control>($"ContentScroll/Content/{row.RowName}");
            var label = GetOverlayContentNode<Label>($"ContentScroll/Content/{row.RowName}/{row.LabelName}");
            var visible = row.Count > 0 && rowControl != null && label != null && row.Input != null && row.MaxButton != null;
            if (rowControl != null)
            {
                rowControl.Visible = visible;
            }
            if (label != null && _context.Localization != null)
            {
                label.Text = _context.Localization.Format("fmt.move_equipment_city_available", _context.Localization.T(row.LabelKey), row.Count);
            }
            if (row.MaxButton != null && _context.Localization != null)
            {
                row.MaxButton.Text = _context.Localization.T("ui.max");
            }
            if (!visible || rowControl == null)
            {
                continue;
            }

            _content.MoveChild(rowControl, insertionIndex++);
        }
    }

    private static void ConnectMaxButton(Button? button, SpinBox? spinBox)
    {
        if (button == null || spinBox == null)
        {
            return;
        }

        button.Pressed += () => spinBox.Value = spinBox.MaxValue;
    }

    private void ApplyMaxButtonTheme(Button? button)
    {
        if (button != null)
        {
            _context.ApplyCommandButtonTheme(button);
        }
    }

    private void SetMaxButtonText(Button? button)
    {
        if (button != null && _context.Localization != null)
        {
            button.Text = _context.Localization.T("ui.max");
        }
    }

    private void ResetContentScrollPosition()
    {
        if (_contentScroll != null)
        {
            _contentScroll.GetVScrollBar().Value = 0;
        }
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
            PopulateMoveOfficerTableRow(row, officer, rowIndex, isCaptive: false);
            rowIndex += 1;
        }
        foreach (var officer in _context.TurnManager.World.Officers
                     .Where(officer => officer.CaptiveFactionId == _context.SelectedCity.OwnerFactionId && officer.JailedCityId == _context.SelectedCity.Id)
                     .OrderBy(officer => officer.NameZhHant)
                     .ThenBy(officer => officer.Name))
        {
            var row = _officerList.CreateItem(tableRoot);
            PopulateMoveOfficerTableRow(row, officer, rowIndex, isCaptive: true);
            rowIndex += 1;
        }

        UpdateOfficerCheckHighlights();
    }

    private void PopulateMoveOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex, bool isCaptive)
    {
        _context.PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: true);
        if (!isCaptive)
        {
            return;
        }

        var localization = _context.Localization;
        row.SetText(2, localization?.T("role.captive") ?? "Captive");
        row.SetText(3, localization?.T("ui.captured_officer.jail") ?? "Jail");
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

    private List<int> GetSelectedCaptiveOfficerIds(IEnumerable<int> selectedOfficerIds)
    {
        var selectedCity = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (selectedCity == null || world == null)
        {
            return new List<int>();
        }

        return selectedOfficerIds
            .Where(id => world.GetOfficer(id) is { } officer &&
                         officer.CaptiveFactionId == selectedCity.OwnerFactionId &&
                         officer.JailedCityId == selectedCity.Id)
            .ToList();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>($"ContentScroll/Content/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ContentScroll/Content/TargetCityRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ContentScroll/Content/GoldRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ContentScroll/Content/FoodRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ContentScroll/Content/HorseRow/{nodeName}");
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
