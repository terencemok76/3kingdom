using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class AssignRoleDialogController
{
    private readonly PersonnelUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _roleOption;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public AssignRoleDialogController(PersonnelUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/personnel/AssignRoleDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _dialog == null || _context.Localization == null)
        {
            return;
        }

        EnsureWidgets();
        RefreshText();
        Populate();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("command.personnel.assign_title");
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

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("AssignRoleDialogRoot");
        if (root == null)
        {
            GD.PushError("AssignRoleDialogRoot not found in AssignRoleDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _roleOption = root.GetNodeOrNull<OptionButton>("RoleOption");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
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
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
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
        var label = _dialog?.GetNodeOrNull<Label>($"AssignRoleDialogRoot/{nodeName}");
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
            _ => role
        };
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
            _context.PopupDialog(_dialog);
            return;
        }

        var roleMetadata = _roleOption?.GetItemMetadata(_roleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : "General";
        var result = commandResolver.ExecuteAssignOfficerRole(turnManager.GetPlayerFactionId(), city.Id, _selectedOfficerId, role);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        _dialog?.Hide();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.assign_role_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            });
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.assign_role_officer")}: {officerName}";
    }
}
