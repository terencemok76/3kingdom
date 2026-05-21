using System.Collections.Generic;
using Godot;

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
    private Tree? _officerList;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(460.0f, 560.0f);

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

        var availableOfficerIds = _context.GetAvailableOfficerIdsForOrder();
        if (_officerList != null)
        {
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
        }

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
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.transfer_officers"));
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityOption");
        _troopsSpinBox = root.GetNodeOrNull<SpinBox>("TroopsSpinBox");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodSpinBox");
        _horseSpinBox = root.GetNodeOrNull<SpinBox>("HorseSpinBox");
        _officerList = root.GetNodeOrNull<Tree>("OfficerTable");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
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
        if (_targetCityOption == null)
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

        var result = _context.ExecuteMoveCommand(
            targetMetadata.AsInt32(),
            _troopsSpinBox != null ? (int)_troopsSpinBox.Value : 0,
            _goldSpinBox != null ? (int)_goldSpinBox.Value : 0,
            _foodSpinBox != null ? (int)_foodSpinBox.Value : 0,
            _horseSpinBox != null ? (int)_horseSpinBox.Value : 0,
            _context.GetCheckedTreeMetadataIds(_officerList));
        if (result.Success)
        {
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

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }
}
