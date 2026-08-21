using Godot;

public partial class CardTile : Button
{
    public event System.Action<CardInstance>? CardChosen;
    public event System.Action<CardInstance>? DetailRequested;
    public CardInstance Card { get; private set; } = null!;
    private bool _faceDown; private string _baseText = "";
    public void Setup(CardInstance value, bool showBack = false, bool small = false)
    {
        Card = value; _faceDown = showBack; SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        ClipText = true; TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (showBack) { Text = small ? "◆" : "◆\n卡背"; TooltipText = "敌方手牌"; }
        else { var kind = Card.Definition.card_kind == CardDefinition.CardKind.Passive ? "被动" : "主动"; _baseText = $"AP {Card.CurrentCost()}\n\n{Card.Definition.display_name}\n\n{kind}"; Text = _baseText; TooltipText = "右键查看完整卡牌详情"; }
    }
    public override void _Ready() { Pressed += () => CardChosen?.Invoke(Card); GuiInput += OnGuiInput; }
    public void RequestDetail() { if (!_faceDown) DetailRequested?.Invoke(Card); }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_faceDown) return default;
        var preview = new Button
        {
            Text = _baseText,
            CustomMinimumSize = new Vector2(144, 192),
            MouseFilter = MouseFilterEnum.Ignore,
            Rotation = 0,
            Scale = Vector2.One
        };
        SetDragPreview(preview);
        Rotation = 0;
        ZIndex = 60;
        return GetInstanceId();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && GetParent() is HandFan fan) fan.ArrangeCards(true);
    }
    public void SetActionPreview(string target, string result) { if (!_faceDown) Text = $"{_baseText}\n\n→ {target}\n预计：{result}"; }
    public void ClearActionPreview() { if (!_faceDown) Text = _baseText; }
    private void OnGuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } || _faceDown) return;
        AcceptEvent();
        RequestDetail();
    }
}
