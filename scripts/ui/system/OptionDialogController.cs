using Godot;

namespace ThreeKingdom.UI;

internal sealed class OptionDialogController
{
    private readonly SystemUiContext _context;
    private readonly System.Action _showSaveLoadDialog;
    private Window? _dialog;
    private Button? _saveLoadButton;
    private Button? _languageButton;
    private Button? _godModeButton;
    private Button? _bgmToggleButton;
    private Button? _sfxToggleButton;
    private HSlider? _bgmVolumeSlider;
    private Label? _bgmVolumeValueLabel;
    private HSlider? _sfxVolumeSlider;
    private Label? _sfxVolumeValueLabel;
    private Button? _saveSettingsButton;
    private Button? _restoreLayoutButton;
    private bool _signalsConnected;

    public OptionDialogController(SystemUiContext context, System.Action showSaveLoadDialog)
    {
        _context = context;
        _showSaveLoadDialog = showSaveLoadDialog;
    }

    public void Initialize()
    {
        _dialog = _context.OptionDialog;
        EnsureWidgets();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        RefreshText();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null)
        {
            return;
        }

        _dialog.Title = _context.GetOptionDialogTitle();

        if (_saveLoadButton != null)
        {
            _saveLoadButton.Text = _context.GetOptionSaveLoadButtonText();
        }

        if (_languageButton != null)
        {
            _languageButton.Text = _context.GetOptionLanguageButtonText();
        }

        if (_godModeButton != null)
        {
            _godModeButton.Text = _context.GetOptionGodModeButtonText();
        }

        if (_bgmToggleButton != null)
        {
            _bgmToggleButton.Text = _context.GetAudioToggleButtonText(true, _context.BgmEnabled);
        }

        if (_sfxToggleButton != null)
        {
            _sfxToggleButton.Text = _context.GetAudioToggleButtonText(false, _context.SfxEnabled);
        }

        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.SetValueNoSignal(System.Math.Round(_context.BgmVolume * 100.0f));
            _bgmVolumeSlider.Editable = _context.BgmEnabled;
            _bgmVolumeSlider.TooltipText = _context.GetBgmVolumeLabelText();
        }

        if (_bgmVolumeValueLabel != null)
        {
            _bgmVolumeValueLabel.Text = $"{System.Math.Round(_context.BgmVolume * 100.0f)}%";
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.SetValueNoSignal(System.Math.Round(_context.SfxVolume * 100.0f));
            _sfxVolumeSlider.Editable = _context.SfxEnabled;
            _sfxVolumeSlider.TooltipText = _context.GetSfxVolumeLabelText();
        }

        if (_sfxVolumeValueLabel != null)
        {
            _sfxVolumeValueLabel.Text = $"{System.Math.Round(_context.SfxVolume * 100.0f)}%";
        }

        if (_saveSettingsButton != null)
        {
            _saveSettingsButton.Text = _context.GetSaveSettingsButtonText();
        }

        if (_restoreLayoutButton != null)
        {
            _restoreLayoutButton.Text = _context.GetRestoreLayoutButtonText();
        }
    }

    private void EnsureWidgets()
    {
        var root = _dialog?.GetNodeOrNull<VBoxContainer>("OptionDialogRoot");
        if (root == null)
        {
            return;
        }

        _saveLoadButton = root.GetNodeOrNull<Button>("SaveLoadButton");
        _languageButton = root.GetNodeOrNull<Button>("LanguageButton");
        _godModeButton = root.GetNodeOrNull<Button>("GodModeButton");
        _bgmToggleButton = root.GetNodeOrNull<Button>("BgmAudioRow/BgmToggleButton");
        _sfxToggleButton = root.GetNodeOrNull<Button>("SfxAudioRow/SfxToggleButton");
        _bgmVolumeSlider = root.GetNodeOrNull<HSlider>("BgmAudioRow/BgmVolumeSlider");
        _bgmVolumeValueLabel = root.GetNodeOrNull<Label>("BgmAudioRow/BgmVolumeValueLabel");
        _sfxVolumeSlider = root.GetNodeOrNull<HSlider>("SfxAudioRow/SfxVolumeSlider");
        _sfxVolumeValueLabel = root.GetNodeOrNull<Label>("SfxAudioRow/SfxVolumeValueLabel");
        _saveSettingsButton = root.GetNodeOrNull<Button>("SaveSettingsButton");
        _restoreLayoutButton = root.GetNodeOrNull<Button>("RestoreLayoutButton");

        ApplyButtonThemes();
        ConnectSignals();
    }

    private void ApplyButtonThemes()
    {
        foreach (var button in new[]
                 {
                     _saveLoadButton,
                     _languageButton,
                     _godModeButton,
                     _bgmToggleButton,
                     _sfxToggleButton,
                     _saveSettingsButton,
                     _restoreLayoutButton
                 })
        {
            if (button != null)
            {
                _context.ApplyButtonTheme(button);
            }
        }
    }

    private void ConnectSignals()
    {
        if (_signalsConnected)
        {
            return;
        }

        if (_saveLoadButton != null)
        {
            _saveLoadButton.Pressed += OnSaveLoadPressed;
        }
        if (_languageButton != null)
        {
            _languageButton.Pressed += OnLanguagePressed;
        }
        if (_godModeButton != null)
        {
            _godModeButton.Pressed += OnGodModePressed;
        }
        if (_bgmToggleButton != null)
        {
            _bgmToggleButton.Pressed += OnBgmTogglePressed;
        }
        if (_sfxToggleButton != null)
        {
            _sfxToggleButton.Pressed += OnSfxTogglePressed;
        }
        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.ValueChanged += OnBgmVolumeChanged;
        }
        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.ValueChanged += OnSfxVolumeChanged;
        }
        if (_saveSettingsButton != null)
        {
            _saveSettingsButton.Pressed += OnSaveSettingsPressed;
        }
        if (_restoreLayoutButton != null)
        {
            _restoreLayoutButton.Pressed += OnRestoreLayoutPressed;
        }
        _signalsConnected = true;
    }

    private void OnSaveLoadPressed() => _showSaveLoadDialog();

    private void OnLanguagePressed()
    {
        _context.ToggleLanguage();
        RefreshText();
    }

    private void OnGodModePressed()
    {
        _context.ToggleGodMode();
        RefreshText();
    }

    private void OnBgmTogglePressed()
    {
        _context.BgmEnabled = !_context.BgmEnabled;
        _context.ApplyAudioSettings();
        RefreshText();
    }

    private void OnSfxTogglePressed()
    {
        _context.SfxEnabled = !_context.SfxEnabled;
        _context.ApplyAudioSettings();
        RefreshText();
    }

    private void OnBgmVolumeChanged(double value)
    {
        _context.BgmVolume = (float)(value / 100.0);
        _context.ApplyAudioSettings();
        RefreshText();
    }

    private void OnSfxVolumeChanged(double value)
    {
        _context.SfxVolume = (float)(value / 100.0);
        _context.ApplyAudioSettings();
        RefreshText();
    }

    private void OnSaveSettingsPressed()
    {
        _context.SaveOptionSettings();
        _context.AddLog(_context.GetOptionSettingsSavedMessage(), isPlayerRelated: true);
    }

    private void OnRestoreLayoutPressed()
    {
        _context.RestoreDefaultLayout();
        _context.SaveOptionSettings();
        RefreshText();
        _context.AddLog(_context.GetRestoreLayoutSavedMessage(), isPlayerRelated: true);
    }
}
