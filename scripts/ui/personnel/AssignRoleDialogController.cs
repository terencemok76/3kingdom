using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AssignRoleDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _clearOfficerButton;
    private Label? _currentAppointmentsValueLabel;
    private OptionButton? _assignRoleOption;
    private OptionButton? _removeRoleOption;
    private Label? _summaryLabel;
    private Button? _assignButton;
    private Button? _removeButton;
    private string _lastResultMessage = string.Empty;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 360.0f);

    public AssignRoleDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/AssignRoleDialog.tscn")
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

        _lastResultMessage = string.Empty;
        Populate();
        RefreshText();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.assign_title"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.assign_role_officer"));
        SetLabelText("CurrentAppointmentsLabel", _context.Localization.T("ui.appointed_titles"));
        SetLabelText("AssignRoleLabel", _context.Localization.T("ui.assign_appointment"));
        SetLabelText("RemoveRoleLabel", _context.Localization.T("ui.clear_appointment"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_clearOfficerButton != null)
        {
            _clearOfficerButton.Text = _context.Localization.T("ui.clear_selection");
        }
        if (_assignButton != null)
        {
            _assignButton.Text = _context.Localization.T("ui.confirm_assign_role");
        }
        if (_removeButton != null)
        {
            _removeButton.Text = _context.Localization.T("ui.confirm_clear_appointment");
        }
        RefreshRoleOptionTexts();
        UpdateSelectedOfficerSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _clearOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/ClearOfficerButton");
        _currentAppointmentsValueLabel = root.GetNodeOrNull<Label>("CurrentAppointmentsValueLabel");
        _assignRoleOption = root.GetNodeOrNull<OptionButton>("AssignRoleOption");
        _removeRoleOption = root.GetNodeOrNull<OptionButton>("RemoveRoleOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _assignButton = root.GetNodeOrNull<Button>("ConfirmRow/AssignButton");
        _removeButton = root.GetNodeOrNull<Button>("ConfirmRow/RemoveButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_clearOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_clearOfficerButton);
        }
        if (_assignButton != null)
        {
            _context.ApplyCommandButtonTheme(_assignButton);
        }
        if (_removeButton != null)
        {
            _context.ApplyCommandButtonTheme(_removeButton);
        }
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_clearOfficerButton != null)
            {
                _clearOfficerButton.Pressed += OnClearOfficerPressed;
            }
            if (_assignRoleOption != null)
            {
                _assignRoleOption.ItemSelected += _ => UpdateSelectedOfficerSummary();
            }
            if (_removeRoleOption != null)
            {
                _removeRoleOption.ItemSelected += _ => UpdateSelectedOfficerSummary();
            }
            if (_assignButton != null)
            {
                _assignButton.Pressed += OnAssignPressed;
            }
            if (_removeButton != null)
            {
                _removeButton.Pressed += OnRemovePressed;
            }
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        _selectedOfficerId = -1;
        PopulateAssignRoleOptions();
        PopulateRemoveRoleOptions();
        UpdateSelectedOfficerSummary();
    }

    private void PopulateAssignRoleOptions()
    {
        if (_assignRoleOption == null || _context.Localization == null)
        {
            return;
        }

        _assignRoleOption.Clear();
        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        foreach (var appointment in GetAssignableAppointments(officer))
        {
            AddRoleOption(_assignRoleOption, appointment);
        }

        if (_assignRoleOption.ItemCount == 0)
        {
            _assignRoleOption.AddItem(_context.Localization.T("ui.none"));
            _assignRoleOption.SetItemMetadata(0, string.Empty);
        }

        if (_assignRoleOption.ItemCount > 0)
        {
            _assignRoleOption.Select(0);
        }
    }

    private void PopulateRemoveRoleOptions()
    {
        if (_removeRoleOption == null || _context.Localization == null)
        {
            return;
        }

        _removeRoleOption.Clear();
        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var removableAppointments = GetRemovableAppointments(officer);
        if (removableAppointments.Count == 0)
        {
            _removeRoleOption.AddItem(_context.Localization.T("ui.none"));
            _removeRoleOption.SetItemMetadata(0, string.Empty);
            _removeRoleOption.Select(0);
            return;
        }

        foreach (var appointment in removableAppointments)
        {
            AddRoleOption(_removeRoleOption, appointment);
        }

        _removeRoleOption.Select(0);
    }

    private void AddRoleOption(OptionButton option, string role)
    {
        if (_context.Localization == null)
        {
            return;
        }

        option.AddItem(GetRoleDisplayName(role));
        option.SetItemMetadata(option.ItemCount - 1, role);
    }

    private void RefreshRoleOptionTexts()
    {
        if (_context.Localization == null)
        {
            return;
        }

        var selectedAssignRole = _assignRoleOption?.Selected >= 0
            ? _assignRoleOption.GetItemMetadata(_assignRoleOption.Selected).AsString()
            : string.Empty;
        var selectedRemoveRole = _removeRoleOption?.Selected >= 0
            ? _removeRoleOption.GetItemMetadata(_removeRoleOption.Selected).AsString()
            : string.Empty;

        PopulateAssignRoleOptions();
        PopulateRemoveRoleOptions();
        SelectRoleOption(_assignRoleOption, selectedAssignRole);
        SelectRoleOption(_removeRoleOption, selectedRemoveRole);
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private string GetRoleDisplayName(string role)
    {
        if (_context.Localization == null)
        {
            return role;
        }

        return role.ToLowerInvariant() switch
        {
            "governor" => _context.Localization.T("role.governor"),
            "removegovernor" => _context.Localization.T("role.remove_governor"),
            "strategist" => _context.Localization.T("role.strategist"),
            "removestrategist" => _context.Localization.T("role.remove_strategist"),
            "chancellor" => _context.Localization.T("ui.chancellor"),
            "removechancellor" => _context.Localization.T("role.remove_chancellor"),
            "chiefstrategist" => _context.Localization.T("ui.chief_strategist"),
            "removechiefstrategist" => _context.Localization.T("role.remove_chief_strategist"),
            _ => role
        };
    }

    private void OnAssignPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        var world = turnManager?.World;
        if (city == null || turnManager == null || commandResolver == null || world == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            ShowOverlay();
            return;
        }

        var roleMetadata = _assignRoleOption?.GetItemMetadata(_assignRoleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : OfficerAppointmentRules.Governor;
        var officer = world.GetOfficer(_selectedOfficerId);
        var sourceCityId = officer?.CityId ?? city.Id;
        var result = role switch
        {
            "Chancellor" or "ChiefStrategist" => commandResolver.ExecuteAssignFactionAdvisor(turnManager.GetPlayerFactionId(), sourceCityId, _selectedOfficerId, role),
            _ => commandResolver.ExecuteAssignOfficerAppointment(turnManager.GetPlayerFactionId(), sourceCityId, _selectedOfficerId, role)
        };
        _lastResultMessage = _context.GetLocalizedResultMessage(result);
        _context.AddLog(_lastResultMessage, isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerAppointmentsChanged(_selectedOfficerId, sourceCityId, city.OwnerFactionId);
            if (role is "Chancellor" or "ChiefStrategist")
            {
                _context.UiEventHub.PublishFactionLeadershipChanged(city.OwnerFactionId, city.Id);
            }

            PopulateAssignRoleOptions();
            PopulateRemoveRoleOptions();
        }
        UpdateSelectedOfficerSummary();
    }

    private void OnRemovePressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        var world = turnManager?.World;
        if (city == null || turnManager == null || commandResolver == null || world == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            ShowOverlay();
            return;
        }

        var roleMetadata = _removeRoleOption?.GetItemMetadata(_removeRoleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : string.Empty;
        if (string.IsNullOrWhiteSpace(role))
        {
            _context.AddLog(_context.Localization?.T("ui.no_appointment_to_clear") ?? string.Empty);
            return;
        }

        var officer = world.GetOfficer(_selectedOfficerId);
        var sourceCityId = officer?.CityId ?? city.Id;
        var result = commandResolver.ExecuteClearOfficerAppointment(turnManager.GetPlayerFactionId(), sourceCityId, _selectedOfficerId, role);
        _lastResultMessage = _context.GetLocalizedResultMessage(result);
        _context.AddLog(_lastResultMessage, isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerAppointmentsChanged(_selectedOfficerId, sourceCityId, city.OwnerFactionId);
            if (role is "Chancellor" or "ChiefStrategist")
            {
                _context.UiEventHub.PublishFactionLeadershipChanged(city.OwnerFactionId, city.Id);
            }

            PopulateAssignRoleOptions();
            PopulateRemoveRoleOptions();
        }
        UpdateSelectedOfficerSummary();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var localCandidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        var factionCandidateOfficerIds = _context.GetNonRulerFactionOfficerIds();
        if (factionCandidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowAssignRoleOfficerSelectorDialog(
            localization.T("ui.assign_role_officer"),
            localCandidateOfficerIds.Count > 0 ? localCandidateOfficerIds : factionCandidateOfficerIds,
            officerId =>
            {
                _selectedOfficerId = officerId;
                _lastResultMessage = string.Empty;
                PopulateAssignRoleOptions();
                PopulateRemoveRoleOptions();
                UpdateSelectedOfficerSummary();
            },
            scopeOptions: new[]
            {
                new HudController.OfficerSelectorScopeOption
                {
                    Key = "local",
                    Label = localization.T("ui.local_place"),
                    CandidateOfficerIds = localCandidateOfficerIds
                },
                new HudController.OfficerSelectorScopeOption
                {
                    Key = "faction",
                    Label = localization.T("ui.whole_faction"),
                    CandidateOfficerIds = factionCandidateOfficerIds
                }
            },
            initialScopeKey: localCandidateOfficerIds.Count > 0 ? "local" : "faction",
            titleFactory: () => _context.Localization?.T("ui.assign_role_officer") ?? localization.T("ui.assign_role_officer"),
            scopeOptionsFactory: () => new[]
            {
                new HudController.OfficerSelectorScopeOption
                {
                    Key = "local",
                    Label = _context.Localization?.T("ui.local_place") ?? localization.T("ui.local_place"),
                    CandidateOfficerIds = localCandidateOfficerIds
                },
                new HudController.OfficerSelectorScopeOption
                {
                    Key = "faction",
                    Label = _context.Localization?.T("ui.whole_faction") ?? localization.T("ui.whole_faction"),
                    CandidateOfficerIds = factionCandidateOfficerIds
                }
            },
            displayConfigFactory: () => _context.BuildAssignRoleOfficerSelectorDisplayConfig());
    }

    private void OnClearOfficerPressed()
    {
        _selectedOfficerId = -1;
        _lastResultMessage = string.Empty;
        PopulateAssignRoleOptions();
        PopulateRemoveRoleOptions();
        UpdateSelectedOfficerSummary();
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var world = _context.TurnManager?.World;
        var officer = _selectedOfficerId > 0 ? world?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.assign_role_officer")}: {officerName}";
        if (_currentAppointmentsValueLabel != null)
        {
            _currentAppointmentsValueLabel.Text = BuildCurrentAppointmentsText(officer);
        }
        if (_assignButton != null)
        {
            _assignButton.Disabled = officer == null || !HasAssignableAppointment(officer);
        }
        if (_removeButton != null)
        {
            _removeButton.Disabled = officer == null || !HasRemovableAppointment(officer);
        }

        if (_summaryLabel == null || world == null || _context.SelectedCity == null)
        {
            return;
        }

        var faction = world.GetFaction(_context.SelectedCity.OwnerFactionId);
        if (faction == null)
        {
            _summaryLabel.Text = string.Empty;
            return;
        }

        var chancellor = world.GetOfficer(faction.ChancellorOfficerId);
        var chiefStrategist = world.GetOfficer(faction.ChiefStrategistOfficerId);
        var chancellorName = chancellor != null ? _context.Localization.GetOfficerName(chancellor) : _context.Localization.T("ui.unassigned");
        var chiefStrategistName = chiefStrategist != null ? _context.Localization.GetOfficerName(chiefStrategist) : _context.Localization.T("ui.unassigned");
        var assignRole = GetSelectedRoleName(_assignRoleOption, OfficerAppointmentRules.Governor);
        var removeRole = GetSelectedRoleName(_removeRoleOption, string.Empty);
        var pendingAssignText = officer == null
            ? _context.Localization.T("ui.none")
            : $"{officerName} -> {assignRole}";
        var pendingRemoveText = officer == null || string.IsNullOrWhiteSpace(removeRole) || removeRole == _context.Localization.T("ui.none")
            ? _context.Localization.T("ui.none")
            : $"{officerName} -> {removeRole}";

        _summaryLabel.Text =
            $"{_context.Localization.T("ui.chancellor")}: {chancellorName}\n" +
            $"{_context.Localization.T("ui.chief_strategist")}: {chiefStrategistName}\n" +
            $"{_context.Localization.T("ui.pending_assign_appointment")}: {pendingAssignText}\n" +
            $"{_context.Localization.T("ui.pending_clear_appointment")}: {pendingRemoveText}" +
            (string.IsNullOrWhiteSpace(_lastResultMessage) ? string.Empty : $"\n{_lastResultMessage}");
    }

    private string GetSelectedRoleName(OptionButton? option, string fallbackRole)
    {
        if (option == null || option.ItemCount == 0 || option.Selected < 0)
        {
            return string.IsNullOrWhiteSpace(fallbackRole)
                ? _context.Localization?.T("ui.none") ?? string.Empty
                : GetRoleDisplayName(fallbackRole);
        }

        var roleMetadata = option.GetItemMetadata(option.Selected);
        if (roleMetadata.VariantType == Variant.Type.String)
        {
            var role = roleMetadata.AsString();
            return string.IsNullOrWhiteSpace(role)
                ? _context.Localization?.T("ui.none") ?? string.Empty
                : GetRoleDisplayName(role);
        }

        return string.IsNullOrWhiteSpace(fallbackRole)
            ? _context.Localization?.T("ui.none") ?? string.Empty
            : GetRoleDisplayName(fallbackRole);
    }

    private static void SelectRoleOption(OptionButton? option, string role)
    {
        if (option == null || string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        for (var index = 0; index < option.ItemCount; index += 1)
        {
            var metadata = option.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.String && metadata.AsString() == role)
            {
                option.Select(index);
                return;
            }
        }
    }

    private string BuildCurrentAppointmentsText(OfficerData? officer)
    {
        if (_context.Localization == null || officer == null)
        {
            return _context.Localization?.T("ui.none") ?? string.Empty;
        }

        var appointments = GetRemovableAppointments(officer)
            .Select(GetRoleDisplayName)
            .Distinct()
            .ToList();
        return appointments.Count > 0
            ? string.Join(" / ", appointments)
            : _context.Localization.T("ui.none");
    }

    private static bool HasRemovableAppointment(OfficerData officer)
    {
        return GetRemovableAppointments(officer).Count > 0;
    }

    private static bool HasAssignableAppointment(OfficerData officer)
    {
        return GetAssignableAppointments(officer).Count > 0;
    }

    private static List<string> GetAssignableAppointments(OfficerData? officer)
    {
        var allAppointments = new[]
        {
            OfficerAppointmentRules.Governor,
            OfficerAppointmentRules.Strategist,
            OfficerAppointmentRules.Chancellor,
            OfficerAppointmentRules.ChiefStrategist
        };

        if (officer == null)
        {
            return allAppointments.ToList();
        }

        return allAppointments
            .Where(appointment => !OfficerAppointmentRules.HasAppointment(officer, appointment))
            .ToList();
    }

    private static List<string> GetRemovableAppointments(OfficerData? officer)
    {
        if (officer == null)
        {
            return new List<string>();
        }

        return officer.Appointments
            .Where(appointment =>
                appointment.Equals(OfficerAppointmentRules.Governor, System.StringComparison.OrdinalIgnoreCase) ||
                appointment.Equals(OfficerAppointmentRules.Strategist, System.StringComparison.OrdinalIgnoreCase) ||
                appointment.Equals(OfficerAppointmentRules.Chancellor, System.StringComparison.OrdinalIgnoreCase) ||
                appointment.Equals(OfficerAppointmentRules.ChiefStrategist, System.StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }
}
