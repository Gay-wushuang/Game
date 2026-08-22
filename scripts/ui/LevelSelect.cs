using Godot;

public partial class LevelSelect : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        for (var i = 1; i <= 5; i++)
        {
            var index = i;
            GetNode<Button>($"%Level{i}").Pressed += () =>
            {
                if (index == 1) SceneRouter.Instance.GoTo(SceneRouter.Scenes.Map);
                else SystemNotice.Instance.Show("敬请期待");
            };
        }
        var saveButtons = new[] { "Save1Save", "Save1Delete", "Save2Save", "Save2Delete", "Save3Save", "Save3Delete", "Save4Save", "Save4Delete" };
        foreach (var name in saveButtons) GetNode<Button>(name).Pressed += () => SystemNotice.Instance.Show("敬请期待");
    }
}