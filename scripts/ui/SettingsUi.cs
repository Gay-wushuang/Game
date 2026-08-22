using Godot;

public partial class SettingsUi : Control
{
    private Label _content = null!;

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
        GetNode<Button>("%Tab1").ButtonPressed = true;
        _content.Text = "设置项 1\n设置内容占位区域";
    }

    private void SelectTab(int index, bool on)
    {
        if (!on) return;
        _content.Text = $"设置项 {index}\n设置内容占位区域";
        for (var i = 1; i <= 5; i++)
        {
            if (i != index) GetNode<Button>($"%Tab{i}").SetPressedNoSignal(false);
        }
    }
}