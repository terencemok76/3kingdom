using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class FireOfficerDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(340.0f, 120.0f);

    public FireOfficerDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/FireOfficerDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        if (GetCandidateOfficerIds().Count == 0)
        {
            _context.AddLog(_context.Localization.Format("ui.no_available_officer_for_command", _context.Localization.T("command.personnel.fire_officer")));
            return;
        }

        RefreshText();
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.fire_officer"));
        var label = GetOverlayContentNode<Label>("OfficerListLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.officers");
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel");
        }
        if (_advisorButton != null) _advisorButton.Text = _context.Localization.T("ui.personnel_advice_dismiss");
        UpdateSelectedOfficerSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _advisorButton = root.GetNodeOrNull<Button>("ConfirmRow/AdvisorButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (_advisorButton != null) _context.ApplyCommandButtonTheme(_advisorButton);
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            if (_advisorButton != null) _advisorButton.Pressed += OnAdvisorPressed;
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var candidateOfficerIds = GetCandidateOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }
        UpdateSelectedOfficerSummary();
    }

    private List<int> GetCandidateOfficerIds()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        var availableOfficerIds = _context.GetAvailableOfficerIdsForOrder();
        return city.OfficerIds
            .Where(availableOfficerIds.Contains)
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && !_context.IsFactionRuler(world, officer);
            })
            .ToList();
    }

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var officer = world?.GetOfficer(_selectedOfficerId);
        if (city == null || world == null || localization == null) return;
        var advisor = _context.FindPersonnelAdvisor();
        var role = advisor?.Id == world.GetFaction(city.OwnerFactionId)?.ChancellorOfficerId ? localization.T("ui.chancellor") : localization.T("ui.local_place");
        var message = officer == null
            ? localization.T("ui.personnel_advice_select_officer")
            : BuildDismissalAdvice(world, city, officer, localization);
        _context.ShowAdvisorMessage(advisor, role, message);
    }

    private string BuildDismissalAdvice(WorldState world, CityData city, OfficerData officer, LocalizationService localization)
    {
        var appointments = GetDismissalAppointmentNames(world, city, officer);
        var replacement = FindDismissalReplacement(world, city, officer, appointments);
        var itemCount = world.Items.Count(item => item.EquippedOfficerId == officer.Id);
        var officerName = localization.GetOfficerName(officer);
        var appointmentText = appointments.Count > 0
            ? string.Join("、", appointments.Select(localization.GetAppointmentName))
            : localization.T("ui.none");
        var replacementName = replacement != null
            ? localization.GetOfficerName(replacement)
            : localization.T("ui.personnel_advice_no_appointment_candidate");

        var conclusion = appointments.Count == 0
            ? localization.T("ui.personnel_advice_dismiss_conclusion_safe")
            : replacement == null
                ? localization.T("ui.personnel_advice_dismiss_conclusion_hold")
                : localization.Format("fmt.personnel_advice_dismiss_conclusion_replace", replacementName);
        var expectedResult = localization.Format(
            "fmt.personnel_advice_dismiss_expected",
            officerName,
            appointmentText,
            itemCount);
        var risk = appointments.Count == 0
            ? localization.Format("fmt.personnel_advice_dismiss_risk_low", officer.Leadership, officer.Intelligence, officer.Politics)
            : replacement == null
                ? localization.Format("fmt.personnel_advice_dismiss_risk_high", appointmentText, officer.Leadership, officer.Intelligence, officer.Politics)
                : localization.Format("fmt.personnel_advice_dismiss_risk_replace", appointmentText, replacementName);
        var alternative = replacement == null
            ? localization.T("ui.personnel_advice_dismiss_alternative_none")
            : localization.Format(
                "fmt.personnel_advice_dismiss_alternative_replace",
                replacementName,
                replacement.Politics,
                replacement.Intelligence,
                replacement.Leadership);

        return localization.Format("fmt.personnel_advice_dismiss_actionable", conclusion, expectedResult, risk, alternative);
    }

    private List<string> GetDismissalAppointmentNames(WorldState world, CityData city, OfficerData officer)
    {
        var appointments = officer.Appointments
            .Where(appointment => !appointment.Equals(OfficerAppointmentRules.Lord, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction?.ChancellorOfficerId == officer.Id)
        {
            appointments.Add(OfficerAppointmentRules.Chancellor);
        }
        if (faction?.ChiefStrategistOfficerId == officer.Id)
        {
            appointments.Add(OfficerAppointmentRules.ChiefStrategist);
        }

        return appointments
            .Where(appointment => !string.IsNullOrWhiteSpace(appointment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private OfficerData? FindDismissalReplacement(WorldState world, CityData city, OfficerData dismissedOfficer, IReadOnlyCollection<string> appointments)
    {
        var availableOfficerIds = _context.GetAvailableOfficerIdsForOrder();
        return city.OfficerIds
            .Where(availableOfficerIds.Contains)
            .Select(world.GetOfficer)
            .Where(candidate => candidate != null && candidate.Id != dismissedOfficer.Id && !_context.IsFactionRuler(world, candidate))
            .OrderByDescending(candidate => GetDismissalReplacementScore(candidate!, appointments))
            .ThenByDescending(candidate => candidate!.Politics)
            .FirstOrDefault();
    }

    private static int GetDismissalReplacementScore(OfficerData officer, IReadOnlyCollection<string> appointments)
    {
        var score = officer.Politics * 2 + officer.Intelligence + officer.Leadership + officer.Charm;
        foreach (var appointment in appointments)
        {
            if (appointment.Equals(OfficerAppointmentRules.Governor, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, officer.Politics * 3 + officer.Intelligence * 2 + officer.Charm);
            }
            else if (appointment.Equals(OfficerAppointmentRules.Strategist, StringComparison.OrdinalIgnoreCase) ||
                     appointment.Equals(OfficerAppointmentRules.ChiefStrategist, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, officer.Intelligence * 3 + officer.Politics + officer.Leadership + officer.Combat);
            }
            else if (appointment.Equals(OfficerAppointmentRules.Chancellor, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, officer.Politics * 3 + officer.Intelligence * 2 + officer.Charm);
            }
        }

        return score;
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            ShowOverlay();
            return;
        }

        var result = commandResolver.ExecuteFireOfficer(turnManager.GetPlayerFactionId(), city.Id, _selectedOfficerId);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            _context.RefreshMapVisuals();
        }
        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetCandidateOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("command.personnel.fire_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            },
            titleFactory: () => _context.Localization?.T("command.personnel.fire_officer") ?? localization.T("command.personnel.fire_officer"),
            displayConfigFactory: BuildFireOfficerSelectorDisplayConfig);
    }

    private HudController.OfficerSelectorDisplayConfig BuildFireOfficerSelectorDisplayConfig()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            throw new InvalidOperationException("Dismiss officer selector requires localization.");
        }

        return new HudController.OfficerSelectorDisplayConfig
        {
            Columns =
            [
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.officers"), MinWidth = 118 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.role"), MinWidth = 72 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.status"), MinWidth = 82 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.age"), MinWidth = 52 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.loyalty_short"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.leadership"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.strength"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.intelligence"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.politics"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.charm"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.combat"), MinWidth = 58 }
            ],
            BuildRowTexts = BuildFireOfficerSelectorRowTexts,
            PanelSize = new Vector2(1050.0f, 360.0f)
        };
    }

    private IReadOnlyList<string> BuildFireOfficerSelectorRowTexts(OfficerData officer)
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var age = officer.BirthYear > 0 && world?.Year > 0
            ? Math.Max(0, world.Year - officer.BirthYear).ToString()
            : "-";
        var status = world != null && localization != null
            ? localization.GetOfficerStatus(world, officer)
            : string.Empty;

        return
        [
            localization?.GetOfficerName(officer) ?? officer.Name,
            localization?.GetOfficerRole(officer) ?? officer.Role,
            status,
            age,
            officer.Loyalty.ToString(),
            officer.Leadership.ToString(),
            officer.Strength.ToString(),
            officer.Intelligence.ToString(),
            officer.Politics.ToString(),
            officer.Charm.ToString(),
            officer.Combat.ToString()
        ];
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
