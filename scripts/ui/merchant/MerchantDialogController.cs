using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MerchantDialogController
{
    private readonly MerchantUiContext _context;
    private Window? _dialog;
    private OptionButton? _tradeModeOption;
    private SpinBox? _amountSpinBox;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private bool _signalsConnected;

    public MerchantDialogController(MerchantUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/merchant/MerchantDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_dialog == null || _tradeModeOption == null || _context.SelectedCity == null)
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

        _dialog.Title = _context.Localization.T("ui.merchant");
        SetLabelText("TradeModeLabel", _context.Localization.T("ui.trade_mode"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.trade_amount"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_merchant");
        }

        UpdateSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("MerchantDialogRoot");
        if (root == null)
        {
            GD.PushError("MerchantDialogRoot not found in MerchantDialog.tscn.");
            return;
        }

        _tradeModeOption = root.GetNodeOrNull<OptionButton>("TradeModeRow/TradeModeOption");
        _amountSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");

        if (_signalsConnected)
        {
            return;
        }

        if (_tradeModeOption != null)
        {
            _tradeModeOption.ItemSelected += _ => OnTradeModeChanged();
        }

        if (_amountSpinBox != null)
        {
            _amountSpinBox.ValueChanged += _ => UpdateSummary();
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        _signalsConnected = true;
    }

    private void Populate()
    {
        if (_tradeModeOption == null || _context.Localization == null)
        {
            return;
        }

        _tradeModeOption.Clear();
        AddTradeOption("ui.buy_food", MerchantTradeMode.BuyFood);
        AddTradeOption("ui.sell_food", MerchantTradeMode.SellFood);
        AddTradeOption("ui.buy_horse", MerchantTradeMode.BuyHorse);
        _tradeModeOption.Select(0);

        UpdateAmountRange();
        UpdateSummary();
    }

    private void AddTradeOption(string localeKey, MerchantTradeMode tradeMode)
    {
        if (_tradeModeOption == null || _context.Localization == null)
        {
            return;
        }

        _tradeModeOption.AddItem(_context.Localization.T(localeKey));
        _tradeModeOption.SetItemMetadata(_tradeModeOption.ItemCount - 1, (int)tradeMode);
    }

    private void OnTradeModeChanged()
    {
        UpdateAmountRange();
        UpdateSummary();
    }

    private void UpdateAmountRange()
    {
        if (_amountSpinBox == null || _tradeModeOption == null || _context.SelectedCity == null)
        {
            return;
        }

        var tradeMode = GetSelectedTradeMode();
        var maxAmount = tradeMode switch
        {
            MerchantTradeMode.SellFood => _context.SelectedCity.Food,
            MerchantTradeMode.BuyHorse => (_context.SelectedCity.Gold / 20) * 10,
            _ => (_context.SelectedCity.Gold / 10) * 100
        };

        _amountSpinBox.MinValue = 0;
        _amountSpinBox.MaxValue = maxAmount;
        _amountSpinBox.Step = tradeMode == MerchantTradeMode.BuyHorse ? 10 : 100;
        _amountSpinBox.Value = maxAmount <= 0 ? 0 : (tradeMode == MerchantTradeMode.BuyHorse ? 10 : 100);
        _amountSpinBox.Value = Mathf.Clamp(_amountSpinBox.Value, 0, maxAmount);
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _amountSpinBox == null || _context.Localization == null)
        {
            return;
        }

        var amount = (int)_amountSpinBox.Value;
        var tradeMode = GetSelectedTradeMode();
        if (tradeMode == MerchantTradeMode.SellFood)
        {
            var goldAmount = amount / 100 * 10;
            _summaryLabel.Text = _context.Localization.Format("fmt.merchant_sell_preview", amount, goldAmount);
            return;
        }

        if (tradeMode == MerchantTradeMode.BuyHorse)
        {
            var goldCost = amount / 10 * 20;
            _summaryLabel.Text = _context.Localization.Format("fmt.merchant_buy_horse_preview", goldCost, amount);
            return;
        }

        var buyGoldAmount = amount / 100 * 10;
        _summaryLabel.Text = _context.Localization.Format("fmt.merchant_buy_preview", buyGoldAmount, amount);
    }

    private void OnConfirmPressed()
    {
        if (_amountSpinBox == null)
        {
            return;
        }

        var result = _context.ExecuteMerchantCommand((int)_amountSpinBox.Value, GetSelectedTradeMode());
        if (result.Success)
        {
            _dialog?.Hide();
        }
    }

    private MerchantTradeMode GetSelectedTradeMode()
    {
        if (_tradeModeOption == null || _tradeModeOption.ItemCount == 0 || _tradeModeOption.Selected < 0)
        {
            return MerchantTradeMode.BuyFood;
        }

        var metadata = _tradeModeOption.GetItemMetadata(_tradeModeOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (MerchantTradeMode)metadata.AsInt32()
            : MerchantTradeMode.BuyFood;
    }

    private void SetLabelText(string nodeName, string text)
    {
        var root = _dialog?.GetNodeOrNull<Control>("MerchantDialogRoot");
        var label = root?.FindChild(nodeName, recursive: true, owned: false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }
}
