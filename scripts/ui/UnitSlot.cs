using Godot;
using System.Threading.Tasks;

public partial class UnitSlot : Button
{
    [Signal] public delegate void SlotChosenEventHandler(UnitSlot slot);
    public string Side { get; set; } = "ally";
    public int SlotIndex { get; set; }
    public UnitState? Unit { get; private set; }
    private string _preview = "";
    public override void _Ready() { SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill; Pressed += () => EmitSignal(SignalName.SlotChosen, this); Refresh(); }
    public void SetUnit(UnitState? value) { Unit = value; _preview = ""; Refresh(); }
    public void SetActionPreview(string value) { _preview = value; Refresh(); }
    public void ClearActionPreview() { _preview = ""; Refresh(); }
    public void SetSelected(bool value) => Modulate = value ? Color.FromHtml("7de6ff") : Colors.White;
    public void Refresh()
    {
        if (Unit == null) { Text = Side == "ally" ? "＋\n空位\n等待部署" : "空位"; Disabled = Side == "enemy"; TooltipText = Side == "ally" ? "选择英雄牌后点击这里部署" : ""; return; }
        Disabled = !Unit.Alive && Side == "enemy"; Modulate = Colors.White;
        var identity = Unit.Number > 0 && Unit.Number.ToString() != Unit.Name ? $"{Unit.Number} · {Unit.Name}" : Unit.Name;
        Text = $"{identity}\n{Unit.Type} · ★{Unit.Star}\nHP {Unit.Hp}/{Unit.MaxHp}\n攻击 {Unit.Attack}　EXP {Unit.Exp}/{Unit.ExpToStar}" + (string.IsNullOrEmpty(_preview) ? "" : $"\n预计：{_preview}");
        TooltipText = Unit.Definition.description;
        if (!Unit.Alive) { Text = $"{Unit.Name}\n已击破\n{(Side == "ally" ? "可重新部署" : "等待AI部署")}"; Modulate = Color.FromHtml("666666"); }
    }
    public async Task PlayDeployAnimation() { PivotOffset = Size / 2; Scale = new(0.65f, 0.65f); Modulate = new(1.4f, 1.4f, 1.4f, 0); var t = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); t.TweenProperty(this, "scale", Vector2.One, .34); t.TweenProperty(this, "modulate", Colors.White, .22); await ToSignal(t, Tween.SignalName.Finished); }
    public async Task PlayTargetFlash(Color? color = null) { var c = color ?? new Color(1.45f, 1.2f, .55f); var t = CreateTween(); t.TweenProperty(this, "modulate", c, .14); t.TweenProperty(this, "modulate", Colors.White, .14); t.TweenProperty(this, "modulate", c, .14); t.TweenProperty(this, "modulate", Colors.White, .14); await ToSignal(t, Tween.SignalName.Finished); }
}
