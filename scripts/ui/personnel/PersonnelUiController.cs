using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.UI;

public sealed class PersonnelUiController
{
    private readonly PersonnelUiContext _context;
    private readonly UiEventHub _uiEventHub;
    private readonly PersonnelCommandDialogController _commandDialogController;
    private readonly PersonnelBonusDialogController _bonusDialogController;
    private readonly AssignRoleDialogController _assignRoleDialogController;
    private readonly PrefectAuthorizationDialogController _prefectAuthorizationDialogController;
    private readonly FireOfficerDialogController _fireOfficerDialogController;
    private readonly RequestItemDialogController _requestItemDialogController;
    private readonly HireOfficerDialogController _hireOfficerDialogController;
    private readonly PrisonerManagementDialogController _prisonerManagementDialogController;
    private readonly SuccessionDialogController _successionDialogController;

    public PersonnelUiController(HudController owner)
    {
        _context = new PersonnelUiContext(owner);
        _uiEventHub = _context.UiEventHub;
        _bonusDialogController = new PersonnelBonusDialogController(_context);
        _assignRoleDialogController = new AssignRoleDialogController(_context);
        _prefectAuthorizationDialogController = new PrefectAuthorizationDialogController(_context);
        _fireOfficerDialogController = new FireOfficerDialogController(_context);
        _requestItemDialogController = new RequestItemDialogController(_context);
        _hireOfficerDialogController = new HireOfficerDialogController(_context);
        _prisonerManagementDialogController = new PrisonerManagementDialogController(_context);
        _successionDialogController = new SuccessionDialogController(_context);
        _commandDialogController = new PersonnelCommandDialogController(
            _context,
            _bonusDialogController.Show,
            _assignRoleDialogController.Show,
            _prefectAuthorizationDialogController.Show,
            _fireOfficerDialogController.Show,
            _requestItemDialogController.Show,
            _hireOfficerDialogController.Show,
            _prisonerManagementDialogController.Show);
    }

    public int PendingSuccessionFactionId
    {
        get => _successionDialogController.PendingFactionId;
        set => _successionDialogController.PendingFactionId = value;
    }

    public void Initialize()
    {
        _commandDialogController.Initialize();
        _bonusDialogController.Initialize();
        _assignRoleDialogController.Initialize();
        _prefectAuthorizationDialogController.Initialize();
        _fireOfficerDialogController.Initialize();
        _requestItemDialogController.Initialize();
        _hireOfficerDialogController.Initialize();
        _prisonerManagementDialogController.Initialize();
        _successionDialogController.Initialize();
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
    }

    public void HideDialogs()
    {
        _commandDialogController.Hide();
        _bonusDialogController.Hide();
        _assignRoleDialogController.Hide();
        _prefectAuthorizationDialogController.Hide();
        _fireOfficerDialogController.Hide();
        _requestItemDialogController.Hide();
        _hireOfficerDialogController.Hide();
        _prisonerManagementDialogController.Hide();
        _successionDialogController.Hide();
    }

    public void RefreshText()
    {
        _commandDialogController.RefreshText();
        _bonusDialogController.RefreshText();
        _assignRoleDialogController.RefreshText();
        _prefectAuthorizationDialogController.RefreshText();
        _fireOfficerDialogController.RefreshText();
        _requestItemDialogController.RefreshText();
        _hireOfficerDialogController.RefreshText();
        _prisonerManagementDialogController.RefreshText();
    }

    public bool HasPendingPlayerSuccession() => _successionDialogController.HasPendingPlayerSuccession();

    public void ShowPersonnelDialog() => _commandDialogController.Show();

    public void ShowSuccessionDialog() => _successionDialogController.Show();

    public void RefreshIfOpen() => _prefectAuthorizationDialogController.RefreshIfOpen();

    private void OnWorldStateChanged(UiEventHub.CityStateChangedEvent _)
    {
        _prefectAuthorizationDialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerStateChangedEvent _)
    {
        _prefectAuthorizationDialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerAppointmentsChangedEvent _)
    {
        _prefectAuthorizationDialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.FactionLeadershipChangedEvent _)
    {
        _prefectAuthorizationDialogController.RefreshIfOpen();
    }

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        AddVisibleOverlay(overlays, _commandDialogController);
        AddVisibleOverlay(overlays, _bonusDialogController);
        AddVisibleOverlay(overlays, _assignRoleDialogController);
        AddVisibleOverlay(overlays, _prefectAuthorizationDialogController);
        AddVisibleOverlay(overlays, _fireOfficerDialogController);
        AddVisibleOverlay(overlays, _requestItemDialogController);
        AddVisibleOverlay(overlays, _hireOfficerDialogController);
        AddVisibleOverlay(overlays, _prisonerManagementDialogController);
        AddVisibleOverlay(overlays, _successionDialogController);
    }

    private static void AddVisibleOverlay(List<Control> overlays, FloatingOverlayController controller)
    {
        if (controller.OverlayControl?.Visible == true)
        {
            overlays.Add(controller.OverlayControl);
        }
    }
}
