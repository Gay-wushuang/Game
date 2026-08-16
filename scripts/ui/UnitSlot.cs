using Godot;
using System.Threading.Tasks;

public partial class UnitSlot : Button
{
    [Signal] public delegate void SlotChosenEventHandler(UnitSlot slot);
    public string Side { get; set; } = "ally";
    public int SlotIndex { get; set; }
    public UnitState? Unit { get; private set; }
    public CardInstance? PassiveCard { get; private set; }
    private string _preview = "";
    private TextureRect _portrait = null!;
    private Label _portraitPlaceholder = null!;
    private PanelContainer _passiveBack = null!;
    public override void _Ready() { SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill; BuildVisualLayers(); Pressed += () => EmitSignal(SignalName.SlotChosen, this); Refresh(); }
    public void SetUnit(UnitState? value) { Unit = value; _preview = ""; Refresh(); }
    public void SetActionPreview(string value) { _preview = value; Refresh(); }
    public void ClearActionPreview() { _preview = ""; Refresh(); }
    public bool SetPassive(CardInstance card) { if (PassiveCard != null || Unit == null) return false; PassiveCard = card; Refresh(); return true; }
    public CardInstance? RemovePassive() { var card = PassiveCard; PassiveCard = null; Refresh(); return card; }
    public void ClearPassive() { PassiveCard = null; Refresh(); }
    public void SetSelected(bool value) => Modulate = value ? Color.FromHtml("7de6ff") : Colors.White;
    public void Refresh()
    {
        if (Unit == null) { Text = Side == "ally" ? "＋\n空位\n等待部署" : "空位"; Disabled = Side == "enemy"; TooltipText = Side == "ally" ? "选择英雄牌后点击这里部署" : ""; _portrait.Visible = false; _portraitPlaceholder.Visible = false; _passiveBack.Visible = false; return; }
        Disabled = !Unit.Alive && Side == "enemy"; Modulate = Colors.White;
        _portrait.Texture = Unit.Definition.artwork; _portrait.Visible = _portrait.Texture != null; _portraitPlaceholder.Visible = _portrait.Texture == null; _portraitPlaceholder.Text = $"◆\n{Unit.Name}"; _passiveBack.Visible = PassiveCard != null;
        var identity = Unit.Number > 0 && Unit.Number.ToString() != Unit.Name ? $"{Unit.Number} · {Unit.Name}" : Unit.Name;
        Text = $"{identity}\n{Unit.Type} · ★{Unit.Star}\nHP {Unit.Hp}/{Unit.MaxHp}\n攻击 {Unit.Attack}　EXP {Unit.Exp}/{Unit.ExpToStar}" + (string.IsNullOrEmpty(_preview) ? "" : $"\n预计：{_preview}");
        TooltipText = Unit.Definition.description;
        if (!Unit.Alive) { Text = $"{Unit.Name}\n已击破\n{(Side == "ally" ? "可重新部署" : "等待AI部署")}"; Modulate = Color.FromHtml("666666"); }
    }
    private void BuildVisualLayers()
    {
        _portrait = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color(1, 1, 1, .38f) };
        _portrait.SetAnchorsPreset(LayoutPreset.FullRect); _portrait.OffsetLeft = 8; _portrait.OffsetTop = 8; _portrait.OffsetRight = -8; _portrait.OffsetBottom = -36; AddChild(_portrait);
        _portraitPlaceholder = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Modulate = new Color(.55f, .8f, 1, .28f) };
        _portraitPlaceholder.SetAnchorsPreset(LayoutPreset.FullRect); _portraitPlaceholder.OffsetBottom = -32; AddChild(_portraitPlaceholder);
        _passiveBack = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(48, 64), TooltipText = "背面设置的被动锦囊" };
        _passiveBack.SetAnchorsPreset(LayoutPreset.BottomRight); _passiveBack.OffsetLeft = -56; _passiveBack.OffsetTop = -76; _passiveBack.OffsetRight = -8; _passiveBack.OffsetBottom = -12;
        var style = new StyleBoxFlat { BgColor = Color.FromHtml("172b4d"), BorderColor = Color.FromHtml("75d7ff"), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5, BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2 };
        _passiveBack.AddThemeStyleboxOverride("panel", style); _passiveBack.AddChild(new Label { Text = "◆\n伏牌", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore }); AddChild(_passiveBack);
    }
    public async Task PlayDeployAnimation() { PivotOffset = Size / 2; Scale = new(0.65f, 0.65f); Modulate = new(1.4f, 1.4f, 1.4f, 0); var t = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); t.TweenProperty(this, "scale", Vector2.One, .34); t.TweenProperty(this, "modulate", Colors.White, .22); await ToSignal(t, Tween.SignalName.Finished); }
    public async Task PlayTargetFlash(Color? color = null) { var c = color ?? new Color(1.45f, 1.2f, .55f); var t = CreateTween(); t.TweenProperty(this, "modulate", c, .14); t.TweenProperty(this, "modulate", Colors.White, .14); t.TweenProperty(this, "modulate", c, .14); t.TweenProperty(this, "modulate", Colors.White, .14); await ToSignal(t, Tween.SignalName.Finished); }
}
