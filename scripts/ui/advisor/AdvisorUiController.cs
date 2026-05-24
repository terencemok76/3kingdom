using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.UI;

public sealed class AdvisorUiController
{
    private readonly AdvisorUiContext _context;
    private readonly UiEventHub _uiEventHub;
    private readonly AdvisorDialogController _dialogController;

    public AdvisorUiController(HudController owner)
    {
        _context = new AdvisorUiContext(owner);
        _uiEventHub = _context.UiEventHub;
        _dialogController = new AdvisorDialogController(_context);
    }

    public void Initialize()
    {
        _dialogController.Initialize();
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
        _dialogController.Hide();
    }

    public void RefreshText()
    {
        _dialogController.RefreshText();
    }

    public void ShowAdvisorDialog()
    {
        _dialogController.Show();
    }

    private void OnWorldStateChanged(UiEventHub.CityStateChangedEvent _)
    {
        _dialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerStateChangedEvent _)
    {
        _dialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.OfficerAppointmentsChangedEvent _)
    {
        _dialogController.RefreshIfOpen();
    }

    private void OnWorldStateChanged(UiEventHub.FactionLeadershipChangedEvent _)
    {
        _dialogController.RefreshIfOpen();
    }

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        if (_dialogController.OverlayControl?.Visible == true)
        {
            overlays.Add(_dialogController.OverlayControl);
        }
    }
}
