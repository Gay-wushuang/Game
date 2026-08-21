using Godot;

public partial class TurnControl : Control
{
    public int Current { get; private set; } = BattleState.DefaultActionPoints;
    public int Maximum { get; private set; } = BattleState.DefaultActionPoints;
    public bool Suggested { get; private set; }
    private Label _actionPoints = null!;

    public override void _Ready()
    {
        _actionPoints = GetNode<Label>("%ActionPoints");
        var button = GetNode<Button>("%EndTurnButton");
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(.075f, .1f, .14f, .94f), new Color(.25f, .48f, .62f, .8f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(.11f, .17f, .23f, .98f), new Color(.35f, .82f, 1f, 1f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(.06f, .08f, .11f, 1f), new Color(.95f, .7f, .25f, 1f)));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(.06f, .07f, .09f, .72f), new Color(.2f, .23f, .27f, .6f)));
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta) { if (Suggested) QueueRedraw(); }

    public void SetActionPoints(int current, int maximum, bool suggested)
    {
        Current = current;
        Maximum = Mathf.Max(1, maximum);
        Suggested = suggested;
        if (IsNodeReady()) _actionPoints.Text = $"AP {Current}/{Maximum}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        var radius = Mathf.Min(Size.X, Size.Y) * .43f;
        DrawArc(center, radius, -Mathf.Pi / 2f, Mathf.Pi * 1.5f, 64, new Color(.22f, .32f, .42f, .85f), 8f, true);
        var ratio = Mathf.Clamp((float)Current / Maximum, 0f, 1f);
        var pulse = Suggested ? .78f + Mathf.Sin((float)Time.GetTicksMsec() * .008f) * .22f : .82f;
        var color = Suggested ? new Color(1f, .72f, .25f, pulse) : new Color(.3f, .82f, 1f, .9f);
        if (ratio > 0) DrawArc(center, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * ratio, 64, color, 8f, true);
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 72,
            CornerRadiusTopRight = 72,
            CornerRadiusBottomLeft = 72,
            CornerRadiusBottomRight = 72
        };
    }
}
