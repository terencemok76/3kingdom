namespace ThreeKingdom.UI;

public sealed class MerchantUiController
{
    private readonly MerchantDialogController _dialogController;

    public MerchantUiController(HudController owner)
    {
        var context = new MerchantUiContext(owner);
        _dialogController = new MerchantDialogController(context);
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

    public void ShowMerchantDialog()
    {
        _dialogController.Show();
    }
}
