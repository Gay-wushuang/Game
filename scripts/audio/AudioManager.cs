using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 音效枚举：新增音效时需同步在 SfxPaths 中补一条路径映射。
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

// 全局音频管理器（project.godot 注册为 Autoload，切场景不销毁）。
// 职责：统一管理 BGM / 音效 / 语音三路播放，
//   - 1 个 BGM 播放器（Music 总线）：主菜单循环曲、战斗 intro→loop、胜利/失败曲
//   - 1 个语音播放器（Voice 总线）：胜利语音等
//   - 12 个并发音效播放器（SFX 总线）：攻击、按钮、抽牌等声音可同时播放
// 音量通过四根总线（Master/Music/SFX/Voice）控制，保存到 user://audio.cfg。
// 所有新加入场景树的 Button 会自动绑定：悬停=ButtonTouch、点击=ButtonClick。
// 注意：assets/audio/sfx/sfx_spare_1/2/3.wav 当前未映射到任何枚举（预留素材）。
public partial class AudioManager : Node
{
    // 总线名（default_bus_layout.tres 已定义，缺失时会在 _Ready 动态创建兜底）
    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";
    public const string VoiceBus = "Voice";

    // 音量配置文件路径（user:// 用户数据目录）
    private const string SettingsPath = "user://audio.cfg";

    // 单例引用：_Ready 时赋值，供各玩法代码直接调用 AudioManager.Instance?.xxx()
    public static AudioManager Instance { get; private set; } = null!;

    // 音效枚举 -> 资源路径。第一次播放时加载并缓存到 _sfx。
    // 文件名与枚举名一一对应，无张冠李戴；用途见行尾备注。
    private static readonly Dictionary<GameSfx, string> SfxPaths = new()
    {
        [GameSfx.ButtonTouch] = "res://assets/audio/sfx/sfx_button_touch.wav", // 按钮悬停
        [GameSfx.ButtonClick] = "res://assets/audio/sfx/sfx_button_click.wav", // 按钮点击
        [GameSfx.Shuffle] = "res://assets/audio/sfx/sfx_shuffle.wav", // 洗牌
        [GameSfx.DrawCard] = "res://assets/audio/sfx/sfx_draw_card.wav", // 抽牌
        [GameSfx.PlayCard] = "res://assets/audio/sfx/sfx_play_cards.wav", // 打出锦囊
        [GameSfx.NextRound] = "res://assets/audio/sfx/sfx_next_round.wav", // 结束回合
        [GameSfx.LevelUp] = "res://assets/audio/sfx/sfx_level_up.wav", // 升星
        [GameSfx.HeroDies] = "res://assets/audio/sfx/sfx_hero_dies.wav", // 英雄死亡
        [GameSfx.Recover] = "res://assets/audio/sfx/sfx_recover.wav", // 恢复
        [GameSfx.VanguardAttack] = "res://assets/audio/sfx/sfx_vanguard_attack.wav", // 先锋攻击
        [GameSfx.AssassinAttack] = "res://assets/audio/sfx/sfx_assassin_attack.wav", // 刺客攻击
        [GameSfx.PriestAttack] = "res://assets/audio/sfx/sfx_priest_attack.wav", // 祭司攻击
        [GameSfx.ScoutAttack] = "res://assets/audio/sfx/sfx_scout_attack.wav", // 斥候攻击
        [GameSfx.VictoryVoice] = "res://assets/audio/sfx/sfx_victory_voice.wav", // 胜利语音
        [GameSfx.Horn] = "res://assets/audio/sfx/sfx_horn.wav", // 号角
        [GameSfx.ComicWhoosh] = "res://assets/audio/sfx/sfx_comic_whoosh.wav", // 漫画转场
        [GameSfx.Advance] = "res://assets/audio/sfx/sfx_advance.wav", // 英雄部署
        [GameSfx.Supplies] = "res://assets/audio/sfx/sfx_supplies.wav", // 补给
        [GameSfx.CheckOut] = "res://assets/audio/sfx/sfx_check_out.wav", // 结账
        [GameSfx.RingBell] = "res://assets/audio/sfx/sfx_ring_bell.wav" // 铃铛
    };

    // 已加载音效缓存：首次使用时 ResourceLoader.Load 后缓存复用
    private readonly Dictionary<GameSfx, AudioStream> _sfx = new();

    // 12 路并发音效播放器池（SFX 总线）
    private readonly List<AudioStreamPlayer> _sfxPlayers = [];

    // BGM 播放器与语音播放器（Music / Voice 总线）
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _voice = null!;

    // 战斗 intro 播完后要切换的 loop 段（_music.Finished 时生效）
    private AudioStream? _pendingLoop;

    // 当前 BGM 标识：防止同一首曲重复触发播放
    private string _musicKey = "";

    // 音效播放器轮换游标：12 路全忙时按顺序抢占下一个
    private int _nextSfxPlayer;

    public override void _Ready()
    {
        Instance = this;
        EnsureAudioBuses();
        _music = CreatePlayer("MusicPlayer", MusicBus);
        _voice = CreatePlayer("VoicePlayer", VoiceBus);
        _music.Finished += ContinueMusicLoop; // intro 结束自动切入循环段
        for (var i = 0; i < 12; i++) _sfxPlayers.Add(CreatePlayer($"SfxPlayer{i + 1}", SfxBus));
        LoadVolumes();
        GetTree().NodeAdded += OnNodeAdded;
        Callable.From(ConnectExistingButtons).CallDeferred(); // 开局给已有按钮补绑音效
    }

    public override void _ExitTree()
    {
        if (GetTree() != null) GetTree().NodeAdded -= OnNodeAdded;
        _pendingLoop = null;
        _sfx.Clear();
        foreach (var player in GetChildren().OfType<AudioStreamPlayer>()) { player.Stop(); player.Stream = null; }
        if (Instance == this) Instance = null!;
    }

    // 战斗 BGM：先播 intro（几秒前奏），播完自动切到对应 loop 段循环。
    // theme 1 = 危险主题甲、2 = 危险主题乙、3 = 城市主题（当前为代码参数，无玩家选择器）。
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

    // 主菜单循环曲（MainMenu._Ready 时调用）
    public void PlayMainMenuMusic() => PlayLoop("main_menu", "res://assets/audio/bgm/main_menu_theme.wav");

    // 胜利：胜利 BGM + 胜利语音
    public void PlayVictory()
    {
        PlayOutcome("victory", "res://assets/audio/bgm/battle_victory.wav");
        PlayVoice(GameSfx.VictoryVoice);
    }

    // 失败 BGM
    public void PlayDefeat() => PlayOutcome("defeat", "res://assets/audio/bgm/battle_defeat.wav");

    // 停止 BGM（切场景前调用，如进入主菜单时停战斗音乐）
    public void StopMusic()
    {
        _musicKey = "";
        _pendingLoop = null;
        _music.Stop();
    }

    // 播放音效：首次使用加载并缓存；从 12 路池中挑空闲播放器（全忙则轮换抢占）；
    // pitchScale 限制在 0.5~2.0（变调用）；volumeScale 为线性音量比例（0~1，悬停音效用 0.5 降一半）。
    public void PlaySfx(GameSfx sound, float pitchScale = 1f, float volumeScale = 1f)
    {
        if (DisplayServer.GetName() == "headless") return; // 无头测试环境不发声
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
        player.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(volumeScale, 0f, 1f)); // 每次播放前显式设置，防池复用残留音量
        player.Play();
    }

    // 按英雄职业播放对应攻击音效
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

    // 读取某总线音量（线性 0~1）
    public float GetVolume(string busName)
    {
        var index = AudioServer.GetBusIndex(busName);
        return index < 0 ? 1f : Mathf.DbToLinear(AudioServer.GetBusVolumeDb(index));
    }

    // 设置某总线音量（线性 0~1），save=true 时写入 user://audio.cfg
    public void SetVolume(string busName, float value, bool save = true)
    {
        var index = AudioServer.GetBusIndex(busName);
        if (index < 0) return;
        var linear = Mathf.Clamp(value, 0f, 1f);
        AudioServer.SetBusVolumeDb(index, linear <= .001f ? -80f : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(index, linear <= .001f); // 静音位由音量 0 联动
        if (save) SaveVolumes();
    }

    // 校验全部音频资源是否存在（启动自检用，缺失时返回第一个错误）
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
            "res://assets/audio/bgm/battle_defeat.wav",
            "res://assets/audio/bgm/main_menu_theme.wav"
        });
        var missing = required.Where(path => !ResourceLoader.Exists(path)).ToArray();
        error = missing.Length == 0 ? "" : "缺少音频资源：" + string.Join(", ", missing);
        return missing.Length == 0;
    }

    // 新建播放器并挂到 Music/SFX/Voice 指定总线
    private AudioStreamPlayer CreatePlayer(string playerName, string bus)
    {
        var player = new AudioStreamPlayer { Name = playerName, Bus = bus };
        AddChild(player);
        return player;
    }

    // 播放 intro 并缓存 loop 段：intro 结束（Finished）时由 ContinueMusicLoop 切入
    private void PlayIntroAndLoop(string key, string introPath, string loopPath)
    {
        if (_musicKey == key && _music.Playing) return; // 同一首已在播则不重复触发
        _musicKey = key;
        _pendingLoop = LoadLoop(loopPath);
        _music.Stop();
        _music.Stream = ResourceLoader.Load<AudioStream>(introPath);
        if (_music.Stream == null) { GD.PushWarning($"无法加载BGM前奏：{introPath}"); return; }
        _music.Play();
    }

    // intro 播完后切入循环段继续播
    private void ContinueMusicLoop()
    {
        if (_pendingLoop == null) return;
        _music.Stream = _pendingLoop;
        _music.Play();
    }

    // 一次性 BGM（胜利/失败），播完即止
    private void PlayOutcome(string key, string path)
    {
        _musicKey = key;
        _pendingLoop = null;
        _music.Stop();
        _music.Stream = ResourceLoader.Load<AudioStream>(path);
        if (_music.Stream != null) _music.Play();
    }

    // 循环 BGM（主菜单等）：从头循环播放，内部走 LoadLoop 取循环副本
    private void PlayLoop(string key, string path)
    {
        if (_musicKey == key && _music.Playing) return;
        _musicKey = key;
        _pendingLoop = null;
        _music.Stop();
        _music.Stream = LoadLoop(path);
        if (_music.Stream == null) { GD.PushWarning($"无法加载循环音乐：{path}"); return; }
        _music.Play();
    }

    // 语音播放（独立 Voice 总线播放器，可压 BGM 不叠音效）
    private void PlayVoice(GameSfx sound)
    {
        if (!SfxPaths.TryGetValue(sound, out var path)) return;
        _voice.Stream = ResourceLoader.Load<AudioStream>(path);
        if (_voice.Stream != null) _voice.Play();
    }

    // 加载 wav 并返回开启 Forward 循环的副本（不修改原资源）。
    // 注意：LoopBegin / LoopEnd 必须显式设置——LoopEnd 默认 0 时循环区间非法，
    // 真实音频设备上播放器初始化失败会直接停播（headless 假播放测不出来），
    // 2026-08 修复：LoopEnd = 总时长 × 采样率，实现整段无缝循环。
    internal static AudioStream? LoadLoop(string path)
    {
        var loaded = ResourceLoader.Load<AudioStream>(path);
        if (loaded is AudioStreamWav wav)
        {
            var copy = (AudioStreamWav)wav.Duplicate();
            copy.LoopBegin = 0;
            copy.LoopEnd = Math.Max(1, Mathf.FloorToInt(copy.GetLength() * copy.MixRate));
            copy.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            return copy;
        }
        return loaded;
    }

    // 确保 Music/SFX/Voice 总线存在（补齐 default_bus_layout 缺失的情况）
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

    // 从 user://audio.cfg 读取四路音量（无配置时用默认值 master=1, music=.8, sfx/voice=.9）
    private void LoadVolumes()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath);
        SetVolume(MasterBus, (float)config.GetValue("audio", "master", 1f), false);
        SetVolume(MusicBus, (float)config.GetValue("audio", "music", .8f), false);
        SetVolume(SfxBus, (float)config.GetValue("audio", "sfx", .9f), false);
        SetVolume(VoiceBus, (float)config.GetValue("audio", "voice", .9f), false);
    }

    // 保存四路音量到 user://audio.cfg（下次启动自动读取）
    private void SaveVolumes()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master", GetVolume(MasterBus));
        config.SetValue("audio", "music", GetVolume(MusicBus));
        config.SetValue("audio", "sfx", GetVolume(SfxBus));
        config.SetValue("audio", "voice", GetVolume(VoiceBus));
        config.Save(SettingsPath);
    }

    // 场景树新节点监听：Button 加入即延迟补绑音效（避免遍历时树在变）
    private void OnNodeAdded(Node node)
    {
        if (node is Button button) Callable.From(() => ConnectButton(button)).CallDeferred();
    }

    // 开局扫描已有按钮补绑（覆盖 _Ready 之前就存在的按钮）
    private void ConnectExistingButtons()
    {
        foreach (var button in GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>()) ConnectButton(button);
    }

    // 给单个按钮绑定音效：悬停=ButtonTouch、点击=ButtonClick（meta 防重复绑定）
    private void ConnectButton(Button button)
    {
        if (!IsInstanceValid(button) || button.HasMeta("audio_connected")) return;
        button.SetMeta("audio_connected", true);
        button.MouseEntered += () => { if (!button.Disabled) PlaySfx(GameSfx.ButtonTouch, 1f, .5f); }; // 悬停音效全局降 50%
        button.Pressed += () => PlaySfx(GameSfx.ButtonClick);
    }
}
