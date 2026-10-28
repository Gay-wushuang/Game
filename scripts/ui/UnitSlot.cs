using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class UnitSlot : Control
{
    [Signal] public delegate void SlotChosenEventHandler(UnitSlot slot);
    [Signal] public delegate void SkillRequestedEventHandler(UnitSlot slot);
    public event Action<UnitSlot>? DetailRequested;
    public event Action<UnitSlot, CardInstance>? CardDropped;

    public string Side { get; set; } = "ally";
    public int SlotIndex { get; set; }
    public UnitState? Unit { get; private set; }
    public string DisplayText => _info.Text;

    private string _preview = "";
    private TextureRect _portrait = null!;
    private Label _portraitPlaceholder = null!;
    private ProgressBar _hpBar = null!;
    private Label _hpText = null!;
    private Label _classIcon = null!;
    private Label _info = null!;
    private Label _status = null!;
    private Control _selectionHighlight = null!;
    private Control _targetHighlight = null!;
    private Button _interactionArea = null!;
    private Button _contextSkillButton = null!;
    private bool _interactionEnabled = true;
    private bool _selected, _hovered, _placement, _dragTarget;
    public void SetPlacementHint(bool value) { _placement = value; UpdatePresentation(); }

    private void UpdatePresentation()
    {
        if (!IsNodeReady()) return;
        GetNode<Control>("Platform").Visible = false;
        _selectionHighlight.Visible = false;
        _targetHighlight.Visible = false;
        _classIcon.Visible = Unit != null && _selected;
        _hpText.Visible = Unit != null && (_selected || _preview.Length > 0);
        _portrait.SelfModulate = _selected ? new Color(1.2f, 1.2f, 1.2f) : _hovered ? new Color(1.1f, 1.1f, 1.1f) : Colors.White;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var target = _dragTarget || _preview.Length > 0;
        var placement = _placement && Unit?.Alive != true;
        if (!_selected && !target && !placement && !(_hovered && Unit != null)) return;
        var color = target ? new Color(1, .75f, .25f) : new Color(.49f, .9f, 1);
        if (!_selected && !target) color.A = placement ? .65f : .22f;
        var rect = new Rect2(new Vector2(12, 0), Size - new Vector2(24, 28));
        foreach (var corner in new[] { Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One })
        {
            var point = rect.Position + rect.Size * corner;
            DrawLine(point, point + new Vector2(corner.X == 0 ? 22 : -22, 0), color, 2);
            DrawLine(point, point + new Vector2(0, corner.Y == 0 ? 22 : -22), color, 2);
        }
    }

    public override void _Ready()
    {
        _portrait = GetNode<TextureRect>("%CharacterSprite");
        _portraitPlaceholder = GetNode<Label>("%CharacterPlaceholder");
        _hpBar = GetNode<ProgressBar>("%HpBar");
        _hpText = GetNode<Label>("%HpText");
        _classIcon = GetNode<Label>("%ClassIcon");
        _info = GetNode<Label>("%UnitInfo");
        _status = GetNode<Label>("%StatusIcons");
        _selectionHighlight = GetNode<Control>("%SelectionHighlight");
        _targetHighlight = GetNode<Control>("%TargetHighlight");
        _interactionArea = GetNode<Button>("%InteractionArea");
        _contextSkillButton = GetNode<Button>("%ContextSkillButton");
        _interactionArea.Pressed += () => EmitSignal(SignalName.SlotChosen, this);
        _interactionArea.MouseEntered += () => { _hovered = true; UpdatePresentation(); };
        _interactionArea.MouseExited += () => { _hovered = false; UpdatePresentation(); };
        _contextSkillButton.Pressed += () => EmitSignal(SignalName.SkillRequested, this);
        Refresh();
    }

    public void Activate() => EmitSignal(SignalName.SlotChosen, this);
    public void RequestDetail() { if (Unit != null) DetailRequested?.Invoke(this); }
    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;
        if (!IsNodeReady()) return;
        _interactionArea.Disabled = !enabled || (Unit == null && Side == "enemy");
        _interactionArea.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (!enabled) _contextSkillButton.Visible = false;
    }
    public void SetUnit(UnitState? value) { Unit = value; _preview = ""; Refresh(); }
    public void SetActionPreview(string value) { _preview = value; Refresh(); }
    public void ClearActionPreview() { _preview = ""; if (IsNodeReady()) _targetHighlight.Visible = false; Refresh(); }
    public void SetSelected(bool value)
    {
        _selected = value;
        if (!IsNodeReady()) return;
        _selectionHighlight.Visible = value;
        _contextSkillButton.Visible = value && _interactionEnabled && Side == "ally" && Unit?.Alive == true;
        if (_contextSkillButton.Visible)
        {
            _contextSkillButton.Disabled = Unit!.Cooldown > 0;
            _contextSkillButton.TooltipText = Unit.Cooldown > 0 ? $"技能冷却剩余 {Unit.Cooldown} 回合" : "使用英雄技能";
        }
        UpdatePresentation();
    }

    public void Refresh()
    {
        if (!IsNodeReady()) return;
        var empty = Unit == null;
        var portrait = empty ? null : ResolvePortrait(Unit!);
        _portrait.Visible = portrait != null;
        _portraitPlaceholder.Visible = !empty && portrait == null;
        _hpBar.Visible = !empty;
        _hpText.Visible = !empty;
        _classIcon.Visible = !empty;
        _status.Visible = !empty;
        _targetHighlight.Visible = !string.IsNullOrEmpty(_preview);
        _interactionArea.Disabled = !_interactionEnabled || (empty && Side == "enemy");
        _interactionArea.MouseFilter = _interactionEnabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _interactionArea.TooltipText = empty ? (Side == "ally" ? "选择英雄牌后点击这里部署" : "") : Unit!.Definition.description;

        if (empty)
        {
            _portraitPlaceholder.Text = Side == "ally" ? "＋\n空位" : "空位";
            _info.Text = "";
            _classIcon.Text = "";
            _status.Text = "";
            Modulate = Colors.White;
            UpdatePresentation();
            return;
        }

        var unit = Unit!;
        _portrait.Texture = portrait;
        _portraitPlaceholder.Text = $"◆\n{unit.Name}";
        _hpBar.MaxValue = Math.Max(1, unit.MaxHp);
        _hpBar.Value = Math.Clamp(unit.Hp, 0, unit.MaxHp);
        _hpText.Text = $"HP {unit.Hp}/{unit.MaxHp}";
        _classIcon.Text = $"{ClassMark(unit.Type)}　★{unit.Star}";
        _info.Text = unit.Alive
            ? (string.IsNullOrEmpty(_preview) ? "" : $"预计：{_preview}")
            : $"{unit.Name}　已击破\n{(Side == "ally" ? "可重新部署" : "等待AI部署")}";
        _interactionArea.TooltipText = $"{unit.Name} · {unit.Type}\nHP {unit.Hp}/{unit.MaxHp}　ATK {unit.Attack}　EXP {unit.Exp}/{unit.ExpToStar}\n左键选择并查看详情";
        _status.Text = StatusSummary(unit);
        Modulate = unit.Alive ? Colors.White : Color.FromHtml("727986");
        UpdatePresentation();
    }

    private static string ClassMark(string type) => type switch { "先锋" => "盾", "刺客" => "刃", "斥候" => "羽", "祭司" => "律", _ => "◇" };

    private static Texture2D? ResolvePortrait(UnitState unit)
    {
        if (unit.Definition is not HeroDefinition hero) return unit.Definition.artwork;
        return unit.Alive
            ? hero.idle_artwork ?? hero.artwork
            : hero.defeated_artwork ?? hero.idle_artwork ?? hero.artwork;
    }

    private static string StatusSummary(UnitState unit)
    {
        var values = new List<string>(5);
        if (unit.Cooldown > 0) values.Add($"CD{unit.Cooldown}");
        if (unit.ShieldTurns > 0) values.Add("盾");
        if (unit.TauntTurns > 0) values.Add("嘲");
        if (unit.DebuffTurns > 0) values.Add("弱");
        if (unit.CeasefireTurns > 0) values.Add("停");
        var visibleCount = Math.Min(4, values.Count);
        var result = string.Join(" · ", values.GetRange(0, visibleCount));
        return values.Count > 4 ? $"{result} +{values.Count - 4}" : result;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return _interactionEnabled && GodotObject.InstanceFromId(data.AsUInt64()) is CardTile { Card: not null } tile && CanAcceptCard(tile.Card);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (GodotObject.InstanceFromId(data.AsUInt64()) is CardTile { Card: not null } tile) CardDropped?.Invoke(this, tile.Card);
    }

    public override void _Notification(int what)
    {
        if (!IsNodeReady()) return;
        if (what == NotificationDragBegin)
        {
            var data = GetViewport().GuiGetDragData();
            var tile = GodotObject.InstanceFromId(data.AsUInt64()) as CardTile;
            var legal = tile?.Card != null && CanAcceptCard(tile.Card);
            _targetHighlight.Visible = legal;
            _dragTarget = legal;
            UpdatePresentation();
            if (!legal) Modulate = new Color(.48f, .48f, .52f, .72f);
        }
        else if (what == NotificationDragEnd)
        {
            _dragTarget = false;
            _targetHighlight.Visible = !string.IsNullOrEmpty(_preview);
            Modulate = Unit?.Alive == false ? Color.FromHtml("727986") : Colors.White;
            UpdatePresentation();
        }
    }

    private bool CanAcceptCard(CardInstance card)
    {
        if (Unit?.Alive != true || card.Definition.target_kind == CardDefinition.TargetKind.None) return false;
        var target = card.Definition.target_kind;
        return Side == "enemy"
            ? target is CardDefinition.TargetKind.Enemy or CardDefinition.TargetKind.AnyUnit or CardDefinition.TargetKind.AllyEnemyPair
            : target is not CardDefinition.TargetKind.Enemy and not CardDefinition.TargetKind.None;
    }

    public async Task PlayDeployAnimation() { PivotOffset = Size / 2; Scale = new(.65f, .65f); Modulate = new(1.4f, 1.4f, 1.4f, 0); var tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); tween.TweenProperty(this, "scale", Vector2.One, .34); tween.TweenProperty(this, "modulate", Colors.White, .22); await ToSignal(tween, Tween.SignalName.Finished); }
    public async Task PlayTargetFlash(Color? color = null) { var flash = color ?? new Color(1.45f, 1.2f, .55f); var tween = CreateTween(); tween.TweenProperty(this, "modulate", flash, .14); tween.TweenProperty(this, "modulate", Colors.White, .14); tween.TweenProperty(this, "modulate", flash, .14); tween.TweenProperty(this, "modulate", Colors.White, .14); await ToSignal(tween, Tween.SignalName.Finished); }
    public Task PlayAttackPose() => PlayPose((Unit?.Definition as HeroDefinition)?.attack_artwork, new(1.55f, 1.25f, .55f));
    public Task PlaySkillPose() => PlayPose((Unit?.Definition as HeroDefinition)?.skill_artwork, new(1.25f, 1.55f, 1.75f));
    public Task PlayHitPose() => PlayPose((Unit?.Definition as HeroDefinition)?.hit_artwork, new(1.55f, .7f, .7f));

    private async Task PlayPose(Texture2D? pose, Color flash)
    {
        if (pose != null) _portrait.Texture = pose;
        await PlayTargetFlash(flash);
        Refresh();
    }
}
