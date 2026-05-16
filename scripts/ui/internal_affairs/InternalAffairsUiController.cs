namespace ThreeKingdom.UI;

public sealed class InternalAffairsUiController
{
    private readonly InternalAffairsDialogController _dialogController;

    public InternalAffairsUiController(HudController owner)
    {
        _dialogController = new InternalAffairsDialogController(new InternalAffairsUiContext(owner));
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

    public void ShowInternalAffairsDialog() => _dialogController.Show();
}
