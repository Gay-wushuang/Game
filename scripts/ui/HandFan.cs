using Godot;

public partial class HandFan : Control
{
    private int _selected = -1;
    public override void _Ready() => Resized += () => ArrangeCards();
    public void SetSelected(int index) { _selected = index; ArrangeCards(true); }
    public void ArrangeCards(bool animated = false)
    {
        var cards = GetChildren(); var count = cards.Count; if (count == 0) return;
        var height = Size.Y * .9f; var width = Mathf.Min(height * .75f, Size.X * .26f); var spread = width * 1.08f;
        if (count > 1) spread = Mathf.Min(spread, (Size.X - width) / (count - 1));
        var start = (Size.X - (width + spread * (count - 1))) / 2;
        for (var i = 0; i < count; i++) { if (cards[i] is not Control card) continue; var x = start + spread * i; if (_selected >= 0) x += i < _selected ? -width * .08f : i > _selected ? width * .08f : 0; var pos = new Vector2(x, (Size.Y - height) / 2 - (i == _selected ? 12 : 0)); card.ZIndex = i == _selected ? 20 : i; if (animated) { var t = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); t.TweenProperty(card, "position", pos, .16); t.TweenProperty(card, "size", new Vector2(width, height), .16); } else { card.Position = pos; card.Size = new(width, height); } }
    }
}
