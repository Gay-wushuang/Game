using Godot;

public partial class CardTile : Button
{
    public static readonly Vector2 NativeSize = CardVisual.NativeSize;
    public event System.Action<CardInstance>? CardChosen;
    public event System.Action<CardInstance>? DetailRequested;
    public CardInstance Card { get; private set; } = null!;
    private bool _faceDown; private string _baseText = "";
    public void Setup(CardInstance value, bool showBack = false, bool small = false)
    {
        Card = value;
        _faceDown = showBack;
        CustomMinimumSize = NativeSize;
        SizeFlagsHorizontal = showBack ? SizeFlags.ExpandFill : SizeFlags.ShrinkCenter;
        SizeFlagsVertical = showBack ? SizeFlags.ExpandFill : SizeFlags.ShrinkCenter;
        foreach (var child in GetChildren()) child.QueueFree();
        Flat = false;
        ClipText = true; TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (showBack) { Text = small ? "◆" : "◆\n卡背"; TooltipText = "敌方手牌"; }
        else
        {
            var kind = Card.Definition.card_kind == CardDefinition.CardKind.Passive ? "被动" : "主动";
            var cooldown = Card.CooldownRemaining > 0 ? $"\n冷却 {Card.CooldownRemaining}" : "";
            _baseText = $"AP {Card.CurrentCost()}\n\n{Card.Definition.display_name}\n\n{kind}{cooldown}";
            Disabled = Card.CooldownRemaining > 0;
            TooltipText = Disabled ? $"冷却剩余 {Card.CooldownRemaining} 回合，暂时不能打出" : "左键选择并预览卡牌";
            if (LoadVisualScene(Card.Definition) is { } visualScene)
            {
                Text = "";
                Flat = true;
                var visual = visualScene.Instantiate<CardVisual>();
                AddChild(visual);
                CenterVisual(visual);
                visual.Bind(Card);
            }
            else Text = _baseText;
        }
        if (GetParent() is HandFan fan) fan.ArrangeCards();
    }
    public override void _Ready() { Pressed += () => CardChosen?.Invoke(Card); }
    public void RequestDetail() { if (!_faceDown) DetailRequested?.Invoke(Card); }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_faceDown) return default;
        Control preview;
        if (LoadVisualScene(Card.Definition) is { } visualScene)
        {
            var visual = visualScene.Instantiate<CardVisual>();
            visual.Bind(Card);
            preview = visual;
        }
        else
        {
            preview = new Button { Text = _baseText };
        }
        preview.CustomMinimumSize = NativeSize;
        preview.MouseFilter = MouseFilterEnum.Ignore;
        preview.Rotation = 0;
        preview.Scale = Vector2.One;
        SetDragPreview(preview);
        Rotation = 0;
        ZIndex = 60;
        return GetInstanceId();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && GetParent() is HandFan fan) fan.ArrangeCards(true);
    }
    public void SetActionPreview(string target, string result)
    {
        // Action previews are displayed by the battlefield context UI. Keeping
        // this hook avoids coupling callers to the card visual implementation.
    }
    public void ClearActionPreview()
    {
        if (_faceDown) return;
        Text = HasFormalVisual() ? "" : _baseText;
        TooltipText = Disabled ? $"冷却剩余 {Card.CooldownRemaining} 回合，暂时不能打出" : "左键选择并预览卡牌";
    }
    private static PackedScene? LoadVisualScene(CardDefinition definition)
    {
        var fileName = definition.id.ToString();
        if (fileName.StartsWith("card_", System.StringComparison.Ordinal)) fileName = fileName[5..];
        var path = $"res://scenes/ui/cards/cards/{fileName}.tscn";
        return ResourceLoader.Exists(path, "PackedScene") ? GD.Load<PackedScene>(path) : null;
    }

    private bool HasFormalVisual()
    {
        foreach (var child in GetChildren()) if (child is CardVisual) return true;
        return false;
    }

    private static void CenterVisual(CardVisual visual)
    {
        visual.SetAnchorsPreset(LayoutPreset.Center);
        visual.OffsetLeft = -NativeSize.X / 2f;
        visual.OffsetTop = -NativeSize.Y / 2f;
        visual.OffsetRight = NativeSize.X / 2f;
        visual.OffsetBottom = NativeSize.Y / 2f;
    }
}
