using Godot;

public partial class SystemNotice : Control
{
    public static SystemNotice Instance { get; private set; } = null!;
    private Label _message = null!;
    private Control _panel = null!;
    private CanvasLayer _layer = null!;

    public override void _Ready()
    {
        Instance = this;
        _message = GetNode<Label>("%Message");
        _panel = GetNode<Control>("%Panel");
        _layer = GetNode<CanvasLayer>("CanvasLayer");
        _layer.Visible = false;
        GetNode<Button>("%Backdrop").Pressed += Hide;
    }

    public void Show(string text = "敬请期待")
    {
        _message.Text = text;
        _layer.Visible = true;
        _panel.PivotOffset = _panel.Size / 2f;
        _panel.Scale = new Vector2(0.9f, 0.9f);
        var tween = CreateTween();
        tween.TweenProperty(_panel, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public new void Hide()
    {
        _layer.Visible = false;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!_layer.Visible) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { Hide(); GetViewport().SetInputAsHandled(); }
    }
}