namespace ThreeKingdom.UI;

public sealed class SystemUiController
{
    private readonly SystemEntryButtonController _entryButtonController;
    private readonly OptionDialogController _optionDialogController;
    private readonly SaveLoadConfirmDialogController _saveLoadConfirmDialogController;
    private readonly SaveLoadDialogController _saveLoadDialogController;

    public SystemUiController(HudController owner)
    {
        var context = new SystemUiContext(owner);
        _entryButtonController = new SystemEntryButtonController(context);
        _saveLoadConfirmDialogController = new SaveLoadConfirmDialogController(context);
        _saveLoadDialogController = new SaveLoadDialogController(context, _saveLoadConfirmDialogController);
        _optionDialogController = new OptionDialogController(context, _saveLoadDialogController.Show);
    }

    public void Initialize()
    {
        _entryButtonController.Initialize(ShowOptionDialog);
        _optionDialogController.Initialize();
        _saveLoadDialogController.Initialize();
        _saveLoadConfirmDialogController.Initialize();
    }

    public void Shutdown()
    {
        _entryButtonController.Shutdown(ShowOptionDialog);
    }

    public void HideDialogs()
    {
        _optionDialogController.Hide();
        _saveLoadDialogController.Hide();
        _saveLoadConfirmDialogController.Hide();
    }

    public void RefreshText()
    {
        _entryButtonController.RefreshText();
        _optionDialogController.RefreshText();
        _saveLoadDialogController.RefreshText();
        _saveLoadConfirmDialogController.RefreshText();
    }

    public void ShowOptionDialog() => _optionDialogController.Show();

    public void CancelPendingConfirmation() => _saveLoadConfirmDialogController.Cancel();
}
