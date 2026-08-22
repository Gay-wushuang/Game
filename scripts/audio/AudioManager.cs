using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum GameSfx
{
    ButtonTouch,
    ButtonClick,
    Shuffle,
    DrawCard,
    PlayCard,
    NextRound,
    LevelUp,
    HeroDies,
    Recover,
    VanguardAttack,
    AssassinAttack,
    PriestAttack,
    ScoutAttack,
    VictoryVoice,
    Horn,
    ComicWhoosh,
    Advance,
    Supplies,
    CheckOut,
    RingBell
}

public partial class AudioManager : Node
{
    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";
    public const string VoiceBus = "Voice";
    private const string SettingsPath = "user://audio.cfg";

    public static AudioManager Instance { get; private set; } = null!;

    private static readonly Dictionary<GameSfx, string> SfxPaths = new()
    {
        [GameSfx.ButtonTouch] = "res://assets/audio/sfx/sfx_button_touch.wav",
        [GameSfx.ButtonClick] = "res://assets/audio/sfx/sfx_button_click.wav",
        [GameSfx.Shuffle] = "res://assets/audio/sfx/sfx_shuffle.wav",
        [GameSfx.DrawCard] = "res://assets/audio/sfx/sfx_draw_card.wav",
        [GameSfx.PlayCard] = "res://assets/audio/sfx/sfx_play_cards.wav",
        [GameSfx.NextRound] = "res://assets/audio/sfx/sfx_next_round.wav",
        [GameSfx.LevelUp] = "res://assets/audio/sfx/sfx_level_up.wav",
        [GameSfx.HeroDies] = "res://assets/audio/sfx/sfx_hero_dies.wav",
        [GameSfx.Recover] = "res://assets/audio/sfx/sfx_recover.wav",
        [GameSfx.VanguardAttack] = "res://assets/audio/sfx/sfx_vanguard_attack.wav",
        [GameSfx.AssassinAttack] = "res://assets/audio/sfx/sfx_assassin_attack.wav",
        [GameSfx.PriestAttack] = "res://assets/audio/sfx/sfx_priest_attack.wav",
        [GameSfx.ScoutAttack] = "res://assets/audio/sfx/sfx_scout_attack.wav",
        [GameSfx.VictoryVoice] = "res://assets/audio/sfx/sfx_victory_voice.wav",
        [GameSfx.Horn] = "res://assets/audio/sfx/sfx_horn.wav",
        [GameSfx.ComicWhoosh] = "res://assets/audio/sfx/sfx_comic_whoosh.wav",
        [GameSfx.Advance] = "res://assets/audio/sfx/sfx_advance.wav",
        [GameSfx.Supplies] = "res://assets/audio/sfx/sfx_supplies.wav",
        [GameSfx.CheckOut] = "res://assets/audio/sfx/sfx_check_out.wav",
        [GameSfx.RingBell] = "res://assets/audio/sfx/sfx_ring_bell.wav"
    };

    private readonly Dictionary<GameSfx, AudioStream> _sfx = new();
    private readonly List<AudioStreamPlayer> _sfxPlayers = [];
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _voice = null!;
    private AudioStream? _pendingLoop;
    private string _musicKey = "";
    private int _nextSfxPlayer;

    public override void _Ready()
    {
        Instance = this;
        EnsureAudioBuses();
        _music = CreatePlayer("MusicPlayer", MusicBus);
        _voice = CreatePlayer("VoicePlayer", VoiceBus);
        _music.Finished += ContinueMusicLoop;
        for (var i = 0; i < 12; i++) _sfxPlayers.Add(CreatePlayer($"SfxPlayer{i + 1}", SfxBus));
        LoadVolumes();
        GetTree().NodeAdded += OnNodeAdded;
        Callable.From(ConnectExistingButtons).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (GetTree() != null) GetTree().NodeAdded -= OnNodeAdded;
        _pendingLoop = null;
        _sfx.Clear();
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>()) { player.Stop(); player.Stream = null; }
        if (Instance == this) Instance = null!;
    }

    public void PlayBattleMusic(int theme = 1)
    {
        var intro = theme == 3 ? "res://assets/audio/bgm/battle_city_intro.wav" : "res://assets/audio/bgm/battle_danger_intro.wav";
        var loop = theme switch
        {
            2 => "res://assets/audio/bgm/battle_danger_loop_b.wav",
            3 => "res://assets/audio/bgm/battle_city_loop.wav",
            _ => "res://assets/audio/bgm/battle_danger_loop_a.wav"
        };
        PlayIntroAndLoop($"battle_{theme}", intro, loop);
    }

    public void PlayVictory()
    {
        PlayOutcome("victory", "res://assets/audio/bgm/battle_victory.wav");
        PlayVoice(GameSfx.VictoryVoice);
    }

    public void PlayDefeat() => PlayOutcome("defeat", "res://assets/audio/bgm/battle_defeat.wav");

    public void StopMusic()
    {
        _musicKey = "";
        _pendingLoop = null;
        _music.Stop();
    }

    public void PlaySfx(GameSfx sound, float pitchScale = 1f)
    {
        if (DisplayServer.GetName() == "headless") return;
        if (!SfxPaths.TryGetValue(sound, out var path)) return;
        if (!_sfx.TryGetValue(sound, out var stream))
        {
            stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null) { GD.PushWarning($"无法加载音效：{path}"); return; }
            _sfx[sound] = stream;
        }
        var player = _sfxPlayers.FirstOrDefault(candidate => !candidate.Playing) ?? _sfxPlayers[_nextSfxPlayer++ % _sfxPlayers.Count];
        player.Stop();
        player.Stream = stream;
        player.PitchScale = Mathf.Clamp(pitchScale, .5f, 2f);
        player.Play();
    }

    public void PlayAttackFor(string heroType)
    {
        PlaySfx(heroType switch
        {
            "先锋" => GameSfx.VanguardAttack,
            "刺客" => GameSfx.AssassinAttack,
            "祭司" => GameSfx.PriestAttack,
            _ => GameSfx.ScoutAttack
        });
    }

    public float GetVolume(string busName)
    {
        var index = AudioServer.GetBusIndex(busName);
        return index < 0 ? 1f : Mathf.DbToLinear(AudioServer.GetBusVolumeDb(index));
    }

    public void SetVolume(string busName, float value, bool save = true)
    {
        var index = AudioServer.GetBusIndex(busName);
        if (index < 0) return;
        var linear = Mathf.Clamp(value, 0f, 1f);
        AudioServer.SetBusVolumeDb(index, linear <= .001f ? -80f : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(index, linear <= .001f);
        if (save) SaveVolumes();
    }

    public static bool ValidateResources(out string error)
    {
        var required = SfxPaths.Values.Concat(new[]
        {
            "res://assets/audio/bgm/battle_danger_intro.wav",
            "res://assets/audio/bgm/battle_danger_loop_a.wav",
            "res://assets/audio/bgm/battle_danger_loop_b.wav",
            "res://assets/audio/bgm/battle_city_intro.wav",
            "res://assets/audio/bgm/battle_city_loop.wav",
            "res://assets/audio/bgm/battle_victory.wav",
            "res://assets/audio/bgm/battle_defeat.wav"
        });
        var missing = required.Where(path => !ResourceLoader.Exists(path)).ToArray();
        error = missing.Length == 0 ? "" : "缺少音频资源：" + string.Join(", ", missing);
        return missing.Length == 0;
    }

    private AudioStreamPlayer CreatePlayer(string playerName, string bus)
    {
        var player = new AudioStreamPlayer { Name = playerName, Bus = bus };
        AddChild(player);
        return player;
    }

    private void PlayIntroAndLoop(string key, string introPath, string loopPath)
    {
        if (_musicKey == key && _music.Playing) return;
        _musicKey = key;
        _pendingLoop = LoadLoop(loopPath);
        _music.Stop();
        _music.Stream = ResourceLoader.Load<AudioStream>(introPath);
        if (_music.Stream == null) { GD.PushWarning($"无法加载BGM前奏：{introPath}"); return; }
        _music.Play();
    }

    private void ContinueMusicLoop()
    {
        if (_pendingLoop == null) return;
        _music.Stream = _pendingLoop;
        _music.Play();
    }

    private void PlayOutcome(string key, string path)
    {
        _musicKey = key;
        _pendingLoop = null;
        _music.Stop();
        _music.Stream = ResourceLoader.Load<AudioStream>(path);
        if (_music.Stream != null) _music.Play();
    }

    private void PlayVoice(GameSfx sound)
    {
        if (!SfxPaths.TryGetValue(sound, out var path)) return;
        _voice.Stream = ResourceLoader.Load<AudioStream>(path);
        if (_voice.Stream != null) _voice.Play();
    }

    private static AudioStream? LoadLoop(string path)
    {
        var loaded = ResourceLoader.Load<AudioStream>(path);
        if (loaded is AudioStreamWav wav)
        {
            var copy = (AudioStreamWav)wav.Duplicate();
            copy.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            return copy;
        }
        return loaded;
    }

    private static void EnsureAudioBuses()
    {
        foreach (var name in new[] { MusicBus, SfxBus, VoiceBus })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            var index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, "Master");
        }
    }

    private void LoadVolumes()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath);
        SetVolume(MasterBus, (float)config.GetValue("audio", "master", 1f), false);
        SetVolume(MusicBus, (float)config.GetValue("audio", "music", .8f), false);
        SetVolume(SfxBus, (float)config.GetValue("audio", "sfx", .9f), false);
        SetVolume(VoiceBus, (float)config.GetValue("audio", "voice", .9f), false);
    }

    private void SaveVolumes()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master", GetVolume(MasterBus));
        config.SetValue("audio", "music", GetVolume(MusicBus));
        config.SetValue("audio", "sfx", GetVolume(SfxBus));
        config.SetValue("audio", "voice", GetVolume(VoiceBus));
        config.Save(SettingsPath);
    }

    private void OnNodeAdded(Node node)
    {
        if (node is Button button) Callable.From(() => ConnectButton(button)).CallDeferred();
    }

    private void ConnectExistingButtons()
    {
        foreach (var button in GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>()) ConnectButton(button);
    }

    private void ConnectButton(Button button)
    {
        if (!IsInstanceValid(button) || button.HasMeta("audio_connected")) return;
        button.SetMeta("audio_connected", true);
        button.MouseEntered += () => { if (!button.Disabled) PlaySfx(GameSfx.ButtonTouch); };
        button.Pressed += () => PlaySfx(GameSfx.ButtonClick);
    }
}
