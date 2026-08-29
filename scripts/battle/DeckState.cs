using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DeckState
{
    public const int HandLimit = 8;
    public readonly List<CardInstance> DrawPile = [];
    public readonly List<CardInstance> Hand = [];
    public readonly List<CardInstance> DiscardPile = [];
    public readonly List<CardInstance> ExilePile = [];
    public string OwnerId { get; private set; } = "player";
    private Random? _random;
    
    public void SetRandom(Random random) => _random = random;
    private Random GetRandom() => _random ??= new Random(0);
    public void Setup(IEnumerable<CardDefinition> cards, string owner)
    {
        OwnerId = owner; DrawPile.Clear(); Hand.Clear(); DiscardPile.Clear(); ExilePile.Clear();
        foreach (var card in cards) DrawPile.Add(new CardInstance(card, owner));
        Shuffle(DrawPile);
    }
    public List<CardInstance> Draw(int amount = 1)
    {
        PrepareDrawPile(amount);
        List<CardInstance> result = [];
        for (var i = 0; i < amount; i++) {
            if (DrawPile.Count == 0) break;
            var card = DrawPile[^1]; DrawPile.RemoveAt(DrawPile.Count - 1); card.Zone = CardInstance.ZoneKind.Hand;
            card.FaceUp = OwnerId == "player";
            if (Hand.Count >= HandLimit) { card.Zone = CardInstance.ZoneKind.Discard; DiscardPile.Add(card); continue; }
            Hand.Add(card); result.Add(card);
        }
        return result;
    }

    /// <summary>在 BEFORE_DRAW 前完成弃牌堆回洗，使触发器看到确定且可复现的牌堆顶。</summary>
    public void PrepareDrawPile(int requestedAmount = 1)
    {
        if (requestedAmount <= 0 || DrawPile.Count > 0 || DiscardPile.Count == 0) return;
        DrawPile.AddRange(DiscardPile);
        DiscardPile.Clear();
        Shuffle(DrawPile);
    }

    public CardInstance? PeekPreparedTop() => DrawPile.Count == 0 ? null : DrawPile[^1];
    public void Discard(CardInstance card) { if (!Hand.Remove(card)) return; card.Zone = CardInstance.ZoneKind.Discard; DiscardPile.Add(card); }
    public void FinishPlayedCard(CardInstance card)
    {
        if (card.IsTemporaryCopy || card.ExileAtTurnEnd) Exile(card);
        else if (card.Definition.components.Lifecycle.OnResolve == CardLifecycleComponent.Exile) Exile(card);
        else Discard(card);
    }
    public bool SetPassive(CardInstance card) { if (!Hand.Remove(card)) return false; card.Zone = CardInstance.ZoneKind.Set; card.FaceUp = false; return true; }
    public void DiscardPlaced(CardInstance card) { card.Zone = CardInstance.ZoneKind.Discard; card.FaceUp = true; DiscardPile.Add(card); }
    public void ReceiveToDiscard(CardInstance card) { card.OwnerId = OwnerId; card.RuntimeCostModifier = 0; card.RuntimeCostOverride = -1; card.ReturnToOriginalOwnerDiscardAtTurnEnd = false; card.Zone = CardInstance.ZoneKind.Discard; card.FaceUp = true; DiscardPile.Add(card); }
    public void Exile(CardInstance card) { Hand.Remove(card); DiscardPile.Remove(card); card.Zone = CardInstance.ZoneKind.Exile; card.FaceUp = true; ExilePile.Add(card); }
    public int DiscardRemainingHand(bool preventDiscard = false)
    {
        var discarded = 0;
        foreach (var card in Hand.ToList())
        {
            if (card.ExileAtTurnEnd) Exile(card);
            else if (!preventDiscard) { Discard(card); discarded++; }
        }
        return discarded;
    }
    public void TickCooldowns()
    {
        foreach (var card in DrawPile.Concat(Hand).Concat(DiscardPile)) card.CooldownRemaining = Math.Max(0, card.CooldownRemaining - 1);
    }
    private void Shuffle<T>(IList<T> list) { var rng = GetRandom(); for (var i = list.Count - 1; i > 0; i--) { var j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
}
