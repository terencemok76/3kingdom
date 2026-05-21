using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class AssignRoleDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _roleOption;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 280.0f);

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
        SetLabelText("RoleLabel", _context.Localization.T("ui.assign_role_title"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_assign_role");
        }
        UpdateSelectedOfficerSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _roleOption = root.GetNodeOrNull<OptionButton>("RoleOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_roleOption != null)
            {
                _roleOption.ItemSelected += _ => UpdateSelectedOfficerSummary();
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            candidateOfficerIds = _context.GetNonRulerFactionOfficerIds();
        }

        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_roleOption != null)
        {
            _roleOption.Clear();
            AddRoleOption("General");
            AddRoleOption("Strategist");
            AddRoleOption("Advisor");
            AddRoleOption("Governor");
            AddRoleOption("Chancellor");
            AddRoleOption("ChiefStrategist");
            if (_roleOption.ItemCount > 0)
            {
                _roleOption.Select(0);
            }
        }

        UpdateSelectedOfficerSummary();
    }

    private void AddRoleOption(string role)
    {
        if (_roleOption == null || _context.Localization == null)
        {
            return;
        }

        _roleOption.AddItem(GetRoleDisplayName(role));
        _roleOption.SetItemMetadata(_roleOption.ItemCount - 1, role);
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
            "general" => _context.Localization.T("role.general"),
            "strategist" => _context.Localization.T("role.strategist"),
            "advisor" => _context.Localization.T("role.advisor"),
            "governor" => _context.Localization.T("role.governor"),
            "chancellor" => _context.Localization.T("ui.chancellor"),
            "chiefstrategist" => _context.Localization.T("ui.chief_strategist"),
            _ => role
        };
    }

    private void OnConfirmPressed()
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

        var officer = world.GetOfficer(_selectedOfficerId);
        var sourceCityId = officer?.CityId ?? city.Id;
        var roleMetadata = _roleOption?.GetItemMetadata(_roleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : "General";
        var result = role switch
        {
            "Chancellor" or "ChiefStrategist" => commandResolver.ExecuteAssignFactionAdvisor(turnManager.GetPlayerFactionId(), sourceCityId, _selectedOfficerId, role),
            _ => commandResolver.ExecuteAssignOfficerRole(turnManager.GetPlayerFactionId(), sourceCityId, _selectedOfficerId, role)
        };
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        HideOverlay();
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

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.assign_role_officer"),
            localCandidateOfficerIds.Count > 0 ? localCandidateOfficerIds : factionCandidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
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
            initialScopeKey: localCandidateOfficerIds.Count > 0 ? "local" : "faction");
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
        var selectedRole = GetSelectedRoleName();

        _summaryLabel.Text =
            $"{_context.Localization.T("ui.chancellor")}: {chancellorName}\n" +
            $"{_context.Localization.T("ui.chief_strategist")}: {chiefStrategistName}\n" +
            $"{_context.Localization.T("ui.advisor_pending_assignment")}: {officerName} -> {selectedRole}";
    }

    private string GetSelectedRoleName()
    {
        if (_roleOption == null || _roleOption.ItemCount == 0 || _roleOption.Selected < 0)
        {
            return GetRoleDisplayName("General");
        }

        var roleMetadata = _roleOption.GetItemMetadata(_roleOption.Selected);
        if (roleMetadata.VariantType == Variant.Type.String)
        {
            return GetRoleDisplayName(roleMetadata.AsString());
        }

        return GetRoleDisplayName("General");
    }
}
