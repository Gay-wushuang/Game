using System;

public sealed class CardExecutionContext
{
    public required BattleState State { get; init; }
    public required CardInstance Card { get; init; }
    public required DeckState OwnerDeck { get; init; }
    public required DeckState OpponentDeck { get; init; }
    public UnitState? Source { get; init; }
    public UnitState? Target { get; init; }
    public required Action<string> Log { get; init; }
    public bool Cancelled { get; set; }
}
