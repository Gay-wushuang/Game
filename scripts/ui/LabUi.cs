using Godot;

public partial class LabUi : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
    }
}