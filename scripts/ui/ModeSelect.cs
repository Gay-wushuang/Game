using Godot;

public partial class ModeSelect : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        GetNode<Button>("%StoryButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.LevelSelect);
        GetNode<Button>("%ChallengeButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%ArenaButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%PvpButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
    }
}