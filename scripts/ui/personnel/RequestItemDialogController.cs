using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class RequestItemDialogController
{
    private readonly PersonnelUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _itemOption;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public RequestItemDialogController(PersonnelUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/personnel/RequestItemDialog.tscn", dialog => dialog.Hide());
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

        _dialog.Title = _context.Localization.T("command.personnel.request_item");
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.request_item_officer"));
        SetLabelText("ItemLabel", _context.Localization.T("ui.request_item"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_request_item");
        }
        UpdateSelectedOfficerSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("RequestItemDialogRoot");
        if (root == null)
        {
            GD.PushError("RequestItemDialogRoot not found in RequestItemDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _itemOption = root.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
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
        var candidateOfficerIds = GetCandidateOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateSelectedOfficerSummary();
        PopulateItemOption();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = _dialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/{nodeName}") ??
                    _dialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void PopulateItemOption()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (_itemOption == null || world == null || localization == null)
        {
            return;
        }

        _itemOption.Clear();
        _itemOption.AddItem(localization.T("ui.no_item"));
        _itemOption.SetItemMetadata(0, 0);

        if (_selectedOfficerId <= 0)
        {
            _itemOption.Select(0);
            return;
        }

        foreach (var item in world.Items
                     .Where(item => item.EquippedOfficerId == _selectedOfficerId)
                     .OrderBy(localization.GetItemName))
        {
            var row = localization.Format(
                "fmt.item_option",
                localization.GetItemName(item),
                localization.GetItemType(item),
                localization.GetItemRarity(item));
            _itemOption.AddItem(row);
            _itemOption.SetItemMetadata(_itemOption.ItemCount - 1, item.Id);
        }

        _itemOption.Select(0);
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

        var item = _context.GetSelectedItemFromOption(_itemOption);
        if (item == null)
        {
            _context.AddLog(_context.Localization?.T("ui.select_item_warning") ?? string.Empty);
            _context.PopupDialog(_dialog);
            return;
        }

        var result = commandResolver.ExecuteRecallOfficerItem(turnManager.GetPlayerFactionId(), city.Id, _selectedOfficerId, item.Id);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        _context.RefreshMapVisuals();
        _dialog?.Hide();
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
            localization.T("ui.request_item_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                PopulateItemOption();
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
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.request_item_officer")}: {officerName}";
    }

    private List<int> GetCandidateOfficerIds()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        return city.OfficerIds
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && world.Items.Any(item => item.EquippedOfficerId == officer.Id);
            })
            .ToList();
    }
}
