using Godot;

namespace ThreeKingdom.UI;

internal sealed class LogPanelController
{
    private readonly MainHudUiContext _context;
    private bool _hasEntries;
    private const float FloatingPanelHeaderHeight = 30.0f;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float LogPanelMinimizedWidth = 200.0f;
    private const float LogPanelMinimumWidth = 320.0f;
    private const float LogPanelMinimumHeight = 140.0f;
    private const float ResizeHandleSize = 30.0f;
    private PanelContainer? _header;
    private Button? _minimizeButton;
    private Button? _resizeHandle;
    private ColorRect? _background;
    private Vector2 _defaultHeaderPosition;
    private Vector2 _headerPosition;
    private readonly Vector2 _backgroundOffset = new(0.0f, FloatingPanelHeaderHeight);
    private Vector2 _defaultBackgroundSize;
    private Vector2 _backgroundSize;
    private Vector2 _contentOffset;
    private Vector2 _defaultContentSize;
    private Vector2 _contentSize;
    private bool _minimized;
    private bool _isResizing;
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
            _hasEntries = !string.IsNullOrWhiteSpace(_context.LogText.Text);
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
        _defaultHeaderPosition = originalPosition;
        _headerPosition = originalPosition;
        _defaultBackgroundSize = new Vector2(_background.Size.X, _background.Size.Y - FloatingPanelHeaderHeight);
        _backgroundSize = _defaultBackgroundSize;
        _contentOffset = (logText.Position - originalPosition) + new Vector2(0.0f, FloatingPanelHeaderHeight);
        _defaultContentSize = new Vector2(logText.Size.X, logText.Size.Y - FloatingPanelHeaderHeight);
        _contentSize = _defaultContentSize;
        _background.MouseFilter = Control.MouseFilterEnum.Ignore;
        logText.MouseFilter = Control.MouseFilterEnum.Ignore;
        ApplyLayout();
    }

    public void RefreshText()
    {
    }

    public void RefreshPanelTitle()
    {
        if (_context.LogPanelHeaderLabel != null)
        {
            _context.LogPanelHeaderLabel.Text = _context.Localization?.T("ui.log_panel_title") ?? "War Log";
        }
    }

    public void AddLog(string message, bool isPlayerRelated = false)
    {
        var logText = _context.LogText;
        if (logText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_hasEntries)
        {
            logText.Newline();
        }

        if (isPlayerRelated)
        {
            logText.PushColor(new Color(0.24f, 0.43f, 0.82f, 1.0f));
        }

        logText.AddText(message);

        if (isPlayerRelated)
        {
            logText.Pop();
        }

        _hasEntries = true;
        var lastLine = Mathf.Max(logText.GetLineCount() - 1, 0);
        logText.CallDeferred("scroll_to_line", lastLine);
    }

    public Vector2 GetHeaderPosition() => _headerPosition;

    public float GetHeaderWidth() => _minimized ? LogPanelMinimizedWidth : _backgroundSize.X;

    public float GetTotalHeight()
    {
        return _minimized
            ? FloatingPanelHeaderHeight
            : Mathf.Max(
                FloatingPanelHeaderHeight + _backgroundOffset.Y + _backgroundSize.Y,
                FloatingPanelHeaderHeight + _contentOffset.Y + _contentSize.Y);
    }

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        _headerPosition = ClampPosition(targetPosition, GetHeaderWidth(), GetTotalHeight());
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
        var maxHeight = Mathf.Max(LogPanelMinimumHeight, viewportSize.Y - _headerPosition.Y - FloatingPanelHeaderHeight - FloatingPanelViewportMargin);
        _backgroundSize = new Vector2(
            Mathf.Clamp(_resizeStartSize.X + mouseDelta.X, LogPanelMinimumWidth, maxWidth),
            Mathf.Clamp(_resizeStartSize.Y + mouseDelta.Y, LogPanelMinimumHeight, maxHeight));
        _contentSize = new Vector2(
            Mathf.Max(80.0f, _backgroundSize.X - 12.0f),
            Mathf.Max(60.0f, _backgroundSize.Y - 12.0f));
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

    public void ApplyLayout()
    {
        var logText = _context.LogText;
        if (_header == null || _background == null || logText == null || _minimizeButton == null)
        {
            return;
        }

        var headerWidth = GetHeaderWidth();
        _headerPosition = ClampPosition(_headerPosition, headerWidth, GetTotalHeight());
        _header.Position = _headerPosition;
        _header.Size = new Vector2(headerWidth, FloatingPanelHeaderHeight);
        _minimizeButton.Text = _minimized ? "+" : "-";

        _background.Visible = !_minimized;
        logText.Visible = !_minimized;
        if (_resizeHandle != null)
        {
            _resizeHandle.Visible = !_minimized;
        }

        if (_minimized)
        {
            return;
        }

        _background.Position = _headerPosition + _backgroundOffset;
        _background.Size = _backgroundSize;
        logText.Position = _headerPosition + _contentOffset;
        logText.Size = _contentSize;
        if (_resizeHandle != null)
        {
            _resizeHandle.Position = _background.Position + _background.Size - new Vector2(ResizeHandleSize + 12.0f, ResizeHandleSize + 12.0f);
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
            _contentSize.X = Mathf.Max(80.0f, width - 12.0f);
        }

        if (height > 0.0f)
        {
            _backgroundSize.Y = height;
            _contentSize.Y = Mathf.Max(60.0f, height - 12.0f);
        }
    }

    public void PopulateSettings(HudController.OptionSettingsData settings)
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
}
