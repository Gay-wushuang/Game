using Godot;
using System;
using System.Linq;

public static class CardSemanticValidator
{
    public static void Validate(Godot.Collections.Array<CardDefinition> cards)
    {
        Check(cards.Count == CardCatalog.V1ExpectedCount, $"V1 数据集应为 {CardCatalog.V1ExpectedCount} 张，实际 {cards.Count} 张");
        Check(cards.All(card => card.target_key != ""), "每张卡必须保留原始 target_key");
        Check(cards.All(card => card.target_key != "SET_SLOT" || card.target_kind == CardDefinition.TargetKind.SetSlot), "SET_SLOT 必须映射到 TargetKind.SetSlot");

        ValidateExpose(cards);
        ValidateExposePenalties(cards);
        ValidateBackstab(cards);
        ValidateCopyResolved(cards);
        ValidateSummonRabbit(cards);

        ValidateStarUp(cards);
        ValidateHealCleanse(cards);
        ValidateCancelPendingEffect(cards);
        ValidateDamageStarAll(cards);
        ValidateApplyShield(cards);
        ValidateLinkResonance(cards);
        ValidateDamageHealAmplify(cards);
        ValidateFreeUnansweredAttack(cards);
        ValidateApplyGrudge(cards);
        ValidateForceMutualAttack(cards);
        ValidateApplyCeasefire(cards);
        ValidateCancelDamage(cards);
        ValidateReviveReducedMaxHp(cards);
        ValidateTargetedHardChoice(cards);
        ValidateNextEnemyCostUp(cards);
        ValidateRefillHand(cards);
        ValidateCancelDraw(cards);
        ValidateSkipEnemyBattlePhase(cards);
        ValidateDiscardEqualDraw(cards);
        ValidateGambleActionPoints(cards);
        ValidateStealTemporary(cards);
        ValidateZeroHandCosts(cards);
        ValidatePrepayAndDiscard(cards);
        ValidateRandomCrossAttack(cards);
    }

    private static void ValidateExpose(Godot.Collections.Array<CardDefinition> cards)
    {
        var expose = cards.First(c => c.handler_key == "COUNTER_PASSIVE_SET");
        var cage = cards.First(c => c.handler_key == "SKIP_ENEMY_BATTLE_PHASE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 7);
        var exposeCard = new CardInstance(expose);
        var enemyPassive = new CardInstance(cage, "ai");
        Check(battle.SetPassive("player", 0, exposeCard), "揭穿伏牌登记失败");
        Check(battle.SetPassive("ai", 1, enemyPassive), "测试用敌方伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "PASSIVE_SET", SubjectCard = enemyPassive, SubjectSlotIndex = 1, SubjectOwnerId = "ai" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "PASSIVE_SET", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1 && triggered[0].Card == exposeCard, "揭穿未在 PASSIVE_SET 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = exposeCard, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "揭穿 Lua 执行失败：" + error);
        Check(battle.Passives.All(placed => placed.Card != enemyPassive), "揭穿未移除敌方刚设置的被动");
        Check(battle.InvalidatedPassives.Any(item => item.Card == enemyPassive), "揭穿未登记失效的敌方被动");
    }

    private static void ValidateExposePenalties(Godot.Collections.Array<CardDefinition> cards)
    {
        var expose = cards.First(c => c.handler_key == "COUNTER_PASSIVE_SET");
        var hardChoice = cards.First(c => c.handler_key == "TARGETED_HARD_CHOICE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 8);
        var hero = new UnitState { Name = "测试英雄", Type = "先锋", Hp = 30, MaxHp = 30 };
        battle.SynchronizeUnits([hero], []);
        var testCard = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"), "ai");
        enemyDeck.Hand.Add(testCard);
        var exposeCard = new CardInstance(expose);
        var passive = new CardInstance(hardChoice, "ai");
        Check(battle.SetPassive("player", 0, exposeCard), "揭穿伏牌登记失败");
        Check(battle.SetPassive("ai", 1, passive), "TARGETED_HARD_CHOICE 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "PASSIVE_SET", SubjectCard = passive, SubjectSlotIndex = 1, SubjectOwnerId = "ai" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "PASSIVE_SET", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "揭穿未在 PASSIVE_SET 时触发 TARGETED_HARD_CHOICE");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = exposeCard, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "揭穿 Lua 执行失败（TARGETED_HARD_CHOICE 惩罚）：" + error);
        Check(enemyDeck.Hand.Count == 0, $"揭穿 TARGETED_HARD_CHOICE 惩罚未弃敌方手牌，期望 0，实际 {enemyDeck.Hand.Count}");
        Check(enemyDeck.DiscardPile.Count == 1, "揭穿 TARGETED_HARD_CHOICE 惩罚未将敌方手牌送入弃牌堆");
    }

    private static void ValidateBackstab(Godot.Collections.Array<CardDefinition> cards)
    {
        var backstab = cards.First(c => c.handler_key == "REDIRECT_TO_ADJACENT");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 9);
        var passive = new CardInstance(backstab);
        Check(battle.SetPassive("player", 1, passive), "背刺伏牌登记失败");
        var ctx = new PassiveEventContext { EventKey = "BEFORE_ATTACK", AttackTargetSlot = 2, AliveAllySlots = [1, 3] };
        Check(PassiveTriggerResolver.CanTrigger(new BattleState.PlacedPassive("player", 1, passive), ctx, battle), "背刺在存在相邻目标时应可触发");
        ctx = new PassiveEventContext { EventKey = "BEFORE_ATTACK", AttackTargetSlot = 2, AliveAllySlots = [2] };
        Check(!PassiveTriggerResolver.CanTrigger(new BattleState.PlacedPassive("player", 1, passive), ctx, battle), "背刺在无相邻目标时不应触发");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "BEFORE_ATTACK", AttackTargetSlot = 2, AliveAllySlots = [1, 3] };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "BEFORE_ATTACK", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "背刺未在 BEFORE_ATTACK 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "背刺 Lua 执行失败：" + error);
        Check(battle.CurrentPassiveEvent.RedirectSlot is 1 or 3, "背刺未将攻击重定向到相邻槽位");
    }

    private static void ValidateCopyResolved(Godot.Collections.Array<CardDefinition> cards)
    {
        var copyCard = cards.First(c => c.handler_key == "COPY_RESOLVED_CARD");
        var resolved = cards.First(c => c.handler_key == "DAMAGE_STAR_ALL");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 11);
        var passive = new CardInstance(copyCard);
        var enemyCard = new CardInstance(resolved, "ai");
        Check(battle.SetPassive("player", 0, passive), "以偏概全伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "AFTER_CARD_RESOLVE", SubjectCard = enemyCard, SubjectOwnerId = "ai" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "AFTER_CARD_RESOLVE", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "以偏概全未在 AFTER_CARD_RESOLVE 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "以偏概全 Lua 执行失败：" + error);
        Check(playerDeck.Hand.Count == 1 && playerDeck.Hand[0].Definition == resolved, "以偏概全未复制敌方刚结算的锦囊");
        Check(playerDeck.Hand[0].CurrentCost() == 0 && playerDeck.Hand[0].ExileAtTurnEnd, "以偏概全复制牌未归零费用或未标记回合结束销毁");
        battle.AdvanceTurn();
        Check(playerDeck.Hand.Count == 0 && playerDeck.ExilePile.Count == 1, "以偏概全复制牌未在回合结束时进入 Exile");
    }

    private static void ValidateSummonRabbit(Godot.Collections.Array<CardDefinition> cards)
    {
        var rabbit = cards.First(c => c.handler_key == "SUMMON_DELAYED_RABBIT");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 13);
        var passive = new CardInstance(rabbit);
        Check(battle.SetPassive("player", 0, passive), "守株待兔伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ENEMY_SLOT_EMPTY", SubjectSlotIndex = 2, SubjectOwnerId = "ai" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ENEMY_SLOT_EMPTY", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "守株待兔未在 ENEMY_SLOT_EMPTY 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "守株待兔 Lua 执行失败：" + error);
        Check(battle.PendingSummons.Count == 1 && battle.PendingSummons[0].Unit.Name == "兔子" && battle.PendingSummons[0].SlotIndex == 2, "守株待兔未在指定空槽登记兔子召唤");
        var enemy = new UnitState { Name = "靶子", Type = "先锋", Hp = 30, MaxHp = 30, Attack = 1 };
        battle.SynchronizeUnits([], [enemy]);
        battle.AdvanceTurn(); battle.AdvanceTurn(); battle.AdvanceTurn();
        Check(enemy.Hp == 10, "守株待兔延迟爆炸未对敌方全体造成 20 伤害");
    }

    private static void ValidateStarUp(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "STAR_UP");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 21);
        var target = new UnitState { Name = "测试英雄", Type = "先锋", Hp = 30, MaxHp = 30, Star = 2 };
        battle.SynchronizeUnits([target], []);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "STAR_UP Lua 执行失败：" + error);
        Check(target.Star == 3, $"STAR_UP 未正确提升星级，期望 3，实际 {target.Star}");
        target.Star = 6;
        context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out error), "STAR_UP 星级上限 Lua 执行失败：" + error);
        Check(target.Star == 6, $"STAR_UP 不应突破 6 星上限，实际 {target.Star}");
    }

    private static void ValidateHealCleanse(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "HEAL_CLEANSE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 22);
        var target = new UnitState { Name = "治疗目标", Type = "祭司", Hp = 10, MaxHp = 30 };
        battle.SynchronizeUnits([target], []);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "HEAL_CLEANSE Lua 执行失败：" + error);
        Check(target.Hp == 30, $"HEAL_CLEANSE 未治疗到满血，期望 30，实际 {target.Hp}");
        context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out error), "HEAL_CLEANSE 满血治疗 Lua 执行失败：" + error);
        Check(target.Hp == 30, $"HEAL_CLEANSE 满血时不应溢出，实际 {target.Hp}");
    }

    private static void ValidateCancelPendingEffect(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "CANCEL_PENDING_EFFECT");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 23);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "CANCEL_PENDING_EFFECT Lua 执行失败：" + error);
        Check(context.Cancelled, "CANCEL_PENDING_EFFECT 未设置 Cancelled 标志");
    }

    private static void ValidateDamageStarAll(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "DAMAGE_STAR_ALL");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 24);
        var target = new UnitState { Name = "受伤目标", Type = "刺客", Hp = 30, MaxHp = 30 };
        battle.SynchronizeUnits([], [target]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "DAMAGE_STAR_ALL Lua 执行失败：" + error);
        Check(target.Hp == 15, $"DAMAGE_STAR_ALL 未造成正确伤害，期望 15，实际 {target.Hp}");
    }

    private static void ValidateApplyShield(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "APPLY_SHIELD");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 25);
        var target = new UnitState { Name = "低星目标", Type = "先锋", Hp = 30, MaxHp = 30, Star = 3 };
        battle.SynchronizeUnits([target], []);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "APPLY_SHIELD Lua 执行失败：" + error);
        Check(target.ShieldRatio == 0.2f, $"APPLY_SHIELD 低星护盾比例错误，期望 0.2，实际 {target.ShieldRatio}");
        Check(target.ShieldTurns == 1, $"APPLY_SHIELD 护盾回合数错误，期望 1，实际 {target.ShieldTurns}");

        target.Star = 5;
        context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out error), "APPLY_SHIELD 高星 Lua 执行失败：" + error);
        Check(target.ShieldRatio == 0.5f, $"APPLY_SHIELD 高星护盾比例错误，期望 0.5，实际 {target.ShieldRatio}");
    }

    private static void ValidateLinkResonance(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "LINK_RESONANCE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 26);
        var source = new UnitState { Name = "链接源", Type = "先锋", Hp = 30, MaxHp = 30 };
        var target = new UnitState { Name = "链接目标", Type = "祭司", Hp = 30, MaxHp = 30 };
        battle.SynchronizeUnits([source], [target]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Source = source, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "LINK_RESONANCE Lua 执行失败：" + error);
        Check(source.LinkTurns == 1, $"LINK_RESONANCE 源单位链接回合错误，期望 1，实际 {source.LinkTurns}");
        Check(target.LinkTurns == 1, $"LINK_RESONANCE 目标单位链接回合错误，期望 1，实际 {target.LinkTurns}");
    }

    private static void ValidateDamageHealAmplify(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "APPLY_DAMAGE_HEAL_AMPLIFY");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 27);
        var target = new UnitState { Name = "放大目标", Type = "先锋", Hp = 30, MaxHp = 30, DamageTakenMultiplier = 1f };
        battle.SynchronizeUnits([target], []);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "APPLY_DAMAGE_HEAL_AMPLIFY Lua 执行失败：" + error);
        Check(target.DamageTakenMultiplier == 1.1f, $"APPLY_DAMAGE_HEAL_AMPLIFY 伤害放大错误，期望 1.1，实际 {target.DamageTakenMultiplier}");
    }

    private static void ValidateFreeUnansweredAttack(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "FREE_UNANSWERED_ATTACK");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 28);
        var source = new UnitState { Name = "攻击源", Type = "刺客", Hp = 30, MaxHp = 30, Attack = 5 };
        var enemy = new UnitState { Name = "随机敌人", Type = "先锋", Hp = 30, MaxHp = 30, Attack = 3 };
        battle.SynchronizeUnits([source], [enemy]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Source = source, Target = enemy, Log = _ => { } };
        var enemyHpBefore = enemy.Hp;
        Check(resolver.Resolve(context, out var error), "FREE_UNANSWERED_ATTACK Lua 执行失败：" + error);
        Check(enemy.Hp < enemyHpBefore, $"FREE_UNANSWERED_ATTACK 未对敌方造成伤害，期望 HP {enemyHpBefore}，实际 {enemy.Hp}");
    }

    private static void ValidateApplyGrudge(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "APPLY_GRUDGE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 29);
        var enemy1 = new UnitState { Name = "敌1", Type = "先锋", Hp = 30, MaxHp = 30, GrudgeStacks = 0 };
        var enemy2 = new UnitState { Name = "敌2", Type = "刺客", Hp = 30, MaxHp = 30, GrudgeStacks = 0 };
        battle.SynchronizeUnits([], [enemy1, enemy2]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "APPLY_GRUDGE Lua 执行失败：" + error);
        Check(enemy1.GrudgeStacks >= 5, $"APPLY_GRUDGE 敌1怨恨层数不足，期望 >=5，实际 {enemy1.GrudgeStacks}");
        Check(enemy2.GrudgeStacks >= 5, $"APPLY_GRUDGE 敌2怨恨层数不足，期望 >=5，实际 {enemy2.GrudgeStacks}");
    }

    private static void ValidateForceMutualAttack(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "FORCE_MUTUAL_ATTACK");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 30);
        var enemy1 = new UnitState { Name = "敌1", Type = "先锋", Hp = 20, MaxHp = 30, Attack = 5 };
        var enemy2 = new UnitState { Name = "敌2", Type = "刺客", Hp = 20, MaxHp = 30, Attack = 8 };
        battle.SynchronizeUnits([], [enemy1, enemy2]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "FORCE_MUTUAL_ATTACK Lua 执行失败：" + error);
        Check(enemy1.Hp < 20, $"FORCE_MUTUAL_ATTACK 敌1未受伤，期望 <20，实际 {enemy1.Hp}");
        Check(enemy2.Hp < 20, $"FORCE_MUTUAL_ATTACK 敌2未受伤，期望 <20，实际 {enemy2.Hp}");
    }

    private static void ValidateApplyCeasefire(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "APPLY_CEASEFIRE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 31);
        var enemy1 = new UnitState { Name = "敌1", Type = "先锋", Hp = 30, MaxHp = 30, CeasefireTurns = 0 };
        var enemy2 = new UnitState { Name = "敌2", Type = "刺客", Hp = 30, MaxHp = 30, CeasefireTurns = 0 };
        battle.SynchronizeUnits([], [enemy1, enemy2]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "APPLY_CEASEFIRE Lua 执行失败：" + error);
        Check(enemy1.CeasefireTurns >= 2, $"APPLY_CEASEFIRE 敌1沉默回合错误，期望 >=2，实际 {enemy1.CeasefireTurns}");
        Check(enemy2.CeasefireTurns >= 2, $"APPLY_CEASEFIRE 敌2沉默回合错误，期望 >=2，实际 {enemy2.CeasefireTurns}");
    }

    private static void ValidateCancelDamage(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "CANCEL_DAMAGE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 32);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "CANCEL_DAMAGE 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "BEFORE_DAMAGE" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "BEFORE_DAMAGE", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "CANCEL_DAMAGE 未在 BEFORE_DAMAGE 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "CANCEL_DAMAGE Lua 执行失败：" + error);
        Check(context.Cancelled, "CANCEL_DAMAGE 未设置 Cancelled 标志");
        Check(PassiveTriggerResolver.CancelsEvent(card, context.Cancelled), "CANCEL_DAMAGE 应通过 Cancelled 标志判定为取消事件");
    }

    private static void ValidateReviveReducedMaxHp(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "REVIVE_REDUCED_MAX_HP");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 33);
        var target = new UnitState { Name = "复活目标", Type = "先锋", Hp = 0, MaxHp = 30 };
        battle.SynchronizeUnits([target], []);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Target = target, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "REVIVE_REDUCED_MAX_HP Lua 执行失败：" + error);
        Check(target.Hp > 0, $"REVIVE_REDUCED_MAX_HP 未复活单位，期望 Hp > 0，实际 {target.Hp}");
        Check(target.MaxHp < 30, $"REVIVE_REDUCED_MAX_HP 未减少上限，期望 <30，实际 {target.MaxHp}");
        Check(target.Hp == target.MaxHp, $"REVIVE_REDUCED_MAX_HP 复活后 HP 应等于新 MaxHp，期望 {target.MaxHp}，实际 {target.Hp}");
    }

    private static void ValidateTargetedHardChoice(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "TARGETED_HARD_CHOICE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 34);
        var card1 = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"), "ai");
        var card2 = new CardInstance(cards.First(c => c.handler_key == "HEAL_CLEANSE"), "ai");
        enemyDeck.Hand.Add(card1);
        enemyDeck.Hand.Add(card2);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "TARGETED_HARD_CHOICE 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ALLY_TURN_ENDED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ALLY_TURN_ENDED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "TARGETED_HARD_CHOICE 未在 ALLY_TURN_ENDED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "TARGETED_HARD_CHOICE Lua 执行失败：" + error);
        Check(enemyDeck.Hand.Count == 1, $"TARGETED_HARD_CHOICE 未弃敌方 1 张手牌，期望 1，实际 {enemyDeck.Hand.Count}");
    }

    private static void ValidateNextEnemyCostUp(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "NEXT_ENEMY_CARD_COST_UP");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 35);
        var enemyCard = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"), "ai");
        enemyCard.RuntimeCostOverride = 2;
        enemyDeck.Hand.Add(enemyCard);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "NEXT_ENEMY_CARD_COST_UP 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ALLY_TURN_ENDED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ALLY_TURN_ENDED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "NEXT_ENEMY_CARD_COST_UP 未在 ALLY_TURN_ENDED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "NEXT_ENEMY_CARD_COST_UP Lua 执行失败：" + error);
        Check(enemyCard.RuntimeCostModifier >= 1, $"NEXT_ENEMY_CARD_COST_UP 未增加敌方手牌费用，期望 >=1，实际 {enemyCard.RuntimeCostModifier}");
    }

    private static void ValidateRefillHand(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "REFILL_HAND");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        playerDeck.Hand.Add(new CardInstance(cards.First(c => c.handler_key == "STAR_UP")));
        playerDeck.Hand.Add(new CardInstance(cards.First(c => c.handler_key == "HEAL_CLEANSE")));
        var battle = new BattleState(playerDeck, enemyDeck, 36);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "REFILL_HAND 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ALLY_BATTLE_PHASE_STARTED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ALLY_BATTLE_PHASE_STARTED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "REFILL_HAND 未在 ALLY_BATTLE_PHASE_STARTED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        var handCountBefore = playerDeck.Hand.Count;
        Check(resolver.Resolve(context, out var error), "REFILL_HAND Lua 执行失败：" + error);
        Check(playerDeck.Hand.Count >= handCountBefore, $"REFILL_HAND 未补满手牌，期望 >= {handCountBefore}，实际 {playerDeck.Hand.Count}");
        Check(playerDeck.Hand.Count <= 5, $"REFILL_HAND 超过 5 张上限，期望 <=5，实际 {playerDeck.Hand.Count}");
    }

    private static void ValidateCancelDraw(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "CANCEL_DRAW");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 37);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "CANCEL_DRAW 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "BEFORE_DRAW" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "BEFORE_DRAW", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "CANCEL_DRAW 未在 BEFORE_DRAW 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "CANCEL_DRAW Lua 执行失败：" + error);
        Check(context.Cancelled, "CANCEL_DRAW 未设置 Cancelled 标志");
        Check(PassiveTriggerResolver.CancelsEvent(card, context.Cancelled), "CANCEL_DRAW 应通过 Cancelled 标志判定为取消事件");
    }

    private static void ValidateSkipEnemyBattlePhase(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "SKIP_ENEMY_BATTLE_PHASE");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 38);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "SKIP_ENEMY_BATTLE_PHASE 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ENEMY_BATTLE_PHASE_STARTED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ENEMY_BATTLE_PHASE_STARTED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "SKIP_ENEMY_BATTLE_PHASE 未在 ENEMY_BATTLE_PHASE_STARTED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "SKIP_ENEMY_BATTLE_PHASE Lua 执行失败：" + error);
        Check(context.Cancelled, "SKIP_ENEMY_BATTLE_PHASE 未设置 Cancelled 标志");
        Check(PassiveTriggerResolver.CancelsEvent(card, context.Cancelled), "SKIP_ENEMY_BATTLE_PHASE 应通过 Cancelled 标志判定为取消事件");
    }

    private static void ValidateDiscardEqualDraw(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "DISCARD_EQUAL_DRAW");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 39);
        var card1 = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"));
        var card2 = new CardInstance(cards.First(c => c.handler_key == "HEAL_CLEANSE"));
        playerDeck.Hand.Add(card1);
        playerDeck.Hand.Add(card2);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "DISCARD_EQUAL_DRAW 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ALLY_TURN_ENDED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ALLY_TURN_ENDED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "DISCARD_EQUAL_DRAW 未在 ALLY_TURN_ENDED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "DISCARD_EQUAL_DRAW Lua 执行失败：" + error);
        Check(playerDeck.Hand.Count == 0, $"DISCARD_EQUAL_DRAW 未弃掉所有其他手牌，期望 0，实际 {playerDeck.Hand.Count}");
    }

    private static void ValidateGambleActionPoints(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "GAMBLE_ACTION_POINTS");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var battle = new BattleState(playerDeck, enemyDeck, 40);
        var passive = new CardInstance(card);
        Check(battle.SetPassive("player", 0, passive), "GAMBLE_ACTION_POINTS 伏牌登记失败");
        battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ALLY_BATTLE_PHASE_STARTED" };
        var triggered = new PassiveTriggerResolver().Collect(battle, "player", "ALLY_BATTLE_PHASE_STARTED", battle.CurrentPassiveEvent);
        Check(triggered.Count == 1, "GAMBLE_ACTION_POINTS 未在 ALLY_BATTLE_PHASE_STARTED 时触发");
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = passive, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "GAMBLE_ACTION_POINTS Lua 执行失败：" + error);
        Check(battle.PlayerActionPoints >= 0 && battle.PlayerActionPoints <= 3, $"GAMBLE_ACTION_POINTS 随机 AP 越界，期望 0-3，实际 {battle.PlayerActionPoints}");
    }

    private static void ValidateStealTemporary(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "STEAL_TEMPORARY");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var stolenCard = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"), "ai");
        stolenCard.Zone = CardInstance.ZoneKind.Hand;
        enemyDeck.Hand.Add(stolenCard);
        var battle = new BattleState(playerDeck, enemyDeck, 41);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "STEAL_TEMPORARY Lua 执行失败：" + error);
        Check(playerDeck.Hand.Contains(stolenCard), "STEAL_TEMPORARY 偷牌未进入玩家手牌");
        Check(stolenCard.CurrentCost() == 0, $"STEAL_TEMPORARY 偷牌未归零费用，期望 0，实际 {stolenCard.CurrentCost()}");
        battle.AdvanceTurn();
        Check(!playerDeck.Hand.Contains(stolenCard), "STEAL_TEMPORARY 回合结束后偷牌未归还");
        Check(enemyDeck.DiscardPile.Contains(stolenCard), "STEAL_TEMPORARY 偷牌未进入对方弃牌堆");
    }

    private static void ValidateZeroHandCosts(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "ZERO_HAND_COSTS");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var otherCard = new CardInstance(cards.First(c => c.handler_key == "STAR_UP"));
        playerDeck.Hand.Add(otherCard);
        var battle = new BattleState(playerDeck, enemyDeck, 42);
        var instance = new CardInstance(card);
        playerDeck.Hand.Add(instance);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "ZERO_HAND_COSTS Lua 执行失败：" + error);
        Check(otherCard.CurrentCost() == 0, $"ZERO_HAND_COSTS 未将其他手牌归零，期望 0，实际 {otherCard.CurrentCost()}");
        Check(instance.RuntimeCostModifier == 0, $"ZERO_HAND_COSTS 不应影响自身卡费用 modifier，期望 0，实际 {instance.RuntimeCostModifier}");
    }

    private static void ValidatePrepayAndDiscard(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "PREPAY_AND_DISCARD");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        enemyDeck.Hand.Add(new CardInstance(cards.First(c => c.handler_key == "STAR_UP"), "ai"));
        enemyDeck.Hand.Add(new CardInstance(cards.First(c => c.handler_key == "HEAL_CLEANSE"), "ai"));
        enemyDeck.Hand.Add(new CardInstance(cards.First(c => c.handler_key == "DAMAGE_STAR_ALL"), "ai"));
        var battle = new BattleState(playerDeck, enemyDeck, 43);
        battle.PlayerActionPoints = 3;
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "PREPAY_AND_DISCARD Lua 执行失败：" + error);
        Check(battle.PlayerActionPoints == 0, $"PREPAY_AND_DISCARD 未清空玩家 AP，期望 0，实际 {battle.PlayerActionPoints}");
        Check(battle.PlayerNextTurnBonus == 3, $"PREPAY_AND_DISCARD 未登记下回合返还，期望 3，实际 {battle.PlayerNextTurnBonus}");
        Check(enemyDeck.Hand.Count == 1, $"PREPAY_AND_DISCARD 未弃敌方 (AP-1) 张牌，期望 1，实际 {enemyDeck.Hand.Count}");
    }

    private static void ValidateRandomCrossAttack(Godot.Collections.Array<CardDefinition> cards)
    {
        var card = cards.First(c => c.handler_key == "RANDOM_CROSS_ATTACK");
        var playerDeck = new DeckState(); playerDeck.Setup([], "player");
        var enemyDeck = new DeckState(); enemyDeck.Setup([], "ai");
        var ally = new UnitState { Name = "友方", Type = "先锋", Hp = 30, MaxHp = 30, Attack = 5 };
        var enemy = new UnitState { Name = "敌方", Type = "刺客", Hp = 30, MaxHp = 30, Attack = 4 };
        var battle = new BattleState(playerDeck, enemyDeck, 44);
        battle.SynchronizeUnits([ally], [enemy]);
        var instance = new CardInstance(card);
        using var resolver = new CardResolver();
        var context = new CardExecutionContext { State = battle, Card = instance, OwnerDeck = playerDeck, OpponentDeck = enemyDeck, Log = _ => { } };
        Check(resolver.Resolve(context, out var error), "RANDOM_CROSS_ATTACK Lua 执行失败：" + error);
        Check(ally.Hp < 30 || enemy.Hp < 30, "RANDOM_CROSS_ATTACK 未造成任何交叉伤害");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
