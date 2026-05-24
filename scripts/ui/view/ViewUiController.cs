using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.UI;

public sealed class ViewUiController
{
    private readonly ViewUiContext _context;
    private readonly UiEventHub _uiEventHub;
    private readonly OfficerListDialogController _officerListDialogController;
    private readonly OfficerDetailDialogController _officerDetailDialogController;

    public ViewUiController(HudController hud)
    {
        _context = new ViewUiContext(hud);
        _uiEventHub = hud.UiEventHub;
        _officerDetailDialogController = new OfficerDetailDialogController(_context);
        _officerListDialogController = new OfficerListDialogController(_context, _officerDetailDialogController);
    }

    public void Initialize()
    {
        _officerListDialogController.Initialize();
        _officerDetailDialogController.Initialize();
        _uiEventHub.CityStateChanged += OnWorldStateChanged;
        _uiEventHub.OfficerStateChanged += OnWorldStateChanged;
        _uiEventHub.OfficerAppointmentsChanged += OnOfficerAppointmentsChanged;
        _uiEventHub.FactionLeadershipChanged += OnWorldStateChanged;
    }

    public void Shutdown()
    {
        _uiEventHub.CityStateChanged -= OnWorldStateChanged;
        _uiEventHub.OfficerStateChanged -= OnWorldStateChanged;
        _uiEventHub.OfficerAppointmentsChanged -= OnOfficerAppointmentsChanged;
        _uiEventHub.FactionLeadershipChanged -= OnWorldStateChanged;
    }

    public void HideDialogs()
    {
        _officerDetailDialogController.Hide();
        _officerListDialogController.Hide();
    }

    public void RefreshText()
    {
        _officerListDialogController.RefreshText();
        _officerDetailDialogController.RefreshText();
    }

    public void RefreshOfficerListContent()
    {
        _officerListDialogController.PopulateDialog();
    }

    public void RefreshOfficerListChrome()
    {
        _officerListDialogController.RefreshChrome();
    }

    public void ShowViewDialog()
    {
        _officerListDialogController.ShowMainDialog();
    }

    private void OnOfficerAppointmentsChanged(UiEventHub.OfficerAppointmentsChangedEvent payload)
    {
        RefreshOpenDialogs();
    }

    private void OnWorldStateChanged(UiEventHub.CityStateChangedEvent payload)
    {
        RefreshOpenDialogs();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerStateChangedEvent payload)
    {
        RefreshOpenDialogs();
    }

    private void OnWorldStateChanged(UiEventHub.FactionLeadershipChangedEvent payload)
    {
        RefreshOpenDialogs();
    }

    private void RefreshOpenDialogs()
    {
        if (_officerListDialogController.IsOpen())
        {
            RefreshOfficerListChrome();
            RefreshOfficerListContent();
        }

        _officerDetailDialogController.RefreshShownOfficer();
    }

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        AddVisibleOverlay(overlays, _officerListDialogController);
        AddVisibleOverlay(overlays, _officerDetailDialogController);
    }

    private static void AddVisibleOverlay(List<Control> overlays, FloatingOverlayController controller)
    {
        if (controller.OverlayControl?.Visible == true)
        {
            overlays.Add(controller.OverlayControl);
        }
    }
}
