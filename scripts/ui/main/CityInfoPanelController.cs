using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal enum CityInfoDataTab
{
    Overview,
    Resources,
    Military,
    Construction,
    Domestic
}

internal sealed class CityInfoPanelController
{
    private const int CityNameEnglishFontSize = 14;
    private const int CityStatsEnglishFontSize = 12;
    private const int CityCommandButtonEnglishFontSize = 12;
    private const float FloatingPanelHeaderHeight = 0.0f;
    private const float CityBarHeight = 56.0f;
    private const float CityBarButtonInset = 12.0f;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float LeftPanelMinimumHeight = 260.0f;
    private const float LeftPanelBottomPadding = 56.0f;
    private const string LeftPanelMinimizeIconPath = "res://assets/ui/icons/128x128/minus.png";
    private const string LeftPanelMaximizeIconPath = "res://assets/ui/icons/128x128/plus.png";
    private static readonly Vector2 DefaultLeftPanelMinimizeButtonSize = new(32.0f, 32.0f);
    // leftPanel_2.png has a decorative outer frame.  Keep the draggable city
    // bar inside its usable inner area, aligned with the tabs and data content.
    private static readonly Vector2 CityBarOffset = new(28.0f, 14.0f);

    private readonly MainHudUiContext _context;
    private PanelContainer? _header;
    private Button? _minimizeButton;
    private ColorRect? _background;
    private VBoxContainer? _content;
    private Vector2 _defaultHeaderPosition;
    private Vector2 _headerPosition;
    private Vector2 _defaultBackgroundSize;
    private readonly Vector2 _backgroundOffset = new(0.0f, FloatingPanelHeaderHeight);
    private Vector2 _backgroundSize;
    private Vector2 _contentOffset;
    private Vector2 _defaultContentSize;
    private Vector2 _contentSize;
    private Vector2 _minimizeButtonSize = DefaultLeftPanelMinimizeButtonSize;
    private Texture2D? _minimizeButtonIcon;
    private Texture2D? _maximizeButtonIcon;
    private StyleBox? _minimizedCityBarStyle;
    private bool _minimized;
    private bool _temporarilyHidden;
    private bool _developButtonConnected;
    private bool _recruitButtonConnected;
    private bool _moveButtonConnected;
    private bool _searchButtonConnected;
    private bool _merchantButtonConnected;
    private bool _diplomacyButtonConnected;
    private bool _spyButtonConnected;
    private bool _personnelButtonConnected;
    private bool _advisorButtonConnected;
    private bool _civilButtonConnected;
    private bool _attackButtonConnected;
    private bool _viewButtonConnected;
    private bool _strategicMapButtonConnected;
    private bool _testCaptureButtonConnected;
    private bool _overviewTabConnected;
    private bool _resourcesTabConnected;
    private bool _militaryTabConnected;
    private bool _constructionTabConnected;
    private bool _domesticTabConnected;
    private CityInfoDataTab _selectedDataTab = CityInfoDataTab.Overview;

    public CityInfoPanelController(MainHudUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        ConnectButtons();
    }

    public void Shutdown()
    {
        DisconnectButtons();
    }

    public void InitializePanelBehavior(PanelContainer? header, Button? minimizeButton, ColorRect? background, VBoxContainer? content)
    {
        _header = header;
        _minimizeButton = minimizeButton;
        _background = background;
        _content = content;
        if (_header != null)
        {
            // Editor defaults to the expanded, frameless presentation.  The
            // bordered version is created only for the collapsed city bar.
            _minimizedCityBarStyle = CreateMinimizedCityBarStyle();
        }
        if (_background == null || _content == null)
        {
            return;
        }

        var originalPosition = _background.Position;
        _defaultHeaderPosition = originalPosition;
        _headerPosition = originalPosition;
        _defaultBackgroundSize = new Vector2(_background.Size.X, _background.Size.Y - FloatingPanelHeaderHeight);
        _backgroundSize = _defaultBackgroundSize;
        _contentOffset = (_content.Position - originalPosition) + new Vector2(0.0f, FloatingPanelHeaderHeight);
        _defaultContentSize = new Vector2(_content.Size.X, _content.Size.Y - FloatingPanelHeaderHeight);
        _contentSize = _defaultContentSize;
        if (_minimizeButton != null)
        {
            _minimizeButtonIcon = _minimizeButton.Icon ?? GD.Load<Texture2D>(LeftPanelMinimizeIconPath);
            _maximizeButtonIcon = GD.Load<Texture2D>(LeftPanelMaximizeIconPath);
            if (_minimizeButton.Size.X > 0.0f && _minimizeButton.Size.Y > 0.0f)
            {
                _minimizeButtonSize = _minimizeButton.Size;
            }
        }
        if (_background != null)
        {
            _background.MouseFilter = Control.MouseFilterEnum.Stop;
        }
        if (_content != null)
        {
            _content.MouseFilter = Control.MouseFilterEnum.Pass;
        }
        ApplyLayout();
    }

    public void RefreshText()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        if (_context.CommandsTitle != null)
        {
            _context.CommandsTitle.Text = localization.T("ui.commands");
        }

        SetButtonText(_context.OverviewTab, localization.T("ui.city_tab_overview"));
        SetButtonText(_context.ResourcesTab, localization.T("ui.city_tab_resources"));
        SetButtonText(_context.MilitaryTab, localization.T("ui.city_tab_military"));
        SetButtonText(_context.ConstructionTab, localization.T("ui.city_tab_construction"));
        SetButtonText(_context.DomesticTab, localization.T("ui.city_tab_domestic"));

        SetButtonText(_context.DevelopButton, localization.T("ui.internal_affairs"));
        SetButtonText(_context.RecruitButton, localization.T("ui.military"));
        SetButtonText(_context.MoveButton, localization.T("ui.move"));
        SetButtonText(_context.SearchButton, localization.T("ui.search"));
        SetButtonText(_context.MerchantButton, localization.T("ui.merchant"));
        SetButtonText(_context.DiplomacyButton, localization.T("ui.diplomacy"));
        SetButtonText(_context.SpyButton, localization.T("ui.spy"));
        SetButtonText(_context.PersonnelButton, localization.T("ui.personnel"));
        SetButtonText(_context.AdvisorButton, localization.T("ui.advisor_menu"));
        SetButtonText(_context.CivilButton, localization.T("ui.civil"));
        SetButtonText(_context.AttackButton, localization.T("ui.attack"));
        SetButtonText(_context.ViewButton, localization.T("ui.view"));
        SetButtonText(_context.StrategicMapButton, localization.T("ui.strategic_map.title"));
        SetButtonText(_context.TestCaptureButton, localization.IsTraditionalChinese ? "測試俘虜" : "Test Capture");

        if (_context.MoveButton != null)
        {
            _context.MoveButton.Visible = false;
        }

        if (_context.AttackButton != null)
        {
            _context.AttackButton.Visible = false;
        }

        UpdateDataTabStates();
        ApplyTypography();
        RefreshPanelTitle();
        RefreshSelectedCity();
    }

    public void RefreshPanelTitle()
    {
        RefreshCityBarLabel(_context.SelectedCity);
    }

    private void RefreshCityBarLabel(CityData? city)
    {
        if (_context.CityPanelHeaderLabel != null)
        {
            _context.CityPanelHeaderLabel.Text = _context.BuildCityHeaderText(city);
        }
    }

    public void RefreshSelectedCity()
    {
        var localization = _context.Localization;
        var world = _context.World;
        if (localization == null || world == null)
        {
            return;
        }

        var selectedCity = _context.SelectedCity;
        if (selectedCity == null)
        {
            if (_context.CityNameLabel != null)
            {
                _context.CityNameLabel.Text = _context.BuildCityHeaderText(null);
            }
            RefreshCityBarLabel(null);

            if (_context.CityStatsPanel != null)
            {
                _context.PopulateCityStats(_context.CityStatsPanel, "-", null, 0, _selectedDataTab);
                ApplyCityStatsTypography(_context.CityStatsPanel, localization.IsTraditionalChinese);
            }

            _context.UpdateGameplayButtonStates();
            _context.RequestFloatingPanelLayoutRefresh();
            return;
        }

        if (_context.CityNameLabel != null)
        {
            _context.CityNameLabel.Text = _context.BuildCityHeaderText(selectedCity);
        }
        RefreshCityBarLabel(selectedCity);

        if (_context.CityStatsPanel != null)
        {
            var ownerName = localization.GetFactionName(world, selectedCity.OwnerFactionId);
            var freeOfficerCount = world.Officers.Count(officer =>
                officer.CityId == selectedCity.Id &&
                FreeOfficerMovement.IsVisibleFreeOfficer(world, officer));
            _context.PopulateCityStats(_context.CityStatsPanel, ownerName, selectedCity, freeOfficerCount, _selectedDataTab);
            ApplyCityStatsTypography(_context.CityStatsPanel, localization.IsTraditionalChinese);
        }

        _context.UpdateGameplayButtonStates();
        _context.RequestFloatingPanelLayoutRefresh();
    }

    // Consumers use this for mouse-offset calculations, so expose the actual
    // visible bar position rather than the invisible outer-frame origin.
    public Vector2 GetHeaderPosition() => _headerPosition + CityBarOffset;

    public bool IsMinimized() => _minimized;

    public float GetHeaderWidth() => Mathf.Max(1.0f, _backgroundSize.X - (CityBarOffset.X * 2.0f));

    public float GetTotalHeight()
    {
        return _minimized
            ? CityBarHeight
            : Mathf.Max(
                CityBarHeight,
                Mathf.Max(
                    _backgroundOffset.Y + _backgroundSize.Y,
                    _contentOffset.Y + _contentSize.Y));
    }

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        // targetPosition is the city bar's position; store the enclosing panel
        // origin so the frame, content, and bar continue to move together.
        _headerPosition = targetPosition - CityBarOffset;
        ApplyLayout();
    }

    public void ToggleMinimized()
    {
        _minimized = !_minimized;
        ApplyLayout();
    }

    public void ApplyLayout()
    {
        if (_header == null || _background == null || _content == null || _minimizeButton == null)
        {
            return;
        }

        ClampSizeToCurrentLimits();
        var headerWidth = GetHeaderWidth();
        _headerPosition = _minimized
            ? ClampPosition(_headerPosition + CityBarOffset, headerWidth, CityBarHeight) - CityBarOffset
            : ClampPosition(_headerPosition, _backgroundSize.X, GetTotalHeight());
        var cityBarPosition = _headerPosition + CityBarOffset;
        _header.Position = cityBarPosition;
        _header.Size = new Vector2(headerWidth, CityBarHeight);
        // The full panel already has its own gold frame.  Showing a second
        // city-bar frame inside it looks like a duplicate, so only use it
        // when the city bar is the collapsed panel itself.
        _header.AddThemeStyleboxOverride(
            "panel",
            _minimized ? _minimizedCityBarStyle ?? new StyleBoxEmpty() : new StyleBoxEmpty());
        _minimizeButton.Text = string.Empty;
        _minimizeButton.Icon = _minimized
            ? _maximizeButtonIcon ?? _minimizeButtonIcon
            : _minimizeButtonIcon;
        _header.Visible = !_temporarilyHidden;
        _minimizeButton.Visible = !_temporarilyHidden;
        _minimizeButton.Position = cityBarPosition + new Vector2(
            headerWidth - _minimizeButtonSize.X - CityBarButtonInset,
            (CityBarHeight - _minimizeButtonSize.Y) * 0.5f);
        _minimizeButton.Size = _minimizeButtonSize;

        _background.Visible = !_temporarilyHidden && !_minimized;
        _content.Visible = !_temporarilyHidden && !_minimized;
        if (_temporarilyHidden || _minimized)
        {
            return;
        }

        _background.Position = _headerPosition + _backgroundOffset;
        _background.Size = _backgroundSize;
        _content.Position = _headerPosition + _contentOffset;
        _content.Size = _contentSize;
    }

    public void BringToFront()
    {
        _context.MoveToFront(_background);
        _context.MoveToFront(_content);
        _context.MoveToFront(_header);
        _context.MoveToFront(_minimizeButton);
    }

    public void CollectVisiblePanelControls(System.Collections.Generic.List<Control> controls)
    {
        AddVisibleControl(controls, _header);
        AddVisibleControl(controls, _background);
        AddVisibleControl(controls, _content);
        AddVisibleControl(controls, _minimizeButton);
    }

    public void SetTemporarilyHidden(bool hidden)
    {
        _temporarilyHidden = hidden;
        ApplyLayout();
    }

    public void ApplyLoadedSettings(bool minimized, float x, float y, float width, float height)
    {
        _minimized = minimized;
        _headerPosition = new Vector2(x, y);
        if (width > 0.0f)
        {
            _backgroundSize.X = _defaultBackgroundSize.X;
            _contentSize.X = _defaultContentSize.X;
        }

        if (height > 0.0f)
        {
            var minimumContentHeight = GetMinimumContentHeight();
            _backgroundSize.Y = GetPreferredHeight(minimumContentHeight);
            _contentSize.Y = minimumContentHeight;
        }
    }

    public void PopulateSettings(OptionSettingsData settings)
    {
        settings.LeftPanelMinimized = _minimized;
        settings.LeftPanelX = _headerPosition.X;
        settings.LeftPanelY = _headerPosition.Y;
        settings.LeftPanelWidth = _backgroundSize.X;
        settings.LeftPanelHeight = _backgroundSize.Y;
    }

    public void RestoreDefaultLayout()
    {
        _minimized = false;
        _headerPosition = _defaultHeaderPosition;
        _backgroundSize = _defaultBackgroundSize;
        _contentSize = _defaultContentSize;
        ApplyLayout();
    }

    private void ClampSizeToCurrentLimits()
    {
        var minimumContentHeight = GetMinimumContentHeight();
        _backgroundSize = new Vector2(_defaultBackgroundSize.X, GetPreferredHeight(minimumContentHeight));
        _contentSize = new Vector2(_defaultContentSize.X, minimumContentHeight);
    }

    private static StyleBoxFlat CreateMinimizedCityBarStyle()
    {
        return new StyleBoxFlat
        {
            ContentMarginLeft = 18.0f,
            ContentMarginTop = 8.0f,
            ContentMarginRight = 62.0f,
            ContentMarginBottom = 8.0f,
            BgColor = new Color(0.025f, 0.022f, 0.018f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.58f, 0.4f, 0.15f, 0.9f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusBottomLeft = 5
        };
    }

    private float GetPreferredHeight()
    {
        return GetPreferredHeight(GetMinimumContentHeight());
    }

    private float GetPreferredHeight(float minimumContentHeight)
    {
        var contentTopInset = Mathf.Max(0.0f, _contentOffset.Y - _backgroundOffset.Y);
        return Mathf.Max(
            LeftPanelMinimumHeight,
            contentTopInset + minimumContentHeight + LeftPanelBottomPadding);
    }

    private float GetMinimumContentHeight()
    {
        if (_content == null)
        {
            return 180.0f;
        }

        return Mathf.Max(180.0f, _content.GetCombinedMinimumSize().Y);
    }

    private Vector2 ClampPosition(Vector2 position, float width, float height)
    {
        if (_background == null)
        {
            return position;
        }

        var viewportSize = _background.GetViewportRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            return position;
        }

        var maxX = Mathf.Max(FloatingPanelViewportMargin, viewportSize.X - width - FloatingPanelViewportMargin);
        var maxY = Mathf.Max(FloatingPanelTopClamp, viewportSize.Y - height - FloatingPanelViewportMargin);
        return new Vector2(
            Mathf.Clamp(position.X, FloatingPanelViewportMargin, maxX),
            Mathf.Clamp(position.Y, FloatingPanelTopClamp, maxY));
    }

    private void ApplyTypography()
    {
        var useChineseSizing = _context.Localization?.IsTraditionalChinese == true;

        if (_context.CityNameLabel != null)
        {
            if (useChineseSizing)
            {
                _context.CityNameLabel.RemoveThemeFontSizeOverride("font_size");
            }
            else
            {
                _context.CityNameLabel.AddThemeFontSizeOverride("font_size", CityNameEnglishFontSize);
            }
        }

        ApplyCityStatsTypography(_context.CityStatsPanel, useChineseSizing);

        foreach (var button in new[]
                 {
                     _context.DevelopButton,
                     _context.RecruitButton,
                     _context.MoveButton,
                     _context.SearchButton,
                     _context.MerchantButton,
                     _context.DiplomacyButton,
                     _context.SpyButton,
                     _context.PersonnelButton,
                     _context.AdvisorButton,
                     _context.CivilButton,
                     _context.AttackButton,
                     _context.ViewButton,
                     _context.StrategicMapButton,
                     _context.TestCaptureButton
                 })
        {
            if (button == null)
            {
                continue;
            }

            button.CustomMinimumSize = new Vector2(0.0f, 38.0f);
            button.AddThemeColorOverride("font_color", new Color(0.94f, 0.82f, 0.58f, 1.0f));
            button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.66f, 1.0f));
            button.AddThemeColorOverride("font_pressed_color", new Color(0.82f, 0.67f, 0.4f, 1.0f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.46f, 0.43f, 0.37f, 1.0f));

            if (useChineseSizing)
            {
                button.AddThemeFontSizeOverride("font_size", 16);
            }
            else
            {
                button.AddThemeFontSizeOverride("font_size", CityCommandButtonEnglishFontSize);
            }
        }
    }

    private static void SetButtonText(Button? button, string text)
    {
        if (button != null)
        {
            button.Text = text;
        }
    }

    private static void AddVisibleControl(System.Collections.Generic.List<Control> controls, Control? control)
    {
        if (control?.Visible == true)
        {
            controls.Add(control);
        }
    }

    private static void ApplyCityStatsTypography(VBoxContainer? panel, bool useChineseSizing)
    {
        if (panel == null)
        {
            return;
        }

        foreach (var child in panel.GetChildren())
        {
            if (child is not Control control)
            {
                continue;
            }

            ApplyFontSizeToControlTree(control, useChineseSizing);
        }
    }

    private static void ApplyFontSizeToControlTree(Control control, bool useChineseSizing)
    {
        if (control is Label label)
        {
            if (useChineseSizing)
            {
                label.RemoveThemeFontSizeOverride("font_size");
            }
            else
            {
                label.AddThemeFontSizeOverride("font_size", CityStatsEnglishFontSize);
            }
        }

        foreach (var child in control.GetChildren())
        {
            if (child is Control nestedControl)
            {
                ApplyFontSizeToControlTree(nestedControl, useChineseSizing);
            }
        }
    }

    private void ConnectButtons()
    {
        ConnectButton(_context.OverviewTab, ref _overviewTabConnected, OnOverviewTabPressed);
        ConnectButton(_context.ResourcesTab, ref _resourcesTabConnected, OnResourcesTabPressed);
        ConnectButton(_context.MilitaryTab, ref _militaryTabConnected, OnMilitaryTabPressed);
        ConnectButton(_context.ConstructionTab, ref _constructionTabConnected, OnConstructionTabPressed);
        ConnectButton(_context.DomesticTab, ref _domesticTabConnected, OnDomesticTabPressed);
        ConnectButton(_context.DevelopButton, ref _developButtonConnected, OnDevelopButtonPressed);
        ConnectButton(_context.RecruitButton, ref _recruitButtonConnected, OnRecruitButtonPressed);
        ConnectButton(_context.MoveButton, ref _moveButtonConnected, OnMoveButtonPressed);
        ConnectButton(_context.SearchButton, ref _searchButtonConnected, OnSearchButtonPressed);
        ConnectButton(_context.MerchantButton, ref _merchantButtonConnected, OnMerchantButtonPressed);
        ConnectButton(_context.DiplomacyButton, ref _diplomacyButtonConnected, OnDiplomacyButtonPressed);
        ConnectButton(_context.SpyButton, ref _spyButtonConnected, OnSpyButtonPressed);
        ConnectButton(_context.PersonnelButton, ref _personnelButtonConnected, OnPersonnelButtonPressed);
        ConnectButton(_context.AdvisorButton, ref _advisorButtonConnected, OnAdvisorButtonPressed);
        ConnectButton(_context.CivilButton, ref _civilButtonConnected, OnCivilButtonPressed);
        ConnectButton(_context.AttackButton, ref _attackButtonConnected, OnAttackButtonPressed);
        ConnectButton(_context.ViewButton, ref _viewButtonConnected, OnViewButtonPressed);
        ConnectButton(_context.StrategicMapButton, ref _strategicMapButtonConnected, OnStrategicMapButtonPressed);
        ConnectButton(_context.TestCaptureButton, ref _testCaptureButtonConnected, OnTestCaptureButtonPressed);
    }

    private void DisconnectButtons()
    {
        DisconnectButton(_context.OverviewTab, ref _overviewTabConnected, OnOverviewTabPressed);
        DisconnectButton(_context.ResourcesTab, ref _resourcesTabConnected, OnResourcesTabPressed);
        DisconnectButton(_context.MilitaryTab, ref _militaryTabConnected, OnMilitaryTabPressed);
        DisconnectButton(_context.ConstructionTab, ref _constructionTabConnected, OnConstructionTabPressed);
        DisconnectButton(_context.DomesticTab, ref _domesticTabConnected, OnDomesticTabPressed);
        DisconnectButton(_context.DevelopButton, ref _developButtonConnected, OnDevelopButtonPressed);
        DisconnectButton(_context.RecruitButton, ref _recruitButtonConnected, OnRecruitButtonPressed);
        DisconnectButton(_context.MoveButton, ref _moveButtonConnected, OnMoveButtonPressed);
        DisconnectButton(_context.SearchButton, ref _searchButtonConnected, OnSearchButtonPressed);
        DisconnectButton(_context.MerchantButton, ref _merchantButtonConnected, OnMerchantButtonPressed);
        DisconnectButton(_context.DiplomacyButton, ref _diplomacyButtonConnected, OnDiplomacyButtonPressed);
        DisconnectButton(_context.SpyButton, ref _spyButtonConnected, OnSpyButtonPressed);
        DisconnectButton(_context.PersonnelButton, ref _personnelButtonConnected, OnPersonnelButtonPressed);
        DisconnectButton(_context.AdvisorButton, ref _advisorButtonConnected, OnAdvisorButtonPressed);
        DisconnectButton(_context.CivilButton, ref _civilButtonConnected, OnCivilButtonPressed);
        DisconnectButton(_context.AttackButton, ref _attackButtonConnected, OnAttackButtonPressed);
        DisconnectButton(_context.ViewButton, ref _viewButtonConnected, OnViewButtonPressed);
        DisconnectButton(_context.StrategicMapButton, ref _strategicMapButtonConnected, OnStrategicMapButtonPressed);
        DisconnectButton(_context.TestCaptureButton, ref _testCaptureButtonConnected, OnTestCaptureButtonPressed);
    }

    private static void ConnectButton(Button? button, ref bool isConnected, Action pressedHandler)
    {
        if (button == null || isConnected)
        {
            return;
        }

        button.Pressed += pressedHandler;
        isConnected = true;
    }

    private static void DisconnectButton(Button? button, ref bool isConnected, Action pressedHandler)
    {
        if (button == null || !isConnected)
        {
            return;
        }

        button.Pressed -= pressedHandler;
        isConnected = false;
    }

    private void OnOverviewTabPressed() => SelectDataTab(CityInfoDataTab.Overview);
    private void OnResourcesTabPressed() => SelectDataTab(CityInfoDataTab.Resources);
    private void OnMilitaryTabPressed() => SelectDataTab(CityInfoDataTab.Military);
    private void OnConstructionTabPressed() => SelectDataTab(CityInfoDataTab.Construction);
    private void OnDomesticTabPressed() => SelectDataTab(CityInfoDataTab.Domestic);

    private void SelectDataTab(CityInfoDataTab tab)
    {
        _selectedDataTab = tab;
        UpdateDataTabStates();
        RefreshSelectedCity();
    }

    private void UpdateDataTabStates()
    {
        SetTabPressed(_context.OverviewTab, _selectedDataTab == CityInfoDataTab.Overview);
        SetTabPressed(_context.ResourcesTab, _selectedDataTab == CityInfoDataTab.Resources);
        SetTabPressed(_context.MilitaryTab, _selectedDataTab == CityInfoDataTab.Military);
        SetTabPressed(_context.ConstructionTab, _selectedDataTab == CityInfoDataTab.Construction);
        SetTabPressed(_context.DomesticTab, _selectedDataTab == CityInfoDataTab.Domestic);
    }

    private static void SetTabPressed(Button? button, bool pressed)
    {
        if (button != null)
        {
            button.ButtonPressed = pressed;
        }
    }

    private void OnDevelopButtonPressed() => _context.OpenInternalAffairs();
    private void OnRecruitButtonPressed() => _context.OpenMilitary();
    private void OnMoveButtonPressed() => _context.OpenMove();
    private void OnSearchButtonPressed() => _context.OpenSearch();
    private void OnMerchantButtonPressed() => _context.OpenMerchant();
    private void OnDiplomacyButtonPressed() => _context.OpenDiplomacy();
    private void OnSpyButtonPressed() => _context.OpenSpy();
    private void OnPersonnelButtonPressed() => _context.OpenPersonnel();
    private void OnAdvisorButtonPressed() => _context.OpenAdvisor();
    private void OnCivilButtonPressed() => _context.OpenCivil();
    private void OnAttackButtonPressed() => _context.OpenAttack();
    private void OnViewButtonPressed() => _context.OpenView();
    private void OnStrategicMapButtonPressed() => _context.OpenStrategicMap();
    private void OnTestCaptureButtonPressed() => _context.OpenTestCapture();
}
