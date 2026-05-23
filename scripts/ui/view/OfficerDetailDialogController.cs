using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class OfficerDetailDialogController : FloatingOverlayController
{
    private readonly ViewUiContext _context;
    private int _shownOfficerId = -1;

    public OfficerDetailDialogController(ViewUiContext context)
        : base(context, "res://scenes/ui/view/OfficerDetailDialog.tscn")
    {
        _context = context;
    }

    protected override Vector2 MinimumOverlaySize => new(700.0f, 360.0f);

    public void Initialize()
    {
        InitializeOverlay();
        _context.OfficerDetailDialog = OverlayRoot;
    }

    public void Hide()
    {
        HideOverlay();
        _shownOfficerId = -1;
    }

    public void RefreshText()
    {
        _context.RefreshDialogsText();
    }

    public void ShowOfficer(OfficerData officer)
    {
        if (!EnsureOverlayReady())
        {
            return;
        }

        _shownOfficerId = officer.Id;
        SetOverlayTitleText(_context.GetOfficerDetailTitle());

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

        ShowOverlay();
    }

    public bool IsOpen() => OverlayRoot?.Visible == true;

    public void RefreshShownOfficer()
    {
        if (!IsOpen() || _shownOfficerId <= 0)
        {
            return;
        }

        var officer = _context.GetOfficerById(_shownOfficerId);
        if (officer == null)
        {
            Hide();
            return;
        }

        ShowOfficer(officer);
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _context.OfficerDetailDialog = OverlayRoot;
        _context.OfficerPortraitRect = root.GetNodeOrNull<TextureRect>("OfficerDetailRoot/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _context.OfficerPortraitPlaceholderLabel = root.GetNodeOrNull<Label>("OfficerDetailRoot/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _context.OfficerDetailText = root.GetNodeOrNull<RichTextLabel>("OfficerDetailRoot/DetailText");
    }
}
