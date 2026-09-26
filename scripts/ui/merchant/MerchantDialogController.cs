using System;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MerchantDialogController : FloatingOverlayController
{
    private readonly MerchantUiContext _context;
    private OptionButton? _tradeModeOption;
    private Tree? _marketTable;
    private SpinBox? _amountSpinBox;
    private Button? _amountMaxButton;
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
        if (_amountMaxButton != null)
        {
            _amountMaxButton.Text = _context.Localization.T("ui.max");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_merchant");
        }

        RefreshTradeModeOptionTexts();
        PopulateMarketTable();
        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _tradeModeOption = root.GetNodeOrNull<OptionButton>("TradeModeRow/TradeModeOption");
        _marketTable = root.GetNodeOrNull<Tree>("MarketTable");
        _amountSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _amountMaxButton = root.GetNodeOrNull<Button>("FoodRow/AmountMaxButton");
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

        if (_marketTable != null)
        {
            _marketTable.ItemSelected += OnMarketItemSelected;
        }

        if (_amountSpinBox != null)
        {
            _amountSpinBox.ValueChanged += _ => UpdateSummary();
            if (_amountSpinBox.GetLineEdit() is LineEdit amountLineEdit)
            {
                amountLineEdit.TextChanged += _ => Callable.From(RefreshTradePreview).CallDeferred();
                amountLineEdit.FocusExited += RefreshTradePreview;
            }
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        if (_amountMaxButton != null)
        {
            _amountMaxButton.Pressed += SetAmountToMaximum;
        }

        _signalsConnected = true;
    }

    private void ApplyButtonThemes()
    {
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }

        if (_amountMaxButton != null)
        {
            _context.ApplyCommandButtonTheme(_amountMaxButton);
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
        AddTradeOption("ui.sell_horse", MerchantTradeMode.SellHorse);
        AddTradeOption("ui.buy_metal", MerchantTradeMode.BuyMetal);
        AddTradeOption("ui.sell_metal", MerchantTradeMode.SellMetal);
        _tradeModeOption.Select(0);

        PopulateMarketTable();

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
        AddTradeOption("ui.sell_horse", MerchantTradeMode.SellHorse);
        AddTradeOption("ui.buy_metal", MerchantTradeMode.BuyMetal);
        AddTradeOption("ui.sell_metal", MerchantTradeMode.SellMetal);

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

    private void RefreshTradePreview()
    {
        UpdateSummary();
        UpdateConfirmAvailability();
    }

    private void SetAmountToMaximum()
    {
        if (_amountSpinBox == null)
        {
            return;
        }

        _amountSpinBox.Value = _amountSpinBox.MaxValue;
        RefreshTradePreview();
    }

    private void PopulateMarketTable()
    {
        if (_marketTable == null || _context.Localization == null || _context.SelectedCity == null)
        {
            return;
        }

        var city = _context.SelectedCity;
        MarketRules.EnsureMarketInitialized(city);
        _marketTable.Clear();
        _marketTable.Columns = 6;
        var headers = new[] { "ui.market_product", "ui.market_buy", "ui.market_sell", "ui.market_previous", "ui.market_change", "ui.market_status" };
        for (var index = 0; index < headers.Length; index += 1)
        {
            _marketTable.SetColumnTitle(index, _context.Localization.T(headers[index]));
            _marketTable.SetColumnCustomMinimumWidth(index, index switch
            {
                0 => 82,
                5 => 88,
                _ => 60
            });
            _marketTable.SetColumnTitleAlignment(index, index switch
            {
                0 => HorizontalAlignment.Left,
                5 => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Right
            });
        }

        var root = _marketTable.CreateItem();
        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            var row = _marketTable.CreateItem(root);
            var current = MarketRules.GetBuyUnitPrice(city, product);
            var previous = MarketRules.GetPreviousBuyPrice(city, product);
            row.SetMetadata(0, (int)product);
            row.SetText(0, GetProductNameKey(product));
            row.SetText(1, current.ToString());
            row.SetText(2, MarketRules.GetSellUnitPrice(city, product).ToString());
            row.SetText(3, previous > 0 ? previous.ToString() : "-");
            row.SetText(4, previous <= 0 ? "-" : current == previous ? "=" : current > previous ? $"↑{current - previous}" : $"↓{previous - current}");
            row.SetText(5, _context.Localization.T(MarketRules.GetStatusKey(city, product)));
            row.SetTextAlignment(0, HorizontalAlignment.Left);
            row.SetTextAlignment(1, HorizontalAlignment.Right);
            row.SetTextAlignment(2, HorizontalAlignment.Right);
            row.SetTextAlignment(3, HorizontalAlignment.Right);
            row.SetTextAlignment(4, HorizontalAlignment.Right);
            row.SetTextAlignment(5, HorizontalAlignment.Center);
        }
    }

    private void OnMarketItemSelected()
    {
        var selected = _marketTable?.GetSelected();
        if (selected == null || _tradeModeOption == null)
        {
            return;
        }

        var metadata = selected.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var product = (MarketProductType)metadata.AsInt32();
        var mode = IsSelling(GetSelectedTradeMode()) ? GetSellMode(product) : GetBuyMode(product);
        for (var index = 0; index < _tradeModeOption.ItemCount; index += 1)
        {
            if (_tradeModeOption.GetItemMetadata(index).AsInt32() == (int)mode)
            {
                _tradeModeOption.Select(index);
                break;
            }
        }

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
        var city = _context.SelectedCity;
        MarketRules.EnsureMarketInitialized(city);
        var product = GetProduct(tradeMode);
        var lotSize = MarketRules.GetTradeLotSize(product);
        var isSelling = IsSelling(tradeMode);
        var maxAmount = isSelling
            ? MarketRules.GetAmount(city, product)
            : Math.Min(MarketRules.GetMerchantStock(city, product), Math.Min(MarketRules.GetAvailableCapacity(city, product), city.Gold / MarketRules.GetBuyUnitPrice(city, product)));

        _amountSpinBox.MinValue = 0;
        _amountSpinBox.MaxValue = maxAmount;
        _amountSpinBox.Step = lotSize;
        _amountSpinBox.Value = maxAmount <= 0 ? 0 : lotSize;
        _amountSpinBox.Value = Mathf.Clamp(_amountSpinBox.Value, 0, maxAmount);
        UpdateConfirmAvailability();
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _amountSpinBox == null || _context.Localization == null)
        {
            return;
        }

        var amount = (int)_amountSpinBox.Value;
        var tradeMode = GetSelectedTradeMode();
        var city = _context.SelectedCity;
        if (city == null)
        {
            return;
        }

        var product = GetProduct(tradeMode);
        var lotSize = MarketRules.GetTradeLotSize(product);
        var isSelling = IsSelling(tradeMode);
        var goldAmount = amount * (isSelling ? MarketRules.GetSellUnitPrice(city, product) : MarketRules.GetBuyUnitPrice(city, product));
        _summaryLabel.Text = _context.Localization.Format(
            isSelling ? "fmt.merchant_sell_total_preview" : "fmt.merchant_buy_total_preview",
            MarketRules.GetAmount(city, product),
            MarketRules.GetCapacity(city, product),
            MarketRules.GetMerchantStock(city, product),
            amount,
            goldAmount);
        UpdateConfirmAvailability();
    }

    private void UpdateConfirmAvailability()
    {
        if (_confirmButton == null || _amountSpinBox == null || _context.SelectedCity == null)
        {
            return;
        }

        var city = _context.SelectedCity;
        var tradeMode = GetSelectedTradeMode();
        var product = GetProduct(tradeMode);
        var amount = (int)_amountSpinBox.Value;
        var lotSize = MarketRules.GetTradeLotSize(product);
        var isSelling = IsSelling(tradeMode);
        var isValid = amount > 0 && amount % lotSize == 0;
        if (isSelling)
        {
            isValid &= amount <= MarketRules.GetAmount(city, product);
        }
        else
        {
            var totalCost = (long)amount * MarketRules.GetBuyUnitPrice(city, product);
            isValid &= amount <= MarketRules.GetMerchantStock(city, product) &&
                       amount <= MarketRules.GetAvailableCapacity(city, product) &&
                       totalCost <= city.Gold;
        }

        _confirmButton.Disabled = !isValid;
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

    private static bool IsSelling(MerchantTradeMode tradeMode) => tradeMode is MerchantTradeMode.SellFood or MerchantTradeMode.SellHorse or MerchantTradeMode.SellMetal;

    private static MarketProductType GetProduct(MerchantTradeMode tradeMode) => tradeMode switch
    {
        MerchantTradeMode.BuyHorse or MerchantTradeMode.SellHorse => MarketProductType.Horse,
        MerchantTradeMode.BuyMetal or MerchantTradeMode.SellMetal => MarketProductType.Metal,
        _ => MarketProductType.Food
    };

    private static MerchantTradeMode GetBuyMode(MarketProductType product) => product switch
    {
        MarketProductType.Horse => MerchantTradeMode.BuyHorse,
        MarketProductType.Metal => MerchantTradeMode.BuyMetal,
        _ => MerchantTradeMode.BuyFood
    };

    private static MerchantTradeMode GetSellMode(MarketProductType product) => product switch
    {
        MarketProductType.Horse => MerchantTradeMode.SellHorse,
        MarketProductType.Metal => MerchantTradeMode.SellMetal,
        _ => MerchantTradeMode.SellFood
    };

    private string GetProductNameKey(MarketProductType product) => _context.Localization?.T(product switch
    {
        MarketProductType.Food => "product.food",
        MarketProductType.Horse => "product.horse",
        _ => "product.metal"
    }) ?? product.ToString();

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }
}
