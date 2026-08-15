public sealed class HeroCardInstance
{
    public enum ZoneKind { HeroBag, Battlefield, Defeated }
    public HeroDefinition Definition { get; }
    public string OwnerId { get; }
    public ZoneKind Zone { get; private set; }
    public UnitState State { get; }
    public HeroCardInstance(HeroDefinition definition, string owner = "player")
    {
        Definition = definition; OwnerId = owner;
        State = new UnitState { Definition = definition, Name = definition.display_name, Number = definition.character_number,
            Type = definition.TypeName(), Hp = definition.max_hp, MaxHp = definition.max_hp, Attack = definition.attack,
            ExpToStar = definition.exp_to_star, Star = definition.initial_star };
    }
    public UnitState Deploy() { Zone = ZoneKind.Battlefield; return State; }
    public void MarkDefeated() => Zone = ZoneKind.Defeated;
}
