using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class RecruitTroopDialogController
{
    private static readonly TroopType[] TroopTypes =
    {
        TroopType.Infantry,
        TroopType.Spearman,
        TroopType.Cavalry,
        TroopType.Archer,
        TroopType.Crossbow,
        TroopType.Siege
    };

    private readonly MilitaryUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _troopTypeOption;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public RecruitTroopDialogController(MilitaryUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/military/RecruitTroopDialog.tscn", dialog => dialog.Hide());
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

        _dialog.Title = _context.Localization.T("ui.military_recruit");
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.officers"));
        SetLabelText("TroopTypeLabel", _context.Localization.T("ui.recruit_troop_type"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_officer_selection");
        }

        UpdateSelectedOfficerSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("RecruitTroopDialogRoot");
        if (root == null)
        {
            GD.PushError("RecruitTroopDialogRoot not found in RecruitTroopDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _troopTypeOption = root.GetNodeOrNull<OptionButton>("TroopTypeOption");
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
        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_troopTypeOption != null)
        {
            _troopTypeOption.Clear();
            foreach (var troopType in TroopTypes)
            {
                _troopTypeOption.AddItem(_context.GetTroopTypeDisplayName(troopType));
                _troopTypeOption.SetItemMetadata(_troopTypeOption.ItemCount - 1, (int)troopType);
            }

            if (_troopTypeOption.ItemCount > 0)
            {
                _troopTypeOption.Select(0);
            }
        }

        UpdateSelectedOfficerSummary();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = _dialog?.GetNodeOrNull<Label>($"RecruitTroopDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.Format("ui.no_available_officer_for_command", _context.GetCommandName(CommandType.Recruit)));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.military_recruit"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Strength,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            });
    }

    private void OnConfirmPressed()
    {
        if (_context.Localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization.T("ui.select_officer_warning"));
            _context.ReopenDialog(_dialog);
            return;
        }

        var result = _context.ExecutePlayerCommand(
            CommandType.Recruit,
            officerIds: new System.Collections.Generic.List<int> { _selectedOfficerId },
            recruitTroopType: GetSelectedRecruitTroopType());
        if (result.Success)
        {
            _dialog?.Hide();
        }
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

    private TroopType GetSelectedRecruitTroopType()
    {
        if (_troopTypeOption == null || _troopTypeOption.Selected < 0)
        {
            return TroopType.Infantry;
        }

        var metadata = _troopTypeOption.GetItemMetadata(_troopTypeOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (TroopType)metadata.AsInt32()
            : TroopType.Infantry;
    }
}
