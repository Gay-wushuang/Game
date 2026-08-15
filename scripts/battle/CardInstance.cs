public sealed class CardInstance
{
    public enum ZoneKind { Deck, Hand, Discard, HeroBag }
    public CardDefinition Definition { get; }
    public string OwnerId { get; set; }
    public ZoneKind Zone { get; set; }
    public bool FaceUp { get; set; }
    public int RuntimeCostModifier { get; set; }
    public CardInstance(CardDefinition definition, string owner = "player") { Definition = definition; OwnerId = owner; }
    public int CurrentCost() => System.Math.Max(0, Definition.action_cost + RuntimeCostModifier);
}
