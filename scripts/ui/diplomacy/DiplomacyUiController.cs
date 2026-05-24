using ThreeKingdom.Core;
using ThreeKingdom.Data;
using System.Collections.Generic;
using Godot;

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

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        AddVisibleOverlay(overlays, _dialogController);
        AddVisibleOverlay(overlays, _proposalDialogController);
    }

    private static void AddVisibleOverlay(List<Control> overlays, FloatingOverlayController controller)
    {
        if (controller.OverlayControl?.Visible == true)
        {
            overlays.Add(controller.OverlayControl);
        }
    }
}
