namespace ThreeKingdom.UI;

using Godot;
using ThreeKingdom.Core;

internal sealed class MainHudUiController
{
    private readonly MainHudUiContext _context;
    private readonly UiEventHub _uiEventHub;
    private readonly TopBarController _topBarController;
    private readonly CityInfoPanelController _cityInfoPanelController;
    private readonly LogPanelController _logPanelController;

    public MainHudUiController(HudController owner)
    {
        _context = new MainHudUiContext(owner);
        _uiEventHub = _context.UiEventHub;
        _topBarController = new TopBarController(_context);
        _cityInfoPanelController = new CityInfoPanelController(_context);
        _logPanelController = new LogPanelController(_context);
    }

    public void Initialize()
    {
        _topBarController.Initialize();
        _cityInfoPanelController.Initialize();
        _logPanelController.Initialize();
        _uiEventHub.CityStateChanged += OnWorldStateChanged;
        _uiEventHub.OfficerStateChanged += OnWorldStateChanged;
        _uiEventHub.OfficerAppointmentsChanged += OnWorldStateChanged;
        _uiEventHub.FactionLeadershipChanged += OnWorldStateChanged;
    }

    public void Shutdown()
    {
        _uiEventHub.CityStateChanged -= OnWorldStateChanged;
        _uiEventHub.OfficerStateChanged -= OnWorldStateChanged;
        _uiEventHub.OfficerAppointmentsChanged -= OnWorldStateChanged;
        _uiEventHub.FactionLeadershipChanged -= OnWorldStateChanged;
        _topBarController.Shutdown();
        _cityInfoPanelController.Shutdown();
    }

    public void RefreshText()
    {
        _topBarController.RefreshText();
        _cityInfoPanelController.RefreshText();
        _logPanelController.RefreshText();
    }

    public void RefreshSelectedCity()
    {
        _cityInfoPanelController.RefreshSelectedCity();
    }

    public void RefreshMonth()
    {
        _topBarController.RefreshMonth();
    }

    public void AddLog(string message, bool isPlayerRelated = false)
    {
        _logPanelController.AddLog(message, isPlayerRelated);
    }

    public void RefreshPanelTitles()
    {
        _cityInfoPanelController.RefreshPanelTitle();
        _logPanelController.RefreshPanelTitle();
    }

    public void InitializeTopBarPanelBehavior(ColorRect? background, HBoxContainer? content) => _topBarController.InitializePanelBehavior(background, content);

    public void InitializeCityInfoPanelBehavior(PanelContainer? header, Button? minimizeButton, ColorRect? background, VBoxContainer? content) =>
        _cityInfoPanelController.InitializePanelBehavior(header, minimizeButton, background, content);

    public void InitializeLogPanelBehavior(PanelContainer? header, Button? minimizeButton, Button? resizeHandle, ColorRect? background) =>
        _logPanelController.InitializePanelBehavior(header, minimizeButton, resizeHandle, background);

    public Vector2 GetTopBarPosition() => _topBarController.GetPosition();
    public Vector2 GetTopBarSize() => _topBarController.GetSize();
    public void SetTopBarDraggedPosition(Vector2 targetPosition) => _topBarController.SetDraggedPosition(targetPosition);
    public void ApplyTopBarLayout() => _topBarController.ApplyLayout();
    public void BringTopBarToFront() => _topBarController.BringToFront();
    public void ApplyTopBarLoadedSettings(float x, float y) => _topBarController.ApplyLoadedSettings(x, y);
    public void PopulateTopBarSettings(OptionSettingsData settings) => _topBarController.PopulateSettings(settings);
    public void RestoreTopBarLayout() => _topBarController.RestoreDefaultLayout();

    public Vector2 GetCityInfoHeaderPosition() => _cityInfoPanelController.GetHeaderPosition();
    public float GetCityInfoHeaderWidth() => _cityInfoPanelController.GetHeaderWidth();
    public float GetCityInfoTotalHeight() => _cityInfoPanelController.GetTotalHeight();
    public void SetCityInfoDraggedPosition(Vector2 targetPosition) => _cityInfoPanelController.SetDraggedPosition(targetPosition);
    public void ToggleCityInfoMinimized() => _cityInfoPanelController.ToggleMinimized();
    public void ApplyCityInfoLayout() => _cityInfoPanelController.ApplyLayout();
    public void BringCityInfoToFront() => _cityInfoPanelController.BringToFront();
    public void ApplyCityInfoLoadedSettings(bool minimized, float x, float y, float width, float height) => _cityInfoPanelController.ApplyLoadedSettings(minimized, x, y, width, height);
    public void PopulateCityInfoSettings(OptionSettingsData settings) => _cityInfoPanelController.PopulateSettings(settings);
    public void RestoreCityInfoLayout() => _cityInfoPanelController.RestoreDefaultLayout();
    public void CollectVisibleCityInfoControls(System.Collections.Generic.List<Control> controls) => _cityInfoPanelController.CollectVisiblePanelControls(controls);
    public void SetCityInfoTemporarilyHidden(bool hidden) => _cityInfoPanelController.SetTemporarilyHidden(hidden);

    public Vector2 GetLogHeaderPosition() => _logPanelController.GetHeaderPosition();
    public float GetLogHeaderWidth() => _logPanelController.GetHeaderWidth();
    public float GetLogTotalHeight() => _logPanelController.GetTotalHeight();
    public void SetLogDraggedPosition(Vector2 targetPosition) => _logPanelController.SetDraggedPosition(targetPosition);
    public void ToggleLogMinimized() => _logPanelController.ToggleMinimized();
    public void ApplyLogLayout() => _logPanelController.ApplyLayout();
    public void BringLogToFront() => _logPanelController.BringToFront();
    public void ApplyLogLoadedSettings(bool minimized, float x, float y, float width, float height) => _logPanelController.ApplyLoadedSettings(minimized, x, y, width, height);
    public void PopulateLogSettings(OptionSettingsData settings) => _logPanelController.PopulateSettings(settings);
    public void RestoreLogLayout() => _logPanelController.RestoreDefaultLayout();
    public void StartLogResize(Vector2 mousePosition) => _logPanelController.StartResize(mousePosition);
    public bool UpdateLogResize(Vector2 mousePosition) => _logPanelController.UpdateResize(mousePosition);
    public bool EndLogResize() => _logPanelController.EndResize();
    public bool IsLogResizing() => _logPanelController.IsResizing();

    private void OnWorldStateChanged(UiEventHub.CityStateChangedEvent _)
    {
        RefreshSelectedCity();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerStateChangedEvent _)
    {
        RefreshSelectedCity();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerAppointmentsChangedEvent _)
    {
        RefreshSelectedCity();
    }

    private void OnWorldStateChanged(UiEventHub.FactionLeadershipChangedEvent _)
    {
        RefreshSelectedCity();
    }
}
