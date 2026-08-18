using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleState
{
    public sealed record PlacedPassive(string OwnerId, int SlotIndex, CardInstance Card);
    private sealed record ScheduledAction(int DueTurn, Action Action);
    private readonly List<ScheduledAction> _scheduled = [];
    public DeckState PlayerDeck { get; }
    public DeckState EnemyDeck { get; }
    public List<UnitState> PlayerUnits { get; } = [];
    public List<UnitState> EnemyUnits { get; } = [];
    public BattleEventBus Events { get; } = new();
    public Random Random { get; private set; }
    private readonly int _initialSeed;
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
    private readonly UnitState?[] _playerSlotUnits = new UnitState?[5];
    private readonly UnitState?[] _enemySlotUnits = new UnitState?[5];

    public BattleState(DeckState playerDeck, DeckState enemyDeck, int randomSeed = 1)
    {
        PlayerDeck = playerDeck;
        EnemyDeck = enemyDeck;
        _initialSeed = randomSeed;
        Random = new Random(randomSeed);
        PlayerDeck.SetRandom(Random);
        EnemyDeck.SetRandom(Random);
    }

    /// <summary>
    /// Reset the authoritative RNG to a new seed and re-inject into both decks.
    /// This ensures same seed + reset = same random sequence.
    /// </summary>
    public void ResetRandom(int seed)
    {
        Random = new Random(seed);
        PlayerDeck.SetRandom(Random);
        EnemyDeck.SetRandom(Random);
    }

    /// <summary>
    /// Reset RNG to the initial seed passed in constructor.
    /// </summary>
    public void ResetRandom() => ResetRandom(_initialSeed);

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

    /// <summary>
    /// 清空所有槽位单位映射。用于重置战斗时确保无残留状态。
    /// </summary>
    public void ClearSlotUnits()
    {
        for (int i = 0; i < 5; i++)
        {
            _playerSlotUnits[i] = null;
            _enemySlotUnits[i] = null;
        }
    }

    /// <summary>
    /// 设置指定阵营指定槽位的单位（用于被动放置验证）。
    /// </summary>
    public void SetSlotUnit(string ownerId, int slotIndex, UnitState? unit)
    {
        if (slotIndex < 0 || slotIndex >= 5) return;
        if (ownerId == "player") _playerSlotUnits[slotIndex] = unit;
        else if (ownerId == "ai" || ownerId == "enemy") _enemySlotUnits[slotIndex] = unit;
    }

    /// <summary>
    /// 获取指定阵营指定槽位的单位。
    /// </summary>
    public UnitState? GetSlotUnit(string ownerId, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 5) return null;
        return ownerId == "player" ? _playerSlotUnits[slotIndex] : _enemySlotUnits[slotIndex];
    }

    internal bool SetPassive(string ownerId, int slotIndex, CardInstance card)
    {
        if (Passives.Exists(placed => placed.OwnerId == ownerId && placed.SlotIndex == slotIndex)) return false;
        Passives.Add(new(ownerId, slotIndex, card)); return true;
    }

    /// <summary>
    /// 完整验证并放置被动锦囊。先验证所有条件，再提交状态。
    /// 验证项：卡非null、Passive类型、Owner匹配、卡在对应Deck的Hand中、Slot有效、目标英雄存在且Alive、该槽无已有Passive。
    /// </summary>
    public bool TryPlacePassive(string ownerId, int slotIndex, CardInstance card, out string error)
    {
        error = "";
        if (card == null) { error = "卡牌不能为空"; return false; }
        if (card.Definition.card_kind != CardDefinition.CardKind.Passive) { error = "该锦囊不是被动类型"; return false; }
        if (!card.OwnerId.Equals(ownerId, StringComparison.OrdinalIgnoreCase)) { error = "锦囊归属与放置阵营不匹配"; return false; }
        
        // 验证卡在对应Deck的Hand中
        var deck = (ownerId == "player") ? PlayerDeck : EnemyDeck;
        if (!deck.Hand.Contains(card)) { error = "锦囊不在手牌中"; return false; }
        
        // 验证Slot索引有效
        if (slotIndex < 0 || slotIndex >= 5) { error = "英雄槽索引无效"; return false; }
        
        // 验证目标英雄存在且Alive
        var unit = GetSlotUnit(ownerId, slotIndex);
        if (unit == null) { error = "该英雄槽没有英雄"; return false; }
        if (!unit.Alive) { error = "该英雄已经阵亡，无法设置被动锦囊"; return false; }
        
        // 验证该槽无已有Passive
        if (Passives.Exists(placed => placed.OwnerId == ownerId && placed.SlotIndex == slotIndex)) { error = "该英雄槽已经设置了一张被动锦囊"; return false; }
        
        // 所有验证通过，提交状态
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
