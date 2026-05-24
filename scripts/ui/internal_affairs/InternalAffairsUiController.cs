using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.UI;

public sealed class InternalAffairsUiController
{
    private readonly InternalAffairsUiContext _context;
    private readonly UiEventHub _uiEventHub;
    private readonly InternalAffairsDialogController _dialogController;

    public InternalAffairsUiController(HudController owner)
    {
        _context = new InternalAffairsUiContext(owner);
        _uiEventHub = _context.UiEventHub;
        _dialogController = new InternalAffairsDialogController(_context);
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

    public void ShowInternalAffairsDialog() => _dialogController.Show();

    public void RefreshIfOpen() => _dialogController.RefreshIfOpen();

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
