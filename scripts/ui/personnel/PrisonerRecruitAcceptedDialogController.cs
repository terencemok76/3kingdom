using System;
using System.Text;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PrisonerRecruitAcceptedDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _summaryLabel;
    private TextureRect? _portraitRect;
    private Label? _portraitPlaceholder;
    private RichTextLabel? _speechLabel;
    private Button? _confirmButton;
    private bool _signalsConnected;
    private Action? _closeAction;

    protected override Vector2 MinimumOverlaySize => new(840.0f, 420.0f);

    public PrisonerRecruitAcceptedDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PrisonerRecruitAcceptedDialog.tscn")
    {
        _context = context;
    }

    public void Initialize() => InitializeOverlay();

    public void Hide()
    {
        _closeAction = null;
        HideOverlay();
    }

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            UpdateOverlayLayoutNow();
        }
    }

    public void ShowDispositionResult(
        OfficerData officer,
        CityData city,
        CapturedOfficerDisposition disposition,
        CapturedOfficerRecruitOfferData? offer,
        Action onClosed)
    {
        var localization = _context.Localization;
        if (localization == null || !EnsureOverlayReady())
        {
            return;
        }

        _closeAction = onClosed;
        SetOverlayTitleText(localization.T(GetTitleKey(disposition)));

        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization.Format(
                GetSummaryKey(disposition),
                localization.GetOfficerName(officer),
                localization.GetCityName(city));
        }

        if (_speechLabel != null)
        {
            _speechLabel.Text = BuildDispositionSpeech(disposition, offer);
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

        if (_confirmButton != null)
        {
            _confirmButton.Text = localization.T("ui.confirm");
        }

        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _portraitRect = root.GetNodeOrNull<TextureRect>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _portraitPlaceholder = root.GetNodeOrNull<Label>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _speechLabel = root.GetNodeOrNull<RichTextLabel>("OfficerInfoRow/InfoColumn/SpeechLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");

        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }

        if (_signalsConnected)
        {
            return;
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += HandleClose;
        }

        _signalsConnected = true;
    }

    protected override void OnOverlayCloseRequested()
    {
        HandleClose();
    }

    private void HandleClose()
    {
        var closeAction = _closeAction;
        _closeAction = null;
        HideOverlay();
        closeAction?.Invoke();
    }

    private string BuildDispositionSpeech(CapturedOfficerDisposition disposition, CapturedOfficerRecruitOfferData? offer)
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return string.Empty;
        }

        var speechKey = disposition switch
        {
            CapturedOfficerDisposition.Recruit => GetRecruitSpeechKey(offer),
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_speech",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_speech",
            _ => "ui.captured_officer.recruit_accept_speech_default"
        };

        var bb = new StringBuilder();
        bb.Append("[color=#D8B56A]");
        bb.Append(EscapeBb(localization.T(GetSpeechLabelKey(disposition))));
        bb.Append("[/color]\n");
        bb.Append(EscapeBb(localization.T(speechKey)));
        return bb.ToString();
    }

    private static string GetRecruitSpeechKey(CapturedOfficerRecruitOfferData? offer)
    {
        if (!string.IsNullOrWhiteSpace(offer?.Appointment))
        {
            return "ui.captured_officer.recruit_accept_speech_appointment";
        }

        if ((offer?.ItemId ?? 0) > 0)
        {
            return "ui.captured_officer.recruit_accept_speech_item";
        }

        return (offer?.GoldAmount ?? 0) >= 500
            ? "ui.captured_officer.recruit_accept_speech_gold"
            : "ui.captured_officer.recruit_accept_speech_default";
    }

    private static string GetTitleKey(CapturedOfficerDisposition disposition)
    {
        return disposition switch
        {
            CapturedOfficerDisposition.Recruit => "ui.captured_officer.recruit_accepted_title",
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_title",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_title",
            _ => "ui.captured_officer.recruit_accepted_title"
        };
    }

    private static string GetSummaryKey(CapturedOfficerDisposition disposition)
    {
        return disposition switch
        {
            CapturedOfficerDisposition.Recruit => "ui.captured_officer.recruit_accepted_summary",
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_summary",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_summary",
            _ => "ui.captured_officer.recruit_accepted_summary"
        };
    }

    private static string GetSpeechLabelKey(CapturedOfficerDisposition disposition)
    {
        return disposition switch
        {
            CapturedOfficerDisposition.Recruit => "ui.captured_officer.recruit_accept_label",
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_label",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_label",
            _ => "ui.captured_officer.recruit_accept_label"
        };
    }

    private static string EscapeBb(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }
}
