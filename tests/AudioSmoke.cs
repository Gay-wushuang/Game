using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class AudioSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var audio = Run();
            audio.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GD.Print("CSHARP_AUDIO_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("AUDIO_SMOKE_FAILED: " + exception);
            GetTree().Quit(1);
        }
    }

    private static AudioManager Run()
    {
        var audio = AudioManager.Instance ?? throw new InvalidOperationException("AudioManager Autoload 未实例化");
        Check(AudioServer.GetBusIndex(AudioManager.MasterBus) >= 0, "缺少 Master Bus");
        Check(AudioServer.GetBusIndex(AudioManager.MusicBus) >= 0, "缺少 Music Bus");
        Check(AudioServer.GetBusIndex(AudioManager.SfxBus) >= 0, "缺少 SFX Bus");
        Check(AudioServer.GetBusIndex(AudioManager.VoiceBus) >= 0, "缺少 Voice Bus");
        Check(AudioManager.ValidateResources(out var error), error);
        Check(audio.GetChildren().OfType<AudioStreamPlayer>().Count(player => player.Bus == AudioManager.SfxBus) == 12, "SFX 并发播放器池不是12路");
        CheckLoopRegion("res://assets/audio/bgm/main_menu_theme.wav");
        CheckLoopRegion("res://assets/audio/bgm/battle_danger_loop_a.wav");
        CheckLoopRegion("res://assets/audio/bgm/battle_city_loop.wav");

        var original = audio.GetVolume(AudioManager.SfxBus);
        audio.SetVolume(AudioManager.SfxBus, .37f, false);
        Check(Mathf.IsEqualApprox(audio.GetVolume(AudioManager.SfxBus), .37f), "SFX Bus 音量写入失败");
        audio.SetVolume(AudioManager.SfxBus, original, false);

        return audio;
    }

    private static void CheckLoopRegion(string path)
    {
        var stream = AudioManager.LoadLoop(path);
        Check(stream is AudioStreamWav, $"循环音乐不是 WAV：{path}");
        var wav = (AudioStreamWav)stream!;
        Check(wav.LoopMode == AudioStreamWav.LoopModeEnum.Forward, $"循环模式错误：{path}");
        Check(wav.LoopBegin == 0, $"循环起点错误：{path}");
        Check(wav.LoopEnd > wav.LoopBegin, $"循环区间无效：{path}");
        Check(wav.LoopEnd <= Mathf.CeilToInt(wav.GetLength() * wav.MixRate), $"循环终点超过音频长度：{path}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
