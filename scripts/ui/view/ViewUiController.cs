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
        _uiEventHub = hud.PersonnelUiEventHub;
        _officerDetailDialogController = new OfficerDetailDialogController(_context);
        _officerListDialogController = new OfficerListDialogController(_context, _officerDetailDialogController);
    }

    public void Initialize()
    {
        _officerListDialogController.Initialize();
        _officerDetailDialogController.Initialize();
        _uiEventHub.OfficerAppointmentsChanged += OnOfficerAppointmentsChanged;
    }

    public void Shutdown()
    {
        _uiEventHub.OfficerAppointmentsChanged -= OnOfficerAppointmentsChanged;
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
        if (!_officerListDialogController.IsOpen())
        {
            return;
        }

        RefreshOfficerListChrome();
        RefreshOfficerListContent();
    }
}
