using Godot;

public partial class MapUi : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        GetNode<Button>("%PrevButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%NextButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%PrepareButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Prepare);
        GetNode<Button>("%MapEntry").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Prepare);
    }
}