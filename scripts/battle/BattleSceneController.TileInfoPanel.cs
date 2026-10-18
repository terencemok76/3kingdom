using Godot;
using static ThreeKingdom.Battle.BattlePresentationSettings;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private const float TileInfoPanelDragThreshold = 6.0f;
    private const float TileInfoPanelResizeGripSize = 24.0f;

    private bool TryHandleMapPanFromTileInfoPanel(InputEvent @event)
    {
        if (_isPanningMapFromTileInfoPanel)
        {
            switch (@event)
            {
                case InputEventMouseMotion mouseMotion:
                    HandleMouseMotion(mouseMotion);
                    return true;
                case InputEventMouseButton
                    {
                        ButtonIndex: MouseButton.Right,
                        Pressed: false
                    } mouseButton:
                    HandleMouseButton(mouseButton);
                    _isPanningMapFromTileInfoPanel = false;
                    return true;
            }
        }

        if (@event is not InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right,
                Pressed: true
            } rightMouseButton ||
            !IsPointInTileInfoPanelMapPanSurface(rightMouseButton.GlobalPosition))
        {
            return false;
        }

        _isPanningMapFromTileInfoPanel = true;
        HandleMouseButton(rightMouseButton);
        return true;
    }

    private bool IsPointInTileInfoPanelMapPanSurface(Vector2 globalPosition)
    {
        if (_tileInfoPanel?.Visible != true ||
            !_tileInfoPanel.GetGlobalRect().HasPoint(globalPosition))
        {
            return false;
        }

        // BattleLogPanel is authored after SidePanel, so it remains the active
        // surface when both cards overlap at the same Z index.
        return _battleLogPanel?.Visible != true ||
               !_battleLogPanel.GetGlobalRect().HasPoint(globalPosition) ||
               _tileInfoPanel.ZIndex > _battleLogPanel.ZIndex;
    }

    private void HandleTileInfoPanelInput(InputEvent @event)
    {
        if (_tileInfoPanel?.Visible != true)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed && _tileInfoPanel.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
                {
                    BringFloatingPanelToFront(_tileInfoPanel);
                    if (IsPointInTileInfoPanelResizeGrip(mouseButton.GlobalPosition))
                    {
                        _isResizingTileInfoPanel = true;
                        _isTileInfoPanelDragPending = false;
                        _isDraggingTileInfoPanel = false;
                        _tileInfoPanelResizeStartMouse = mouseButton.GlobalPosition;
                        _tileInfoPanelResizeStartSize = _tileInfoPanel.Size;
                    }
                    else
                    {
                        _isTileInfoPanelDragPending = true;
                        _tileInfoPanelDragStartMouse = mouseButton.GlobalPosition;
                        _tileInfoPanelDragOffset = mouseButton.GlobalPosition - _tileInfoPanel.Position;
                    }

                    GetViewport().SetInputAsHandled();
                }
                else if (!mouseButton.Pressed)
                {
                    _isTileInfoPanelDragPending = false;
                    _isDraggingTileInfoPanel = false;
                    _isResizingTileInfoPanel = false;
                }

                break;
            case InputEventMouseMotion mouseMotion when _isResizingTileInfoPanel:
                ResizeTileInfoPanel(_tileInfoPanelResizeStartSize + mouseMotion.GlobalPosition - _tileInfoPanelResizeStartMouse);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion mouseMotion when _isTileInfoPanelDragPending || _isDraggingTileInfoPanel:
                if (!_isDraggingTileInfoPanel &&
                    mouseMotion.GlobalPosition.DistanceTo(_tileInfoPanelDragStartMouse) >= TileInfoPanelDragThreshold)
                {
                    _isDraggingTileInfoPanel = true;
                }

                if (_isDraggingTileInfoPanel)
                {
                    _tileInfoPanel.Position = ClampTileInfoPanelPosition(
                        mouseMotion.GlobalPosition - _tileInfoPanelDragOffset,
                        _tileInfoPanel.Size);
                    GetViewport().SetInputAsHandled();
                }

                break;
        }
    }

    private Vector2 ClampTileInfoPanelPosition(Vector2 desiredPosition, Vector2 panelSize)
    {
        var viewportSize = GetViewportRect().Size;
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - panelSize.X)),
            Mathf.Clamp(desiredPosition.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - panelSize.Y)));
    }

    private void BringFloatingPanelToFront(Control panel)
    {
        if (_tileInfoPanel != null && _tileInfoPanel != panel)
        {
            _tileInfoPanel.ZIndex = 0;
        }

        if (_battleLogPanel != null && _battleLogPanel != panel)
        {
            _battleLogPanel.ZIndex = 0;
        }

        panel.ZIndex = 1;
    }

    private bool IsPointInTileInfoPanelResizeGrip(Vector2 globalPosition)
    {
        if (_tileInfoPanel == null)
        {
            return false;
        }

        var panelRect = _tileInfoPanel.GetGlobalRect();
        var gripRect = new Rect2(
            panelRect.End - new Vector2(TileInfoPanelResizeGripSize, TileInfoPanelResizeGripSize),
            new Vector2(TileInfoPanelResizeGripSize, TileInfoPanelResizeGripSize));
        return gripRect.HasPoint(globalPosition);
    }

    private void ResizeTileInfoPanel(Vector2 desiredSize)
    {
        if (_tileInfoPanel == null)
        {
            return;
        }

        var viewportSize = GetViewportRect().Size;
        var targetSize = new Vector2(
            Mathf.Clamp(desiredSize.X, TileInfoPanelMinimumWidth, Mathf.Max(TileInfoPanelMinimumWidth, viewportSize.X - 20.0f)),
            Mathf.Clamp(desiredSize.Y, TileInfoPanelMinimumHeight, Mathf.Max(TileInfoPanelMinimumHeight, viewportSize.Y - 20.0f)));
        _tileInfoPanel.Size = targetSize;
        _tileInfoPanel.Position = ClampTileInfoPanelPosition(_tileInfoPanel.Position, targetSize);
    }
}
