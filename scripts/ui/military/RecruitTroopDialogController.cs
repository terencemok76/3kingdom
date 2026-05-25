using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class RecruitTroopDialogController : FloatingOverlayController
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
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _troopTypeOption;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(380.0f, 182.0f);

    public RecruitTroopDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/RecruitTroopDialog.tscn")
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

        SetOverlayTitleText(_context.Localization.T("ui.military_recruit"));
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

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _troopTypeOption = root.GetNodeOrNull<OptionButton>("TroopTypeOption");
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

            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }

            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_troopTypeOption != null && city != null)
        {
            _troopTypeOption.Clear();
            foreach (var troopType in TroopTypes)
            {
                if (!CanRecruitTroopType(city, troopType))
                {
                    continue;
                }

                _troopTypeOption.AddItem(_context.GetTroopTypeDisplayName(troopType));
                _troopTypeOption.SetItemMetadata(_troopTypeOption.ItemCount - 1, (int)troopType);
            }

            if (_troopTypeOption.ItemCount > 0)
            {
                _troopTypeOption.Select(0);
            }
        }

        if (_confirmButton != null)
        {
            _confirmButton.Disabled = city == null || _troopTypeOption == null || _troopTypeOption.ItemCount <= 0;
        }

        UpdateSelectedOfficerSummary();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
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
        var localization = _context.Localization;
        var city = _context.SelectedCity;
        if (localization == null || city == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            ShowOverlay();
            return;
        }

        var result = _context.ExecutePlayerCommand(
            CommandType.Recruit,
            officerIds: new System.Collections.Generic.List<int> { _selectedOfficerId },
            recruitTroopType: GetSelectedRecruitTroopType());
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            HideOverlay();
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

    private static bool CanRecruitTroopType(CityData city, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => city.Horses > 0,
            TroopType.Crossbow => city.BowWorkshopLevel >= 1,
            TroopType.Siege => city.SiegeWorkshopLevel >= 1,
            _ => true
        };
    }
}
