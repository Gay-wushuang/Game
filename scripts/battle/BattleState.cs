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
    public int PlayerReserveHeroCount { get; private set; }
    public int EnemyReserveHeroCount { get; private set; }
    public List<PlacedPassive> Passives { get; } = [];
    public PassiveEventContext? CurrentPassiveEvent { get; set; }
    public List<(string OwnerId, int SlotIndex, CardInstance Card)> InvalidatedPassives { get; } = [];
    public List<(string OwnerId, int SlotIndex, UnitState Unit)> PendingSummons { get; } = [];
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Playing;
    public bool IsFinished => Outcome != BattleOutcome.Playing;

    public BattleOutcome EvaluateOutcome()
    {
        if (IsFinished) return Outcome;
        
        bool playerAlive = PlayerUnits.Any(x => x.Alive) || PlayerReserveHeroCount > 0;
        bool enemyAlive = EnemyUnits.Any(x => x.Alive) || EnemyReserveHeroCount > 0;
        
        if (!playerAlive && !enemyAlive)
        {
            Outcome = BattleOutcome.Draw;
            Events.Publish(new BattleEventData(BattleEvent.BattleEnded) { Amount = 0 });
        }
        else if (!enemyAlive)
        {
            Outcome = BattleOutcome.PlayerVictory;
            Events.Publish(new BattleEventData(BattleEvent.BattleEnded) { Amount = 1 });
        }
        else if (!playerAlive)
        {
            Outcome = BattleOutcome.EnemyVictory;
            Events.Publish(new BattleEventData(BattleEvent.BattleEnded) { Amount = -1 });
        }
        
        return Outcome;
    }

    /// <summary>
    /// 设置指定阵营的后备英雄数量。
    /// </summary>
    public void SetReserveHeroCount(string ownerId, int count)
    {
        if (ownerId == "player") PlayerReserveHeroCount = count;
        else if (ownerId == "ai" || ownerId == "enemy") EnemyReserveHeroCount = count;
    }

    /// <summary>
    /// 指定阵营部署一个后备英雄后调用，减少Reserve计数。
    /// </summary>
    public void DecrementReserveHero(string ownerId)
    {
        if (ownerId == "player" && PlayerReserveHeroCount > 0) PlayerReserveHeroCount--;
        else if ((ownerId == "ai" || ownerId == "enemy") && EnemyReserveHeroCount > 0) EnemyReserveHeroCount--;
    }

    /// <summary>
    /// 指定阵营是否仍有存活英雄（场上+后备）。
    /// </summary>
    public bool HasLivingHeroes(string ownerId)
    {
        if (ownerId == "player")
            return PlayerUnits.Any(x => x.Alive) || PlayerReserveHeroCount > 0;
        return EnemyUnits.Any(x => x.Alive) || EnemyReserveHeroCount > 0;
    }

    /// <summary>
    /// 统一致死结算边界：同步单位状态后评估战斗结果。
    /// 所有致死来源最终都应通过此方法结算。
    /// </summary>
    public BattleOutcome FinalizeDeaths(IEnumerable<UnitState> playerUnits, IEnumerable<UnitState> enemyUnits)
    {
        SynchronizeUnits(playerUnits, enemyUnits);
        return EvaluateOutcome();
    }

    public void ResetOutcome()
    {
        Outcome = BattleOutcome.Playing;
    }

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

    /// <summary>
    /// 完整验证并放置被动锦囊。先验证所有条件，再提交状态。
    /// 验证项：卡仍在正确Owner的Hand、卡是Passive类型、Slot有效、Slot未被占用、Owner匹配。
    /// </summary>
    public bool TryPlacePassive(string ownerId, int slotIndex, CardInstance card, out string error)
    {
        error = "";
        if (card == null) { error = "卡牌不能为空"; return false; }
        if (card.Definition.card_kind != CardDefinition.CardKind.Passive) { error = "该锦囊不是被动类型"; return false; }
        if (!card.OwnerId.Equals(ownerId, StringComparison.OrdinalIgnoreCase)) { error = "锦囊归属与放置阵营不匹配"; return false; }
        if (Passives.Exists(placed => placed.OwnerId == ownerId && placed.SlotIndex == slotIndex)) { error = "该英雄槽已经设置了一张被动锦囊"; return false; }
        
        Passives.Add(new(ownerId, slotIndex, card));
        return true;
    }

    /// <summary>
    /// 验证后放置被动（无输出错误信息版本）。
    /// </summary>
    public bool TryPlacePassive(string ownerId, int slotIndex, CardInstance card)
        => TryPlacePassive(ownerId, slotIndex, card, out _);

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
