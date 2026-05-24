using System.Collections.Generic;
using Godot;

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

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        AddVisibleOverlay(overlays, _commandDialogController);
        AddVisibleOverlay(overlays, _reliefDialogController);
        AddVisibleOverlay(overlays, _visitCitizenDialogController);
    }

    private static void AddVisibleOverlay(List<Control> overlays, FloatingOverlayController controller)
    {
        if (controller.OverlayControl?.Visible == true)
        {
            overlays.Add(controller.OverlayControl);
        }
    }
}
