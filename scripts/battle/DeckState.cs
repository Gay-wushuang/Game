using System;
using System.Collections.Generic;

public sealed class DeckState
{
    public readonly List<CardInstance> DrawPile = [];
    public readonly List<CardInstance> Hand = [];
    public readonly List<CardInstance> DiscardPile = [];
    public string OwnerId { get; private set; } = "player";
    private readonly Random _random = new();
    public void Setup(IEnumerable<CardDefinition> cards, string owner)
    {
        OwnerId = owner; DrawPile.Clear(); Hand.Clear(); DiscardPile.Clear();
        var normal = new List<CardDefinition>(); CardDefinition? star = null;
        foreach (var card in cards) { if (card.builtin_effect == CardDefinition.BuiltinEffect.StarUp) star = card; else normal.Add(card); }
        for (var i = 0; i < 15 && normal.Count > 0; i++) DrawPile.Add(new CardInstance(normal[i % normal.Count], owner));
        if (star != null) { DrawPile.Add(new CardInstance(star, owner)); DrawPile.Add(new CardInstance(star, owner)); }
        Shuffle(DrawPile);
    }
    public List<CardInstance> Draw(int amount = 1)
    {
        List<CardInstance> result = [];
        for (var i = 0; i < amount; i++) {
            if (DrawPile.Count == 0) { if (DiscardPile.Count == 0) break; DrawPile.AddRange(DiscardPile); DiscardPile.Clear(); Shuffle(DrawPile); }
            var card = DrawPile[^1]; DrawPile.RemoveAt(DrawPile.Count - 1); card.Zone = CardInstance.ZoneKind.Hand;
            card.FaceUp = OwnerId == "player"; Hand.Add(card); result.Add(card);
        }
        return result;
    }
    public void Discard(CardInstance card) { if (!Hand.Remove(card)) return; card.Zone = CardInstance.ZoneKind.Discard; DiscardPile.Add(card); }
    public bool SetPassive(CardInstance card) { if (!Hand.Remove(card)) return false; card.Zone = CardInstance.ZoneKind.Set; card.FaceUp = false; return true; }
    public void DiscardPlaced(CardInstance card) { card.Zone = CardInstance.ZoneKind.Discard; card.FaceUp = true; DiscardPile.Add(card); }
    private void Shuffle<T>(IList<T> list) { for (var i = list.Count - 1; i > 0; i--) { var j = _random.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
}
