namespace ThreeKingdom.UI;

public sealed class AdvisorUiController
{
    private readonly AdvisorAssignmentDialogController _assignmentDialogController;

    public AdvisorUiController(HudController owner)
    {
        var context = new AdvisorUiContext(owner);
        _assignmentDialogController = new AdvisorAssignmentDialogController(context);
    }

    public void Initialize()
    {
        _assignmentDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _assignmentDialogController.Hide();
    }

    public void RefreshText()
    {
        _assignmentDialogController.RefreshText();
    }

    public void ShowAdvisorDialog()
    {
        _assignmentDialogController.Show();
    }
}
