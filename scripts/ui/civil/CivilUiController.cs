namespace ThreeKingdom.UI;

public sealed class CivilUiController
{
    private readonly CivilCommandDialogController _commandDialogController;
    private readonly CivilReliefDialogController _reliefDialogController;
    private readonly VisitCitizenDialogController _visitCitizenDialogController;

    public CivilUiController(HudController owner)
    {
        var context = new CivilUiContext(owner);
        _reliefDialogController = new CivilReliefDialogController(context);
        _visitCitizenDialogController = new VisitCitizenDialogController(context);
        _commandDialogController = new CivilCommandDialogController(context, _reliefDialogController.Show, _visitCitizenDialogController.Show);
    }

    public void Initialize()
    {
        _commandDialogController.Initialize();
        _reliefDialogController.Initialize();
        _visitCitizenDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _commandDialogController.Hide();
        _reliefDialogController.Hide();
        _visitCitizenDialogController.Hide();
    }

    public void RefreshText()
    {
        _commandDialogController.RefreshText();
        _reliefDialogController.RefreshText();
        _visitCitizenDialogController.RefreshText();
    }

    public void ShowCivilDialog() => _commandDialogController.Show();

    public void ShowVisitCitizenDialog() => _visitCitizenDialogController.Show();
}
