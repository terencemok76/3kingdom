using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MerchantDialogController : FloatingOverlayController
{
    private readonly MerchantUiContext _context;
    private OptionButton? _tradeModeOption;
    private SpinBox? _amountSpinBox;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private bool _signalsConnected;

    public MerchantDialogController(MerchantUiContext context)
        : base(context, "res://scenes/ui/merchant/MerchantDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null)
        {
            return;
        }

        RefreshText();
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.merchant"));
        SetLabelText("TradeModeRow/TradeModeLabel", _context.Localization.T("ui.trade_mode"));
        SetLabelText("FoodRow/FoodLabel", _context.Localization.T("ui.trade_amount"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_merchant");
        }

        RefreshTradeModeOptionTexts();
        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _tradeModeOption = root.GetNodeOrNull<OptionButton>("TradeModeRow/TradeModeOption");
        _amountSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        ApplyInputThemes();
        ApplyButtonThemes();

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

    private void ApplyButtonThemes()
    {
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
    }

    private void ApplyInputThemes()
    {
        if (_amountSpinBox?.GetLineEdit() is not LineEdit lineEdit)
        {
            return;
        }

        lineEdit.AddThemeColorOverride("font_color", new Color(0.93f, 0.9f, 0.84f, 1.0f));
        lineEdit.AddThemeColorOverride("font_placeholder_color", new Color(0.72f, 0.68f, 0.62f, 0.9f));
        lineEdit.AddThemeColorOverride("caret_color", new Color(0.95f, 0.83f, 0.56f, 1.0f));
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

    private void RefreshTradeModeOptionTexts()
    {
        if (_tradeModeOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedTradeMode = GetSelectedTradeMode();
        _tradeModeOption.Clear();
        AddTradeOption("ui.buy_food", MerchantTradeMode.BuyFood);
        AddTradeOption("ui.sell_food", MerchantTradeMode.SellFood);
        AddTradeOption("ui.buy_horse", MerchantTradeMode.BuyHorse);

        for (var index = 0; index < _tradeModeOption.ItemCount; index += 1)
        {
            var metadata = _tradeModeOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)selectedTradeMode)
            {
                _tradeModeOption.Select(index);
                return;
            }
        }

        if (_tradeModeOption.ItemCount > 0)
        {
            _tradeModeOption.Select(0);
        }
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

        var city = _context.SelectedCity;
        var result = _context.ExecuteMerchantCommand((int)_amountSpinBox.Value, GetSelectedTradeMode());
        if (result.Success)
        {
            if (city != null)
            {
                _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            }
            HideOverlay();
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
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }
}
