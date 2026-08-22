using Godot;

public partial class SettingsUi : Control
{
    private Label _content = null!;
    private VBoxContainer _audioPanel = null!;

    public override void _Ready()
    {
        _content = GetNode<Label>("%Content");
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        var bar = GetNode<PlayerBar>("%PlayerBar");
        var settingsButton = bar.GetNode<Button>("%SettingsButton");
        settingsButton.Disabled = true;
        settingsButton.MouseDefaultCursorShape = Control.CursorShape.Arrow;
        for (var i = 1; i <= 5; i++)
        {
            var index = i;
            GetNode<Button>($"%Tab{i}").Toggled += on => SelectTab(index, on);
        }
        CreateAudioPanel();
        GetNode<Button>("%Tab1").ButtonPressed = true;
        SelectTab(1, true);
    }

    private void SelectTab(int index, bool on)
    {
        if (!on) return;
        _audioPanel.Visible = index == 1;
        _content.Visible = index != 1;
        if (index != 1) _content.Text = $"设置项 {index}\n设置内容占位区域";
        for (var i = 1; i <= 5; i++)
        {
            if (i != index) GetNode<Button>($"%Tab{i}").SetPressedNoSignal(false);
        }
    }

    private void CreateAudioPanel()
    {
        _audioPanel = new VBoxContainer { Name = "AudioPanel", MouseFilter = MouseFilterEnum.Pass };
        _audioPanel.AddThemeConstantOverride("separation", 20);
        AddChild(_audioPanel);
        _audioPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _audioPanel.AnchorTop = .28f;
        _audioPanel.AnchorBottom = .72f;
        _audioPanel.OffsetLeft = 300;
        _audioPanel.OffsetRight = -300;
        var heading = new Label { Text = "音频设置", HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 32);
        _audioPanel.AddChild(heading);
        AddVolumeSlider("主音量", "MasterVolume", AudioManager.MasterBus);
        AddVolumeSlider("音乐音量", "MusicVolume", AudioManager.MusicBus);
        AddVolumeSlider("音效音量", "SfxVolume", AudioManager.SfxBus);
        AddVolumeSlider("语音音量", "VoiceVolume", AudioManager.VoiceBus);
    }

    private void AddVolumeSlider(string title, string nodeName, string busName)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 54) };
        row.AddThemeConstantOverride("separation", 18);
        var label = new Label { Text = title, CustomMinimumSize = new Vector2(180, 0), VerticalAlignment = VerticalAlignment.Center };
        var slider = new HSlider { Name = nodeName, MinValue = 0, MaxValue = 100, Step = 1, Value = AudioManager.Instance.GetVolume(busName) * 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var value = new Label { Text = $"{Mathf.RoundToInt((float)slider.Value)}%", CustomMinimumSize = new Vector2(80, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += amount => { value.Text = $"{Mathf.RoundToInt((float)amount)}%"; AudioManager.Instance.SetVolume(busName, (float)amount / 100f); };
        row.AddChild(label);
        row.AddChild(slider);
        row.AddChild(value);
        _audioPanel.AddChild(row);
    }
}
