namespace ThreeKingdom.UI;

public sealed class MilitaryUiController
{
    private readonly MilitaryCommandDialogController _commandDialogController;
    private readonly RecruitTroopDialogController _recruitTroopDialogController;

    public MilitaryUiController(HudController owner)
    {
        var context = new MilitaryUiContext(owner);
        _recruitTroopDialogController = new RecruitTroopDialogController(context);
        _commandDialogController = new MilitaryCommandDialogController(
            context,
            _recruitTroopDialogController.Show);
    }

    public void Initialize()
    {
        _commandDialogController.Initialize();
        _recruitTroopDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _commandDialogController.Hide();
        _recruitTroopDialogController.Hide();
    }

    public void RefreshText()
    {
        _commandDialogController.RefreshText();
        _recruitTroopDialogController.RefreshText();
    }

    public void ShowMilitaryDialog() => _commandDialogController.Show();
}
