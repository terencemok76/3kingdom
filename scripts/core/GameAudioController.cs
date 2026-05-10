using Godot;

namespace ThreeKingdom.Core;

public partial class GameAudioController : Node
{
    private const string DefaultBgmPath = "res://assets/bgm/bgm_main_menu_01.ogg";
    private const string SecondaryBgmPath = "res://assets/bgm/bgm_main_menu_02.ogg";
    private const string ClickSfxPath = "res://assets/sfx/click_sound.ogg";
    private const string ClickCitySfxPath = "res://assets/sfx/click_city_sound.ogg";

    public static GameAudioController? Instance { get; private set; }

    private readonly string[] _bgmPaths =
    {
        DefaultBgmPath,
        SecondaryBgmPath
    };

    private AudioStreamPlayer? _bgmPlayer;
    private AudioStreamPlayer? _sfxPlayer;
    private AudioStreamPlayer? _citySfxPlayer;
    private int _currentBgmIndex;
    private bool _bgmEnabled = true;
    private bool _sfxEnabled = true;
    private float _bgmVolume = 1.0f;
    private float _sfxVolume = 1.0f;

    public override void _Ready()
    {
        Instance = this;

        _bgmPlayer = new AudioStreamPlayer
        {
            Name = "BgmPlayer",
            Bus = "Master",
            ProcessMode = ProcessModeEnum.Always
        };
        _bgmPlayer.Finished += OnBgmFinished;
        AddChild(_bgmPlayer);

        _sfxPlayer = new AudioStreamPlayer
        {
            Name = "SfxPlayer",
            Bus = "Master",
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_sfxPlayer);

        _citySfxPlayer = new AudioStreamPlayer
        {
            Name = "CitySfxPlayer",
            Bus = "Master",
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_citySfxPlayer);

        PlayCurrentBgm();
        LoadClickSfx();
        LoadCityClickSfx();
        ApplyBgmState();
        ApplySfxState();
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    public void SetBgmEnabled(bool enabled)
    {
        _bgmEnabled = enabled;
        ApplyBgmState();
    }

    public void SetSfxEnabled(bool enabled)
    {
        _sfxEnabled = enabled;
        ApplySfxState();
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp(volume, 0.0f, 1.0f);
        ApplyBgmState();
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp(volume, 0.0f, 1.0f);
        ApplySfxState();
    }

    public void PlayClickSfx()
    {
        if (!_sfxEnabled || _sfxPlayer?.Stream == null)
        {
            return;
        }

        _sfxPlayer.Play();
    }

    public void PlayCityClickSfx()
    {
        if (!_sfxEnabled || _citySfxPlayer?.Stream == null)
        {
            return;
        }

        _citySfxPlayer.Play();
    }

    private void OnBgmFinished()
    {
        _currentBgmIndex = (_currentBgmIndex + 1) % _bgmPaths.Length;
        PlayCurrentBgm();
        ApplyBgmState();
    }

    private void PlayCurrentBgm()
    {
        if (_bgmPlayer == null)
        {
            return;
        }

        var stream = ResourceLoader.Load<AudioStream>(_bgmPaths[_currentBgmIndex]);
        if (stream == null)
        {
            GD.PushWarning($"BGM resource missing: {_bgmPaths[_currentBgmIndex]}");
            return;
        }

        _bgmPlayer.Stream = stream;
        _bgmPlayer.Play();
    }

    private void ApplyBgmState()
    {
        if (_bgmPlayer == null)
        {
            return;
        }

        _bgmPlayer.StreamPaused = !_bgmEnabled;
        _bgmPlayer.VolumeDb = _bgmEnabled ? Mathf.LinearToDb(Mathf.Max(_bgmVolume, 0.0001f)) : -80.0f;
    }

    private void LoadClickSfx()
    {
        if (_sfxPlayer == null)
        {
            return;
        }

        var stream = ResourceLoader.Load<AudioStream>(ClickSfxPath);
        if (stream == null)
        {
            GD.PushWarning($"SFX resource missing: {ClickSfxPath}");
            return;
        }

        _sfxPlayer.Stream = stream;
    }

    private void LoadCityClickSfx()
    {
        if (_citySfxPlayer == null)
        {
            return;
        }

        var stream = ResourceLoader.Load<AudioStream>(ClickCitySfxPath);
        if (stream == null)
        {
            GD.PushWarning($"SFX resource missing: {ClickCitySfxPath}");
            return;
        }

        _citySfxPlayer.Stream = stream;
    }

    private void ApplySfxState()
    {
        if (_sfxPlayer == null)
        {
            return;
        }

        _sfxPlayer.StreamPaused = !_sfxEnabled;
        _sfxPlayer.VolumeDb = _sfxEnabled ? Mathf.LinearToDb(Mathf.Max(_sfxVolume, 0.0001f)) : -80.0f;

        if (_citySfxPlayer == null)
        {
            return;
        }

        _citySfxPlayer.StreamPaused = !_sfxEnabled;
        _citySfxPlayer.VolumeDb = _sfxEnabled ? Mathf.LinearToDb(Mathf.Max(_sfxVolume, 0.0001f)) : -80.0f;
    }
}
