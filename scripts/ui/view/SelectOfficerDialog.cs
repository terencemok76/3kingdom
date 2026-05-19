using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public sealed partial class SelectOfficerDialog : Window
{
    public sealed class ScopeOption
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required IReadOnlyList<RowData> Rows { get; init; }
    }

    public sealed class RowData
    {
        public required int OfficerId { get; init; }
        public required string OfficerName { get; init; }
        public required string RoleName { get; init; }
        public required string StatusName { get; init; }
        public required string PrimaryStatText { get; init; }
    }

    private Tree? _officerTable;
    private HBoxContainer? _scopeRow;
    private Button? _primaryScopeButton;
    private Button? _secondaryScopeButton;
    private Button? _confirmButton;
    private Action<int>? _confirmedAction;
    private readonly List<ScopeOption> _scopeOptions = new();
    private string _activeScopeKey = string.Empty;

    public override void _Ready()
    {
        _officerTable = GetNodeOrNull<Tree>("SelectOfficerDialogRoot/ContentSection/OfficerTable");
        _scopeRow = GetNodeOrNull<HBoxContainer>("SelectOfficerDialogRoot/ScopeRow");
        _primaryScopeButton = GetNodeOrNull<Button>("SelectOfficerDialogRoot/ScopeRow/PrimaryScopeButton");
        _secondaryScopeButton = GetNodeOrNull<Button>("SelectOfficerDialogRoot/ScopeRow/SecondaryScopeButton");
        _confirmButton = GetNodeOrNull<Button>("SelectOfficerDialogRoot/FooterSection/ConfirmRow/ConfirmButton");

        if (_officerTable != null)
        {
            _officerTable.ItemSelected += OnOfficerTableSelected;
        }

        if (_primaryScopeButton != null)
        {
            _primaryScopeButton.Pressed += () => ActivateScope(0);
        }

        if (_secondaryScopeButton != null)
        {
            _secondaryScopeButton.Pressed += () => ActivateScope(1);
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        CloseRequested += OnCloseRequested;
    }

    public void ShowSelector(
        string title,
        string confirmText,
        string officerTitle,
        string roleTitle,
        string statusTitle,
        string statTitle,
        IReadOnlyList<RowData> rows,
        Action<int> onConfirmed,
        IReadOnlyList<ScopeOption>? scopeOptions = null,
        string? initialScopeKey = null)
    {
        if (_officerTable == null || _confirmButton == null)
        {
            return;
        }

        Title = title;
        _confirmButton.Text = confirmText;
        _confirmedAction = onConfirmed;
        ConfigureColumns(officerTitle, roleTitle, statusTitle, statTitle);

        _scopeOptions.Clear();
        if (scopeOptions != null)
        {
            _scopeOptions.AddRange(scopeOptions.Where(option => option.Rows.Count > 0));
        }

        if (_scopeRow != null)
        {
            _scopeRow.Visible = _scopeOptions.Count >= 2;
        }

        if (_scopeOptions.Count >= 2)
        {
            if (_primaryScopeButton != null)
            {
                _primaryScopeButton.Text = _scopeOptions[0].Label;
            }

            if (_secondaryScopeButton != null)
            {
                _secondaryScopeButton.Text = _scopeOptions[1].Label;
            }

            _activeScopeKey = !string.IsNullOrWhiteSpace(initialScopeKey) &&
                              _scopeOptions.Any(option => option.Key == initialScopeKey)
                ? initialScopeKey
                : _scopeOptions[0].Key;
            RenderRows(GetActiveScopeRows());
            UpdateScopeButtonStates();
        }
        else
        {
            _activeScopeKey = string.Empty;
            RenderRows(rows);
            UpdateScopeButtonStates();
        }

        var sceneSize = Size;
        if (sceneSize.X > 0 && sceneSize.Y > 0)
        {
            PopupCentered(sceneSize);
        }
        else
        {
            PopupCentered(new Vector2I(620, 320));
        }
    }

    private void ConfigureColumns(string officerTitle, string roleTitle, string statusTitle, string statTitle)
    {
        if (_officerTable == null)
        {
            return;
        }

        _officerTable.Clear();
        _officerTable.Columns = 4;
        _officerTable.SetColumnTitle(0, officerTitle);
        _officerTable.SetColumnCustomMinimumWidth(0, 150);
        _officerTable.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _officerTable.SetColumnTitle(1, roleTitle);
        _officerTable.SetColumnCustomMinimumWidth(1, 100);
        _officerTable.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _officerTable.SetColumnTitle(2, statusTitle);
        _officerTable.SetColumnCustomMinimumWidth(2, 100);
        _officerTable.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _officerTable.SetColumnTitle(3, statTitle);
        _officerTable.SetColumnCustomMinimumWidth(3, 90);
        _officerTable.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
    }

    private void RenderRows(IReadOnlyList<RowData> rows)
    {
        if (_officerTable == null)
        {
            return;
        }

        _officerTable.Clear();
        var root = _officerTable.CreateItem();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex += 1)
        {
            var rowData = rows[rowIndex];
            var row = _officerTable.CreateItem(root);
            row.SetMetadata(0, rowData.OfficerId);
            row.SetText(0, rowData.OfficerName);
            row.SetText(1, rowData.RoleName);
            row.SetText(2, rowData.StatusName);
            row.SetText(3, rowData.PrimaryStatText);
            ApplyRowStriping(row, rowIndex, 4);
        }
    }

    private void ActivateScope(int index)
    {
        if (index < 0 || index >= _scopeOptions.Count)
        {
            return;
        }

        _activeScopeKey = _scopeOptions[index].Key;
        RenderRows(_scopeOptions[index].Rows);
        UpdateScopeButtonStates();
    }

    private IReadOnlyList<RowData> GetActiveScopeRows()
    {
        var activeScope = _scopeOptions.FirstOrDefault(option => option.Key == _activeScopeKey);
        return activeScope?.Rows ?? _scopeOptions[0].Rows;
    }

    private void UpdateScopeButtonStates()
    {
        if (_primaryScopeButton != null)
        {
            _primaryScopeButton.Disabled = _scopeOptions.Count >= 1 && _activeScopeKey == _scopeOptions[0].Key;
        }

        if (_secondaryScopeButton != null)
        {
            _secondaryScopeButton.Disabled = _scopeOptions.Count >= 2 && _activeScopeKey == _scopeOptions[1].Key;
        }
    }

    private void OnOfficerTableSelected()
    {
        if (_officerTable == null)
        {
            return;
        }

        var selectedItem = _officerTable.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _officerTable.GetRoot();
        if (root == null)
        {
            return;
        }

        var row = root.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selectedItem)
            {
                ApplySelectedRowStyle(row, _officerTable.Columns);
            }
            else
            {
                ApplyRowStriping(row, rowIndex, _officerTable.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private void OnConfirmPressed()
    {
        var selectedItem = _officerTable?.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        _confirmedAction?.Invoke(metadata.AsInt32());
        _confirmedAction = null;
        Hide();
    }

    private void OnCloseRequested()
    {
        _confirmedAction = null;
        Hide();
    }

    private static void ApplyRowStriping(TreeItem row, int rowIndex, int columnCount)
    {
        var background = rowIndex % 2 == 0
            ? new Color(0.96f, 0.93f, 0.86f, 1.0f)
            : new Color(0.92f, 0.88f, 0.8f, 1.0f);
        var textColor = new Color(0.16f, 0.12f, 0.08f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private static void ApplySelectedRowStyle(TreeItem row, int columnCount)
    {
        var background = new Color(0.82f, 0.68f, 0.38f, 1.0f);
        var textColor = new Color(0.22f, 0.05f, 0.02f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }
}
