using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class CapturedOfficerDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private readonly CapturedOfficerSpeechDialogController _speechDialogController;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private TextureRect? _portraitRect;
    private Label? _portraitPlaceholder;
    private RichTextLabel? _detailLabel;
    private Button? _killButton;
    private Button? _recruitButton;
    private Button? _freeButton;
    private Button? _jailButton;
    private bool _signalsConnected;
    private int _pendingOfficerId = -1;

    protected override Vector2 MinimumOverlaySize => new(860.0f, 430.0f);

    public CapturedOfficerDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/CapturedOfficerDialog.tscn")
    {
        _context = context;
        _speechDialogController = new CapturedOfficerSpeechDialogController(context);
    }

    public void Initialize()
    {
        InitializeOverlay();
        _speechDialogController.Initialize();
    }

    public void Hide()
    {
        HideOverlay();
        _speechDialogController.Hide();
    }

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            Show();
        }

        _speechDialogController.RefreshText();
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

        if (_detailLabel != null)
        {
            _detailLabel.Text = _context.BuildOfficerDetailText(officer);
        }

        if (_portraitRect != null)
        {
            _portraitRect.Texture = _context.BuildOfficerPortraitTexture(officer.Id);
        }

        if (_portraitPlaceholder != null)
        {
            _portraitPlaceholder.Visible = _portraitRect?.Texture == null;
            _portraitPlaceholder.Text = $"{_context.GetPortraitLabel()}\n{localization.GetOfficerName(officer)}";
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
        _portraitRect = root.GetNodeOrNull<TextureRect>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _portraitPlaceholder = root.GetNodeOrNull<Label>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _detailLabel = root.GetNodeOrNull<RichTextLabel>("OfficerInfoRow/OfficerInfoColumn/DetailLabel");
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
            _killButton.Pressed += () => PromptDisposition(CapturedOfficerDisposition.Kill);
        }

        if (_recruitButton != null)
        {
            _recruitButton.Pressed += () => PromptDisposition(CapturedOfficerDisposition.Recruit);
        }

        if (_freeButton != null)
        {
            _freeButton.Pressed += () => PromptDisposition(CapturedOfficerDisposition.Free);
        }

        if (_jailButton != null)
        {
            _jailButton.Pressed += () => PromptDisposition(CapturedOfficerDisposition.Jail);
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

    private void PromptDisposition(CapturedOfficerDisposition disposition)
        => ResolveDisposition(disposition);

    private void ResolveDisposition(CapturedOfficerDisposition disposition)
    {
        var commandResolver = _context.CommandResolver;
        var world = _context.TurnManager?.World;
        var playerFactionId = _context.TurnManager?.GetPlayerFactionId() ?? -1;
        if (commandResolver == null || world == null || playerFactionId <= 0 || _pendingOfficerId <= 0)
        {
            return;
        }

        var pendingRecord = world.GetNextPendingCapturedOfficer(playerFactionId);
        var isTestOnly = pendingRecord?.IsTestOnly == true;
        var officer = pendingRecord != null ? world.GetOfficer(pendingRecord.OfficerId) : null;
        var city = pendingRecord != null ? world.GetCity(pendingRecord.WinnerCityId) : null;
        var result = commandResolver.ResolveCapturedOfficerDisposition(playerFactionId, _pendingOfficerId, disposition);
        if (!result.Success)
        {
            if (officer != null && city != null && disposition == CapturedOfficerDisposition.Recruit)
            {
                HideOverlay();
                _speechDialogController.ShowDispositionSpeech(
                    officer,
                    city,
                    disposition,
                    dispositionSucceeded: false,
                    () => Show());
                return;
            }

            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.GetLocalizedResultMessage(result);
            }

            return;
        }

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _pendingOfficerId = -1;

        if (officer != null && city != null)
        {
            HideOverlay();
            _speechDialogController.ShowDispositionSpeech(
                officer,
                city,
                disposition,
                dispositionSucceeded: true,
                () => AfterDispositionSpeechClosed(disposition, isTestOnly));
            return;
        }

        HideOverlay();
        AfterDispositionSpeechClosed(disposition, isTestOnly);
    }

    private void AfterDispositionSpeechClosed(CapturedOfficerDisposition disposition, bool isTestOnly)
    {
        if (isTestOnly)
        {
            return;
        }

        if (disposition == CapturedOfficerDisposition.Recruit)
        {
            Show();
            return;
        }

        if (_context.IsResolvingEndTurn())
        {
            if (HasPendingPlayerCapturedOfficer())
            {
                Show();
            }
            else
            {
                _context.ContinuePendingAttackResolution();
            }

            return;
        }

        if (HasPendingPlayerCapturedOfficer())
        {
            Show();
        }
    }
}
