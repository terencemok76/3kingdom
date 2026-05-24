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

    private const float ResizeHandleSize = 30.0f;

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

    private FloatingPanelKind _draggingFloatingPanel = FloatingPanelKind.None;
    private Vector2 _floatingPanelDragMouseOffset;
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
        var monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        var playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        var storyLabel = GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
        var leftSpacer = GetNodeOrNull<Control>("Root/TopBar/LeftSpacer");
        var rightSpacer = GetNodeOrNull<Control>("Root/TopBar/RightSpacer");
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

        _topBarBg.GuiInput += OnTopBarGuiInput;
        _topBar.GuiInput += OnTopBarGuiInput;
        foreach (var dragControl in new Control?[] { monthLabel, playerFactionLabel, storyLabel, leftSpacer, rightSpacer })
        {
            if (dragControl == null)
            {
                continue;
            }

            dragControl.MouseFilter = Control.MouseFilterEnum.Pass;
            dragControl.GuiInput += OnTopBarGuiInput;
        }

        ApplyPanelContentMouseFilters(_leftPanelContent);

        _leftPanelHeader = CreateFloatingPanelHeader(
            "LeftPanelHeader",
            _localization?.T("ui.city_panel_title") ?? "City Affairs",
            OnLeftPanelHeaderGuiInput,
            OnLeftPanelMinimizePressed,
            out _leftPanelHeaderLabel,
            out _leftPanelMinimizeButton);
        _logPanelHeader = CreateFloatingPanelHeader(
            "LogPanelHeader",
            _localization?.T("ui.log_panel_title") ?? "War Log",
            OnLogPanelHeaderGuiInput,
            OnLogPanelMinimizePressed,
            out _logPanelHeaderLabel,
            out _logPanelMinimizeButton);
        _logResizeHandle = CreateResizeHandle("LogResizeHandle", OnLogResizeHandleGuiInput);
        _logResizeHandle.ButtonDown += StartLogPanelResize;

        _mainHudUiController.InitializeTopBarPanelBehavior(_topBarBg, _topBar);
        _mainHudUiController.InitializeCityInfoPanelBehavior(_leftPanelHeader, _leftPanelMinimizeButton, _leftPanelBg, _leftPanelContent);
        _mainHudUiController.InitializeLogPanelBehavior(_logPanelHeader, _logPanelMinimizeButton, _logResizeHandle, _logPanelBg);

        _mainHudUiController.ApplyTopBarLayout();
        _mainHudUiController.ApplyCityInfoLayout();
        _mainHudUiController.ApplyLogLayout();
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

    private Button CreateResizeHandle(string name, Action<InputEvent> guiInputHandler)
    {
        var handle = new Button
        {
            Name = name,
            Text = "/",
            
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
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            _mainHudUiController == null ||
            _leftPanelHeader == null)
        {
            return;
        }

        _mainHudUiController.BringCityInfoToFront();
        _draggingFloatingPanel = FloatingPanelKind.Left;
        _floatingPanelDragMouseOffset = GetViewport().GetMousePosition() - _leftPanelHeader.Position;
        GetViewport().SetInputAsHandled();
    }

    private void OnLogPanelHeaderGuiInput(InputEvent @event)
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


