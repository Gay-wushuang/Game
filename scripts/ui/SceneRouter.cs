using Godot;
using System;
using System.Collections.Generic;

public partial class SceneRouter : Node
{
    public static class Scenes
    {
        public static readonly string MainMenu = "res://scenes/main_menu.tscn";
        public static readonly string ModeSelect = "res://scenes/mode_select.tscn";
        public static readonly string LevelSelect = "res://scenes/level_select.tscn";
        public static readonly string Map = "res://scenes/map_ui.tscn";
        public static readonly string Prepare = "res://scenes/prepare_ui.tscn";
        public static readonly string Loading = "res://scenes/loading_ui.tscn";
        public static readonly string Battle = "res://scenes/training_arena.tscn";
        public static readonly string Shop = "res://scenes/shop_ui.tscn";
        public static readonly string Lab = "res://scenes/lab_ui.tscn";
        public static readonly string DeckSelect = "res://scenes/deck_select.tscn";
        public static readonly string DeckEdit = "res://scenes/deck_ui.tscn";
        public static readonly string Settings = "res://scenes/settings_ui.tscn";
    }

    public static SceneRouter Instance { get; private set; } = null!;

    // 黑幕转场完全结束（黑屏消失）时触发，供场景在视觉切换完成后接续动作（如播放开场音效）
    public event Action? TransitionCompleted;

    private readonly Stack<string> _history = new();
    private ColorRect _fade = null!;
    private bool _fading = false;
    public string CurrentScenePath { get; private set; } = "";
    public string PendingLoad { get; private set; } = "";

    // 是否正在黑幕转场中（场景 _Ready 时判断用，决定"转场后动作"立即执行还是等转场完成）
    public bool IsTransitioning => _fading;

    public override void _Ready()
    {
        Instance = this;
        CurrentScenePath = GetTree().CurrentScene.SceneFilePath;
        var layer = new CanvasLayer { Layer = 200 };
        AddChild(layer);
        _fade = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(_fade);
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public async void DoTransition(PackedScene scene)
    {
        if (_fading) return;
        _fading = true;
        var t1 = CreateTween();
        t1.TweenProperty(_fade, "color", new Color(0, 0, 0, 1), 0.3f);
        await ToSignal(t1, Tween.SignalName.Finished);
        GetTree().ChangeSceneToPacked(scene);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var t2 = CreateTween();
        t2.TweenInterval(0.15);
        t2.TweenProperty(_fade, "color", new Color(0, 0, 0, 0), 0.3f);
        await ToSignal(t2, Tween.SignalName.Finished);
        _fading = false;
        TransitionCompleted?.Invoke();
    }

    public void GoTo(string scenePath)
    {
        _history.Push(CurrentScenePath);
        ChangeTo(scenePath);
    }

    public void Back()
    {
        if (_history.Count > 0) ChangeTo(_history.Pop());
    }

    public void EnterBattle(string scenePath)
    {
        _history.Clear();
        ChangeTo(scenePath);
    }

    public void LoadAndEnter(string scenePath)
    {
        PendingLoad = scenePath;
        GoTo(Scenes.Loading);
    }

    private void ChangeTo(string scenePath)
    {
        CurrentScenePath = scenePath;
        GetTree().ChangeSceneToFile(scenePath);
    }
}
