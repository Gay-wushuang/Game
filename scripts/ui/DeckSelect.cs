using Godot;

public partial class DeckSelect : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
    }
}