using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class PrepareUi : Control
{
    private readonly HashSet<string> _selected = [];
    private Godot.Collections.Array<CardDefinition> _cards = [];
    private Godot.Collections.Array<HeroDefinition> _heroes = [];
    private PackedScene _cardScene = null!;
    private GridContainer _grid = null!;
    private ScrollContainer _scroll = null!;
    private int _category = 1;
    private static readonly string[] CategoryNames = ["英雄卡", "主动锦囊", "被动锦囊"];

    public override void _Ready()
    {
        _cards = CardCatalog.Load(); _heroes = GD.Load<TrainingContent>("res://data/training_content.tres").heroes; _cardScene = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn"); _grid = GetNode<GridContainer>("%CardGrid"); _scroll = GetNode<ScrollContainer>("%CardScroll");
        foreach (var id in GameSaveManager.SelectedDeckIds) _selected.Add(id);
        GetNode<Button>("%BackButton").Pressed += () => SceneRouter.Instance.Back(); GetNode<Button>("%PrevButton").Pressed += () => ChangeCategory(-1); GetNode<Button>("%NextButton").Pressed += () => ChangeCategory(1); GetNode<Button>("%StartButton").Pressed += OnStartPressed;
        RefreshCards();
    }

    private void ChangeCategory(int delta) { _category = Mathf.PosMod(_category + delta, CategoryNames.Length); RefreshCards(); }
    private void RefreshCards()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        if (_category == 0)
        {
            foreach (var definition in _heroes)
            {
                var hero = new HeroCardInstance(definition); var tile = new Button { CustomMinimumSize = new Vector2(255, 340), Text = $"{definition.character_number} · {definition.display_name}\n{definition.TypeName()}\n\nHP {hero.State.MaxHp}\nATK {hero.State.Attack}\n★{hero.State.Star}", TooltipText = definition.description, MouseDefaultCursorShape = CursorShape.PointingHand };
                _grid.AddChild(tile);
            }
        }
        else foreach (var definition in _cards.Where(card => card.card_kind == (_category == 1 ? CardDefinition.CardKind.Active : CardDefinition.CardKind.Passive)))
        {
            var card = new CardInstance(definition); var tile = _cardScene.Instantiate<CardTile>(); _grid.AddChild(tile); tile.Setup(card); tile.Flat = false; tile.CustomMinimumSize = new Vector2(255, 340); tile.ToggleMode = true; tile.SetPressedNoSignal(_selected.Contains(definition.id.ToString())); ApplySelectionStyle(tile, tile.ButtonPressed); tile.CardChosen += _ => ToggleCard(definition, tile);
        }
        _scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 0); UpdateCount();
    }
    private void ToggleCard(CardDefinition definition, CardTile tile)
    {
        var id = definition.id.ToString();
        if (_selected.Contains(id)) _selected.Remove(id);
        else
        {
            var limit = definition.card_kind == CardDefinition.CardKind.Active ? 10 : 5;
            var current = Count(definition.card_kind);
            if (current >= limit) { tile.SetPressedNoSignal(false); SystemNotice.Instance.Show(definition.card_kind == CardDefinition.CardKind.Active ? "主动锦囊最多携带 10 张" : "被动锦囊最多携带 5 张"); ApplySelectionStyle(tile, false); return; }
            _selected.Add(id);
        }
        tile.SetPressedNoSignal(_selected.Contains(id)); ApplySelectionStyle(tile, tile.ButtonPressed); UpdateCount();
    }
    private static void ApplySelectionStyle(CardTile tile, bool selected) { tile.Modulate = selected ? new Color(1.15f, 1.15f, .72f) : Colors.White; }
    private int Count(CardDefinition.CardKind kind) => _cards.Count(card => card.card_kind == kind && _selected.Contains(card.id.ToString()));
    private void UpdateCount()
    {
        var active = Count(CardDefinition.CardKind.Active); var passive = Count(CardDefinition.CardKind.Passive); GetNode<Label>("%DeckCount").Text = $"{CategoryNames[_category]}　　主动 {active}/10　被动 {passive}/5";
        GetNode<Label>("%DeckValidation").Text = active == 10 && passive == 5 ? "牌组符合出战条件" : active < 10 ? $"主动锦囊还少 {10 - active} 张" : passive < 5 ? $"被动锦囊还少 {5 - passive} 张" : "牌组数量不符合要求";
    }
    private void OnStartPressed()
    {
        var active = Count(CardDefinition.CardKind.Active); var passive = Count(CardDefinition.CardKind.Passive);
        if (active != 10 || passive != 5) { var messages = new List<string>(); if (active < 10) messages.Add($"主动少带 {10 - active} 张"); if (active > 10) messages.Add($"主动多带 {active - 10} 张"); if (passive < 5) messages.Add($"被动少带 {5 - passive} 张"); if (passive > 5) messages.Add($"被动多带 {passive - 5} 张"); SystemNotice.Instance.Show(string.Join("，", messages)); return; }
        GameSaveManager.SelectedDeckIds.Clear(); GameSaveManager.SelectedDeckIds.AddRange(_cards.Where(card => _selected.Contains(card.id.ToString())).Select(card => card.id.ToString())); SceneRouter.Instance.LoadAndEnter(SceneRouter.Scenes.Battle);
    }
}
