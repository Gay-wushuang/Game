public enum BattleEvent
{
    BeforeDamage,
    AfterDamage,
    BeforeDraw,
    AfterDraw,
    BeforeAttack,
    AfterAttack,
    CardPlayed,
    CardResolved,
    HeroDefeated,
    BattleEnded,
    TurnStarted,
    TurnEnded,
}

public sealed class BattleEventData(BattleEvent eventType)
{
    public BattleEvent EventType { get; } = eventType;
    public UnitState? Source { get; init; }
    public UnitState? Target { get; init; }
    public CardInstance? Card { get; init; }
    public int Amount { get; set; }
    public bool Cancelled { get; set; }
}
