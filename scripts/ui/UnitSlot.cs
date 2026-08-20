using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class UnitSlot : Control
{
    [Signal] public delegate void SlotChosenEventHandler(UnitSlot slot);
    public event Action<UnitSlot>? DetailRequested;
    public event Action<UnitSlot, CardInstance>? CardDropped;

    public string Side { get; set; } = "ally";
    public int SlotIndex { get; set; }
    public UnitState? Unit { get; private set; }
    public CardInstance? PassiveCard { get; private set; }
    public string DisplayText => _info.Text;

    private string _preview = "";
    private TextureRect _portrait = null!;
    private Label _portraitPlaceholder = null!;
    private ProgressBar _hpBar = null!;
    private Label _hpText = null!;
    private Label _info = null!;
    private Label _status = null!;
    private Control _passiveBack = null!;
    private Control _selectionHighlight = null!;
    private Control _targetHighlight = null!;
    private Button _interactionArea = null!;

    public override void _Ready()
    {
        _portrait = GetNode<TextureRect>("%CharacterSprite");
        _portraitPlaceholder = GetNode<Label>("%CharacterPlaceholder");
        _hpBar = GetNode<ProgressBar>("%HpBar");
        _hpText = GetNode<Label>("%HpText");
        _info = GetNode<Label>("%UnitInfo");
        _status = GetNode<Label>("%StatusIcons");
        _passiveBack = GetNode<Control>("%PassiveCardSlot");
        _selectionHighlight = GetNode<Control>("%SelectionHighlight");
        _targetHighlight = GetNode<Control>("%TargetHighlight");
        _interactionArea = GetNode<Button>("%InteractionArea");
        _interactionArea.Pressed += () => EmitSignal(SignalName.SlotChosen, this);
        _interactionArea.GuiInput += OnInteractionInput;
        Refresh();
    }

    public void Activate() => EmitSignal(SignalName.SlotChosen, this);
    public void RequestDetail() { if (Unit != null) DetailRequested?.Invoke(this); }
    public void SetUnit(UnitState? value) { Unit = value; _preview = ""; Refresh(); }
    public void SetActionPreview(string value) { _preview = value; Refresh(); }
    public void ClearActionPreview() { _preview = ""; if (IsNodeReady()) _targetHighlight.Visible = false; Refresh(); }
    public bool SetPassive(CardInstance card) { if (PassiveCard != null || Unit == null) return false; PassiveCard = card; Refresh(); return true; }
    public CardInstance? RemovePassive() { var card = PassiveCard; PassiveCard = null; Refresh(); return card; }
    public void ClearPassive() { PassiveCard = null; Refresh(); }
    public void SetSelected(bool value) { if (IsNodeReady()) _selectionHighlight.Visible = value; }

    public void Refresh()
    {
        if (!IsNodeReady()) return;
        var empty = Unit == null;
        _portrait.Visible = !empty && Unit!.Definition.artwork != null;
        _portraitPlaceholder.Visible = empty || Unit!.Definition.artwork == null;
        _hpBar.Visible = !empty;
        _hpText.Visible = !empty;
        _status.Visible = !empty;
        _passiveBack.Visible = !empty && PassiveCard != null;
        _targetHighlight.Visible = !string.IsNullOrEmpty(_preview);
        _interactionArea.Disabled = empty && Side == "enemy";
        _interactionArea.TooltipText = empty ? (Side == "ally" ? "选择英雄牌后点击这里部署" : "") : Unit!.Definition.description;

        if (empty)
        {
            _portraitPlaceholder.Text = Side == "ally" ? "＋\n空位" : "空位";
            _info.Text = Side == "ally" ? "等待部署" : "等待敌方部署";
            _status.Text = "";
            Modulate = Colors.White;
            return;
        }

        var unit = Unit!;
        _portrait.Texture = unit.Definition.artwork;
        _portraitPlaceholder.Text = $"◆\n{unit.Name}";
        _hpBar.MaxValue = Math.Max(1, unit.MaxHp);
        _hpBar.Value = Math.Clamp(unit.Hp, 0, unit.MaxHp);
        _hpText.Text = $"HP {unit.Hp}/{unit.MaxHp}";
        var identity = unit.Number > 0 && unit.Number.ToString() != unit.Name ? $"{unit.Number} · {unit.Name}" : unit.Name;
        _info.Text = unit.Alive
            ? $"{identity}　{unit.Type} ★{unit.Star}\nATK {unit.Attack}　EXP {unit.Exp}/{unit.ExpToStar}" + (string.IsNullOrEmpty(_preview) ? "" : $"\n预计：{_preview}")
            : $"{unit.Name}　已击破\n{(Side == "ally" ? "可重新部署" : "等待AI部署")}";
        _status.Text = StatusSummary(unit);
        Modulate = unit.Alive ? Colors.White : Color.FromHtml("727986");
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

    private void OnInteractionInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } || Unit == null) return;
        _interactionArea.AcceptEvent();
        DetailRequested?.Invoke(this);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return GodotObject.InstanceFromId(data.AsUInt64()) is CardTile { Card: not null } tile && CanAcceptCard(tile.Card);
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
            if (!legal) Modulate = new Color(.48f, .48f, .52f, .72f);
        }
        else if (what == NotificationDragEnd)
        {
            _targetHighlight.Visible = !string.IsNullOrEmpty(_preview);
            Modulate = Unit?.Alive == false ? Color.FromHtml("727986") : Colors.White;
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
}
