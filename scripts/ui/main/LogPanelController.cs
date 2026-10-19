using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;

namespace ThreeKingdom.UI;

internal sealed class LogPanelController
{
    private enum LogFilterMode
    {
        SelfFaction,
        All
    }

    private enum LogEntryType
    {
        GeneralInformation,
        PlayerAction,
        EconomyReport,
        WarOrDanger,
        SystemNotification
    }

    private sealed class LogEntry
    {
        public required string Message { get; init; }
        public required bool IsPlayerRelated { get; init; }
        public required LogEntryType Type { get; init; }
    }

    private readonly MainHudUiContext _context;
    private readonly List<LogEntry> _entries = new();
    private bool _hasEntries;
    private const float FloatingPanelHeaderHeight = 44.0f;
    private const float LogPanelInnerMargin = 28.0f;
    private const float LogPanelHeaderTopInset = 14.0f;
    private const float LogPanelContentGap = 8.0f;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float LogPanelMinimizedWidth = 450.0f;
    private const float LogPanelMinimumWidth = 320.0f;
    private const float LogPanelMinimumHeight = 140.0f;
    private const float ResizeHandleSize = 30.0f;
    private const float LogPanelScrollBarClearance = 28.0f;
    // Title + four actions + minimize button + five HBox gaps.  Below this
    // width, keeping partial actions is less usable than a clean title bar.
    private const float StandardHeaderActionsMinimumWidth = 300.0f;
    private const float DeveloperHeaderActionsMinimumWidth = 404.0f;
    // Normal play hides the developer-only copy button.  This leaves room for
    // the title, three player-facing actions, and the minimize button without
    // forcing an overly wide default panel.
    private const float LogPanelDefaultMinimumWidth = 360.0f;
    private const float HeaderButtonHeight = 28.0f;
    private PanelContainer? _header;
    private HBoxContainer? _headerRow;
    private Button? _minimizeButton;
    private Button? _selfFactionButton;
    private Button? _allLogButton;
    private Button? _copyLogButton;
    private Button? _clearLogButton;
    private Button? _resizeHandle;
    private ColorRect? _background;
    private Vector2 _defaultHeaderPosition;
    private Vector2 _headerPosition;
    private Vector2 _defaultBackgroundSize;
    private Vector2 _backgroundSize;
    private Vector2 _contentOffset;
    private Vector2 _defaultContentSize;
    private Vector2 _contentSize;
    private Texture2D? _minimizeButtonIcon;
    private Texture2D? _maximizeButtonIcon;
    private StyleBox? _expandedHeaderStyle;
    private StyleBox? _minimizedHeaderStyle;
    private bool _minimized;
    private bool _temporarilyHidden;
    private bool _isResizing;
    private LogFilterMode _filterMode = LogFilterMode.SelfFaction;
    private Vector2 _resizeStartMousePosition;
    private Vector2 _resizeStartSize;

    public LogPanelController(MainHudUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        if (_context.LogText != null)
        {
            _context.LogText.ScrollFollowing = true;
            _context.LogText.ScrollActive = true;
            ApplyLogScrollBarStyle(_context.LogText);
            if (!string.IsNullOrWhiteSpace(_context.LogText.Text))
            {
                _entries.Add(new LogEntry
                {
                    Message = _context.LogText.Text,
                    IsPlayerRelated = true,
                    Type = LogEntryType.PlayerAction
                });
                _hasEntries = true;
            }
        }
    }

    public void InitializePanelBehavior(PanelContainer? header, Button? minimizeButton, Button? resizeHandle, ColorRect? background)
    {
        _header = header;
        _minimizeButton = minimizeButton;
        _resizeHandle = resizeHandle;
        _background = background;
        var logText = _context.LogText;
        if (_background == null || logText == null)
        {
            return;
        }

        var originalPosition = _background.Position;
        // The scene stores the framed body below its editor-visible header.
        _defaultHeaderPosition = originalPosition;
        _headerPosition = _defaultHeaderPosition;
        _defaultBackgroundSize = new Vector2(
            Mathf.Max(LogPanelDefaultMinimumWidth, _background.Size.X),
            _background.Size.Y);
        _backgroundSize = _defaultBackgroundSize;
        _contentOffset = new Vector2(
            LogPanelInnerMargin,
            LogPanelHeaderTopInset + FloatingPanelHeaderHeight + LogPanelContentGap);
        _defaultContentSize = new Vector2(
            Mathf.Max(80.0f, _backgroundSize.X - (LogPanelInnerMargin * 2.0f)),
            Mathf.Max(60.0f, _backgroundSize.Y - _contentOffset.Y - LogPanelInnerMargin));
        _contentSize = _defaultContentSize;
        if (_minimizeButton != null)
        {
            _minimizeButtonIcon = GD.Load<Texture2D>("res://assets/ui/icons/128x128/minus.png");
            _maximizeButtonIcon = GD.Load<Texture2D>("res://assets/ui/icons/128x128/plus.png");
        }
        // The expanded header sits inside the NinePatch frame, so another panel
        // border there only overlaps the frame border.  When minimized it becomes
        // its own floating bar and needs the dark gold edge again.
        _expandedHeaderStyle = new StyleBoxEmpty();
        _minimizedHeaderStyle = CreateMinimizedHeaderStyle();
        _background.MouseFilter = Control.MouseFilterEnum.Stop;
        logText.MouseFilter = Control.MouseFilterEnum.Stop;
        InitializeHeaderButtons();
        if (_headerRow != null && _minimizeButton != null)
        {
            _headerRow.MoveChild(_minimizeButton, _headerRow.GetChildCount() - 1);
        }
        RefreshText();
        ApplyLayout();
    }

    public void RefreshText()
    {
        RefreshPanelTitle();
        UpdateFilterButtonText();
        UpdateFilterButtonVisualState();
        RebuildLogView();
        ApplyLayout();
    }

    public void RefreshPanelTitle()
    {
        if (_context.LogPanelHeaderLabel != null)
        {
            _context.LogPanelHeaderLabel.Text = GetLocalizedText("ui.gameplay_log_title", "Log");
        }
    }

    public void AddLog(string message, bool isPlayerRelated = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _entries.Add(new LogEntry
        {
            Message = message,
            IsPlayerRelated = isPlayerRelated,
            Type = ClassifyLogEntry(message, isPlayerRelated)
        });
        UpdateCopyButtonText();
        RebuildLogView();
    }

    public Vector2 GetHeaderPosition() => _minimized
        ? _headerPosition
        : _headerPosition + new Vector2(LogPanelInnerMargin, LogPanelHeaderTopInset);

    public float GetHeaderWidth() => _minimized
        ? LogPanelMinimizedWidth
        : Mathf.Max(LogPanelMinimumWidth - (LogPanelInnerMargin * 2.0f), _backgroundSize.X - (LogPanelInnerMargin * 2.0f));

    public float GetTotalHeight()
    {
        return _minimized
            ? FloatingPanelHeaderHeight
            : _backgroundSize.Y;
    }

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        _headerPosition = _minimized
            ? ClampPosition(targetPosition, GetHeaderWidth(), FloatingPanelHeaderHeight)
            : ClampPosition(
                targetPosition - new Vector2(LogPanelInnerMargin, LogPanelHeaderTopInset),
                _backgroundSize.X,
                _backgroundSize.Y);
        ApplyLayout();
    }

    public void ToggleMinimized()
    {
        _minimized = !_minimized;
        ApplyLayout();
    }

    public void StartResize(Vector2 mousePosition)
    {
        _isResizing = true;
        _resizeStartMousePosition = mousePosition;
        _resizeStartSize = _backgroundSize;
    }

    public bool UpdateResize(Vector2 mousePosition)
    {
        if (!_isResizing || _background == null)
        {
            return false;
        }

        var mouseDelta = mousePosition - _resizeStartMousePosition;
        var viewportSize = _background.GetViewportRect().Size;
        var maxWidth = Mathf.Max(LogPanelMinimumWidth, viewportSize.X - _headerPosition.X - FloatingPanelViewportMargin);
        var maxHeight = Mathf.Max(LogPanelMinimumHeight, viewportSize.Y - _headerPosition.Y - FloatingPanelViewportMargin);
        _backgroundSize = new Vector2(
            Mathf.Clamp(_resizeStartSize.X + mouseDelta.X, LogPanelMinimumWidth, maxWidth),
            Mathf.Clamp(_resizeStartSize.Y + mouseDelta.Y, LogPanelMinimumHeight, maxHeight));
        _contentSize = GetContentSizeForBackground();
        ApplyLayout();
        return true;
    }

    public bool EndResize()
    {
        if (!_isResizing)
        {
            return false;
        }

        _isResizing = false;
        return true;
    }

    public bool IsResizing() => _isResizing;

    public void SetTemporarilyHidden(bool hidden)
    {
        _temporarilyHidden = hidden;
        if (hidden)
        {
            _isResizing = false;
        }
        ApplyLayout();
    }

    public void ApplyLayout()
    {
        var logText = _context.LogText;
        if (_header == null || _background == null || logText == null || _minimizeButton == null)
        {
            return;
        }

        var headerWidth = GetHeaderWidth();
        _headerPosition = _minimized
            ? ClampPosition(_headerPosition, headerWidth, FloatingPanelHeaderHeight)
            : ClampPosition(_headerPosition, _backgroundSize.X, _backgroundSize.Y);
        _header.Position = _minimized
            ? _headerPosition
            : _headerPosition + new Vector2(LogPanelInnerMargin, LogPanelHeaderTopInset);
        _header.Size = new Vector2(headerWidth, FloatingPanelHeaderHeight);
        _header.AddThemeStyleboxOverride(
            "panel",
            _minimized ? _minimizedHeaderStyle! : _expandedHeaderStyle!);
        _minimizeButton.Text = string.Empty;
        _minimizeButton.Icon = _minimized ? _maximizeButtonIcon : _minimizeButtonIcon;
        _minimizeButton.IconAlignment = HorizontalAlignment.Center;
        _minimizeButton.Alignment = HorizontalAlignment.Center;
        _minimizeButton.AddThemeConstantOverride("icon_max_width", 18);
        _header.Visible = !_temporarilyHidden;
        var godModeEnabled = _context.IsGodModeEnabled();
        var requiredHeaderWidth = godModeEnabled
            ? DeveloperHeaderActionsMinimumWidth
            : StandardHeaderActionsMinimumWidth;
        var showHeaderActions = !_minimized && headerWidth >= requiredHeaderWidth;
        foreach (var actionButton in new[] { _selfFactionButton, _allLogButton, _clearLogButton })
        {
            if (actionButton != null)
            {
                actionButton.Visible = showHeaderActions;
            }
        }
        if (_copyLogButton != null)
        {
            _copyLogButton.Visible = showHeaderActions && godModeEnabled;
        }

        _background.Visible = !_temporarilyHidden && !_minimized;
        logText.Visible = !_temporarilyHidden && !_minimized;
        if (_resizeHandle != null)
        {
            _resizeHandle.Visible = !_temporarilyHidden && !_minimized;
        }

        if (_temporarilyHidden || _minimized)
        {
            return;
        }

        _background.Position = _headerPosition;
        _background.Size = _backgroundSize;
        logText.Position = _headerPosition + _contentOffset;
        logText.Size = _contentSize;
        if (_resizeHandle != null)
        {
            _resizeHandle.Position = _background.Position + _background.Size - new Vector2(
                ResizeHandleSize + LogPanelInnerMargin + LogPanelScrollBarClearance,
                ResizeHandleSize + LogPanelInnerMargin);
            _resizeHandle.Size = new Vector2(ResizeHandleSize, ResizeHandleSize);
            _context.MoveToFront(_resizeHandle);
            _resizeHandle.QueueRedraw();
        }
    }

    public void BringToFront()
    {
        _context.MoveToFront(_background);
        _context.MoveToFront(_context.LogText);
        _context.MoveToFront(_resizeHandle);
        _context.MoveToFront(_header);
    }

    public void ApplyLoadedSettings(bool minimized, float x, float y, float width, float height)
    {
        _minimized = minimized;
        _headerPosition = new Vector2(x, y);
        if (width > 0.0f)
        {
            _backgroundSize.X = width;
            _contentSize = GetContentSizeForBackground();
        }

        if (height > 0.0f)
        {
            _backgroundSize.Y = height;
            _contentSize = GetContentSizeForBackground();
        }
    }

    public void PopulateSettings(OptionSettingsData settings)
    {
        settings.LogPanelMinimized = _minimized;
        settings.LogPanelX = _headerPosition.X;
        settings.LogPanelY = _headerPosition.Y;
        settings.LogPanelWidth = _backgroundSize.X;
        settings.LogPanelHeight = _backgroundSize.Y;
    }

    public void RestoreDefaultLayout()
    {
        _minimized = false;
        _headerPosition = _defaultHeaderPosition;
        _backgroundSize = _defaultBackgroundSize;
        _contentSize = _defaultContentSize;
        ApplyLayout();
    }

    private Vector2 GetContentSizeForBackground()
    {
        return new Vector2(
            Mathf.Max(80.0f, _backgroundSize.X - (LogPanelInnerMargin * 2.0f)),
            Mathf.Max(60.0f, _backgroundSize.Y - _contentOffset.Y - LogPanelInnerMargin));
    }

    private Vector2 ClampPosition(Vector2 position, float width, float height)
    {
        if (_background == null)
        {
            return position;
        }

        var viewportSize = _background.GetViewportRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            return position;
        }

        var maxX = Mathf.Max(FloatingPanelViewportMargin, viewportSize.X - width - FloatingPanelViewportMargin);
        var maxY = Mathf.Max(FloatingPanelTopClamp, viewportSize.Y - height - FloatingPanelViewportMargin);
        return new Vector2(
            Mathf.Clamp(position.X, FloatingPanelViewportMargin, maxX),
            Mathf.Clamp(position.Y, FloatingPanelTopClamp, maxY));
    }

    private void InitializeHeaderButtons()
    {
        if (_headerRow != null || _header == null)
        {
            return;
        }

        _headerRow = _header.GetChildCount() > 0 ? _header.GetChild(0) as HBoxContainer : null;
        if (_headerRow == null)
        {
            return;
        }

        _selfFactionButton = GetOrCreateHeaderActionButton("SelfFactionButton", OnSelfFactionFilterPressed, 64.0f);
        _allLogButton = GetOrCreateHeaderActionButton("AllLogButton", OnAllLogFilterPressed, 64.0f);
        _copyLogButton = GetOrCreateHeaderActionButton("CopyLogButton", OnCopyLogPressed, 64.0f);
        _clearLogButton = GetOrCreateHeaderActionButton("ClearLogButton", OnClearLogPressed, 72.0f);
    }

    private static StyleBoxFlat CreateMinimizedHeaderStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.032f, 0.027f, 0.88f),
            BorderColor = new Color(0.5f, 0.34f, 0.13f, 0.85f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusBottomLeft = 5,
            ContentMarginLeft = 6.0f,
            ContentMarginTop = 4.0f,
            ContentMarginRight = 6.0f,
            ContentMarginBottom = 6.0f
        };
    }

    private static void ApplyLogScrollBarStyle(RichTextLabel logText)
    {
        var scrollBar = logText.GetVScrollBar();
        scrollBar.CustomMinimumSize = new Vector2(8.0f, 0.0f);

        var track = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.042f, 0.018f, 0.86f),
            BorderColor = new Color(0.34f, 0.25f, 0.10f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var grabber = new StyleBoxFlat
        {
            BgColor = new Color(0.55f, 0.40f, 0.15f, 0.96f),
            BorderColor = new Color(0.86f, 0.66f, 0.27f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        var hoverGrabber = (StyleBoxFlat)grabber.Duplicate();
        hoverGrabber.BgColor = new Color(0.82f, 0.62f, 0.22f, 1.0f);
        hoverGrabber.BorderColor = new Color(1.0f, 0.84f, 0.44f, 1.0f);
        var pressedGrabber = (StyleBoxFlat)grabber.Duplicate();
        pressedGrabber.BgColor = new Color(0.37f, 0.27f, 0.09f, 1.0f);

        scrollBar.AddThemeStyleboxOverride("scroll", track);
        scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
        scrollBar.AddThemeStyleboxOverride("grabber", grabber);
        scrollBar.AddThemeStyleboxOverride("grabber_highlight", hoverGrabber);
        scrollBar.AddThemeStyleboxOverride("grabber_pressed", pressedGrabber);
    }

    private Button CreateHeaderActionButton(System.Action pressedAction, float minimumWidth = 72.0f)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(minimumWidth, HeaderButtonHeight),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        CopySharedButtonTheme(button);
        button.Pressed += pressedAction;
        return button;
    }

    private Button GetOrCreateHeaderActionButton(string nodeName, System.Action pressedAction, float minimumWidth = 72.0f)
    {
        var existing = _headerRow?.GetNodeOrNull<Button>(nodeName);
        if (existing != null)
        {
            existing.CustomMinimumSize = new Vector2(minimumWidth, HeaderButtonHeight);
            existing.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            existing.Pressed += pressedAction;
            CopySharedButtonTheme(existing);
            return existing;
        }

        var button = CreateHeaderActionButton(pressedAction, minimumWidth);
        button.Name = nodeName;
        _headerRow!.AddChild(button);
        return button;
    }

    private void CopySharedButtonTheme(Button target)
    {
        var source = _context.ViewButton;
        if (source == null)
        {
            return;
        }

        foreach (var name in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = source.GetThemeStylebox(name);
            if (style != null)
            {
                target.AddThemeStyleboxOverride(name, style);
            }
        }

        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" })
        {
            target.RemoveThemeColorOverride(name);
        }

        // Command buttons use dark text because their normal background is
        // light.  The log toolbar sits on a dark panel, so copying those
        // colors made every action except the title appear nearly black.
        target.AddThemeColorOverride("font_color", new Color(0.94f, 0.82f, 0.58f, 1.0f));
        target.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.66f, 1.0f));
        target.AddThemeColorOverride("font_pressed_color", new Color(0.82f, 0.67f, 0.4f, 1.0f));
        target.AddThemeColorOverride("font_disabled_color", new Color(0.46f, 0.43f, 0.37f, 1.0f));
        target.AddThemeFontSizeOverride("font_size", 14);
    }

    private void UpdateFilterButtonText()
    {
        if (_selfFactionButton != null)
        {
            _selfFactionButton.Text = GetLocalizedText("ui.gameplay_log_filter_self", "Self");
        }

        if (_allLogButton != null)
        {
            _allLogButton.Text = GetLocalizedText("ui.gameplay_log_filter_all", "All");
        }

        if (_clearLogButton != null)
        {
            _clearLogButton.Text = GetLocalizedText("ui.gameplay_log_clear", "Clear Log");
        }

        UpdateCopyButtonText();
    }

    private void UpdateFilterButtonVisualState()
    {
        UpdateSingleButtonState(_selfFactionButton, _filterMode == LogFilterMode.SelfFaction);
        UpdateSingleButtonState(_allLogButton, _filterMode == LogFilterMode.All);
        UpdateSingleButtonState(_clearLogButton, false);
    }

    private static void UpdateSingleButtonState(Button? button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        button.Modulate = selected
            ? Colors.White
            : new Color(0.82f, 0.82f, 0.82f, 0.92f);
    }

    private void OnSelfFactionFilterPressed()
    {
        _filterMode = LogFilterMode.SelfFaction;
        UpdateFilterButtonVisualState();
        UpdateCopyButtonText();
        RebuildLogView();
    }

    private void OnAllLogFilterPressed()
    {
        _filterMode = LogFilterMode.All;
        UpdateFilterButtonVisualState();
        UpdateCopyButtonText();
        RebuildLogView();
    }

    private void OnClearLogPressed()
    {
        _entries.Clear();
        UpdateCopyButtonText();
        RebuildLogView();
    }

    private void OnCopyLogPressed()
    {
        var entriesToCopy = GetFilteredEntries().ToList();
        if (entriesToCopy.Count == 0)
        {
            return;
        }

        DisplayServer.ClipboardSet(string.Join("\n", entriesToCopy.Select(entry => entry.Message)));
        if (_copyLogButton != null)
        {
            _copyLogButton.Text = GetLocalizedText("ui.gameplay_log_copied", "Copied");
        }
    }

    private void UpdateCopyButtonText()
    {
        if (_copyLogButton == null)
        {
            return;
        }

        _copyLogButton.Text = _filterMode == LogFilterMode.SelfFaction
            ? GetLocalizedText("ui.gameplay_log_copy_self", "Copy Self")
            : GetLocalizedText("ui.gameplay_log_copy_all", "Copy All");
        _copyLogButton.Disabled = !GetFilteredEntries().Any();
    }

    private string GetLocalizedText(string key, string fallback) => _context.Localization?.T(key) ?? fallback;

    private IEnumerable<LogEntry> GetFilteredEntries()
    {
        return _filterMode == LogFilterMode.SelfFaction
            ? _entries.Where(entry => entry.IsPlayerRelated)
            : _entries;
    }

    private static LogEntryType ClassifyLogEntry(string message, bool isPlayerRelated)
    {
        // Keep the existing AddLog API intact while assigning meaningful UI
        // categories for the many existing gameplay call sites.
        if (ContainsAny(message, "經濟", "收入", "產出", "糧", "金", "木材", "石材", "金屬", "馬", "人口", "商業", "農業", "economy", "income", "food", "gold", "wood", "metal", "stone", "horse"))
        {
            return LogEntryType.EconomyReport;
        }

        if (ContainsAny(message, "戰", "攻", "防", "兵", "武將", "俘", "滅", "敗", "危", "傷", "死亡", "battle", "attack", "defend", "troop", "siege", "defeat", "victory", "danger", "wounded"))
        {
            return LogEntryType.WarOrDanger;
        }

        if (ContainsAny(message, "系統", "設定", "儲存", "讀取", "新月份", "God Mode", "Debug", "測試", "system", "option", "saved", "loaded", "new month", "god mode", "debug", "test"))
        {
            return LogEntryType.SystemNotification;
        }

        return isPlayerRelated ? LogEntryType.PlayerAction : LogEntryType.GeneralInformation;
    }

    private static bool ContainsAny(string message, params string[] values) =>
        values.Any(value => message.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static Color GetLogEntryColor(LogEntryType type) => type switch
    {
        LogEntryType.PlayerAction => Color.FromHtml("#8DB9E8"),
        LogEntryType.EconomyReport => Color.FromHtml("#D6B77C"),
        LogEntryType.WarOrDanger => Color.FromHtml("#E09A7A"),
        LogEntryType.SystemNotification => Color.FromHtml("#A5AAA8"),
        _ => Color.FromHtml("#D6D6CE")
    };

    private void RebuildLogView()
    {
        var logText = _context.LogText;
        if (logText == null)
        {
            return;
        }

        logText.Clear();
        _hasEntries = false;

        foreach (var entry in GetFilteredEntries())
        {
            if (_hasEntries)
            {
                logText.Newline();
            }

            logText.PushColor(GetLogEntryColor(entry.Type));
            logText.AddText(entry.Message);
            logText.Pop();

            _hasEntries = true;
        }

        var lastLine = Mathf.Max(logText.GetLineCount() - 1, 0);
        logText.CallDeferred("scroll_to_line", lastLine);
    }
}
