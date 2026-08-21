using Godot;

public partial class PrepareUi : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        GetNode<Button>("%PrevButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%NextButton").Pressed += () => SystemNotice.Instance.Show("敬请期待");
        GetNode<Button>("%StartButton").Pressed += OnStartPressed;
        for (var i = 1; i <= 8; i++)
        {
            var card = GetNode<Button>($"%Card{i}");
            var index = i;
            card.Toggled += on => SelectCard(index, on);
        }
    }

    private void SelectCard(int index, bool on)
    {
        if (!on) return;
        for (var i = 1; i <= 8; i++)
        {
            var card = GetNode<Button>($"%Card{i}");
            if (i == index) continue;
            if (card.ButtonPressed) card.SetPressedNoSignal(false);
        }
    }

    private void OnStartPressed()
    {
        var any = false;
        for (var i = 1; i <= 8; i++) any |= GetNode<Button>($"%Card{i}").ButtonPressed;
        if (!any)
        {
            SystemNotice.Instance.Show("请先选择一张卡牌");
            return;
        }
        SceneRouter.Instance.LoadAndEnter(SceneRouter.Scenes.Battle);
    }
}