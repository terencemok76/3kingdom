using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class CityInfoPanelController
{
    private const int CityNameEnglishFontSize = 14;
    private const int CityStatsEnglishFontSize = 12;
    private const int CityCommandButtonEnglishFontSize = 12;
    private const float FloatingPanelHeaderHeight = 30.0f;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float LeftPanelMinimizedWidth = 176.0f;
    private const float LeftPanelMinimumHeight = 260.0f;

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
    private bool _testCaptureButtonConnected;

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
        if (_background != null)
        {
            _background.MouseFilter = Control.MouseFilterEnum.Ignore;
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
        SetButtonText(_context.TestCaptureButton, localization.IsTraditionalChinese ? "測試俘虜" : "Test Capture");

        if (_context.MoveButton != null)
        {
            _context.MoveButton.Visible = false;
        }

        if (_context.AttackButton != null)
        {
            _context.AttackButton.Visible = false;
        }

        ApplyTypography();
        RefreshPanelTitle();
        RefreshSelectedCity();
    }

    public void RefreshPanelTitle()
    {
        if (_context.CityPanelHeaderLabel != null)
        {
            _context.CityPanelHeaderLabel.Text = _context.Localization?.T("ui.city_panel_title") ?? "City Info";
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

            if (_context.CityStatsPanel != null)
            {
                _context.PopulateCityStats(_context.CityStatsPanel, "-", null, 0);
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

        if (_context.CityStatsPanel != null)
        {
            var ownerName = localization.GetFactionName(world, selectedCity.OwnerFactionId);
            var freeOfficerCount = world.Officers.Count(officer =>
                officer.CityId == selectedCity.Id &&
                FreeOfficerMovement.IsVisibleFreeOfficer(world, officer));
            _context.PopulateCityStats(_context.CityStatsPanel, ownerName, selectedCity, freeOfficerCount);
            ApplyCityStatsTypography(_context.CityStatsPanel, localization.IsTraditionalChinese);
        }

        _context.UpdateGameplayButtonStates();
        _context.RequestFloatingPanelLayoutRefresh();
    }

    public Vector2 GetHeaderPosition() => _headerPosition;

    public float GetHeaderWidth() => _minimized ? LeftPanelMinimizedWidth : _backgroundSize.X;

    public float GetTotalHeight()
    {
        return _minimized
            ? FloatingPanelHeaderHeight
            : Mathf.Max(
                FloatingPanelHeaderHeight + _backgroundOffset.Y + _backgroundSize.Y,
                FloatingPanelHeaderHeight + _contentOffset.Y + _contentSize.Y);
    }

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        _headerPosition = ClampPosition(targetPosition, GetHeaderWidth(), GetTotalHeight());
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
        _headerPosition = ClampPosition(_headerPosition, headerWidth, GetTotalHeight());
        _header.Position = _headerPosition;
        _header.Size = new Vector2(headerWidth, FloatingPanelHeaderHeight);
        _minimizeButton.Text = _minimized ? "+" : "-";
        _header.Visible = !_temporarilyHidden;

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
    }

    public void CollectVisiblePanelControls(System.Collections.Generic.List<Control> controls)
    {
        AddVisibleControl(controls, _header);
        AddVisibleControl(controls, _background);
        AddVisibleControl(controls, _content);
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
            _backgroundSize.Y = GetPreferredHeight();
            _contentSize.Y = Mathf.Max(GetMinimumContentHeight(), _backgroundSize.Y - 20.0f);
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
        _backgroundSize = new Vector2(_defaultBackgroundSize.X, GetPreferredHeight());
        _contentSize = new Vector2(_defaultContentSize.X, Mathf.Max(GetMinimumContentHeight(), _backgroundSize.Y - 20.0f));
    }

    private float GetPreferredHeight()
    {
        return Mathf.Max(LeftPanelMinimumHeight, GetMinimumContentHeight() + 20.0f) + 4.0f;
    }

    private float GetMinimumContentHeight()
    {
        var commandBottom = 0.0f;
        if (_context.CommandsTitle != null && _context.CommandsTitle.Visible)
        {
            commandBottom = Mathf.Max(commandBottom, _context.CommandsTitle.Position.Y + _context.CommandsTitle.Size.Y);
        }

        var commandButtons = _context.DevelopButton?.GetParent() as Control;
        if (commandButtons != null && commandButtons.Visible)
        {
            commandBottom = Mathf.Max(commandBottom, commandButtons.Position.Y + commandButtons.Size.Y);
        }

        if (commandBottom > 0.0f)
        {
            return Mathf.Max(180.0f, commandBottom + 12.0f);
        }

        if (_content == null)
        {
            return 180.0f;
        }

        var maxBottom = 0.0f;
        foreach (var child in _content.GetChildren())
        {
            if (child is not Control control || !control.Visible)
            {
                continue;
            }

            maxBottom = Mathf.Max(maxBottom, control.Position.Y + control.Size.Y);
        }

        return Mathf.Max(180.0f, maxBottom + 8.0f);
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
                     _context.TestCaptureButton
                 })
        {
            if (button == null)
            {
                continue;
            }

            if (useChineseSizing)
            {
                button.RemoveThemeFontSizeOverride("font_size");
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
        ConnectButton(_context.TestCaptureButton, ref _testCaptureButtonConnected, OnTestCaptureButtonPressed);
    }

    private void DisconnectButtons()
    {
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
    private void OnTestCaptureButtonPressed() => _context.OpenTestCapture();
}
