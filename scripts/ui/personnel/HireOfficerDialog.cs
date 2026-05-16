using System;
using Godot;

namespace ThreeKingdom.UI;

public sealed partial class HireOfficerDialog : Window
{
    private Label? _officerListLabel;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _goldLabel;
    private SpinBox? _goldSpinBox;
    private Label? _foodLabel;
    private SpinBox? _foodSpinBox;
    private Label? _itemLabel;
    private OptionButton? _itemOption;
    private Label? _summaryLabel;
    private Button? _confirmButton;

    public event Action? SelectOfficerPressed;
    public event Action? ConfirmPressed;
    public event Action<double>? GoldValueChanged;
    public event Action<double>? FoodValueChanged;
    public event Action<long>? ItemSelected;

    public Label? SelectedOfficerLabel => _selectedOfficerLabel;
    public SpinBox? GoldSpinBox => _goldSpinBox;
    public SpinBox? FoodSpinBox => _foodSpinBox;
    public OptionButton? ItemOption => _itemOption;
    public Label? SummaryLabel => _summaryLabel;
    public Button? ConfirmButton => _confirmButton;

    public override void _Ready()
    {
        _officerListLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/OfficerListLabel");
        _selectedOfficerLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = GetNodeOrNull<Button>("HireOfficerDialogRoot/OfficerSelectorRow/SelectOfficerButton");
        _goldLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/GoldRow/GoldLabel");
        _goldSpinBox = GetNodeOrNull<SpinBox>("HireOfficerDialogRoot/GoldRow/GoldSpinBox");
        _foodLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/FoodRow/FoodLabel");
        _foodSpinBox = GetNodeOrNull<SpinBox>("HireOfficerDialogRoot/FoodRow/FoodSpinBox");
        _itemLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/ItemRow/ItemLabel");
        _itemOption = GetNodeOrNull<OptionButton>("HireOfficerDialogRoot/ItemRow/ItemOption");
        _summaryLabel = GetNodeOrNull<Label>("HireOfficerDialogRoot/SummaryLabel");
        _confirmButton = GetNodeOrNull<Button>("HireOfficerDialogRoot/ConfirmRow/ConfirmButton");

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Pressed += () => SelectOfficerPressed?.Invoke();
        }

        if (_goldSpinBox != null)
        {
            _goldSpinBox.ValueChanged += value => GoldValueChanged?.Invoke(value);
        }

        if (_foodSpinBox != null)
        {
            _foodSpinBox.ValueChanged += value => FoodValueChanged?.Invoke(value);
        }

        if (_itemOption != null)
        {
            _itemOption.ItemSelected += index => ItemSelected?.Invoke(index);
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += () => ConfirmPressed?.Invoke();
        }

        CloseRequested += () => Hide();
    }

    public void SetDialogText(
        string title,
        string officerLabelText,
        string selectOfficerButtonText,
        string goldLabelText,
        string foodLabelText,
        string itemLabelText,
        string confirmButtonText)
    {
        Title = title;

        if (_officerListLabel != null)
        {
            _officerListLabel.Text = officerLabelText;
        }

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = selectOfficerButtonText;
        }

        if (_goldLabel != null)
        {
            _goldLabel.Text = goldLabelText;
        }

        if (_foodLabel != null)
        {
            _foodLabel.Text = foodLabelText;
        }

        if (_itemLabel != null)
        {
            _itemLabel.Text = itemLabelText;
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = confirmButtonText;
        }
    }

    public void SetSelectedOfficerSummary(string text)
    {
        if (_selectedOfficerLabel != null)
        {
            _selectedOfficerLabel.Text = text;
        }
    }

    public void SetSummaryText(string text)
    {
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = text;
        }
    }
}
