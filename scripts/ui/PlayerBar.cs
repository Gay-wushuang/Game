using Godot;

public partial class PlayerBar : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%SettingsButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Settings);
    }
}