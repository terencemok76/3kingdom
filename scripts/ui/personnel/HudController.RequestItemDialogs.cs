using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureRequestItemDialogWidgets()
    {
        if (_requestItemDialog == null)
        {
            return;
        }

        var existingRoot = _requestItemDialog.GetNodeOrNull<VBoxContainer>("RequestItemDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("RequestItemDialogRoot not found in RequestItemDialog.tscn.");
            return;
        }

        _requestItemSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _requestItemSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _requestItemOption = existingRoot.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _requestItemConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_requestItemDialogSignalsConnected)
        {
            if (_requestItemSelectOfficerButton != null)
            {
                _requestItemSelectOfficerButton.Pressed += OnRequestItemSelectOfficerPressed;
            }
            if (_requestItemConfirmButton != null)
            {
                _requestItemConfirmButton.Pressed += OnRequestItemDialogConfirmed;
            }
            _requestItemDialogSignalsConnected = true;
        }
    }

    private void ShowRequestItemDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _requestItemDialog == null || _localization == null)
        {
            return;
        }

        EnsureRequestItemDialogWidgets();
        UpdateRequestItemDialogText();
        PopulateRequestItemDialog();
        PopupDialogUsingSceneSize(_requestItemDialog);
    }

    private void PopulateRequestItemDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var candidateOfficerIds = GetRequestItemCandidateIds();
        if (!candidateOfficerIds.Contains(_requestItemSelectedOfficerId))
        {
            _requestItemSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateRequestItemSelectedOfficerSummary();
        PopulateRequestItemOption();
    }

    private void UpdateRequestItemDialogText()
    {
        if (_requestItemDialog == null || _localization == null)
        {
            return;
        }

        _requestItemDialog.Title = _localization.T("command.personnel.request_item");
        SetRequestItemDialogLabelText("OfficerListLabel", _localization.T("ui.request_item_officer"));
        SetRequestItemDialogLabelText("ItemLabel", _localization.T("ui.request_item"));
        if (_requestItemSelectOfficerButton != null)
        {
            _requestItemSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_requestItemConfirmButton != null)
        {
            _requestItemConfirmButton.Text = _localization.T("ui.confirm_request_item");
        }
        UpdateRequestItemSelectedOfficerSummary();
    }

    private void SetRequestItemDialogLabelText(string nodeName, string text)
    {
        var label = _requestItemDialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/{nodeName}") ??
                    _requestItemDialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void PopulateRequestItemOption()
    {
        if (_requestItemOption == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        _requestItemOption.Clear();
        _requestItemOption.AddItem(_localization.T("ui.no_item"));
        _requestItemOption.SetItemMetadata(0, 0);

        if (_requestItemSelectedOfficerId <= 0)
        {
            _requestItemOption.Select(0);
            return;
        }

        foreach (var item in _turnManager.World.Items
                     .Where(item => item.EquippedOfficerId == _requestItemSelectedOfficerId)
                     .OrderBy(item => _localization.GetItemName(item)))
        {
            var row = _localization.Format(
                "fmt.item_option",
                _localization.GetItemName(item),
                _localization.GetItemType(item),
                _localization.GetItemRarity(item));
            _requestItemOption.AddItem(row);
            _requestItemOption.SetItemMetadata(_requestItemOption.ItemCount - 1, item.Id);
        }

        _requestItemOption.Select(0);
    }

    private void OnRequestItemDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_requestItemSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenRequestItemDialog();
            return;
        }

        var item = GetSelectedItemFromOption(_requestItemOption);
        if (item == null)
        {
            AddLog(_localization?.T("ui.select_item_warning") ?? string.Empty);
            ReopenRequestItemDialog();
            return;
        }

        var result = _commandResolver.ExecuteRecallOfficerItem(_turnManager.GetPlayerFactionId(), _selectedCity.Id, _requestItemSelectedOfficerId, item.Id);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
        _requestItemDialog?.Hide();
    }

    private void ReopenRequestItemDialog()
    {
        CallDeferred(nameof(ReopenRequestItemDialogDeferred));
    }

    private void ReopenRequestItemDialogDeferred()
    {
        PopupDialogUsingSceneSize(_requestItemDialog);
    }

    private void OnRequestItemSelectOfficerPressed()
    {
        if (_localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetRequestItemCandidateIds();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.request_item_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            SelectRequestItemOfficerById);
    }

    private void SelectRequestItemOfficerById(int officerId)
    {
        _requestItemSelectedOfficerId = officerId;
        UpdateRequestItemSelectedOfficerSummary();
        PopulateRequestItemOption();
    }

    private void UpdateRequestItemSelectedOfficerSummary()
    {
        if (_requestItemSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _requestItemSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_requestItemSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _requestItemSelectedOfficerLabel.Text = $"{_localization.T("ui.request_item_officer")}: {officerName}";
    }

    private List<int> GetRequestItemCandidateIds()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return new List<int>();
        }

        return _selectedCity.OfficerIds
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && _turnManager.World.Items.Any(item => item.EquippedOfficerId == officer.Id);
            })
            .ToList();
    }
}
