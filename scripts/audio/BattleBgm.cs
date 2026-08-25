using Godot;
using System;

// 战斗 BGM 状态机（三态）：
//   引子1 → 1循环（无缝直切）→ 满场(10张英雄) 同步叠加 2循环鼓层 → 任意英雄死亡/3星 → 3引子+3循环（终态）。
//   只有【替换整首】（进入3、战斗结束）走 4 秒交叉淡入淡出；其余衔接全部无缝。
public partial class BattleBgm : Node
{
    private const string Intro1 = "res://assets/audio/bgm/battle_danger_intro.wav";
    private const string Loop1 = "res://assets/audio/bgm/battle_danger_loop_a.wav";
    private const string Layer2 = "res://assets/audio/bgm/battle_danger_loop_b.wav";
    private const string Intro3 = "res://assets/audio/bgm/battle_city_intro.wav";
    private const string Loop3 = "res://assets/audio/bgm/battle_city_loop.wav";

    // 主轨：引子/1循环 复用同一个播放器（无缝换流）；3引子/3循环 用专用轨
    private AudioStreamPlayer _main = null!;
    private AudioStreamPlayer _layer = null!; // 2循环鼓层，只叠加
    private AudioStreamPlayer _climax = null!; // 3 专用轨

    private bool _layerActive;
    private bool _climaxActive;
    private bool _settled;

    public override void _Ready()
    {
        _main = CreatePlayer("BgmMain");
        _layer = CreatePlayer("BgmLayer");
        _climax = CreatePlayer("BgmClimax");
        _main.Finished += ContinueMainLoop;
        _climax.Finished += ContinueClimaxLoop;
    }

    // 开局：延迟 2 秒后开始，主轨做 10 秒渐起（音量 0→满）
    public void Start()
    {
        GetTree().CreateTimer(2.0).Timeout += () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return; // 延迟期间已退出战斗则不播
            _main.Stream = GD.Load<AudioStream>(Intro1);
            _main.VolumeDb = -80f;
            var tween = CreateTween();
            tween.TweenProperty(_main, "volume_db", 0f, 10.0);
            _main.Play();
        };
    }

    // 场上铺满 10 张英雄牌：同步从头叠入 2循环（鼓层），与 1循环对齐（两轨等长）。
    // 仅在主轨正放 1循环 时生效；终态/未开始则不叠。
    public async void ActivateLayer()
    {
        if (_layerActive || _climaxActive || _settled) return;
        _layerActive = true;
        _layer.Stream = LoopCopy(Layer2);
        _layer.Play();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // 等播放器起播后才能取 playback
        _layer.GetStreamPlayback()?.Seek(_main.GetPlaybackPosition()); // 对齐当前主轨进度
    }

    // 任意英雄死亡 / 任意英雄3星：停掉全部旧轨并交叉淡入 3引子+3循环。终态，只触发一次。
    public void TriggerClimax()
    {
        if (_climaxActive || _settled) return;
        _climaxActive = true;
        _climax.Stream = GD.Load<AudioStream>(Intro3);
        _climax.VolumeDb = -80f;
        _climax.Play();
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_climax, "volume_db", 0f, 4.0).From(-80f); // 新轨 4 秒淡入
        tween.TweenProperty(_main, "volume_db", -80f, 4.0).From(0f); // 旧轨 4 秒淡出（同 start 同 end，严格同步）
        if (_layerActive) tween.TweenProperty(_layer, "volume_db", -80f, 4.0).From(0f);
    }

    // 战斗结束（胜利/失败）：全部 4 秒交叉淡出，完成后执行 then（播胜负 BGM）
    public async void FadeOutAnd(Action then)
    {
        _settled = true;
        var tween = CreateTween().SetParallel();
        if (_main.Playing) tween.TweenProperty(_main, "volume_db", -80f, 4.0);
        if (_layer.Playing) tween.TweenProperty(_layer, "volume_db", -80f, 4.0);
        if (_climax.Playing) tween.TweenProperty(_climax, "volume_db", -80f, 4.0);
        await ToSignal(tween, Tween.SignalName.Finished);
        _main.Stop(); _layer.Stop(); _climax.Stop();
        then?.Invoke();
    }

    private AudioStreamPlayer CreatePlayer(string name)
    {
        var player = new AudioStreamPlayer { Name = name, Bus = AudioManager.MusicBus };
        AddChild(player);
        return player;
    }

    // 引子播完（Finished）→ 无缝换 1循环（不停顿、不淡入淡出）
    private void ContinueMainLoop()
    {
        _main.Stream = LoopCopy(Loop1);
        _main.Play();
    }

    // 3引子播完 → 无缝换 3循环
    private void ContinueClimaxLoop()
    {
        _climax.Stream = LoopCopy(Loop3);
        _climax.Play();
    }

    // 循环副本：LoopEnd 必须显式设置为整段长度，否则循环区间非法、播放器不播（见 AudioManager.LoadLoop 同款修复）
    private static AudioStream LoopCopy(string path)
    {
        return AudioManager.LoadLoop(path)
            ?? throw new InvalidOperationException($"无法加载循环音乐：{path}");
    }
}
