using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public sealed class OfficerDetailDialogController
{
    private readonly ViewUiContext _context;

    public OfficerDetailDialogController(ViewUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        var dialog = GD.Load<PackedScene>("res://scenes/ui/view/OfficerDetailDialog.tscn").Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += OnCloseRequested;
        _context.AddChild(dialog);
        _context.OfficerDetailDialog = dialog;
        _context.EnsureOfficerDetailWidgets();
        dialog.Hide();
    }

    public void Hide()
    {
        _context.OfficerDetailDialog?.Hide();
    }

    public void RefreshText()
    {
        _context.RefreshDialogsText();
    }

    public void ShowOfficer(OfficerData officer)
    {
        var dialog = _context.OfficerDetailDialog;
        if (dialog == null)
        {
            return;
        }

        dialog.Title = _context.GetOfficerDetailTitle();

        if (_context.OfficerDetailText != null)
        {
            _context.OfficerDetailText.Text = _context.BuildOfficerDetailText(officer);
        }

        if (_context.OfficerPortraitRect != null)
        {
            _context.OfficerPortraitRect.Texture = _context.CanViewOfficerFullInformation(officer)
                ? _context.BuildOfficerPortraitTexture(officer.Id)
                : null;
        }

        if (_context.OfficerPortraitPlaceholderLabel != null)
        {
            var officerName = _context.CanViewOfficerFullInformation(officer)
                ? _context.GetOfficerDisplayName(officer)
                : _context.UnknownInfoText;
            var hasPortrait = _context.OfficerPortraitRect?.Texture != null;
            _context.OfficerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _context.OfficerPortraitPlaceholderLabel.Text = $"{_context.GetPortraitLabel()}\n{officerName}";
        }

        if (dialog.Visible)
        {
            dialog.Show();
        }
        else
        {
            dialog.PopupCentered(new Vector2I(700, 360));
        }
    }

    private void OnCloseRequested()
    {
        _context.PlayUiClickSfx();
        _context.OfficerDetailDialog?.Hide();
    }
}
