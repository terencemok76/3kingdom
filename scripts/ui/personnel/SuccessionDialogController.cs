using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class SuccessionDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _summaryLabel;
    private Label? _officerListLabel;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _clearOfficerButton;
    private Label? _warningLabel;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private int _pendingFactionId = -1;
    private bool _isVoluntaryRulerChange;
    private List<int> _rulerChangeCandidateOfficerIds = new();
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(760.0f, 240.0f);

    public SuccessionDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/SuccessionDialog.tscn")
    {
        _context = context;
    }

    public int PendingFactionId
    {
        get => _pendingFactionId;
        set => _pendingFactionId = value;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void ShowRulerChange()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var factionId = _context.TurnManager?.GetPlayerFactionId() ?? 0;
        var faction = world?.GetFaction(factionId);
        if (world == null || localization == null || faction == null || !faction.IsPlayer || faction.RulerOfficerId <= 0 || !EnsureOverlayReady())
        {
            return;
        }

        _rulerChangeCandidateOfficerIds = faction.OfficerIds
            .Where(officerId => officerId != faction.RulerOfficerId)
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null &&
                       officer.CityId > 0 &&
                       officer.CaptiveFactionId <= 0 &&
                       (officer.DeathYear <= 0 || world.Year <= officer.DeathYear) &&
                       !BattleCampaignService.IsOfficerCommitted(world, officer.Id);
            })
            .ToList();
        if (_rulerChangeCandidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("cmd.ruler_change.invalid_successor"), isPlayerRelated: true);
            return;
        }

        _isVoluntaryRulerChange = true;
        _pendingFactionId = factionId;
        SetOverlayTitleText(localization.T("ui.change_ruler"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = localization.T("ui.confirm_change_ruler");
        }
        if (_summaryLabel != null)
        {
            var currentRuler = world.GetOfficer(faction.RulerOfficerId);
            _summaryLabel.Text = localization.Format(
                "ui.change_ruler_summary",
                currentRuler == null ? localization.T("ui.unknown") : localization.GetOfficerName(currentRuler));
        }
        if (_warningLabel != null)
        {
            _warningLabel.Text = string.Empty;
        }
        if (_officerListLabel != null)
        {
            _officerListLabel.Text = localization.T("ui.succession_successor");
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = localization.T("ui.select_officer");
        }
        RefreshActionButtonText(localization);

        if (!_rulerChangeCandidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = _rulerChangeCandidateOfficerIds[0];
        }
        UpdateSelectedOfficerSummary();
        ShowOverlay();
    }

    public bool HasPendingPlayerSuccession()
    {
        var world = _context.TurnManager?.World;
        if (world == null)
        {
            return false;
        }

        var playerFactionId = _context.TurnManager!.GetPlayerFactionId();
        return playerFactionId > 0 && world.GetPendingSuccession(playerFactionId) != null;
    }

    public void Show()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null || !EnsureOverlayReady())
        {
            return;
        }

        var factionId = _context.TurnManager!.GetPlayerFactionId();
        var pendingSuccession = world.GetPendingSuccession(factionId);
        var faction = world.GetFaction(factionId);
        if (pendingSuccession == null || faction == null)
        {
            return;
        }

        _isVoluntaryRulerChange = false;
        _rulerChangeCandidateOfficerIds.Clear();
        _pendingFactionId = factionId;
        SetOverlayTitleText(localization.T("ui.succession"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = localization.T("ui.confirm_succession");
        }
        if (_summaryLabel != null)
        {
            var previousRuler = pendingSuccession.PreviousRulerOfficerId > 0
                ? world.GetOfficer(pendingSuccession.PreviousRulerOfficerId)
                : null;
            _summaryLabel.Text = previousRuler == null
                ? localization.Format("ui.succession_summary", localization.GetFactionName(world, factionId))
                : localization.Format(
                    pendingSuccession.TriggeredByCapture
                        ? "ui.succession_ruler_captured_summary"
                        : "ui.succession_ruler_died_summary",
                    localization.GetOfficerName(previousRuler),
                    localization.GetFactionName(world, factionId));
        }
        if (_warningLabel != null)
        {
            _warningLabel.Text = string.Empty;
        }
        if (_officerListLabel != null)
        {
            _officerListLabel.Text = localization.T("ui.succession_successor");
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = localization.T("ui.select_officer");
        }
        RefreshActionButtonText(localization);

        var candidateOfficer = pendingSuccession.CandidateOfficerIds.Contains(_selectedOfficerId)
            ? world.GetOfficer(_selectedOfficerId)
            : null;
        if (candidateOfficer == null)
        {
            _selectedOfficerId = pendingSuccession.CandidateOfficerIds.FirstOrDefault();
        }

        UpdateSelectedOfficerSummary();
        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _officerListLabel = root.GetNodeOrNull<Label>("OfficerListLabel");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _clearOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/ClearOfficerButton");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        _advisorButton = root.GetNodeOrNull<Button>("ConfirmRow/AdvisorButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_clearOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_clearOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (_advisorButton != null)
        {
            _context.ApplyCommandButtonTheme(_advisorButton);
        }
        if (_context.Localization != null) RefreshActionButtonText(_context.Localization);
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_clearOfficerButton != null) _clearOfficerButton.Pressed += OnClearOfficerPressed;
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            if (_advisorButton != null) _advisorButton.Pressed += OnAdvisorPressed;
            _signalsConnected = true;
        }
    }

    private void OnAdvisorPressed()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null) return;
        var advisor = _context.FindPersonnelAdvisor();
        var role = advisor?.Id == world.GetFaction(_pendingFactionId)?.ChancellorOfficerId ? localization.T("ui.chancellor") : localization.T("ui.local_place");
        var candidateIds = _isVoluntaryRulerChange
            ? _rulerChangeCandidateOfficerIds
            : world.GetPendingSuccession(_pendingFactionId)?.CandidateOfficerIds ?? new List<int>();
        var candidates = candidateIds.Select(world.GetOfficer).Where(candidate => candidate != null).Cast<OfficerData>().ToList();
        var candidate = world.GetOfficer(_selectedOfficerId);
        var currentRuler = world.GetOfficer(world.GetFaction(_pendingFactionId)?.RulerOfficerId ?? 0);
        var currentYear = world.Year;
        var recommended = candidates.OrderByDescending(candidate => GetRulerFit(candidate, currentRuler, currentYear)).FirstOrDefault();
        var message = recommended == null
            ? localization.T("ui.personnel_advice_select_officer")
            : candidate == null
                ? localization.Format("fmt.personnel_advice_succession_recommend", localization.GetOfficerName(recommended), GetRulerFit(recommended, currentRuler, currentYear), BuildRulerStats(recommended, currentRuler, currentYear, localization))
                : localization.Format("fmt.personnel_advice_succession_compare", localization.GetOfficerName(candidate), GetRulerFit(candidate, currentRuler, currentYear), BuildRulerStats(candidate, currentRuler, currentYear, localization), currentRuler == null ? localization.T("ui.unknown") : localization.GetOfficerName(currentRuler), currentRuler == null ? 0 : GetRulerFit(currentRuler, null, currentYear), candidate.Id == recommended.Id ? localization.T("ui.personnel_advice_good_choice") : localization.Format("ui.personnel_advice_ruler_better_candidate", localization.GetOfficerName(recommended), GetRulerFit(recommended, currentRuler, currentYear)));
        _context.ShowAdvisorMessage(advisor, role, message);
    }

    private void OnClearOfficerPressed()
    {
        _selectedOfficerId = -1;
        if (_warningLabel != null) _warningLabel.Text = string.Empty;
        UpdateSelectedOfficerSummary();
    }

    private void RefreshActionButtonText(LocalizationService localization)
    {
        if (_clearOfficerButton != null) _clearOfficerButton.Text = localization.T("ui.clear_selection");
        if (_advisorButton != null) _advisorButton.Text = localization.T("ui.personnel_advice_succession");
    }

    private static int GetRulerFit(OfficerData officer, OfficerData? currentRuler, int currentYear) =>
        officer.Leadership + officer.Strength + officer.Intelligence + officer.Politics + officer.Charm + officer.Combat - officer.Ambition / 2 +
        GetAgeBonus(officer, currentYear) + GetRelationshipBonus(officer, currentRuler);

    private static string BuildRulerStats(OfficerData officer, OfficerData? currentRuler, int currentYear, LocalizationService localization) =>
        localization.Format("fmt.personnel_advice_ruler_stats", officer.Leadership, officer.Strength, officer.Intelligence, officer.Politics, officer.Charm, officer.Combat, officer.Ambition, FormatAdviceAge(officer, currentYear), GetPreviousRulerRelationshipText(officer, currentRuler, localization));

    private static int GetAgeBonus(OfficerData officer, int currentYear)
    {
        var age = officer.BirthYear > 0 && currentYear > 0 ? Math.Max(0, currentYear - officer.BirthYear) : 45;
        return age switch { >= 30 and <= 60 => 12, >= 20 and <= 70 => 5, >= 71 and <= 79 => -8, _ => -15 };
    }

    private static int GetRelationshipBonus(OfficerData officer, OfficerData? currentRuler)
    {
        if (currentRuler == null) return 0;
        var relationship = FindRelationshipType(officer, currentRuler) ?? FindRelationshipType(currentRuler, officer);
        return relationship?.Trim().ToLowerInvariant() switch { "family,blood" => 28, "family,non-blood" => 14, "family" => 12, _ => 0 };
    }

    private static string FormatAdviceAge(OfficerData officer, int currentYear) =>
        officer.BirthYear > 0 && currentYear > 0 ? Math.Max(0, currentYear - officer.BirthYear).ToString() : "-";

    private void OnConfirmPressed()
    {
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (commandResolver == null || localization == null || _pendingFactionId <= 0)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = localization.T("ui.select_officer_warning");
            }
            ShowOverlay();
            return;
        }

        var factionId = _pendingFactionId;
        var currentWorld = _context.TurnManager?.World;
        var currentFaction = currentWorld?.GetFaction(factionId);
        var wasVoluntaryRulerChange = _isVoluntaryRulerChange;
        var pendingSuccession = wasVoluntaryRulerChange
            ? null
            : currentWorld?.GetPendingSuccession(factionId);
        var previousRulerOfficerId = wasVoluntaryRulerChange
            ? currentFaction?.RulerOfficerId ?? 0
            : pendingSuccession?.PreviousRulerOfficerId ?? 0;
        var previousRuler = currentWorld?.GetOfficer(previousRulerOfficerId);
        var result = wasVoluntaryRulerChange
            ? commandResolver.ResolvePlayerRulerChange(factionId, _selectedOfficerId)
            : commandResolver.ResolvePlayerSuccession(factionId, _selectedOfficerId);
        if (!result.Success)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.GetLocalizedResultMessage(result);
            }
            ShowOverlay();
            return;
        }

        var successor = _context.TurnManager?.World?.GetOfficer(_selectedOfficerId);
        var cityId = successor?.CityId ?? _context.SelectedCity?.Id ?? 0;
        _pendingFactionId = -1;
        _isVoluntaryRulerChange = false;
        _rulerChangeCandidateOfficerIds.Clear();
        HideOverlay();
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        var previousRulerName = previousRuler == null
            ? localization.T("ui.unknown")
            : localization.GetOfficerName(previousRuler);
        var successorName = successor == null
            ? localization.T("ui.unknown")
            : localization.GetOfficerName(successor);
        var reasonMessage = wasVoluntaryRulerChange
            ? localization.Format("ui.ruler_changed_manual_reason", previousRulerName)
            : localization.Format(
                pendingSuccession?.TriggeredByCapture == true
                    ? "ui.ruler_changed_captured_reason"
                    : "ui.ruler_changed_death_reason",
                previousRulerName);
        _context.QueueFactionOutcome(
            localization.T("ui.ruler_changed_title"),
            $"{reasonMessage}\n\n{localization.Format("ui.ruler_changed_message", previousRulerName, successorName)}");
        _context.UiEventHub.PublishFactionLeadershipChanged(factionId, cityId);
        if (cityId > 0)
        {
            _context.UiEventHub.PublishCityStateChanged(cityId, factionId);
        }
        _context.ContinuePendingNonAttackResolution();
    }

    protected override void OnOverlayCloseRequested()
    {
        if (!_isVoluntaryRulerChange && _pendingFactionId > 0)
        {
            Show();
            return;
        }

        _pendingFactionId = -1;
        _isVoluntaryRulerChange = false;
        _rulerChangeCandidateOfficerIds.Clear();
        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null || _pendingFactionId <= 0)
        {
            return;
        }

        var pendingSuccession = world.GetPendingSuccession(_pendingFactionId);
        var candidateOfficerIds = _isVoluntaryRulerChange
            ? _rulerChangeCandidateOfficerIds.ToList()
            : pendingSuccession?.CandidateOfficerIds.ToList() ?? new List<int>();
        var previousRulerOfficerId = _isVoluntaryRulerChange
            ? world.GetFaction(_pendingFactionId)?.RulerOfficerId ?? 0
            : pendingSuccession?.PreviousRulerOfficerId ?? 0;
        var previousRuler = previousRulerOfficerId > 0
            ? world.GetOfficer(previousRulerOfficerId)
            : null;
        if (candidateOfficerIds.Count == 0)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = localization.T("ui.select_officer_warning");
            }
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.succession"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                if (_warningLabel != null)
                {
                    _warningLabel.Text = string.Empty;
                }
            },
            titleFactory: () => _context.Localization?.T("ui.succession") ?? localization.T("ui.succession"),
            displayConfigFactory: () => BuildSuccessionOfficerSelectorDisplayConfig(previousRuler));
    }

    private HudController.OfficerSelectorDisplayConfig BuildSuccessionOfficerSelectorDisplayConfig(OfficerData? previousRuler)
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            throw new InvalidOperationException("Succession officer selector requires localization.");
        }

        return new HudController.OfficerSelectorDisplayConfig
        {
            Columns =
            [
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.officers"), MinWidth = 118 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.succession_previous_ruler_relation"), MinWidth = 112 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.age"), MinWidth = 52 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.leadership"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.strength"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.intelligence"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.politics"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.charm"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.loyalty_short"), MinWidth = 58 }
            ],
            BuildRowTexts = officer =>
            [
                localization.GetOfficerName(officer),
                GetPreviousRulerRelationshipText(officer, previousRuler, localization),
                FormatOfficerAge(officer),
                officer.Leadership.ToString(),
                officer.Strength.ToString(),
                officer.Intelligence.ToString(),
                officer.Politics.ToString(),
                officer.Charm.ToString(),
                officer.Loyalty.ToString()
            ],
            PanelSize = new Vector2(940.0f, 360.0f)
        };
    }

    private string FormatOfficerAge(OfficerData officer)
    {
        var currentYear = _context.TurnManager?.World?.Year ?? 0;
        return officer.BirthYear > 0 && currentYear > 0
            ? Math.Max(0, currentYear - officer.BirthYear).ToString()
            : "-";
    }

    private static string GetPreviousRulerRelationshipText(
        OfficerData candidate,
        OfficerData? previousRuler,
        LocalizationService localization)
    {
        if (previousRuler == null)
        {
            return localization.T("ui.succession_relation_none");
        }

        var relationshipType = FindRelationshipType(candidate, previousRuler) ??
                               FindRelationshipType(previousRuler, candidate);
        return relationshipType?.Trim().ToLowerInvariant() switch
        {
            "family,blood" => localization.T("ui.succession_relation_blood"),
            "family,non-blood" => localization.T("ui.succession_relation_non_blood"),
            "family" => localization.T("ui.succession_relation_family"),
            null or "" => localization.T("ui.succession_relation_none"),
            _ => relationshipType
        };
    }

    private static string? FindRelationshipType(OfficerData source, OfficerData target)
    {
        return source.RelationshipType
            .FirstOrDefault(relationship =>
                relationship.Key.Equals(target.Name, StringComparison.OrdinalIgnoreCase) ||
                relationship.Key.Equals(target.NameZhHant, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.officers")}: {officerName}";
    }
}
