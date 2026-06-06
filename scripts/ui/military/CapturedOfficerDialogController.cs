using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class CapturedOfficerDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private Button? _killButton;
    private Button? _recruitButton;
    private Button? _freeButton;
    private Button? _jailButton;
    private bool _signalsConnected;
    private int _pendingOfficerId = -1;
    protected override Vector2 MinimumOverlaySize => new(760.0f, 240.0f);

    public CapturedOfficerDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/CapturedOfficerDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            Show();
        }
    }

    public bool HasPendingPlayerCapturedOfficer()
    {
        var world = _context.TurnManager?.World;
        var playerFactionId = _context.TurnManager?.GetPlayerFactionId() ?? -1;
        return world != null && playerFactionId > 0 && world.GetNextPendingCapturedOfficer(playerFactionId) != null;
    }

    public void Show()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var playerFactionId = _context.TurnManager?.GetPlayerFactionId() ?? -1;
        if (world == null || localization == null || playerFactionId <= 0 || !EnsureOverlayReady())
        {
            return;
        }

        var pendingRecord = world.GetNextPendingCapturedOfficer(playerFactionId);
        var officer = pendingRecord != null ? world.GetOfficer(pendingRecord.OfficerId) : null;
        var city = pendingRecord != null ? world.GetCity(pendingRecord.WinnerCityId) : null;
        if (pendingRecord == null || officer == null || city == null)
        {
            return;
        }

        _pendingOfficerId = officer.Id;
        SetOverlayTitleText(localization.T("ui.captured_officer"));
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization.Format(
                "ui.captured_officer_summary",
                localization.GetOfficerName(officer),
                localization.GetCityName(city));
        }

        if (_warningLabel != null)
        {
            _warningLabel.Text = string.Empty;
        }

        if (_killButton != null)
        {
            _killButton.Text = localization.T("ui.captured_officer.kill");
        }

        if (_recruitButton != null)
        {
            _recruitButton.Text = localization.T("ui.captured_officer.recruit");
        }

        if (_freeButton != null)
        {
            _freeButton.Text = localization.T("ui.captured_officer.free");
        }

        if (_jailButton != null)
        {
            _jailButton.Text = localization.T("ui.captured_officer.jail");
        }

        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        _killButton = root.GetNodeOrNull<Button>("ActionRow/KillButton");
        _recruitButton = root.GetNodeOrNull<Button>("ActionRow/RecruitButton");
        _freeButton = root.GetNodeOrNull<Button>("ActionRow/FreeButton");
        _jailButton = root.GetNodeOrNull<Button>("ActionRow/JailButton");

        foreach (var button in new[] { _killButton, _recruitButton, _freeButton, _jailButton })
        {
            if (button != null)
            {
                _context.ApplyCommandButtonTheme(button);
            }
        }

        if (_signalsConnected)
        {
            return;
        }

        if (_killButton != null)
        {
            _killButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Kill);
        }

        if (_recruitButton != null)
        {
            _recruitButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Recruit);
        }

        if (_freeButton != null)
        {
            _freeButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Free);
        }

        if (_jailButton != null)
        {
            _jailButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Jail);
        }

        _signalsConnected = true;
    }

    protected override void OnOverlayCloseRequested()
    {
        if (HasPendingPlayerCapturedOfficer())
        {
            Show();
            return;
        }

        HideOverlay();
    }

    private void ResolveDisposition(CapturedOfficerDisposition disposition)
    {
        var commandResolver = _context.CommandResolver;
        var world = _context.TurnManager?.World;
        var playerFactionId = _context.TurnManager?.GetPlayerFactionId() ?? -1;
        if (commandResolver == null || world == null || playerFactionId <= 0 || _pendingOfficerId <= 0)
        {
            return;
        }

        var result = commandResolver.ResolveCapturedOfficerDisposition(playerFactionId, _pendingOfficerId, disposition);
        if (!result.Success)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.GetLocalizedResultMessage(result);
            }

            ShowOverlay();
            return;
        }

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        HideOverlay();
        _pendingOfficerId = -1;
        _context.ContinuePendingAttackResolution();
    }
}
