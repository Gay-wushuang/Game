public sealed class CardInstance
{
    public enum ZoneKind { Deck, Hand, Set, Discard, Exile, HeroBag }
    public CardDefinition Definition { get; }
    public string OwnerId { get; set; }
    public string OriginalOwnerId { get; }
    public ZoneKind Zone { get; set; }
    public bool FaceUp { get; set; }
    public int RuntimeCostModifier { get; set; }
    public int RuntimeCostOverride { get; set; } = -1;
    public bool ReturnToOriginalOwnerDiscardAtTurnEnd { get; set; }
    public CardInstance(CardDefinition definition, string owner = "player") { Definition = definition; OwnerId = owner; OriginalOwnerId = owner; }
    public int CurrentCost(int currentAp = 3, int maxAp = 3)
    {
        var baseCost = Definition.cost_mode switch { "ALL_CURRENT" => currentAp, "MAX_AP" => maxAp, _ => Definition.action_cost };
        return RuntimeCostOverride >= 0 ? RuntimeCostOverride : System.Math.Max(0, baseCost + RuntimeCostModifier);
    }
}
