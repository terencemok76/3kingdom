using Godot;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureSuccessionDialogWidgets()
    {
        if (_successionDialog == null)
        {
            return;
        }

        var existingRoot = _successionDialog.GetNodeOrNull<VBoxContainer>("SuccessionDialogRoot");
        if (existingRoot != null)
        {
            _successionSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _successionOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerList");
            _successionWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "SuccessionDialogRoot",
            CustomMinimumSize = new Vector2(720.0f, 420.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        _successionDialog.AddChild(root);

        _successionSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_successionSummaryLabel);

        _successionOfficerList = new Tree
        {
            Name = "OfficerList",
            Columns = 5,
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _successionOfficerList.ItemSelected += OnSuccessionOfficerSelected;
        root.AddChild(_successionOfficerList);

        _successionWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_successionWarningLabel);
    }

    private bool HasPendingPlayerSuccession()
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        return playerFactionId > 0 && _turnManager.World.GetPendingSuccession(playerFactionId) != null;
    }

    private void ShowSuccessionDialog()
    {
        if (_turnManager?.World == null || _localization == null || _successionDialog == null)
        {
            return;
        }

        var factionId = _turnManager.GetPlayerFactionId();
        var pendingSuccession = _turnManager.World.GetPendingSuccession(factionId);
        var faction = _turnManager.World.GetFaction(factionId);
        if (pendingSuccession == null || faction == null)
        {
            return;
        }

        _pendingSuccessionFactionId = factionId;
        EnsureSuccessionDialogWidgets();
        _successionDialog.Title = _localization.T("ui.succession");
        _successionDialog.OkButtonText = _localization.T("ui.confirm_succession");
        _successionSummaryLabel!.Text = _localization.Format("ui.succession_summary", _localization.GetFactionName(_turnManager.World, factionId));
        _successionWarningLabel!.Text = string.Empty;

        _successionOfficerList!.Columns = 5;
        _successionOfficerList.SetColumnTitle(0, _localization.T("ui.officers"));
        _successionOfficerList.SetColumnTitle(1, _localization.T("ui.role"));
        _successionOfficerList.SetColumnTitle(2, _localization.T("ui.leadership"));
        _successionOfficerList.SetColumnTitle(3, _localization.T("ui.intelligence"));
        _successionOfficerList.SetColumnTitle(4, _localization.T("ui.loyalty"));
        _successionOfficerList.Clear();
        var root = _successionOfficerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in pendingSuccession.CandidateOfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var row = _successionOfficerList.CreateItem(root);
            row.SetMetadata(0, officer.Id);
            row.SetText(0, _localization.GetOfficerName(officer));
            row.SetText(1, _localization.GetOfficerRole(officer));
            row.SetText(2, officer.Leadership.ToString());
            row.SetText(3, officer.Intelligence.ToString());
            row.SetText(4, officer.Loyalty.ToString());
            ApplyViewTableRowStriping(row, rowIndex, 5);
            rowIndex += 1;
        }

        var first = _successionOfficerList.GetRoot()?.GetFirstChild();
        first?.Select(0);
        OnSuccessionOfficerSelected();
        _successionDialog.PopupCentered(new Vector2I(760, 460));
    }

    private void OnSuccessionOfficerSelected()
    {
        if (_successionOfficerList == null)
        {
            return;
        }

        var selected = _successionOfficerList.GetSelected();
        var row = _successionOfficerList.GetRoot()?.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selected)
            {
                ApplyViewTableSelectedRowStyle(row, _successionOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _successionOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private void OnSuccessionDialogConfirmed()
    {
        if (_commandResolver == null || _localization == null || _pendingSuccessionFactionId <= 0 || _successionOfficerList == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_successionOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            _successionWarningLabel!.Text = _localization.T("ui.select_officer_warning");
            _successionDialog?.PopupCentered(new Vector2I(760, 460));
            return;
        }

        var result = _commandResolver.ResolvePlayerSuccession(_pendingSuccessionFactionId, selectedOfficerIds[0]);
        if (!result.Success)
        {
            _successionWarningLabel!.Text = GetLocalizedResultMessage(result);
            _successionDialog?.PopupCentered(new Vector2I(760, 460));
            return;
        }

        _pendingSuccessionFactionId = -1;
        _successionDialog?.Hide();
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        ContinuePendingNonAttackResolution();
    }

    private void OnSuccessionDialogCloseRequested()
    {
        if (_pendingSuccessionFactionId > 0)
        {
            ShowSuccessionDialog();
        }
    }
}
