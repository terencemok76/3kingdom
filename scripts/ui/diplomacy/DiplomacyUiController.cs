using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public sealed class DiplomacyUiController
{
    private readonly DiplomacyDialogController _dialogController;
    private readonly DiplomacyProposalDialogController _proposalDialogController;

    public DiplomacyUiController(
        HudController owner,
        System.Func<bool> hasPendingPlayerSuccession,
        System.Action showSuccessionDialog)
    {
        var context = new DiplomacyUiContext(owner);
        _dialogController = new DiplomacyDialogController(context);
        _proposalDialogController = new DiplomacyProposalDialogController(context, hasPendingPlayerSuccession, showSuccessionDialog);
    }

    public PendingCommandData? PendingProposalCommand
    {
        get => _proposalDialogController.PendingProposalCommand;
        set => _proposalDialogController.PendingProposalCommand = value;
    }

    public void Initialize()
    {
        _dialogController.Initialize();
        _proposalDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _dialogController.Hide();
        _proposalDialogController.Hide();
    }

    public void RefreshText()
    {
        _dialogController.RefreshText();
        _proposalDialogController.RefreshText();
    }

    public void ShowDiplomacyDialog() => _dialogController.Show();

    public void ShowProposalDialog(PendingCommandData pendingCommand) => _proposalDialogController.Show(pendingCommand);
}
