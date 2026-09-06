using Godot;
using System;
using System.Linq;
using System.Text;
using static ThreeKingdom.Battle.BattlePresentationSettings;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void AppendBattleLog(BattleOccupantInfo actor, string category, string message)
    {
        AppendBattleLog(actor.TeamName, category, message);
    }

    private void AppendBattleLog(string teamName, string category, string message)
    {
        _battleLogs.Add(new BattleLogEntry(_turnNumber, teamName, category, message));
        RefreshBattleLogPanel();
    }

    private void RefreshBattleLogPanel()
    {
        if (_battleLogPanel == null || _battleLogLabel == null)
        {
            return;
        }

        _battleLogPanel.Visible = true;
        ApplyBattleLogPanelLayout();
        if (_allLogButton != null)
        {
            _allLogButton.Disabled = !_showSelfTeamLogOnly;
            _allLogButton.Text = _showSelfTeamLogOnly
                ? BattleText("ui.battle.all_log", "All")
                : BattleFormat("ui.battle.active_log_filter", "{0} *", BattleText("ui.battle.all_log", "All"));
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Disabled = _showSelfTeamLogOnly;
            _selfLogButton.Text = _showSelfTeamLogOnly
                ? BattleFormat("ui.battle.active_log_filter", "{0} *", BattleText("ui.battle.self_log", "Self"))
                : BattleText("ui.battle.self_log", "Self");
        }

        var selfTeamName = GetCurrentTurnSideName();
        var visibleLogs = _battleLogs
            .Where(entry => !_showSelfTeamLogOnly || entry.TeamName == selfTeamName)
            .TakeLast(80)
            .Reverse()
            .ToList();
        if (visibleLogs.Count == 0)
        {
            _battleLogLabel.Text = _showSelfTeamLogOnly
                ? BattleFormat("ui.battle.no_self_log", "No {0} log yet.", FormatTeamName(selfTeamName))
                : BattleText("ui.battle.no_log", "No battle log yet.");
            return;
        }

        var builder = new StringBuilder();
        foreach (var entry in visibleLogs)
        {
            builder.AppendLine($"T{entry.Turn} [{FormatLogTeamName(entry.TeamName)}] {entry.Category}: {entry.Message}");
        }

        _battleLogLabel.Text = builder.ToString().TrimEnd();
    }

    private void ApplyBattleLogPanelStyle()
    {
        if (_battleLogPanel is PanelContainer panel)
        {
            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
                BorderColor = new Color(0.48f, 0.39f, 0.24f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 0
            };
            panel.AddThemeStyleboxOverride("panel", panelStyle);
        }

        if (_battleLogTitleLabel != null)
        {
            _battleLogTitleLabel.Text = BattleText("ui.battle.battle_log", "Battle Log");
            _battleLogTitleLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.78f, 0.62f, 1.0f));
        }

        ApplyBattleLogButtonStyle(_allLogButton);
        ApplyBattleLogButtonStyle(_selfLogButton);
        ApplyBattleLogButtonStyle(_minimizeLogButton);
        if (_battleLogLabel != null)
        {
            _battleLogLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.78f, 1.0f));
        }
    }

    private static void ApplyBattleLogButtonStyle(Button? button)
    {
        if (button == null)
        {
            return;
        }

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.72f, 0.62f, 0.43f, 1.0f),
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.58f, 0.49f, 0.33f, 1.0f);
        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", normalStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("disabled", pressedStyle);
        button.AddThemeColorOverride("font_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.08f, 0.06f, 0.04f, 1.0f));
        if (button.Name == "MinimizeLogButton")
        {
            button.CustomMinimumSize = new Vector2(36.0f, button.CustomMinimumSize.Y);
        }
        else
        {
            button.CustomMinimumSize = new Vector2(Mathf.Max(52.0f, button.CustomMinimumSize.X), button.CustomMinimumSize.Y);
        }
    }

    private void OnAllLogButtonPressed()
    {
        _showSelfTeamLogOnly = false;
        RefreshBattleLogPanel();
    }

    private void OnSelfLogButtonPressed()
    {
        _showSelfTeamLogOnly = true;
        RefreshBattleLogPanel();
    }

    private void HandleBattleLogPanelInput(InputEvent @event)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    var mousePosition = mouseButton.GlobalPosition;
                    if (IsPointInBattleLogButtonArea(mousePosition))
                    {
                        return;
                    }

                    if (!_isBattleLogMinimized && IsPointInBattleLogResizeGrip(mousePosition))
                    {
                        _isResizingBattleLog = true;
                        _isDraggingBattleLog = false;
                        _battleLogResizeStartMouse = mousePosition;
                        _battleLogResizeStartSize = _battleLogPanel.Size;
                        GetViewport().SetInputAsHandled();
                        return;
                    }

                    if (IsPointInBattleLogDragArea(mousePosition))
                    {
                        _isDraggingBattleLog = true;
                        _isResizingBattleLog = false;
                        _battleLogDragOffset = mousePosition - _battleLogPanel.Position;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                else
                {
                    _isDraggingBattleLog = false;
                    _isResizingBattleLog = false;
                }

                break;
            case InputEventMouseMotion mouseMotion:
                if (_isDraggingBattleLog)
                {
                    _battleLogPanel.Position = ClampBattleLogPanelPosition(mouseMotion.GlobalPosition - _battleLogDragOffset, _battleLogPanel.Size);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (_isResizingBattleLog && !_isBattleLogMinimized)
                {
                    var resizeDelta = mouseMotion.GlobalPosition - _battleLogResizeStartMouse;
                    ResizeBattleLogPanel(_battleLogResizeStartSize + resizeDelta);
                    GetViewport().SetInputAsHandled();
                }

                break;
        }
    }

    private bool IsPointInBattleLogDragArea(Vector2 globalPosition)
    {
        if (_battleLogPanel == null || !_battleLogPanel.GetGlobalRect().HasPoint(globalPosition))
        {
            return false;
        }

        return !IsPointInBattleLogButtonArea(globalPosition) &&
               (_isBattleLogMinimized || !IsPointInBattleLogResizeGrip(globalPosition));
    }

    private bool IsPointInBattleLogButtonArea(Vector2 globalPosition)
    {
        return (_allLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_selfLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_minimizeLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false);
    }

    private bool IsPointInBattleLogResizeGrip(Vector2 globalPosition)
    {
        if (_battleLogPanel == null)
        {
            return false;
        }

        var panelRect = _battleLogPanel.GetGlobalRect();
        var gripRect = new Rect2(panelRect.End - new Vector2(24.0f, 24.0f), new Vector2(24.0f, 24.0f));
        return gripRect.HasPoint(globalPosition);
    }

    private void OnMinimizeLogButtonPressed()
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        _isBattleLogMinimized = !_isBattleLogMinimized;
        if (_isBattleLogMinimized)
        {
            _battleLogExpandedSize = _battleLogPanel.Size;
        }

        ApplyBattleLogPanelLayout();
    }

    private void ApplyBattleLogPanelLayout()
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        if (_battleLogScroll != null)
        {
            _battleLogScroll.Visible = !_isBattleLogMinimized;
        }

        if (_battleLogResizeGrip != null)
        {
            _battleLogResizeGrip.Visible = !_isBattleLogMinimized;
        }

        if (_allLogButton != null)
        {
            _allLogButton.Visible = !_isBattleLogMinimized;
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Visible = !_isBattleLogMinimized;
        }

        if (_minimizeLogButton != null)
        {
            _minimizeLogButton.Text = _isBattleLogMinimized ? "+" : "_";
        }

        var targetSize = _isBattleLogMinimized
            ? new Vector2(Mathf.Max(220.0f, _battleLogPanel.Size.X), 52.0f)
            : GetClampedBattleLogPanelSize(_battleLogExpandedSize);
        _battleLogPanel.Size = targetSize;
        _battleLogPanel.Position = ClampBattleLogPanelPosition(_battleLogPanel.Position, targetSize);
    }

    private void ResizeBattleLogPanel(Vector2 desiredSize)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        var targetSize = GetClampedBattleLogPanelSize(desiredSize);
        _battleLogExpandedSize = targetSize;
        _battleLogPanel.Size = targetSize;
        _battleLogPanel.Position = ClampBattleLogPanelPosition(_battleLogPanel.Position, targetSize);
    }

    private Vector2 GetClampedBattleLogPanelSize(Vector2 desiredSize)
    {
        var viewportSize = GetViewportRect().Size;
        return new Vector2(
            Mathf.Clamp(desiredSize.X, BattleLogMinimumWidth, Mathf.Max(BattleLogMinimumWidth, viewportSize.X - 20.0f)),
            Mathf.Clamp(desiredSize.Y, BattleLogMinimumHeight, Mathf.Max(BattleLogMinimumHeight, viewportSize.Y - 20.0f)));
    }

    private Vector2 ClampBattleLogPanelPosition(Vector2 desiredPosition, Vector2 panelSize)
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(0.0f, viewportSize.X - panelSize.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - panelSize.Y);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, maxX),
            Mathf.Clamp(desiredPosition.Y, 0.0f, maxY));
    }

    private void OnBattleLogHeaderGuiInput(InputEvent @event)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isDraggingBattleLog = true;
                _battleLogDragOffset = mouseButton.GlobalPosition - _battleLogPanel.Position;
                GetViewport().SetInputAsHandled();
            }
            else
            {
                _isDraggingBattleLog = false;
            }
        }
    }
}
