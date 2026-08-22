using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class PassiveGate : PanelContainer
{
    public event System.Action<CardInstance>? DetailRequested;
    private static readonly Vector2 CardSize = new(72, 96);
    private const float CollapsedStep = 18f;
    private const float ExpandedStep = 84f;

    public bool Expanded { get; private set; }
    public int CardCount => PlayerCardCount + EnemyCardCount;
    public int PlayerCardCount { get; private set; }
    public int EnemyCardCount { get; private set; }
    public CardTile? ExpandedPlayerCard { get; private set; }

    private readonly List<BattleState.PlacedPassive> _playerCards = [];
    private readonly List<BattleState.PlacedPassive> _enemyCards = [];
    private Label _header = null!;
    private Control _enemyStack = null!;
    private Control _playerStack = null!;
    private PopupPanel _expandedPanel = null!;
    private VBoxContainer _expandedContent = null!;

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        var root = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _header = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(_header);
        root.AddChild(CreateSide("敌方伏牌", out _enemyStack));
        root.AddChild(CreateSide("我方伏牌", out _playerStack));
        AddChild(root);

        _expandedPanel = new PopupPanel { Unresizable = true };
        _expandedContent = new VBoxContainer();
        _expandedPanel.AddChild(_expandedContent);
        _expandedPanel.PopupHide += OnPopupHidden;
        AddChild(_expandedPanel);

        GuiInput += OnGuiInput;
        RefreshCards();
    }

    public void SetCards(IEnumerable<BattleState.PlacedPassive> cards)
    {
        _playerCards.Clear();
        _enemyCards.Clear();
        foreach (var card in cards.OrderBy(card => card.SlotIndex))
        {
            if (card.OwnerId == "player") _playerCards.Add(card);
            else _enemyCards.Add(card);
        }
        PlayerCardCount = _playerCards.Count;
        EnemyCardCount = _enemyCards.Count;
        if (IsNodeReady()) RefreshCards();
    }

    private static VBoxContainer CreateSide(string title, out Control stack)
    {
        var section = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        section.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore });
        stack = new Control { CustomMinimumSize = CardSize, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        section.AddChild(stack);
        return section;
    }

    private void RefreshCards()
    {
        _header.Text = $"被动战门  敌 {EnemyCardCount}/{BattleState.PassiveGateCapacity} · 我 {PlayerCardCount}/{BattleState.PassiveGateCapacity}\n点击横向展开";
        FillStack(_enemyStack, _enemyCards.Count, CollapsedStep, true);
        FillStack(_playerStack, _playerCards.Count, CollapsedStep, true);
        if (Expanded) RefreshExpandedPanel();
    }

    private static void FillStack(Control stack, int count, float step, bool showEmptySlot)
    {
        foreach (var child in stack.GetChildren()) child.QueueFree();
        var displayCount = Mathf.Max(count, showEmptySlot ? 1 : 0);
        for (var index = 0; index < displayCount; index++)
        {
            var card = CreateCardBack(count == 0);
            card.Position = new Vector2(index * step, 0);
            stack.AddChild(card);
        }
    }

    private static PanelContainer CreateCardBack(bool empty)
    {
        var card = new PanelContainer { CustomMinimumSize = CardSize, Size = CardSize, MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = empty ? new Color(.035f, .055f, .08f, .42f) : new Color(.045f, .09f, .17f),
            BorderColor = empty ? new Color(.22f, .34f, .43f, .55f) : new Color(.28f, .68f, .88f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
        });
        card.AddChild(new Label
        {
            Text = empty ? "空" : "✦\n伏牌\n✦",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = empty ? new Color(1, 1, 1, .35f) : new Color(.55f, .82f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        });
        return card;
    }

    private void RefreshExpandedPanel()
    {
        ExpandedPlayerCard = null;
        foreach (var child in _expandedContent.GetChildren()) child.QueueFree();
        AddExpandedRow("敌方伏牌", _enemyCards, true);
        AddExpandedRow("我方伏牌", _playerCards, false);
    }

    private void AddExpandedRow(string title, IReadOnlyList<BattleState.PlacedPassive> cards, bool faceDown)
    {
        _expandedContent.AddChild(new Label { Text = $"{title}  {cards.Count}", MouseFilter = MouseFilterEnum.Ignore });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, CardTile.NativeSize.Y + 8), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(row);
        if (cards.Count == 0) row.AddChild(CreateCardBack(true));
        else foreach (var placed in cards)
        {
            if (faceDown)
            {
                var back = CreateCardBack(false);
                back.CustomMinimumSize = CardTile.NativeSize;
                row.AddChild(back);
            }
            else
            {
                var tile = new CardTile { CustomMinimumSize = CardTile.NativeSize, SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SizeFlagsVertical = SizeFlags.ShrinkBegin };
                row.AddChild(tile);
                tile.Setup(placed.Card);
                ExpandedPlayerCard ??= tile;
                tile.DetailRequested += card =>
                {
                    _expandedPanel.Hide();
                    DetailRequested?.Invoke(card);
                };
            }
        }
        _expandedContent.AddChild(scroll);
    }

    private void OnGuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
        if (Expanded) _expandedPanel.Hide();
        else
        {
            Expanded = true;
            RefreshExpandedPanel();
            var cardSpan = CardSize.X + (Mathf.Max(PlayerCardCount, EnemyCardCount) - 1) * ExpandedStep;
            var width = Mathf.Clamp((int)Mathf.Max(220, cardSpan + 24), 220, 720);
            var screenPosition = (Vector2I)(GlobalPosition + new Vector2(Size.X + 8, 0));
            _expandedPanel.Popup(new Rect2I(screenPosition, new Vector2I(width, 540)));
        }
        AcceptEvent();
    }

    private void OnPopupHidden()
    {
        Expanded = false;
    }
}
