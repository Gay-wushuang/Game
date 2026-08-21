using Godot;
using System;

public partial class HandFan : Control
{
    private int _selected = -1;
    private int _hovered = -1;

    public override void _Ready()
    {
        Resized += () => ArrangeCards();
        ChildEnteredTree += child => CallDeferred(MethodName.RegisterCard, child);
    }

    public void SetSelected(int index) { _selected = index; ArrangeCards(true); }

    public void RegisterCard(Node node)
    {
        if (node is not Control card || card.HasMeta("hand_fan_registered")) return;
        card.SetMeta("hand_fan_registered", true);
        card.MouseEntered += () => { _hovered = card.GetIndex(); ArrangeCards(true); };
        card.MouseExited += () => { if (_hovered == card.GetIndex()) _hovered = -1; ArrangeCards(true); };
        ArrangeCards();
    }

    public void ArrangeCards(bool animated = false)
    {
        var cards = GetChildren();
        var count = cards.Count;
        if (count == 0) { _hovered = -1; return; }
        if (_hovered >= count) _hovered = -1;

        var height = Mathf.Min(Size.Y * .84f, 232f);
        var width = height * .75f;
        var desiredSpread = count switch { <= 1 => 0f, <= 4 => width * .98f, <= 6 => width * .8f, _ => width * .63f };
        var spread = count <= 1 ? 0 : Mathf.Min(desiredSpread, (Size.X - width - 24f) / (count - 1));
        var totalWidth = width + spread * Math.Max(0, count - 1);
        var start = (Size.X - totalWidth) / 2f;
        var center = (count - 1) / 2f;

        for (var index = 0; index < count; index++)
        {
            if (cards[index] is not Control card) continue;
            var distance = index - center;
            var x = start + spread * index;
            if (_hovered >= 0) x += index < _hovered ? -28f : index > _hovered ? 28f : 0f;
            else if (_selected >= 0) x += index < _selected ? -18f : index > _selected ? 18f : 0f;
            var active = index == _hovered || (_hovered < 0 && index == _selected);
            var y = 8f + distance * distance * 3.5f - (active ? 18f : 0f);
            var rotation = active ? 0f : Mathf.DegToRad(Mathf.Clamp(distance * 4f, -12f, 12f));
            var scale = index == _hovered ? new Vector2(1.05f, 1.05f) : Vector2.One;
            var position = new Vector2(x, y);
            var size = new Vector2(width, height);
            card.PivotOffset = size / 2f;
            card.ZIndex = active ? 50 : index;
            ApplyLayout(card, position, size, rotation, scale, animated);
        }
    }

    private static void ApplyLayout(Control card, Vector2 position, Vector2 size, float rotation, Vector2 scale, bool animated)
    {
        if (!animated)
        {
            card.Position = position;
            card.Size = size;
            card.Rotation = rotation;
            card.Scale = scale;
            return;
        }

        var tween = card.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "position", position, .16);
        tween.TweenProperty(card, "size", size, .16);
        tween.TweenProperty(card, "rotation", rotation, .16);
        tween.TweenProperty(card, "scale", scale, .16);
    }
}
