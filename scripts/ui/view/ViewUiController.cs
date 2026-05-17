namespace ThreeKingdom.UI;

public sealed class ViewUiController
{
    private readonly ViewUiContext _context;
    private readonly OfficerListDialogController _officerListDialogController;
    private readonly OfficerDetailDialogController _officerDetailDialogController;

    public ViewUiController(HudController hud)
    {
        _context = new ViewUiContext(hud);
        _officerDetailDialogController = new OfficerDetailDialogController(_context);
        _officerListDialogController = new OfficerListDialogController(_context, _officerDetailDialogController);
    }

    public void Initialize()
    {
        _officerListDialogController.Initialize();
        _officerDetailDialogController.Initialize();
    }

    public void Shutdown()
    {
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
}
