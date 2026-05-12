using System;
using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private enum FloatingPanelKind
    {
        None,
        Top,
        Left,
        Log
    }

    private const float FloatingPanelHeaderHeight = 30.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float TopBarWidth = 720.0f;
    private const float TopBarHeight = 42.0f;
    private const float LeftPanelMinimizedWidth = 176.0f;
    private const float LeftPanelMinimumWidth = 260.0f;
    private const float LeftPanelMinimumHeight = 260.0f;
    private const float LogPanelMinimizedWidth = 200.0f;
    private const float LogPanelMinimumWidth = 320.0f;
    private const float LogPanelMinimumHeight = 140.0f;
    private const float ResizeHandleSize = 30.0f;

    private Control? _rootControl;
    private ColorRect? _topBarBg;
    private HBoxContainer? _topBar;
    private Vector2 _topBarDefaultPosition;
    private Vector2 _topBarPosition;
    private Vector2 _topBarSize;
    private Vector2 _topBarContentOffset;
    private Vector2 _topBarContentSize;

    private ColorRect? _leftPanelBg;
    private VBoxContainer? _leftPanelContent;
    private ColorRect? _logPanelBg;
    private PanelContainer? _leftPanelHeader;
    private Label? _leftPanelHeaderLabel;
    private Button? _leftPanelMinimizeButton;
    private PanelContainer? _logPanelHeader;
    private Label? _logPanelHeaderLabel;
    private Button? _logPanelMinimizeButton;
    private Button? _logResizeHandle;

    private Vector2 _leftPanelDefaultHeaderPosition;
    private Vector2 _leftPanelHeaderPosition;
    private Vector2 _leftPanelDefaultBgSize;
    private Vector2 _leftPanelBgOffset;
    private Vector2 _leftPanelBgSize;
    private Vector2 _leftPanelContentOffset;
    private Vector2 _leftPanelDefaultContentSize;
    private Vector2 _leftPanelContentSize;
    private bool _leftPanelMinimized;

    private Vector2 _logPanelDefaultHeaderPosition;
    private Vector2 _logPanelHeaderPosition;
    private Vector2 _logPanelBgOffset;
    private Vector2 _logPanelDefaultBgSize;
    private Vector2 _logPanelBgSize;
    private Vector2 _logPanelContentOffset;
    private Vector2 _logPanelDefaultContentSize;
    private Vector2 _logPanelContentSize;
    private bool _logPanelMinimized;

    private FloatingPanelKind _draggingFloatingPanel = FloatingPanelKind.None;
    private Vector2 _floatingPanelDragMouseOffset;
    private bool _isResizingLogPanel;
    private Vector2 _logResizeStartMousePosition;
    private Vector2 _logResizeStartSize;
    private bool _floatingPanelsInitialized;
    private int _pendingFloatingPanelRefreshFrames;

    private void InitializeFloatingPanels()
    {
        if (_floatingPanelsInitialized)
        {
            return;
        }

        _rootControl = GetNodeOrNull<Control>("Root");
        _topBarBg = GetNodeOrNull<ColorRect>("Root/TopBarBg");
        _topBar = GetNodeOrNull<HBoxContainer>("Root/TopBar");
        var monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        var playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        var storyLabel = GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
        var leftSpacer = GetNodeOrNull<Control>("Root/TopBar/LeftSpacer");
        var rightSpacer = GetNodeOrNull<Control>("Root/TopBar/RightSpacer");
        _leftPanelBg = GetNodeOrNull<ColorRect>("Root/LeftPanelBg");
        _leftPanelContent = GetNodeOrNull<VBoxContainer>("Root/LeftPanel");
        _logPanelBg = GetNodeOrNull<ColorRect>("Root/LogPanelBg");
        if (_rootControl == null || _topBarBg == null || _topBar == null || _leftPanelBg == null || _leftPanelContent == null || _logPanelBg == null || _logText == null)
        {
            return;
        }

        MakeControlAbsolute(_topBarBg);
        MakeControlAbsolute(_topBar);
        MakeControlAbsolute(_leftPanelBg);
        MakeControlAbsolute(_leftPanelContent);
        MakeControlAbsolute(_logPanelBg);
        MakeControlAbsolute(_logText);

        _topBarDefaultPosition = _topBarBg.Position;
        _topBarPosition = _topBarBg.Position;
        _topBarSize = new Vector2(Mathf.Min(TopBarWidth, GetViewport().GetVisibleRect().Size.X - (FloatingPanelViewportMargin * 2.0f)), TopBarHeight);
        if (_topBarSize.X < 520.0f)
        {
            _topBarSize.X = Mathf.Max(420.0f, _topBarBg.Size.X);
        }
        _topBarContentOffset = new Vector2(12.0f, 6.0f);
        _topBarContentSize = new Vector2(_topBarSize.X - 24.0f, 28.0f);
        _topBarBg.Color = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        _topBarBg.MouseFilter = Control.MouseFilterEnum.Pass;
        _topBar.MouseFilter = Control.MouseFilterEnum.Pass;
        _topBarBg.GuiInput += OnTopBarGuiInput;
        _topBar.GuiInput += OnTopBarGuiInput;
        _topBar.AddThemeConstantOverride("separation", 10);
        foreach (var dragControl in new Control?[] { monthLabel, playerFactionLabel, storyLabel, leftSpacer, rightSpacer })
        {
            if (dragControl == null)
            {
                continue;
            }

            dragControl.MouseFilter = Control.MouseFilterEnum.Pass;
            dragControl.GuiInput += OnTopBarGuiInput;
        }

        _leftPanelBg.MouseFilter = Control.MouseFilterEnum.Ignore;
        _leftPanelContent.MouseFilter = Control.MouseFilterEnum.Pass;
        _logPanelBg.MouseFilter = Control.MouseFilterEnum.Ignore;
        _logText.MouseFilter = Control.MouseFilterEnum.Ignore;
        ApplyPanelContentMouseFilters(_leftPanelContent);

        var leftPanelOriginalPosition = _leftPanelBg.Position;
        _leftPanelDefaultHeaderPosition = leftPanelOriginalPosition;
        _leftPanelHeaderPosition = leftPanelOriginalPosition;
        _leftPanelBgOffset = new Vector2(0.0f, FloatingPanelHeaderHeight);
        _leftPanelDefaultBgSize = new Vector2(_leftPanelBg.Size.X, _leftPanelBg.Size.Y - FloatingPanelHeaderHeight);
        _leftPanelBgSize = new Vector2(_leftPanelBg.Size.X, _leftPanelBg.Size.Y - FloatingPanelHeaderHeight);
        _leftPanelContentOffset = (_leftPanelContent.Position - leftPanelOriginalPosition) + new Vector2(0.0f, FloatingPanelHeaderHeight);
        _leftPanelDefaultContentSize = new Vector2(_leftPanelContent.Size.X, _leftPanelContent.Size.Y - FloatingPanelHeaderHeight);
        _leftPanelContentSize = new Vector2(_leftPanelContent.Size.X, _leftPanelContent.Size.Y - FloatingPanelHeaderHeight);

        var logPanelOriginalPosition = _logPanelBg.Position;
        _logPanelDefaultHeaderPosition = logPanelOriginalPosition;
        _logPanelHeaderPosition = logPanelOriginalPosition;
        _logPanelBgOffset = new Vector2(0.0f, FloatingPanelHeaderHeight);
        _logPanelDefaultBgSize = new Vector2(_logPanelBg.Size.X, _logPanelBg.Size.Y - FloatingPanelHeaderHeight);
        _logPanelBgSize = new Vector2(_logPanelBg.Size.X, _logPanelBg.Size.Y - FloatingPanelHeaderHeight);
        _logPanelContentOffset = (_logText.Position - logPanelOriginalPosition) + new Vector2(0.0f, FloatingPanelHeaderHeight);
        _logPanelDefaultContentSize = new Vector2(_logText.Size.X, _logText.Size.Y - FloatingPanelHeaderHeight);
        _logPanelContentSize = new Vector2(_logText.Size.X, _logText.Size.Y - FloatingPanelHeaderHeight);

        _leftPanelHeader = CreateFloatingPanelHeader(
            "LeftPanelHeader",
            _localization?.T("ui.city_panel_title") ?? "City Affairs",
            FloatingPanelKind.Left,
            out _leftPanelHeaderLabel,
            out _leftPanelMinimizeButton);
        _logPanelHeader = CreateFloatingPanelHeader(
            "LogPanelHeader",
            _localization?.T("ui.log_panel_title") ?? "War Log",
            FloatingPanelKind.Log,
            out _logPanelHeaderLabel,
            out _logPanelMinimizeButton);
        _logResizeHandle = CreateResizeHandle("LogResizeHandle", OnLogResizeHandleGuiInput);
        _logResizeHandle.ButtonDown += StartLogPanelResize;

        ApplyTopBarLayout();
        ApplyLeftPanelLayout();
        ApplyLogPanelLayout();
        _floatingPanelsInitialized = true;
        RequestFloatingPanelLayoutRefresh();
    }

    private static void ApplyPanelContentMouseFilters(Control root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Control control)
            {
                continue;
            }

            if (control is BaseButton or OptionButton or Tree or ItemList or LineEdit or TextEdit or SpinBox or HSlider or VSlider or ScrollContainer)
            {
                continue;
            }

            control.MouseFilter = control is Container
                ? Control.MouseFilterEnum.Pass
                : Control.MouseFilterEnum.Ignore;

            ApplyPanelContentMouseFilters(control);
        }
    }

    private void RefreshFloatingPanelLayoutsDeferred()
    {
        if (!_floatingPanelsInitialized)
        {
            return;
        }

        ApplyTopBarLayout();
        ApplyLeftPanelLayout();
        ApplyLogPanelLayout();
        SaveOptionSettings();
    }

    private void RefreshFloatingPanelTitleText()
    {
        if (_leftPanelHeaderLabel != null)
        {
            _leftPanelHeaderLabel.Text = _localization?.T("ui.city_panel_title") ?? "City Affairs";
        }

        if (_logPanelHeaderLabel != null)
        {
            _logPanelHeaderLabel.Text = _localization?.T("ui.log_panel_title") ?? "War Log";
        }
    }

    private void RequestFloatingPanelLayoutRefresh()
    {
        if (!_floatingPanelsInitialized)
        {
            return;
        }

        _pendingFloatingPanelRefreshFrames = Math.Max(_pendingFloatingPanelRefreshFrames, 4);
    }

    private void ProcessFloatingPanelDeferredRefresh()
    {
        if (_pendingFloatingPanelRefreshFrames <= 0)
        {
            return;
        }

        _pendingFloatingPanelRefreshFrames -= 1;
        RefreshFloatingPanelLayoutsDeferred();
    }

    private PanelContainer CreateFloatingPanelHeader(
        string name,
        string title,
        FloatingPanelKind panelKind,
        out Label headerLabel,
        out Button minimizeButton)
    {
        var header = new PanelContainer
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        var headerStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.13f, 0.11f, 0.96f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
        header.AddThemeStyleboxOverride("panel", headerStyle);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 6);
        header.AddChild(row);

        headerLabel = new Label
        {
            Text = title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.87f, 0.77f, 1.0f));
        row.AddChild(headerLabel);

        minimizeButton = new Button
        {
            Text = "-",
            CustomMinimumSize = new Vector2(28.0f, 22.0f),
            FocusMode = Control.FocusModeEnum.None
        };
        var buttonStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.62f, 0.53f, 0.36f, 0.95f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.26f, 0.2f, 0.12f, 1.0f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
        var buttonHoverStyle = (StyleBoxFlat)buttonStyle.Duplicate();
        buttonHoverStyle.BgColor = new Color(0.73f, 0.62f, 0.42f, 1.0f);
        var buttonPressedStyle = (StyleBoxFlat)buttonStyle.Duplicate();
        buttonPressedStyle.BgColor = new Color(0.46f, 0.38f, 0.25f, 1.0f);
        minimizeButton.AddThemeStyleboxOverride("normal", buttonStyle);
        minimizeButton.AddThemeStyleboxOverride("hover", buttonHoverStyle);
        minimizeButton.AddThemeStyleboxOverride("pressed", buttonPressedStyle);
        minimizeButton.AddThemeColorOverride("font_color", new Color(0.12f, 0.09f, 0.05f, 1.0f));
        RegisterButtonClickSfx(minimizeButton);
        minimizeButton.Pressed += () => ToggleFloatingPanel(panelKind);
        row.AddChild(minimizeButton);

        header.GuiInput += @event => OnFloatingPanelHeaderGuiInput(panelKind, @event);
        _rootControl?.AddChild(header);
        return header;
    }

    private Button CreateResizeHandle(string name, Action<InputEvent> guiInputHandler)
    {
        var handle = new Button
        {
            Name = name,
            Text = "↘",
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(ResizeHandleSize, ResizeHandleSize),
            FocusMode = Control.FocusModeEnum.None
        };

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.88f, 0.77f, 0.54f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.18f, 0.14f, 0.08f, 1.0f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.95f, 0.84f, 0.62f, 1.0f);
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.76f, 0.63f, 0.4f, 1.0f);
        handle.AddThemeStyleboxOverride("normal", normalStyle);
        handle.AddThemeStyleboxOverride("hover", hoverStyle);
        handle.AddThemeStyleboxOverride("pressed", pressedStyle);
        handle.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.05f, 1.0f));
        handle.AddThemeFontSizeOverride("font_size", 16);
        handle.MouseDefaultCursorShape = Control.CursorShape.Fdiagsize;
        handle.GuiInput += @event => guiInputHandler(@event);
        _rootControl?.AddChild(handle);
        handle.MoveToFront();
        return handle;
    }

    private static void MakeControlAbsolute(Control control)
    {
        if (control.HasMeta("_floating_absolute"))
        {
            return;
        }

        var parentControl = control.GetParent() as Control;
        if (parentControl == null)
        {
            return;
        }

        var globalRect = control.GetGlobalRect();
        var parentGlobalRect = parentControl.GetGlobalRect();
        control.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        control.Position = globalRect.Position - parentGlobalRect.Position;
        control.Size = globalRect.Size;
        control.SetMeta("_floating_absolute", true);
    }

    private void OnTopBarGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed)
        {
            return;
        }

        BringFloatingPanelToFront(FloatingPanelKind.Top);
        _draggingFloatingPanel = FloatingPanelKind.Top;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - _topBarPosition;
        GetViewport().SetInputAsHandled();
    }

    private void OnFloatingPanelHeaderGuiInput(FloatingPanelKind panelKind, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed)
        {
            return;
        }

        var header = panelKind == FloatingPanelKind.Left ? _leftPanelHeader : _logPanelHeader;
        if (header == null)
        {
            return;
        }

        BringFloatingPanelToFront(panelKind);
        _draggingFloatingPanel = panelKind;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - header.Position;
        GetViewport().SetInputAsHandled();
    }

    private void OnLogResizeHandleGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed)
        {
            return;
        }

        StartLogPanelResize();
    }

    private void StartLogPanelResize()
    {
        BringFloatingPanelToFront(FloatingPanelKind.Log);
        _isResizingLogPanel = true;
        _draggingFloatingPanel = FloatingPanelKind.None;
        _logResizeStartMousePosition = GetViewport().GetMousePosition();
        _logResizeStartSize = _logPanelBgSize;
        GetViewport().SetInputAsHandled();
    }

    private void BringFloatingPanelToFront(FloatingPanelKind panelKind)
    {
        switch (panelKind)
        {
            case FloatingPanelKind.Top:
                MoveFloatingControlToFront(_topBarBg);
                MoveFloatingControlToFront(_topBar);
                break;
            case FloatingPanelKind.Left:
                MoveFloatingControlToFront(_leftPanelBg);
                MoveFloatingControlToFront(_leftPanelContent);
                MoveFloatingControlToFront(_leftPanelHeader);
                break;
            case FloatingPanelKind.Log:
                MoveFloatingControlToFront(_logPanelBg);
                MoveFloatingControlToFront(_logText);
                MoveFloatingControlToFront(_logResizeHandle);
                MoveFloatingControlToFront(_logPanelHeader);
                break;
        }
    }

    private static void MoveFloatingControlToFront(CanvasItem? item)
    {
        item?.MoveToFront();
    }

    private void UpdateFloatingPanelDragging()
    {
        if (_isResizingLogPanel)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _isResizingLogPanel = false;
                SaveOptionSettings();
            }
            else
            {
                var mouseDelta = GetViewport().GetMousePosition() - _logResizeStartMousePosition;
                var viewportSize = GetViewport().GetVisibleRect().Size;
                var maxWidth = Mathf.Max(LogPanelMinimumWidth, viewportSize.X - _logPanelHeaderPosition.X - FloatingPanelViewportMargin);
                var maxHeight = Mathf.Max(LogPanelMinimumHeight, viewportSize.Y - _logPanelHeaderPosition.Y - FloatingPanelHeaderHeight - FloatingPanelViewportMargin);
                _logPanelBgSize = new Vector2(
                    Mathf.Clamp(_logResizeStartSize.X + mouseDelta.X, LogPanelMinimumWidth, maxWidth),
                    Mathf.Clamp(_logResizeStartSize.Y + mouseDelta.Y, LogPanelMinimumHeight, maxHeight));
                _logPanelContentSize = new Vector2(
                    Mathf.Max(80.0f, _logPanelBgSize.X - 12.0f),
                    Mathf.Max(60.0f, _logPanelBgSize.Y - 12.0f));
                ApplyLogPanelLayout();
            }

            return;
        }

        if (_draggingFloatingPanel == FloatingPanelKind.None)
        {
            return;
        }

        if (!Input.IsMouseButtonPressed(MouseButton.Left))
        {
            SaveOptionSettings();
            _draggingFloatingPanel = FloatingPanelKind.None;
            return;
        }

        var targetPosition = GetViewport().GetMousePosition() - _floatingPanelDragMouseOffset;
        if (_draggingFloatingPanel == FloatingPanelKind.Top)
        {
            _topBarPosition = ClampFloatingPanelPosition(targetPosition, _topBarSize.X, _topBarSize.Y);
            ApplyTopBarLayout();
        }
        else if (_draggingFloatingPanel == FloatingPanelKind.Left)
        {
            _leftPanelHeaderPosition = ClampFloatingPanelPosition(
                targetPosition,
                _leftPanelMinimized ? LeftPanelMinimizedWidth : _leftPanelHeader?.Size.X ?? 0.0f,
                CalculateLeftPanelTotalHeight());
            ApplyLeftPanelLayout();
        }
        else if (_draggingFloatingPanel == FloatingPanelKind.Log)
        {
            _logPanelHeaderPosition = ClampFloatingPanelPosition(
                targetPosition,
                _logPanelMinimized ? LogPanelMinimizedWidth : _logPanelHeader?.Size.X ?? 0.0f,
                CalculateLogPanelTotalHeight());
            ApplyLogPanelLayout();
        }
    }

    private Vector2 ClampFloatingPanelPosition(Vector2 position, float width, float height)
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
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

    private float CalculateLeftPanelTotalHeight()
    {
        return _leftPanelMinimized
            ? FloatingPanelHeaderHeight
            : Mathf.Max(
                FloatingPanelHeaderHeight + _leftPanelBgOffset.Y + _leftPanelBgSize.Y,
                FloatingPanelHeaderHeight + _leftPanelContentOffset.Y + _leftPanelContentSize.Y);
    }

    private float CalculateLogPanelTotalHeight()
    {
        return _logPanelMinimized
            ? FloatingPanelHeaderHeight
            : Mathf.Max(
                FloatingPanelHeaderHeight + _logPanelBgOffset.Y + _logPanelBgSize.Y,
                FloatingPanelHeaderHeight + _logPanelContentOffset.Y + _logPanelContentSize.Y);
    }

    private void ToggleFloatingPanel(FloatingPanelKind panelKind)
    {
        if (panelKind == FloatingPanelKind.Left)
        {
            _leftPanelMinimized = !_leftPanelMinimized;
            ApplyLeftPanelLayout();
            SaveOptionSettings();
            return;
        }

        if (panelKind == FloatingPanelKind.Log)
        {
            _logPanelMinimized = !_logPanelMinimized;
            ApplyLogPanelLayout();
            SaveOptionSettings();
        }
    }

    private void ApplyTopBarLayout()
    {
        if (_topBarBg == null || _topBar == null)
        {
            return;
        }

        _topBarPosition = ClampFloatingPanelPosition(_topBarPosition, _topBarSize.X, _topBarSize.Y);
        _topBarBg.Position = _topBarPosition;
        _topBarBg.Size = _topBarSize;
        _topBar.Position = _topBarPosition + _topBarContentOffset;
        _topBar.Size = _topBarContentSize;
    }

    private void ApplyLeftPanelLayout()
    {
        if (_leftPanelHeader == null || _leftPanelBg == null || _leftPanelContent == null || _leftPanelMinimizeButton == null)
        {
            return;
        }

        ClampLeftPanelSizeToCurrentLimits();

        var headerWidth = _leftPanelMinimized ? LeftPanelMinimizedWidth : _leftPanelBgSize.X;
        _leftPanelHeaderPosition = ClampFloatingPanelPosition(_leftPanelHeaderPosition, headerWidth, CalculateLeftPanelTotalHeight());
        _leftPanelHeader.Position = _leftPanelHeaderPosition;
        _leftPanelHeader.Size = new Vector2(headerWidth, FloatingPanelHeaderHeight);
        _leftPanelMinimizeButton.Text = _leftPanelMinimized ? "+" : "-";

        _leftPanelBg.Visible = !_leftPanelMinimized;
        _leftPanelContent.Visible = !_leftPanelMinimized;
        if (_leftPanelMinimized)
        {
            return;
        }

        _leftPanelBg.Position = _leftPanelHeaderPosition + _leftPanelBgOffset;
        _leftPanelBg.Size = _leftPanelBgSize;
        _leftPanelContent.Position = _leftPanelHeaderPosition + _leftPanelContentOffset;
        _leftPanelContent.Size = _leftPanelContentSize;
    }

    private void ApplyLogPanelLayout()
    {
        if (_logPanelHeader == null || _logPanelBg == null || _logText == null || _logPanelMinimizeButton == null)
        {
            return;
        }

        var headerWidth = _logPanelMinimized ? LogPanelMinimizedWidth : _logPanelBgSize.X;
        _logPanelHeaderPosition = ClampFloatingPanelPosition(_logPanelHeaderPosition, headerWidth, CalculateLogPanelTotalHeight());
        _logPanelHeader.Position = _logPanelHeaderPosition;
        _logPanelHeader.Size = new Vector2(headerWidth, FloatingPanelHeaderHeight);
        _logPanelMinimizeButton.Text = _logPanelMinimized ? "+" : "-";

        _logPanelBg.Visible = !_logPanelMinimized;
        _logText.Visible = !_logPanelMinimized;
        if (_logResizeHandle != null)
        {
            _logResizeHandle.Visible = !_logPanelMinimized;
        }

        if (_logPanelMinimized)
        {
            return;
        }

        _logPanelBg.Position = _logPanelHeaderPosition + _logPanelBgOffset;
        _logPanelBg.Size = _logPanelBgSize;
        _logText.Position = _logPanelHeaderPosition + _logPanelContentOffset;
        _logText.Size = _logPanelContentSize;
        if (_logResizeHandle != null)
        {
            _logResizeHandle.Position = _logPanelBg.Position + _logPanelBg.Size - new Vector2(ResizeHandleSize + 12.0f, ResizeHandleSize + 12.0f);
            _logResizeHandle.Size = new Vector2(ResizeHandleSize, ResizeHandleSize);
            _logResizeHandle.MoveToFront();
            _logResizeHandle.QueueRedraw();
        }
    }

    private void ApplyLoadedFloatingPanelSettings(
        bool leftMinimized,
        float leftX,
        float leftY,
        float leftWidth,
        float leftHeight,
        float topBarX,
        float topBarY,
        bool logMinimized,
        float logX,
        float logY,
        float logWidth,
        float logHeight)
    {
        if (!_floatingPanelsInitialized)
        {
            return;
        }

        _leftPanelMinimized = leftMinimized;
        _leftPanelHeaderPosition = new Vector2(leftX, leftY);
        if (leftWidth > 0.0f)
        {
            _leftPanelBgSize.X = _leftPanelDefaultBgSize.X;
            _leftPanelContentSize.X = _leftPanelDefaultContentSize.X;
        }

        if (leftHeight > 0.0f)
        {
            _leftPanelBgSize.Y = GetLeftPanelPreferredHeight();
            _leftPanelContentSize.Y = Mathf.Max(GetLeftPanelMinimumContentHeight(), _leftPanelBgSize.Y - 20.0f);
        }

        _topBarPosition = new Vector2(topBarX, topBarY);

        _logPanelMinimized = logMinimized;
        _logPanelHeaderPosition = new Vector2(logX, logY);
        if (logWidth > 0.0f)
        {
            _logPanelBgSize.X = logWidth;
            _logPanelContentSize.X = Mathf.Max(80.0f, logWidth - 12.0f);
        }

        if (logHeight > 0.0f)
        {
            _logPanelBgSize.Y = logHeight;
            _logPanelContentSize.Y = Mathf.Max(60.0f, logHeight - 12.0f);
        }

        ApplyTopBarLayout();
        ApplyLeftPanelLayout();
        ApplyLogPanelLayout();
    }

    private void PopulateFloatingPanelSettings(OptionSettingsData settings)
    {
        settings.LeftPanelMinimized = _leftPanelMinimized;
        settings.LeftPanelX = _leftPanelHeaderPosition.X;
        settings.LeftPanelY = _leftPanelHeaderPosition.Y;
        settings.LeftPanelWidth = _leftPanelBgSize.X;
        settings.LeftPanelHeight = _leftPanelBgSize.Y;
        settings.TopBarX = _topBarPosition.X;
        settings.TopBarY = _topBarPosition.Y;
        settings.LogPanelMinimized = _logPanelMinimized;
        settings.LogPanelX = _logPanelHeaderPosition.X;
        settings.LogPanelY = _logPanelHeaderPosition.Y;
        settings.LogPanelWidth = _logPanelBgSize.X;
        settings.LogPanelHeight = _logPanelBgSize.Y;
    }

    private void RestoreDefaultFloatingPanelLayout()
    {
        if (!_floatingPanelsInitialized)
        {
            return;
        }

        _topBarPosition = _topBarDefaultPosition;

        _leftPanelMinimized = false;
        _leftPanelHeaderPosition = _leftPanelDefaultHeaderPosition;
        _leftPanelBgSize = _leftPanelDefaultBgSize;
        _leftPanelContentSize = _leftPanelDefaultContentSize;

        _logPanelMinimized = false;
        _logPanelHeaderPosition = _logPanelDefaultHeaderPosition;
        _logPanelBgSize = _logPanelDefaultBgSize;
        _logPanelContentSize = _logPanelDefaultContentSize;

        ApplyTopBarLayout();
        ApplyLeftPanelLayout();
        ApplyLogPanelLayout();
    }

    private float GetLeftPanelMinimumContentWidth()
    {
        return Mathf.Max(180.0f, _leftPanelDefaultContentSize.X);
    }

    private float GetLeftPanelMinimumContentHeight()
    {
        var commandBottom = 0.0f;
        if (_commandsTitle != null && _commandsTitle.Visible)
        {
            commandBottom = Mathf.Max(commandBottom, _commandsTitle.Position.Y + _commandsTitle.Size.Y);
        }

        if (_commandButtons != null && _commandButtons.Visible)
        {
            commandBottom = Mathf.Max(commandBottom, _commandButtons.Position.Y + _commandButtons.Size.Y);
        }

        if (commandBottom > 0.0f)
        {
            return Mathf.Max(180.0f, commandBottom + 12.0f);
        }

        return Mathf.Max(180.0f, GetLeftPanelOccupiedContentHeight());
    }

    private float GetLeftPanelMinimumHeight()
    {
        return Mathf.Max(LeftPanelMinimumHeight, GetLeftPanelMinimumContentHeight() + 20.0f);
    }

    private float GetLeftPanelPreferredHeight()
    {
        return GetLeftPanelMinimumHeight() + 4.0f;
    }

    private void ClampLeftPanelSizeToCurrentLimits()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            return;
        }

        var fixedHeight = GetLeftPanelPreferredHeight();

        _leftPanelBgSize = new Vector2(
            _leftPanelDefaultBgSize.X,
            fixedHeight);
        _leftPanelContentSize = new Vector2(
            _leftPanelDefaultContentSize.X,
            Mathf.Max(GetLeftPanelMinimumContentHeight(), _leftPanelBgSize.Y - 20.0f));
    }

    private float GetLeftPanelOccupiedContentWidth()
    {
        if (_leftPanelContent == null)
        {
            return 180.0f;
        }

        var maxRight = 0.0f;
        foreach (var child in _leftPanelContent.GetChildren())
        {
            if (child is not Control control || !control.Visible)
            {
                continue;
            }

            maxRight = Mathf.Max(maxRight, control.Position.X + control.Size.X);
        }

        return maxRight + 8.0f;
    }

    private float GetLeftPanelOccupiedContentHeight()
    {
        if (_leftPanelContent == null)
        {
            return 180.0f;
        }

        var maxBottom = 0.0f;
        foreach (var child in _leftPanelContent.GetChildren())
        {
            if (child is not Control control || !control.Visible)
            {
                continue;
            }

            maxBottom = Mathf.Max(maxBottom, control.Position.Y + control.Size.Y);
        }

        return maxBottom + 8.0f;
    }
}
