using Godot;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        AudioManager.Instance?.StopMusic();
        GetNode<Button>("%StartButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.ModeSelect);
        GetNode<Button>("%RecruitButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Shop);
        GetNode<Button>("%LabButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Lab);
        GetNode<Button>("%PrepareButton").Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.DeckSelect);
        GetNode<Button>("%ExitButton").Pressed += () => GetTree().Quit();
    }
}
