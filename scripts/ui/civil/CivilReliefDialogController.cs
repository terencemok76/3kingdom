using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class CivilReliefDialogController
{
    private readonly CivilUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public CivilReliefDialogController(CivilUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/civil/CivilReliefDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _dialog == null || _context.Localization == null)
        {
            return;
        }

        EnsureWidgets();
        RefreshText();
        Populate();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("command.civil.relief");
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.civil_relief_officer"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.civil_relief_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.civil_relief_food"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_civil_relief");
        }
        UpdateSelectedOfficerSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("CivilReliefDialogRoot");
        if (root == null)
        {
            GD.PushError("CivilReliefDialogRoot not found in CivilReliefDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_goldSpinBox != null)
            {
                _goldSpinBox.ValueChanged += _ => { UpdateSummary(); UpdateConfirmButtonState(); };
            }
            if (_foodSpinBox != null)
            {
                _foodSpinBox.ValueChanged += _ => { UpdateSummary(); UpdateConfirmButtonState(); };
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        if (city == null)
        {
            return;
        }

        var availableOfficerIds = _context.GetAvailableCityOfficerIds();
        if (!availableOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = availableOfficerIds.FirstOrDefault();
        }

        _context.ConfigureMoveSpinBox(_goldSpinBox, city.Gold, 0);
        _context.ConfigureMoveSpinBox(_foodSpinBox, city.Food, 0);
        if (_goldSpinBox != null)
        {
            _goldSpinBox.Step = 100;
        }
        if (_foodSpinBox != null)
        {
            _foodSpinBox.Step = 1000;
        }

        UpdateSummary();
        UpdateSelectedOfficerSummary();
        UpdateConfirmButtonState();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = _dialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/{nodeName}") ??
                    _dialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/GoldRow/{nodeName}") ??
                    _dialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/FoodRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _goldSpinBox == null || _foodSpinBox == null || _context.Localization == null)
        {
            return;
        }

        var gold = (int)_goldSpinBox.Value;
        var food = (int)_foodSpinBox.Value;
        var gain = gold / 100 * 10 + food / 1000 * 10;
        _summaryLabel.Text = _context.Localization.Format("fmt.civil_relief_preview", gain);
    }

    private void UpdateConfirmButtonState()
    {
        if (_confirmButton == null)
        {
            return;
        }

        var hasOfficer = _selectedOfficerId > 0;
        var gold = (int)(_goldSpinBox?.Value ?? 0);
        var food = (int)(_foodSpinBox?.Value ?? 0);
        var hasReliefAmount = gold > 0 || food > 0;
        var effectiveGain = gold / 100 * 10 + food / 1000 * 10;
        _confirmButton.Disabled = !hasOfficer || !hasReliefAmount || effectiveGain <= 0;
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            _context.PopupDialog(_dialog);
            return;
        }

        var result = commandResolver.ExecuteCivilRelief(
            turnManager.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            (int)(_goldSpinBox?.Value ?? 0),
            (int)(_foodSpinBox?.Value ?? 0));
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        _context.RefreshMapVisuals();
        _dialog?.Hide();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.civil_relief_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                UpdateConfirmButtonState();
            });
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.civil_relief_officer")}: {officerName}";
    }
}
