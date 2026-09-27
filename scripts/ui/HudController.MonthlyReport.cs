using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private Control? _monthlyEconomyReportDialog;
    private PanelContainer? _monthlyEconomyReportPanel;
    private MonthlyEconomyResult? _pendingMonthlyEconomyReport;
    private Vector2 _monthlyReportDragOffset;
    private bool _isDraggingMonthlyReport;
    private bool _monthlyEconomyReportEnabled = true;

    private void ShowMonthlyEconomyReport(MonthlyEconomyResult result)
    {
        if (!_monthlyEconomyReportEnabled || _localization == null || _turnManager?.World == null || _monthlyEconomyReportDialog != null)
        {
            return;
        }

        var world = _turnManager.World;
        var root = new Control
        {
            Name = "MonthlyEconomyReportDialog",
            LayoutMode = 1,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(900, 620),
            Size = new Vector2(900, 620),
            Position = new Vector2(80, 70),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", CreateMonthlyReportPanelStyle());
        root.AddChild(panel);
        _monthlyEconomyReportPanel = panel;
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48), MouseFilter = Control.MouseFilterEnum.Stop };
        header.GuiInput += OnMonthlyReportHeaderGuiInput;
        content.AddChild(header);
        var title = new Label
        {
            Text = _localization.T("ui.monthly_report_title"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.94f, 0.84f, 0.62f));
        header.AddChild(title);
        var close = new Button { Text = "×", CustomMinimumSize = new Vector2(38, 32) };
        close.Pressed += CloseMonthlyEconomyReport;
        header.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        content.AddChild(scroll);
        var cities = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cities.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(cities);
        if (result.PlayerCityEconomyReports.Count == 0)
        {
            cities.AddChild(new Label { Text = _localization.T("ui.monthly_report_no_cities") });
        }
        else
        {
            foreach (var report in result.PlayerCityEconomyReports)
            {
                var city = world.GetCity(report.CityId);
                if (city != null)
                {
                    cities.AddChild(CreateMonthlyReportCityCard(city, report, result));
                }
            }
        }

        var buttonRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var dontShowAgain = new CheckBox
        {
            Text = _localization.T("ui.monthly_report_dont_show_again"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 34)
        };
        ConfigureMonthlyReportCheckbox(dontShowAgain);
        dontShowAgain.Toggled += OnMonthlyEconomyReportDontShowAgainToggled;
        buttonRow.AddChild(dontShowAgain);
        var acknowledge = new Button { Text = _localization.T("ui.monthly_report_close"), CustomMinimumSize = new Vector2(120, 36) };
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, acknowledge);
        }
        acknowledge.Pressed += CloseMonthlyEconomyReport;
        buttonRow.AddChild(acknowledge);
        content.AddChild(buttonRow);
        var overlayRoot = GetNodeOrNull<Control>("Root") as Node ?? this;
        overlayRoot.AddChild(root);
        _monthlyEconomyReportDialog = root;
    }

    private void QueueMonthlyEconomyReport(MonthlyEconomyResult result)
    {
        _pendingMonthlyEconomyReport = result;
        TryShowQueuedMonthlyEconomyReport();
    }

    private void TryShowQueuedMonthlyEconomyReport()
    {
        if (_pendingMonthlyEconomyReport == null || _activeEventPresentation != null || _pendingEventPresentations.Count > 0)
        {
            return;
        }

        var report = _pendingMonthlyEconomyReport;
        _pendingMonthlyEconomyReport = null;
        ShowMonthlyEconomyReport(report);
    }

    private Control CreateMonthlyReportCityCard(CityData city, MonthlyCityEconomyReport report, MonthlyEconomyResult monthlyResult)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", CreateMonthlyReportCardStyle());
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        card.AddChild(content);
        var status = BuildStorageStatus(city);
        var cityLabel = new Label { Text = $"{_localization!.GetCityName(city)}｜{status}" };
        cityLabel.AddThemeFontSizeOverride("font_size", 18);
        cityLabel.AddThemeColorOverride("font_color", status == _localization.T("ui.storage_full") ? new Color(0.96f, 0.5f, 0.4f) : new Color(0.94f, 0.84f, 0.62f));
        content.AddChild(cityLabel);
        content.AddChild(new Label
        {
            Text = _localization.Format(
                "fmt.monthly_report_stock_line",
                FormatSignedNumber(report.FoodDelta), report.FoodAmount, report.FoodCapacity,
                FormatSignedNumber(report.HorseDelta), report.HorseAmount, report.HorseCapacity,
                FormatSignedNumber(report.WoodDelta), report.WoodAmount, report.WoodCapacity,
                FormatSignedNumber(report.MetalDelta), report.MetalAmount, report.MetalCapacity,
                FormatSignedNumber(report.StoneDelta), report.StoneAmount, report.StoneCapacity)
        });
        var reasons = new Label
        {
            Text = BuildMonthlyReportReasons(report, monthlyResult),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        reasons.AddThemeColorOverride("font_color", new Color(0.76f, 0.76f, 0.7f));
        content.AddChild(reasons);
        content.AddChild(new Label
        {
            Text = _localization.Format(
                "fmt.monthly_report_facilities",
                city.GranaryLevel,
                city.HorseStableLevel,
                city.ResourceDepotLevel)
        });
        if (report.StorageLoss.HasLoss)
        {
            content.AddChild(new Label
            {
                Text = _localization.Format(
                    "log.phase5_monthly_storage_loss",
                    _localization.GetCityName(city), report.StorageLoss.Food, report.StorageLoss.Horse,
                    report.StorageLoss.Metal, report.StorageLoss.Wood, report.StorageLoss.Stone),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        return card;
    }

    private string BuildMonthlyReportReasons(MonthlyCityEconomyReport report, MonthlyEconomyResult monthlyResult)
    {
        var groups = new List<string>
        {
            BuildMonthlyResourceReason(
                "ui.monthly_report_food",
                report.FoodDelta,
                BuildFoodMonthlyReasons(report, monthlyResult)),
            BuildMonthlyResourceReason(
                "ui.monthly_report_horse",
                report.HorseDelta,
                BuildHorseMonthlyReasons(report, monthlyResult)),
            BuildMonthlyResourceReason(
                "ui.monthly_report_wood",
                report.WoodDelta,
                BuildStorageLossReason(report.StorageLoss.Wood)),
            BuildMonthlyResourceReason(
                "ui.monthly_report_metal",
                report.MetalDelta,
                BuildStorageLossReason(report.StorageLoss.Metal)),
            BuildMonthlyResourceReason(
                "ui.monthly_report_stone",
                report.StoneDelta,
                BuildStorageLossReason(report.StorageLoss.Stone))
        };

        return _localization!.Format("fmt.monthly_report_reasons", string.Join("｜", groups));
    }

    private string BuildFoodMonthlyReasons(MonthlyCityEconomyReport report, MonthlyEconomyResult monthlyResult)
    {
        var reasons = new List<string>();
        var harvest = monthlyResult.PlayerCityFoodIncome.Find(entry => entry.CityId == report.CityId).Amount;
        if (harvest > 0)
        {
            reasons.Add(_localization!.Format("fmt.monthly_report_harvest", FormatSignedNumber(harvest)));
        }

        if (report.FoodUpkeep > 0)
        {
            reasons.Add(_localization!.Format("fmt.monthly_report_upkeep", FormatSignedNumber(-report.FoodUpkeep)));
        }

        AddMonthlyFoodEventReasons(reasons, report.CityId, monthlyResult);
        AddStorageLossReason(reasons, report.StorageLoss.Food);
        return string.Join("、", reasons);
    }

    private string BuildHorseMonthlyReasons(MonthlyCityEconomyReport report, MonthlyEconomyResult monthlyResult)
    {
        var reasons = new List<string>();
        var births = monthlyResult.PlayerCityHorseBirths.Find(entry => entry.CityId == report.CityId).Amount;
        if (births > 0)
        {
            reasons.Add(_localization!.Format("fmt.monthly_report_horse_birth", FormatSignedNumber(births)));
        }

        AddStorageLossReason(reasons, report.StorageLoss.Horse);
        return string.Join("、", reasons);
    }

    private void AddMonthlyFoodEventReasons(List<string> reasons, int cityId, MonthlyEconomyResult monthlyResult)
    {
        foreach (var cityEvent in monthlyResult.PlayerCityEvents)
        {
            if (cityEvent.CityId == cityId && cityEvent.FoodDelta != 0)
            {
                reasons.Add(_localization!.Format(
                    "fmt.monthly_report_event",
                    GetEventDisplayName(cityEvent.EventType),
                    FormatSignedNumber(cityEvent.FoodDelta)));
            }
        }
    }

    private string BuildStorageLossReason(int loss) => loss > 0
        ? _localization!.Format("fmt.monthly_report_storage_loss", FormatSignedNumber(-loss))
        : string.Empty;

    private void AddStorageLossReason(List<string> reasons, int loss)
    {
        var reason = BuildStorageLossReason(loss);
        if (!string.IsNullOrEmpty(reason))
        {
            reasons.Add(reason);
        }
    }

    private string BuildMonthlyResourceReason(string resourceKey, int delta, string reasons)
    {
        if (string.IsNullOrEmpty(reasons))
        {
            reasons = delta == 0
                ? _localization!.T("ui.monthly_report_no_change")
                : _localization!.Format("fmt.monthly_report_net_change", FormatSignedNumber(delta));
        }

        return _localization!.Format("fmt.monthly_report_resource_reason", _localization.T(resourceKey), reasons);
    }

    private string BuildStorageStatus(CityData city)
    {
        var products = new[] { MarketProductType.Food, MarketProductType.Horse, MarketProductType.Wood, MarketProductType.Metal, MarketProductType.Stone };
        foreach (var product in products)
        {
            if (MarketRules.GetAmount(city, product) >= MarketRules.GetCapacity(city, product))
            {
                return _localization!.T("ui.storage_full");
            }
        }

        foreach (var product in products)
        {
            if (MarketRules.GetAmount(city, product) * 4 >= MarketRules.GetCapacity(city, product) * 3)
            {
                return _localization!.T("ui.storage_near_full");
            }
        }

        return _localization!.T("ui.storage_normal");
    }

    private void CloseMonthlyEconomyReport()
    {
        _monthlyEconomyReportDialog?.QueueFree();
        _monthlyEconomyReportDialog = null;
        _monthlyEconomyReportPanel = null;
        _isDraggingMonthlyReport = false;
    }

    private void OnMonthlyEconomyReportDontShowAgainToggled(bool pressed)
    {
        if (!pressed)
        {
            return;
        }

        _monthlyEconomyReportEnabled = false;
        SaveOptionSettings();
    }

    private static void ConfigureMonthlyReportCheckbox(CheckBox checkBox)
    {
        checkBox.AddThemeIconOverride("unchecked", CreateMonthlyReportCheckboxIcon(false));
        checkBox.AddThemeIconOverride("checked", CreateMonthlyReportCheckboxIcon(true));
        checkBox.AddThemeIconOverride("unchecked_disabled", CreateMonthlyReportCheckboxIcon(false));
        checkBox.AddThemeIconOverride("checked_disabled", CreateMonthlyReportCheckboxIcon(true));
        checkBox.AddThemeColorOverride("font_color", new Color(0.96f, 0.92f, 0.84f));
        checkBox.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.83f, 0.42f));
        checkBox.AddThemeConstantOverride("h_separation", 10);
    }

    private static Texture2D CreateMonthlyReportCheckboxIcon(bool isChecked)
    {
        var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);
        var border = new Color(0.88f, 0.69f, 0.31f);
        var fill = isChecked ? new Color(0.75f, 0.54f, 0.19f) : new Color(0.16f, 0.14f, 0.11f);
        image.Fill(border);
        image.FillRect(new Rect2I(2, 2, 18, 18), fill);

        if (isChecked)
        {
            var mark = new Color(0.12f, 0.09f, 0.05f);
            image.FillRect(new Rect2I(4, 10, 5, 3), mark);
            image.FillRect(new Rect2I(7, 13, 3, 3), mark);
            image.FillRect(new Rect2I(11, 6, 3, 10), mark);
        }

        return ImageTexture.CreateFromImage(image);
    }

    private void OnMonthlyReportHeaderGuiInput(InputEvent inputEvent)
    {
        if (_monthlyEconomyReportPanel == null)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isDraggingMonthlyReport = mouseButton.Pressed;
            if (_isDraggingMonthlyReport)
            {
                _monthlyReportDragOffset = mouseButton.GlobalPosition - _monthlyEconomyReportPanel.GlobalPosition;
            }
            return;
        }

        if (inputEvent is InputEventMouseMotion mouseMotion && _isDraggingMonthlyReport)
        {
            var viewportSize = GetViewport().GetVisibleRect().Size;
            var position = mouseMotion.GlobalPosition - _monthlyReportDragOffset;
            position.X = Mathf.Clamp(position.X, 0, Mathf.Max(0, viewportSize.X - _monthlyEconomyReportPanel.Size.X));
            position.Y = Mathf.Clamp(position.Y, 0, Mathf.Max(0, viewportSize.Y - _monthlyEconomyReportPanel.Size.Y));
            _monthlyEconomyReportPanel.GlobalPosition = position;
        }
    }

    private static StyleBoxFlat CreateMonthlyReportPanelStyle() => new()
    {
        BgColor = new Color(0.075f, 0.075f, 0.09f, 0.98f),
        BorderColor = new Color(0.58f, 0.45f, 0.27f),
        BorderWidthLeft = 3,
        BorderWidthTop = 3,
        BorderWidthRight = 3,
        BorderWidthBottom = 3,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
        ContentMarginLeft = 20,
        ContentMarginTop = 14,
        ContentMarginRight = 20,
        ContentMarginBottom = 14
    };

    private static StyleBoxFlat CreateMonthlyReportCardStyle() => new()
    {
        BgColor = new Color(0.1f, 0.1f, 0.12f, 0.94f),
        BorderColor = new Color(0.42f, 0.34f, 0.22f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 12,
        ContentMarginTop = 8,
        ContentMarginRight = 12,
        ContentMarginBottom = 8
    };
}
