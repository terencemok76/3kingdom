using System;
using Godot;
using ThreeKingdom.Core;

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

    private const float ResizeHandleSize = 30.0f;
    private const float MinimizedButtonDragThreshold = 5.0f;

    private Control? _rootControl;
    private ColorRect? _topBarBg;
    private HBoxContainer? _topBar;
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
    private Viewport? _floatingPanelViewport;

    private FloatingPanelKind _draggingFloatingPanel = FloatingPanelKind.None;
    private Vector2 _floatingPanelDragMouseOffset;
    private Vector2 _leftPanelMinimizeDragStartMouse;
    private bool _leftPanelMinimizeDragCandidate;
    private bool _leftPanelMinimizeDragged;
    private bool _floatingPanelsInitialized;
    private int _pendingFloatingPanelRefreshFrames;

    private void InitializeFloatingPanels()
    {
        if (_floatingPanelsInitialized || _mainHudUiController == null)
        {
            return;
        }

        _rootControl = GetNodeOrNull<Control>("Root");
        _topBarBg = GetNodeOrNull<ColorRect>("Root/TopBarBg");
        _topBar = GetNodeOrNull<HBoxContainer>("Root/TopBar");
        var dateLabel = GetNodeOrNull<Label>("Root/TopBar/DateLabel");
        var playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        var storyLabel = GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
        _leftPanelBg = GetNodeOrNull<ColorRect>("Root/LeftPanelBg");
        _leftPanelContent = GetNodeOrNull<VBoxContainer>("Root/LeftPanel");
        _logPanelBg = GetNodeOrNull<ColorRect>("Root/LogPanelBg");
        var logText = MainHudLogText;
        if (_rootControl == null || _topBarBg == null || _topBar == null || _leftPanelBg == null || _leftPanelContent == null || _logPanelBg == null || logText == null)
        {
            return;
        }

        MakeControlAbsolute(_topBarBg);
        MakeControlAbsolute(_topBar);
        MakeControlAbsolute(_leftPanelBg);
        MakeControlAbsolute(_leftPanelContent);
        MakeControlAbsolute(_logPanelBg);
        MakeControlAbsolute(logText);
        logText.Reparent(_rootControl, true);

        // The log text keeps its own mouse handling for scrolling.  Every other
        // exposed part of the framed log body is a drag surface.
        _logPanelBg.GuiInput += OnLogPanelBodyGuiInput;
        logText.GuiInput += OnLogPanelBodyGuiInput;

        _topBarBg.GuiInput += OnTopBarGuiInput;
        _topBar.GuiInput += OnTopBarGuiInput;
        foreach (var dragControl in new Control?[] { dateLabel, playerFactionLabel, storyLabel })
        {
            if (dragControl == null)
            {
                continue;
            }

            dragControl.MouseFilter = Control.MouseFilterEnum.Pass;
            dragControl.GuiInput += OnTopBarGuiInput;
        }

        ApplyPanelContentMouseFilters(_leftPanelBg);
        ApplyPanelContentMouseFilters(_leftPanelContent);
        _leftPanelBg.MouseFilter = Control.MouseFilterEnum.Stop;
        _leftPanelBg.GuiInput += OnLeftPanelBodyGuiInput;
        _leftPanelContent.MouseFilter = Control.MouseFilterEnum.Stop;
        _leftPanelContent.GuiInput += OnLeftPanelBodyGuiInput;

        _leftPanelHeader = GetNodeOrNull<PanelContainer>("Root/LeftPanelHeader");
        _leftPanelMinimizeButton = GetNodeOrNull<Button>("Root/LeftPanelMinimizeButton");
        if (_leftPanelHeader == null || _leftPanelMinimizeButton == null)
        {
            _leftPanelHeader = CreateFloatingPanelHeader(
                "LeftPanelHeader",
                _localization?.T("ui.city_panel_title") ?? "City Affairs",
                OnLeftPanelHeaderGuiInput,
                OnLeftPanelMinimizePressed,
                out _leftPanelHeaderLabel,
                out _leftPanelMinimizeButton);
            ConfigureLeftPanelMinimizeButton(_leftPanelHeader, _leftPanelHeaderLabel, _leftPanelMinimizeButton, false);
        }
        else
        {
            _leftPanelHeaderLabel = _leftPanelHeader.GetNodeOrNull<Label>("CityBarLabel");
            ConfigureLeftPanelMinimizeButton(_leftPanelHeader, _leftPanelHeaderLabel, _leftPanelMinimizeButton, true);
            // Scene-authored controls do not go through CreateFloatingPanelHeader(),
            // so they must explicitly receive the same drag handler.
            _leftPanelHeader.GuiInput += OnLeftPanelHeaderGuiInput;
            RegisterButtonClickSfx(_leftPanelMinimizeButton);
        }
        var minimizePressedCallable = Callable.From(OnLeftPanelMinimizePressed);
        if (_leftPanelMinimizeButton.IsConnected(BaseButton.SignalName.Pressed, minimizePressedCallable))
        {
            _leftPanelMinimizeButton.Pressed -= OnLeftPanelMinimizePressed;
        }
        _leftPanelMinimizeButton.GuiInput += OnLeftPanelMinimizeButtonGuiInput;
        _logPanelHeader = GetNodeOrNull<PanelContainer>("Root/LogPanelBg/LogPanelHeader");
        _logPanelMinimizeButton = GetNodeOrNull<Button>("Root/LogPanelBg/LogPanelHeader/HeaderRow/LogPanelMinimizeButton");
        _logResizeHandle = GetNodeOrNull<Button>("Root/LogPanelBg/LogResizeHandle");
        if (_logPanelHeader == null)
        {
            _logPanelHeader = CreateFloatingPanelHeader(
                "LogPanelHeader",
                _localization?.T("ui.log_panel_title") ?? "War Log",
                OnLogPanelHeaderGuiInput,
                OnLogPanelMinimizePressed,
                out _logPanelHeaderLabel,
                out _logPanelMinimizeButton);
            _logResizeHandle = CreateResizeHandle("LogResizeHandle", OnLogResizeHandleGuiInput);
        }
        else
        {
            MakeControlAbsolute(_logPanelHeader);
            _logPanelHeader.Reparent(_rootControl, true);
            MakeControlAbsolute(_logResizeHandle!);
            _logResizeHandle!.Reparent(_rootControl, true);
            _logPanelHeaderLabel = _logPanelHeader.GetNodeOrNull<Label>("HeaderRow/LogTitleLabel");
            _logPanelMinimizeButton ??= _logPanelHeader.GetNodeOrNull<Button>("HeaderRow/LogPanelMinimizeButton");
            if (_logPanelMinimizeButton == null || _logResizeHandle == null)
            {
                GD.PushError("Scene-authored battle log is missing its minimize or resize button.");
                return;
            }
            _logPanelHeader.GuiInput += OnLogPanelHeaderGuiInput;
            _logPanelMinimizeButton.Pressed += OnLogPanelMinimizePressed;
            _logResizeHandle.GuiInput += OnLogResizeHandleGuiInput;
            RegisterButtonClickSfx(_logPanelMinimizeButton);
        }
        _logResizeHandle!.ButtonDown += StartLogPanelResize;

        _mainHudUiController.InitializeTopBarPanelBehavior(_topBarBg, _topBar);
        _mainHudUiController.InitializeCityInfoPanelBehavior(_leftPanelHeader, _leftPanelMinimizeButton, _leftPanelBg, _leftPanelContent);
        _mainHudUiController.InitializeLogPanelBehavior(_logPanelHeader, _logPanelMinimizeButton, _logResizeHandle, _logPanelBg);

        _mainHudUiController.ApplyTopBarLayout();
        _mainHudUiController.ApplyCityInfoLayout();
        _mainHudUiController.ApplyLogLayout();
        _floatingPanelViewport = GetViewport();
        _floatingPanelViewport.SizeChanged += OnFloatingPanelViewportSizeChanged;
        _floatingPanelsInitialized = true;
        RequestFloatingPanelLayoutRefresh();
    }

    private void ShutdownFloatingPanels()
    {
        if (_floatingPanelViewport != null)
        {
            _floatingPanelViewport.SizeChanged -= OnFloatingPanelViewportSizeChanged;
            _floatingPanelViewport = null;
        }
    }

    private void OnFloatingPanelViewportSizeChanged()
    {
        _mainHudUiController?.RefreshTopBarViewportLayout();
        RequestFloatingPanelLayoutRefresh();
    }

    private void ApplyPanelContentMouseFilters(Control root)
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

            control.MouseFilter = Control.MouseFilterEnum.Stop;
            control.GuiInput += OnLeftPanelBodyGuiInput;

            ApplyPanelContentMouseFilters(control);
        }
    }

    private void RefreshFloatingPanelLayoutsDeferred()
    {
        if (!_floatingPanelsInitialized || _mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.ApplyTopBarLayout();
        _mainHudUiController.ApplyCityInfoLayout();
        _mainHudUiController.ApplyLogLayout();
        SaveOptionSettings();
    }

    private void RefreshFloatingPanelTitleText()
    {
        _mainHudUiController?.RefreshPanelTitles();
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
        Action<InputEvent> guiInputHandler,
        Action toggleAction,
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
        minimizeButton.Pressed += toggleAction;
        row.AddChild(minimizeButton);

        header.GuiInput += @event => guiInputHandler(@event);
        _rootControl?.AddChild(header);
        return header;
    }

    private void ConfigureLeftPanelMinimizeButton(PanelContainer header, Label? headerLabel, Button minimizeButton, bool preserveSceneLayout)
    {
        var icon = GD.Load<Texture2D>("res://assets/ui/icons/128x128/minus.png");
        if (icon == null)
        {
            return;
        }

        minimizeButton.Text = string.Empty;
        minimizeButton.Icon = icon;
        minimizeButton.ExpandIcon = false;
        minimizeButton.IconAlignment = HorizontalAlignment.Center;
        minimizeButton.Alignment = HorizontalAlignment.Center;
        minimizeButton.AddThemeConstantOverride("icon_max_width", 24);
        // A flat Godot button deliberately skips StyleBox backgrounds.
        // This button uses the option-button style boxes, so it must draw.
        minimizeButton.Flat = false;
        if (!preserveSceneLayout)
        {
            minimizeButton.CustomMinimumSize = new Vector2(32.0f, 32.0f);
        }

        // Match the dark, gold-edged option button so the gold icon remains
        // legible against both the map and the left-panel artwork.
        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.065f, 0.05f, 0.96f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.58f, 0.4f, 0.15f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6
        };
        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.15f, 0.12f, 0.075f, 0.98f);
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.035f, 0.03f, 0.025f, 1.0f);
        minimizeButton.AddThemeStyleboxOverride("normal", normalStyle);
        minimizeButton.AddThemeStyleboxOverride("hover", hoverStyle);
        minimizeButton.AddThemeStyleboxOverride("pressed", pressedStyle);
        minimizeButton.AddThemeStyleboxOverride("focus", hoverStyle);
        if (headerLabel != null)
        {
            headerLabel.Visible = true;
            headerLabel.AddThemeFontSizeOverride("font_size", 24);
            headerLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.55f, 1.0f));
        }
        header.MouseFilter = Control.MouseFilterEnum.Stop;
        if (minimizeButton.GetParent() != _rootControl)
        {
            minimizeButton.Reparent(_rootControl, false);
            if (_leftPanelBg != null)
            {
                minimizeButton.Position = _leftPanelBg.Position + new Vector2(_leftPanelBg.Size.X - 44.0f, 12.0f);
                minimizeButton.Size = new Vector2(32.0f, 32.0f);
            }
        }
        minimizeButton.MoveToFront();
    }

    private Button CreateResizeHandle(string name, Action<InputEvent> guiInputHandler)
    {
        var handle = new Button
        {
            Name = name,
            Icon = GD.Load<Texture2D>("res://assets/ui/icons/128x128/resize.png"),
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(ResizeHandleSize, ResizeHandleSize),
            FocusMode = Control.FocusModeEnum.None
        };

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.065f, 0.05f, 0.96f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.62f, 0.43f, 0.16f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.15f, 0.12f, 0.075f, 0.98f);
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.035f, 0.03f, 0.025f, 1.0f);
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
            !mouseButton.Pressed ||
            _mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.BringTopBarToFront();
        _draggingFloatingPanel = FloatingPanelKind.Top;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - _mainHudUiController.GetTopBarPosition();
        GetViewport().SetInputAsHandled();
    }

    private void OnLeftPanelHeaderGuiInput(InputEvent @event)
    {
        OnLeftPanelBodyGuiInput(@event);
    }

    private void OnLeftPanelBodyGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion &&
            _draggingFloatingPanel == FloatingPanelKind.Left &&
            Input.IsMouseButtonPressed(MouseButton.Left) &&
            _mainHudUiController != null)
        {
            var targetPosition = GetViewport().GetMousePosition() - _floatingPanelDragMouseOffset;
            _mainHudUiController.SetCityInfoDraggedPosition(targetPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            _mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.BringCityInfoToFront();
        _draggingFloatingPanel = FloatingPanelKind.Left;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - _mainHudUiController.GetCityInfoHeaderPosition();
        GetViewport().SetInputAsHandled();
    }

    private void OnLogPanelHeaderGuiInput(InputEvent @event)
    {
        StartLogPanelDrag(@event);
    }

    private void OnLogPanelBodyGuiInput(InputEvent @event)
    {
        StartLogPanelDrag(@event);
    }

    private void StartLogPanelDrag(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            _mainHudUiController == null ||
            _logPanelHeader == null)
        {
            return;
        }

        _mainHudUiController.BringLogToFront();
        _draggingFloatingPanel = FloatingPanelKind.Log;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - _logPanelHeader.Position;
        GetViewport().SetInputAsHandled();
    }

    private void OnLeftPanelMinimizePressed()
    {
        _mainHudUiController?.ToggleCityInfoMinimized();
        SaveOptionSettings();
    }

    private void OnLeftPanelMinimizeButtonGuiInput(InputEvent @event)
    {
        if (_mainHudUiController == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _leftPanelMinimizeDragCandidate = true;
                _leftPanelMinimizeDragged = false;
                _leftPanelMinimizeDragStartMouse = mouseButton.Position;

                if (_mainHudUiController.IsCityInfoMinimized())
                {
                    _mainHudUiController.BringCityInfoToFront();
                    _floatingPanelDragMouseOffset = mouseButton.Position - _mainHudUiController.GetCityInfoHeaderPosition();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_leftPanelMinimizeDragCandidate)
            {
                return;
            }

            var wasDrag = _leftPanelMinimizeDragged;
            _leftPanelMinimizeDragCandidate = false;
            _leftPanelMinimizeDragged = false;
            if (wasDrag)
            {
                SaveOptionSettings();
            }
            else
            {
                OnLeftPanelMinimizePressed();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseMotion mouseMotion ||
            (mouseMotion.ButtonMask & MouseButtonMask.Left) == 0 ||
            !_leftPanelMinimizeDragCandidate ||
            !_mainHudUiController.IsCityInfoMinimized())
        {
            return;
        }

        if (!_leftPanelMinimizeDragged &&
            mouseMotion.Position.DistanceTo(_leftPanelMinimizeDragStartMouse) < MinimizedButtonDragThreshold)
        {
            return;
        }

        _leftPanelMinimizeDragged = true;
        var targetPosition = mouseMotion.Position - _floatingPanelDragMouseOffset;
        _mainHudUiController.SetCityInfoDraggedPosition(targetPosition);
        GetViewport().SetInputAsHandled();
    }

    private void OnLogPanelMinimizePressed()
    {
        _mainHudUiController?.ToggleLogMinimized();
        SaveOptionSettings();
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
        if (_mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.BringLogToFront();
        _draggingFloatingPanel = FloatingPanelKind.None;
        _mainHudUiController.StartLogResize(GetViewport().GetMousePosition());
        GetViewport().SetInputAsHandled();
    }

    private void UpdateFloatingPanelDragging()
    {
        if (_mainHudUiController == null)
        {
            return;
        }

        if (_mainHudUiController.IsLogResizing())
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                if (_mainHudUiController.EndLogResize())
                {
                    SaveOptionSettings();
                }
            }
            else
            {
                _mainHudUiController.UpdateLogResize(GetViewport().GetMousePosition());
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
        switch (_draggingFloatingPanel)
        {
            case FloatingPanelKind.Top:
                _mainHudUiController.SetTopBarDraggedPosition(targetPosition);
                break;
            case FloatingPanelKind.Left:
                _mainHudUiController.SetCityInfoDraggedPosition(targetPosition);
                break;
            case FloatingPanelKind.Log:
                _mainHudUiController.SetLogDraggedPosition(targetPosition);
                break;
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
        if (!_floatingPanelsInitialized || _mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.ApplyCityInfoLoadedSettings(leftMinimized, leftX, leftY, leftWidth, leftHeight);
        _mainHudUiController.ApplyTopBarLoadedSettings(topBarX, topBarY);
        _mainHudUiController.ApplyLogLoadedSettings(logMinimized, logX, logY, logWidth, logHeight);
        _mainHudUiController.ApplyTopBarLayout();
        _mainHudUiController.ApplyCityInfoLayout();
        _mainHudUiController.ApplyLogLayout();
    }

    private void PopulateFloatingPanelSettings(OptionSettingsData settings)
    {
        _mainHudUiController?.PopulateCityInfoSettings(settings);
        _mainHudUiController?.PopulateTopBarSettings(settings);
        _mainHudUiController?.PopulateLogSettings(settings);
    }

    private void RestoreDefaultFloatingPanelLayout()
    {
        if (!_floatingPanelsInitialized || _mainHudUiController == null)
        {
            return;
        }

        _mainHudUiController.RestoreTopBarLayout();
        _mainHudUiController.RestoreCityInfoLayout();
        _mainHudUiController.RestoreLogLayout();
    }
}


