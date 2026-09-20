using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public sealed partial class SelectOfficerDialog : Control
{
    public sealed class ColumnDefinition
    {
        public required string Title { get; init; }
        public int MinWidth { get; init; } = 90;
    }

    public sealed class ScopeOption
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required IReadOnlyList<RowData> Rows { get; init; }
    }

    public sealed class RowData
    {
        public required int OfficerId { get; init; }
        public required IReadOnlyList<string> ColumnTexts { get; init; }
        public string LocationKey { get; init; } = string.Empty;
        public string LocationLabel { get; init; } = string.Empty;
    }

    private Tree? _officerTable;
    private HBoxContainer? _scopeRow;
    private HBoxContainer? _locationFilterRow;
    private Label? _locationFilterLabel;
    private OptionButton? _locationFilterOption;
    private Button? _primaryScopeButton;
    private Button? _secondaryScopeButton;
    private Button? _confirmButton;
    private PanelContainer? _dialogPanel;
    private Control? _titleBar;
    private Label? _titleLabel;
    private Action<int>? _confirmedAction;
    private readonly List<ScopeOption> _scopeOptions = new();
    private readonly List<RowData> _activeRows = new();
    private string _activeScopeKey = string.Empty;
    private int _sortColumn = -1;
    private bool _sortAscending = true;
    private bool _dragging;
    private Vector2 _dragOffset = Vector2.Zero;

    public override void _Ready()
    {
        _dialogPanel = GetNodeOrNull<PanelContainer>("CenterContainer/AdvisorDialogPanel");
        _titleBar = GetNodeOrNull<Control>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar");
        _titleLabel = GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        _officerTable = GetNodeOrNull<Tree>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/ContentSection/OfficerTable");
        _scopeRow = GetNodeOrNull<HBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/ScopeRow");
        _locationFilterRow = GetNodeOrNull<HBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/LocationFilterRow");
        _locationFilterLabel = GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/LocationFilterRow/LocationFilterLabel");
        _locationFilterOption = GetNodeOrNull<OptionButton>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/LocationFilterRow/LocationFilterOption");
        _primaryScopeButton = GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/ScopeRow/PrimaryScopeButton");
        _secondaryScopeButton = GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/ScopeRow/SecondaryScopeButton");
        _confirmButton = GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/FooterSection/ConfirmRow/ConfirmButton");
        var closeButton = GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/CloseButton");

        ApplyExistingButtonThemes();

        if (_officerTable != null)
        {
            _officerTable.ItemSelected += OnOfficerTableSelected;
            _officerTable.ColumnTitleClicked += OnColumnTitleClicked;
        }

        if (_primaryScopeButton != null)
        {
            _primaryScopeButton.Pressed += () => ActivateScope(0);
        }

        if (_secondaryScopeButton != null)
        {
            _secondaryScopeButton.Pressed += () => ActivateScope(1);
        }

        if (_locationFilterOption != null)
        {
            _locationFilterOption.ItemSelected += _ => RenderActiveRows();
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        if (_titleBar != null)
        {
            _titleBar.GuiInput += OnTitleBarGuiInput;
        }
        if (_dialogPanel != null)
        {
            _dialogPanel.GuiInput += OnDialogPanelGuiInput;
        }
        if (closeButton != null)
        {
            closeButton.Pressed += OnCloseRequested;
        }
    }

    public void ShowSelector(
        string title,
        string confirmText,
        IReadOnlyList<ColumnDefinition> columns,
        IReadOnlyList<RowData> rows,
        Action<int> onConfirmed,
        IReadOnlyList<ScopeOption>? scopeOptions = null,
        string? initialScopeKey = null,
        Vector2? panelSize = null,
        int preferredOfficerId = -1,
        string locationFilterLabel = "Location",
        string allLocationsLabel = "All Locations")
    {
        if (_officerTable == null || _confirmButton == null)
        {
            return;
        }

        if (_titleLabel != null)
        {
            _titleLabel.Text = title;
        }
        _confirmButton.Text = confirmText;
        _confirmedAction = onConfirmed;
        _sortColumn = -1;
        _sortAscending = true;
        ConfigureColumns(columns);

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
            ConfigureLocationFilter(GetActiveScopeRows(), null, locationFilterLabel, allLocationsLabel);
            SetActiveRows(GetActiveScopeRows());
            UpdateScopeButtonStates();
        }
        else
        {
            _activeScopeKey = string.Empty;
            ConfigureLocationFilter(rows, null, locationFilterLabel, allLocationsLabel);
            SetActiveRows(rows);
            UpdateScopeButtonStates();
        }

        SelectOfficer(preferredOfficerId);
        Show();
        MoveToFront();
        CenterPanel(panelSize ?? new Vector2(620.0f, 320.0f));
    }

    public int GetSelectedOfficerId()
    {
        var selectedItem = _officerTable?.GetSelected();
        if (selectedItem == null)
        {
            return -1;
        }

        var metadata = selectedItem.GetMetadata(0);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void ConfigureColumns(IReadOnlyList<ColumnDefinition> columns)
    {
        if (_officerTable == null || columns.Count == 0)
        {
            return;
        }

        _officerTable.Clear();
        _officerTable.Columns = columns.Count;
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex += 1)
        {
            var column = columns[columnIndex];
            _officerTable.SetColumnTitle(columnIndex, column.Title);
            _officerTable.SetColumnCustomMinimumWidth(columnIndex, column.MinWidth);
            _officerTable.SetColumnTitleAlignment(columnIndex, HorizontalAlignment.Left);
        }
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
            for (var columnIndex = 0; columnIndex < _officerTable.Columns; columnIndex += 1)
            {
                var text = columnIndex < rowData.ColumnTexts.Count ? rowData.ColumnTexts[columnIndex] : string.Empty;
                row.SetText(columnIndex, text);
            }

            ApplyRowStriping(row, rowIndex, _officerTable.Columns);
        }
    }

    private void ActivateScope(int index)
    {
        if (index < 0 || index >= _scopeOptions.Count)
        {
            return;
        }

        _activeScopeKey = _scopeOptions[index].Key;
        ConfigureLocationFilter(_scopeOptions[index].Rows, null, _locationFilterLabel?.Text ?? "Location", _locationFilterOption?.GetItemText(0) ?? "All Locations");
        SetActiveRows(_scopeOptions[index].Rows);
        UpdateScopeButtonStates();
    }

    private IReadOnlyList<RowData> GetActiveScopeRows()
    {
        var activeScope = _scopeOptions.FirstOrDefault(option => option.Key == _activeScopeKey);
        return activeScope?.Rows ?? _scopeOptions[0].Rows;
    }

    private void ConfigureLocationFilter(
        IReadOnlyList<RowData> rows,
        IReadOnlyList<ScopeOption>? scopeOptions,
        string label,
        string allLocationsLabel)
    {
        if (_locationFilterRow == null || _locationFilterLabel == null || _locationFilterOption == null)
        {
            return;
        }

        var filterRows = scopeOptions is { Count: >= 2 }
            ? scopeOptions.FirstOrDefault(option => option.Key == _activeScopeKey)?.Rows ?? scopeOptions[0].Rows
            : rows;
        var locations = filterRows
            .Where(row => !string.IsNullOrWhiteSpace(row.LocationKey))
            .GroupBy(row => row.LocationKey)
            .Select(group => (Key: group.Key, Label: group.First().LocationLabel))
            .OrderBy(location => location.Label, StringComparer.CurrentCulture)
            .ToList();
        _locationFilterOption.Clear();
        _locationFilterOption.AddItem(allLocationsLabel);
        _locationFilterOption.SetItemMetadata(0, string.Empty);
        for (var index = 0; index < locations.Count; index += 1)
        {
            _locationFilterOption.AddItem(locations[index].Label);
            _locationFilterOption.SetItemMetadata(index + 1, locations[index].Key);
        }

        _locationFilterLabel.Text = label;
        _locationFilterRow.Visible = locations.Count > 1;
    }

    private void SetActiveRows(IReadOnlyList<RowData> rows)
    {
        _activeRows.Clear();
        _activeRows.AddRange(rows);
        RenderActiveRows();
    }

    private void RenderActiveRows()
    {
        var locationKey = GetSelectedLocationKey();
        var rows = _activeRows
            .Where(row => string.IsNullOrEmpty(locationKey) || row.LocationKey == locationKey);
        var orderedRows = _sortColumn >= 0
            ? rows.OrderBy(row => row, Comparer<RowData>.Create(CompareRows)).ToList()
            : rows.ToList();
        RenderRows(orderedRows);
    }

    private string GetSelectedLocationKey()
    {
        if (_locationFilterOption == null || _locationFilterOption.Selected < 0)
        {
            return string.Empty;
        }

        var metadata = _locationFilterOption.GetItemMetadata(_locationFilterOption.Selected);
        return metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
    }

    private int CompareRows(RowData left, RowData right)
    {
        var leftValue = _sortColumn >= 0 && _sortColumn < left.ColumnTexts.Count ? left.ColumnTexts[_sortColumn] : string.Empty;
        var rightValue = _sortColumn >= 0 && _sortColumn < right.ColumnTexts.Count ? right.ColumnTexts[_sortColumn] : string.Empty;
        var comparison = TryGetSortableNumber(leftValue, out var leftNumber) && TryGetSortableNumber(rightValue, out var rightNumber)
            ? leftNumber.CompareTo(rightNumber)
            : string.Compare(leftValue, rightValue, StringComparison.CurrentCulture);
        if (comparison == 0)
        {
            comparison = left.OfficerId.CompareTo(right.OfficerId);
        }

        return _sortAscending ? comparison : -comparison;
    }

    private void OnColumnTitleClicked(long column, long mouseButtonIndex)
    {
        if (mouseButtonIndex != (long)MouseButton.Left || column < 0 || _officerTable == null || column >= _officerTable.Columns)
        {
            return;
        }

        var selectedOfficerId = GetSelectedOfficerId();
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = (int)column;
            var valuesAreNumeric = _activeRows.Count > 0 && _activeRows.All(row =>
                _sortColumn < row.ColumnTexts.Count && TryGetSortableNumber(row.ColumnTexts[_sortColumn], out _));
            _sortAscending = !valuesAreNumeric;
        }

        RenderActiveRows();
        SelectOfficer(selectedOfficerId);
    }

    private static bool TryGetSortableNumber(string value, out int number)
    {
        if (value == "--")
        {
            number = int.MinValue;
            return true;
        }

        return int.TryParse(value, out number);
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

    private void SelectOfficer(int officerId)
    {
        if (_officerTable == null || officerId <= 0)
        {
            return;
        }

        var root = _officerTable.GetRoot();
        var row = root?.GetFirstChild();
        while (row != null)
        {
            var metadata = row.GetMetadata(0);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == officerId)
            {
                row.Select(0);
                OnOfficerTableSelected();
                return;
            }

            row = row.GetNext();
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

    private void OnDialogPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            MoveToFront();
        }
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (_dialogPanel == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                MoveToFront();
                _dragging = true;
                _dragOffset = mouseButton.GlobalPosition - _dialogPanel.GlobalPosition;
            }
            else
            {
                _dragging = false;
            }

            return;
        }

        if (@event is InputEventMouseMotion mouseMotion && _dragging)
        {
            var viewportSize = GetViewportRect().Size;
            var panelSize = _dialogPanel.Size;
            var target = mouseMotion.GlobalPosition - _dragOffset;
            _dialogPanel.Position = new Vector2(
                Mathf.Clamp(target.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - panelSize.X)),
                Mathf.Clamp(target.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - panelSize.Y)));
        }
    }

    private void CenterPanel(Vector2 fallbackSize)
    {
        if (_dialogPanel == null)
        {
            return;
        }

        var viewportSize = GetViewportRect().Size;
        var panelSize = _dialogPanel.Size;
        if (panelSize.X <= 0.0f || panelSize.Y <= 0.0f)
        {
            panelSize = _dialogPanel.CustomMinimumSize;
        }
        if (panelSize.X <= 0.0f || panelSize.Y <= 0.0f)
        {
            panelSize = fallbackSize;
            _dialogPanel.CustomMinimumSize = fallbackSize;
            _dialogPanel.Size = fallbackSize;
        }

        _dialogPanel.Position = new Vector2(
            Mathf.Max(0.0f, (viewportSize.X - panelSize.X) * 0.5f),
            Mathf.Max(0.0f, (viewportSize.Y - panelSize.Y) * 0.5f));
    }

    private void ApplyExistingButtonThemes()
    {
        var hudController = FindAncestor<HudController>(this);
        var sourceButton = hudController?.GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/ViewButton") ??
                           hudController?.GetNodeOrNull<Button>("Root/TopBar/EndTurnButton");
        if (sourceButton == null)
        {
            return;
        }

        foreach (var button in new[] { _primaryScopeButton, _secondaryScopeButton, _confirmButton })
        {
            if (button == null)
            {
                continue;
            }

            CopyButtonTheme(sourceButton, button);
        }
    }

    private static T? FindAncestor<T>(Node? node) where T : class
    {
        var current = node;
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static void CopyButtonTheme(Button source, Button target)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = source.GetThemeStylebox(state);
            if (style != null)
            {
                target.AddThemeStyleboxOverride(state, style);
            }
        }

        foreach (var colorName in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" })
        {
            if (source.HasThemeColor(colorName))
            {
                target.AddThemeColorOverride(colorName, source.GetThemeColor(colorName));
            }
        }
    }

    private static void ApplyRowStriping(TreeItem row, int rowIndex, int columnCount)
    {
        var background = rowIndex % 2 == 0
            ? new Color(0.12f, 0.12f, 0.14f, 0.84f)
            : new Color(0.16f, 0.16f, 0.18f, 0.8f);
        var textColor = new Color(0.92f, 0.89f, 0.82f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private static void ApplySelectedRowStyle(TreeItem row, int columnCount)
    {
        var background = new Color(0.55f, 0.45f, 0.28f, 0.92f);
        var textColor = new Color(0.98f, 0.95f, 0.9f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }
}
