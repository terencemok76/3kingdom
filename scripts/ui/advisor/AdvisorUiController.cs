namespace ThreeKingdom.UI;

public sealed class AdvisorUiController
{
    private readonly AdvisorDialogController _dialogController;

    public AdvisorUiController(HudController owner)
    {
        var context = new AdvisorUiContext(owner);
        _dialogController = new AdvisorDialogController(context);
    }

    public void Initialize()
    {
        _dialogController.Initialize();
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
}
