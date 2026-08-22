using Godot;

public partial class DeckEdit : Control
{
    private Label _preview = null!;
    private int _selectedCount = 0;

    public override void _Ready()
    {
        _preview = GetNode<Label>("%Preview");
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back();
        GetNode<Button>("%TabAll").Pressed += () => SelectTab("%TabAll");
        GetNode<Button>("%TabHero").Pressed += () => SelectTab("%TabHero");
        GetNode<Button>("%TabTactic").Pressed += () => SelectTab("%TabTactic");
        for (var i = 1; i <= 10; i++)
        {
            var card = GetNode<Button>($"%Card{i}");
            card.Toggled += on =>
            {
                _selectedCount += on ? 1 : -1;
                RefreshPreview();
            };
        }
        GetNode<Button>("%TabAll").ButtonPressed = true;
        RefreshPreview();
    }

    private void SelectTab(string active)
    {
        GetNode<Button>("%TabAll").SetPressedNoSignal(active == "%TabAll");
        GetNode<Button>("%TabHero").SetPressedNoSignal(active == "%TabHero");
        GetNode<Button>("%TabTactic").SetPressedNoSignal(active == "%TabTactic");
    }

    private void RefreshPreview()
    {
        _preview.Text = _selectedCount == 0 ? "已选卡牌信息占位" : $"已选卡牌：{_selectedCount} 张";
    }
}