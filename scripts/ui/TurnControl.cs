using Godot;

public partial class TurnControl : Control
{
    public const int SegmentCount = 16;
    public int Current { get; private set; } = BattleState.DefaultActionPoints;
    public int Maximum { get; private set; } = BattleState.DefaultActionPoints;
    public bool Suggested { get; private set; }
    public int LitSegments { get; private set; } = SegmentCount;
    private Label _actionPoints = null!;
    private Button _button = null!;
    private ShaderMaterial _ringMaterial = null!;
    private TextureRect _suggestedOverlay = null!;

    public override void _Ready()
    {
        _actionPoints = GetNode<Label>("%ActionPoints");
        _button = GetNode<Button>("%EndTurnButton");
        _ringMaterial = (ShaderMaterial)GetNode<TextureRect>("%ApLightLayer").Material;
        _suggestedOverlay = GetNode<TextureRect>("%SuggestedOverlay");
        var transparent = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            _button.AddThemeStyleboxOverride(state, transparent);
        SetProcess(true);
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        var canSuggest = Suggested && !_button.Disabled;
        var pulse = canSuggest ? .5f + Mathf.Sin((float)Time.GetTicksMsec() * .0025f) * .5f : 0f;
        _ringMaterial.SetShaderParameter("pulse", pulse);
        _suggestedOverlay.Visible = canSuggest;
        _suggestedOverlay.SelfModulate = new Color(1f, .78f, .3f, canSuggest ? .025f + pulse * .055f : 0f);
    }

    public void SetActionPoints(int current, int maximum, bool suggested)
    {
        Current = current;
        Maximum = Mathf.Max(1, maximum);
        Suggested = suggested;
        LitSegments = Mathf.Clamp(Mathf.RoundToInt(SegmentCount * Mathf.Clamp((float)Current / Maximum, 0f, 1f)), 0, SegmentCount);
        if (IsNodeReady()) UpdateVisuals();
    }

    public void SetDisabled(bool disabled)
    {
        _button.Disabled = disabled;
        if (IsNodeReady()) UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        _actionPoints.Text = $"AP {Current}/{Maximum}";
        _ringMaterial.SetShaderParameter("progress", LitSegments / (float)SegmentCount);
        _ringMaterial.SetShaderParameter("disabled", _button.Disabled);
        _ringMaterial.SetShaderParameter("active_color", Suggested ? new Color(.38f, .83f, .92f, 1f) : new Color(.196f, .784f, .949f, 1f));
        _suggestedOverlay.Visible = Suggested && !_button.Disabled;
        Modulate = _button.Disabled ? new Color(.62f, .65f, .68f, .72f) : Colors.White;
    }
}
