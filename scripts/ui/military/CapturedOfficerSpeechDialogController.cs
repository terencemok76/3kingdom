using System;
using System.Text;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class CapturedOfficerSpeechDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private Label? _summaryLabel;
    private TextureRect? _portraitRect;
    private Label? _portraitPlaceholder;
    private RichTextLabel? _speechLabel;
    private Button? _closeButton;
    private bool _signalsConnected;
    private Action? _closeAction;

    protected override Vector2 MinimumOverlaySize => new(820.0f, 400.0f);

    public CapturedOfficerSpeechDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/CapturedOfficerSpeechDialog.tscn")
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

    public void ShowDispositionSpeech(
        OfficerData officer,
        CityData city,
        CapturedOfficerDisposition disposition,
        bool dispositionSucceeded,
        Action onClosed)
    {
        var localization = _context.Localization;
        if (localization == null || !EnsureOverlayReady())
        {
            return;
        }

        _closeAction = onClosed;
        SetOverlayTitleText(localization.T("ui.captured_officer"));

        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization.Format(
                GetSummaryKey(disposition, dispositionSucceeded),
                localization.GetOfficerName(officer),
                localization.GetCityName(city));
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

        if (_speechLabel != null)
        {
            _speechLabel.Text = BuildDispositionSpeech(city.OwnerFactionId, officer, disposition, dispositionSucceeded);
        }

        if (_closeButton != null)
        {
            _closeButton.Text = localization.T("ui.confirm");
        }

        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _portraitRect = root.GetNodeOrNull<TextureRect>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _portraitPlaceholder = root.GetNodeOrNull<Label>("OfficerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _speechLabel = root.GetNodeOrNull<RichTextLabel>("OfficerInfoRow/InfoColumn/SpeechLabel");
        _closeButton = root.GetNodeOrNull<Button>("ActionRow/CloseButtonAction");

        if (_closeButton != null)
        {
            _context.ApplyCommandButtonTheme(_closeButton);
        }

        if (_signalsConnected)
        {
            return;
        }

        if (_closeButton != null)
        {
            _closeButton.Pressed += HandleClose;
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

    private string BuildDispositionSpeech(int worldCityOwnerId, OfficerData officer, CapturedOfficerDisposition disposition, bool dispositionSucceeded)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return string.Empty;
        }

        var bb = new StringBuilder();
        bb.Append("[color=#D8B56A]");
        bb.Append(EscapeBb(localization.T(GetSpeechLabelKey(disposition))));
        bb.Append("[/color]\n");
        bb.Append(EscapeBb(localization.T(GetSpeechKey(world, officer, worldCityOwnerId, disposition, dispositionSucceeded))));
        return bb.ToString();
    }

    private static string GetSummaryKey(CapturedOfficerDisposition disposition, bool dispositionSucceeded)
    {
        if (disposition == CapturedOfficerDisposition.Recruit && !dispositionSucceeded)
        {
            return "ui.captured_officer.recruit_failed_summary";
        }

        return disposition switch
        {
            CapturedOfficerDisposition.Recruit => "ui.captured_officer.recruit_prompt_summary",
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_prompt_summary",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_prompt_summary",
            CapturedOfficerDisposition.Jail => "ui.captured_officer.jail_prompt_summary",
            _ => "ui.captured_officer_summary"
        };
    }

    private static string GetSpeechLabelKey(CapturedOfficerDisposition disposition)
    {
        return disposition switch
        {
            CapturedOfficerDisposition.Recruit => "ui.captured_officer.speech_label",
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_label",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_label",
            CapturedOfficerDisposition.Jail => "ui.captured_officer.jail_label",
            _ => "ui.captured_officer.speech_label"
        };
    }

    private static string GetSpeechKey(WorldState world, OfficerData officer, int captorFactionId, CapturedOfficerDisposition disposition, bool dispositionSucceeded)
    {
        if (disposition == CapturedOfficerDisposition.Recruit)
        {
            return dispositionSucceeded
                ? "ui.captured_officer.speech_accept_after_hire"
                : GetRecruitSpeechKey(world, officer, captorFactionId);
        }

        return disposition switch
        {
            CapturedOfficerDisposition.Free => "ui.captured_officer.free_speech",
            CapturedOfficerDisposition.Kill => "ui.captured_officer.kill_speech",
            CapturedOfficerDisposition.Jail => "ui.captured_officer.jail_speech",
            _ => "ui.captured_officer.speech_wait_and_see"
        };
    }

    private static string GetRecruitSpeechKey(WorldState world, OfficerData officer, int captorFactionId)
    {
        var relationshipBonus = GetCaptorRelationshipBonus(world, officer, captorFactionId);
        if (relationshipBonus >= 0.18 || officer.Loyalty <= 55)
        {
            return "ui.captured_officer.speech_accept";
        }

        if (officer.Loyalty >= 92 && officer.Ambition <= 55)
        {
            return "ui.captured_officer.speech_kill_me";
        }

        if (officer.Loyalty >= 85)
        {
            return "ui.captured_officer.speech_defiant";
        }

        if (officer.Ambition >= 82)
        {
            return "ui.captured_officer.speech_offer_more";
        }

        return "ui.captured_officer.speech_wait_and_see";
    }

    private static double GetCaptorRelationshipBonus(WorldState world, OfficerData captiveOfficer, int captorFactionId)
    {
        if (captorFactionId <= 0 || captiveOfficer.RelationshipType == null || captiveOfficer.RelationshipType.Count == 0)
        {
            return 0.0;
        }

        var faction = world.GetFaction(captorFactionId);
        if (faction == null)
        {
            return 0.0;
        }

        var bestBonus = 0.0;
        var ruler = world.GetOfficer(faction.RulerOfficerId);
        if (HasRelationshipWith(captiveOfficer, ruler))
        {
            bestBonus = Math.Max(bestBonus, 0.35);
        }

        foreach (var officerId in faction.OfficerIds)
        {
            var factionOfficer = world.GetOfficer(officerId);
            if (factionOfficer == null || factionOfficer.Id == ruler?.Id)
            {
                continue;
            }

            if (HasRelationshipWith(captiveOfficer, factionOfficer))
            {
                bestBonus = Math.Max(bestBonus, 0.18);
            }
        }

        return bestBonus;
    }

    private static bool HasRelationshipWith(OfficerData sourceOfficer, OfficerData? targetOfficer)
    {
        if (targetOfficer == null || sourceOfficer.RelationshipType == null || sourceOfficer.RelationshipType.Count == 0)
        {
            return false;
        }

        foreach (var relatedName in sourceOfficer.RelationshipType.Keys)
        {
            if (string.IsNullOrWhiteSpace(relatedName))
            {
                continue;
            }

            if ((!string.IsNullOrWhiteSpace(targetOfficer.NameZhHant) &&
                 relatedName.Equals(targetOfficer.NameZhHant, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(targetOfficer.Name) &&
                 relatedName.Equals(targetOfficer.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeBb(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }
}
