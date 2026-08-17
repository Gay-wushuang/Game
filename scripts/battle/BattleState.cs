using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleState(DeckState playerDeck, DeckState enemyDeck, int randomSeed = 1)
{
    public sealed record PlacedPassive(string OwnerId, int SlotIndex, CardInstance Card);
    private sealed record ScheduledAction(int DueTurn, Action Action);
    private readonly List<ScheduledAction> _scheduled = [];
    public DeckState PlayerDeck { get; } = playerDeck;
    public DeckState EnemyDeck { get; } = enemyDeck;
    public List<UnitState> PlayerUnits { get; } = [];
    public List<UnitState> EnemyUnits { get; } = [];
    public BattleEventBus Events { get; } = new();
    public Random Random { get; } = new(randomSeed);
    public int Turn { get; set; } = 1;
    public int PlayerActionPoints { get; set; } = 3;
    public int EnemyActionPoints { get; set; } = 3;
    public int PlayerNextTurnBonus { get; set; }
    public int EnemyNextTurnBonus { get; set; }
    public List<PlacedPassive> Passives { get; } = [];
    public PassiveEventContext? CurrentPassiveEvent { get; set; }
    public List<(string OwnerId, int SlotIndex, CardInstance Card)> InvalidatedPassives { get; } = [];
    public List<(string OwnerId, int SlotIndex, UnitState Unit)> PendingSummons { get; } = [];

    public void QueueSummon(string ownerId, int slotIndex, UnitState unit) => PendingSummons.Add((ownerId, slotIndex, unit));

    public void SynchronizeUnits(IEnumerable<UnitState> player, IEnumerable<UnitState> enemy)
    {
        PlayerUnits.Clear(); PlayerUnits.AddRange(player);
        EnemyUnits.Clear(); EnemyUnits.AddRange(enemy);
    }

    public bool SetPassive(string ownerId, int slotIndex, CardInstance card)
    {
        if (Passives.Exists(placed => placed.OwnerId == ownerId && placed.SlotIndex == slotIndex)) return false;
        Passives.Add(new(ownerId, slotIndex, card)); return true;
    }
    public void RemovePassive(CardInstance card) => Passives.RemoveAll(placed => placed.Card == card);
    public IEnumerable<PlacedPassive> MatchingPassives(string ownerId, string eventKey) => Passives.FindAll(placed => placed.OwnerId == ownerId && Array.Exists(placed.Card.Definition.trigger_keys, key => key == eventKey));
    public void Schedule(int turns, Action action) => _scheduled.Add(new(Turn + Math.Max(1, turns), action));
    public void AdvanceTurn()
    {
        Turn++;
        ExileExpiredCopies(PlayerDeck); ExileExpiredCopies(EnemyDeck);
        ReturnTemporaryCards(PlayerDeck, EnemyDeck); ReturnTemporaryCards(EnemyDeck, PlayerDeck);
        foreach (var scheduled in _scheduled.FindAll(item => item.DueTurn <= Turn)) scheduled.Action();
        _scheduled.RemoveAll(item => item.DueTurn <= Turn);
    }
    private static void ExileExpiredCopies(DeckState deck)
    {
        foreach (var card in deck.Hand.FindAll(card => card.ExileAtTurnEnd).ToList()) deck.Exile(card);
    }
    private static void ReturnTemporaryCards(DeckState current, DeckState original)
    {
        foreach (var card in current.Hand.FindAll(card => card.ReturnToOriginalOwnerDiscardAtTurnEnd && card.OriginalOwnerId == original.OwnerId)) { current.Hand.Remove(card); original.ReceiveToDiscard(card); }
    }
}
