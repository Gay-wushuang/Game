using System;
using System.Linq;

public sealed class CardApi(CardExecutionContext context)
{
    public CardExecutionContext Context { get; } = context;
    private System.Collections.Generic.List<UnitState> FriendlyUnits => Context.OwnerDeck.OwnerId == "player" ? Context.State.PlayerUnits : Context.State.EnemyUnits;
    private System.Collections.Generic.List<UnitState> OpposingUnits => Context.OwnerDeck.OwnerId == "player" ? Context.State.EnemyUnits : Context.State.PlayerUnits;

    public int DamageTarget(int amount)
    {
        var target = Context.Target; if (target?.Alive != true) return 0;
        var before = new BattleEventData(BattleEvent.BeforeDamage) { Source = Context.Source, Target = target, Card = Context.Card, Amount = Math.Max(0, amount) };
        Context.State.Events.Publish(before); if (before.Cancelled) return 0;
        var incoming = Math.Max(0, before.Amount);
        var absorbed = Math.Min(target.ShieldPoints, incoming); target.ShieldPoints -= absorbed; incoming -= absorbed;
        var applied = Math.Min(target.Hp, incoming); target.Hp -= applied;
        Context.State.Events.Publish(new(BattleEvent.AfterDamage) { Source = Context.Source, Target = target, Card = Context.Card, Amount = applied });
        if (!target.Alive) Context.State.Events.Publish(new(BattleEvent.HeroDefeated) { Source = Context.Source, Target = target, Card = Context.Card });
        return applied;
    }

    public int HealTarget(int amount)
    {
        var target = Context.Target; if (target?.Alive != true) return 0;
        var applied = Math.Min(Math.Max(0, amount), target.MaxHp - target.Hp); target.Hp += applied; return applied;
    }

    public void AddTargetAttack(int amount) { if (Context.Target?.Alive == true) Context.Target.Attack = Math.Max(0, Context.Target.Attack + amount); }
    public void AddTargetExp(int amount) { if (Context.Target?.Alive == true) Context.Target.Exp = Math.Max(0, Context.Target.Exp + amount); }
    public int Draw(int count) => Context.OwnerDeck.Draw(Math.Max(0, count)).Count;
    public void Cancel() => Context.Cancelled = true;

    public void ZeroOtherHandCosts()
    {
        foreach (var card in Context.OwnerDeck.Hand.Where(card => card != Context.Card)) card.RuntimeCostModifier = -card.Definition.action_cost;
    }

    public int DiscardOtherHand()
    {
        if (Context.State.PreventsDiscard(Context.OwnerDeck.OwnerId)) return 0;
        var cards = Context.OwnerDeck.Hand.Where(card => card != Context.Card).ToList();
        foreach (var card in cards) Context.OwnerDeck.Discard(card);
        return cards.Count;
    }

    public int DiscardOpponentHand(int count)
    {
        if (Context.State.PreventsDiscard(Context.OpponentDeck.OwnerId)) return 0;
        var amount = Math.Min(Math.Max(0, count), Context.OpponentDeck.Hand.Count);
        for (var index = 0; index < amount; index++) Context.OpponentDeck.Discard(Context.OpponentDeck.Hand[Context.State.Random.Next(Context.OpponentDeck.Hand.Count)]);
        return amount;
    }

    public bool StealRandomOpponentCard()
    {
        if (Context.OpponentDeck.Hand.Count == 0) return false;
        var original = Context.OpponentDeck.Hand[Context.State.Random.Next(Context.OpponentDeck.Hand.Count)];
        Context.OpponentDeck.Discard(original);
        var copy = new CardInstance(original.Definition, Context.OwnerDeck.OwnerId) { Zone = CardInstance.ZoneKind.Hand, FaceUp = Context.OwnerDeck.OwnerId == "player", RuntimeCostOverride = 0, ExileAtTurnEnd = true, IsTemporaryCopy = true };
        if (Context.OwnerDeck.Hand.Count >= DeckState.HandLimit) Context.OwnerDeck.Exile(copy); else Context.OwnerDeck.Hand.Add(copy);
        return true;
    }

    public void PrepayAllActionPointsAndDiscardOpponent()
    {
        var isPlayer = Context.OwnerDeck.OwnerId == "player";
        var paid = isPlayer ? Context.State.PlayerActionPoints : Context.State.EnemyActionPoints;
        DiscardOpponentHand(Math.Max(0, paid - 1));
        if (isPlayer) { Context.State.PlayerActionPoints = 0; Context.State.PlayerNextTurnBonus += paid; }
        else { Context.State.EnemyActionPoints = 0; Context.State.EnemyNextTurnBonus += paid; }
    }

    public void RandomCrossAttack()
    {
        var allies = Context.State.PlayerUnits.Where(unit => unit.Alive).ToList(); var enemies = Context.State.EnemyUnits.Where(unit => unit.Alive).ToList();
        if (allies.Count == 0 || enemies.Count == 0) return;
        var ally = allies[Context.State.Random.Next(allies.Count)]; var enemy = enemies[Context.State.Random.Next(enemies.Count)];
        if (Context.State.Random.NextDouble() < .5) enemy.Hp = Math.Max(0, enemy.Hp - ally.Attack); else ally.Hp = Math.Max(0, ally.Hp - enemy.Attack);
    }

    public void TemporarilyRandomizeOpponentClass()
    {
        var living = OpposingUnits.Where(unit => unit.Alive).ToList(); if (living.Count == 0) return;
        var target = living[Context.State.Random.Next(living.Count)];
        // 首次临时修改时记录原始 Type，避免嵌套效果后恢复到中间状态。
        if (target.OriginalType == null) target.OriginalType = target.Type;
        var classes = new[] { "先锋", "刺客", "斥候", "祭司" };
        target.Type = classes[Context.State.Random.Next(classes.Length)];
        Context.State.Schedule(1, () =>
        {
            if (target.OriginalType != null) { target.Type = target.OriginalType; target.OriginalType = null; }
        });
    }

    public void TemporarilySwapOpposingStats()
    {
        var friendly = FriendlyUnits.Where(unit => unit.Alive).ToList(); var opposing = OpposingUnits.Where(unit => unit.Alive).ToList(); if (friendly.Count == 0 || opposing.Count == 0) return;
        var first = friendly[Context.State.Random.Next(friendly.Count)]; var second = opposing[Context.State.Random.Next(opposing.Count)];
        // 首次临时修改时记录双方最初的 Type / Attack，避免嵌套效果后恢复到中间状态。
        if (first.OriginalType == null) first.OriginalType = first.Type;
        if (first.OriginalAttack == null) first.OriginalAttack = first.Attack;
        if (second.OriginalType == null) second.OriginalType = second.Type;
        if (second.OriginalAttack == null) second.OriginalAttack = second.Attack;
        // 先缓存原始值，再交换，避免直接赋值导致 second 被覆盖
        var firstType = first.Type; var firstAttack = first.Attack;
        first.Type = second.Type; first.Attack = second.Attack;
        second.Type = firstType; second.Attack = firstAttack;
        Context.State.Schedule(1, () =>
        {
            // 使用保存的最初值恢复，而不是闭包中捕获的"当前值"。
            if (first.OriginalType != null) { first.Type = first.OriginalType; first.OriginalType = null; }
            if (first.OriginalAttack != null) { first.Attack = first.OriginalAttack.Value; first.OriginalAttack = null; }
            if (second.OriginalType != null) { second.Type = second.OriginalType; second.OriginalType = null; }
            if (second.OriginalAttack != null) { second.Attack = second.OriginalAttack.Value; second.OriginalAttack = null; }
        });
    }

    public int DamageRandomEnemy(int amount)
    {
        var living = OpposingUnits.Where(unit => unit.Alive).ToList(); if (living.Count == 0) return 0;
        var target = living[Context.State.Random.Next(living.Count)];
        var redirected = new CardExecutionContext { State = Context.State, Card = Context.Card, OwnerDeck = Context.OwnerDeck, OpponentDeck = Context.OpponentDeck, Source = Context.Source, Target = target, Log = Context.Log };
        return new CardApi(redirected).DamageTarget(amount);
    }

    public void SetTargetStar(int amount) { if (Context.Target?.Alive == true) Context.Target.Star = Math.Min(6, Context.Target.Star + amount); }
    public void SetTargetShield(float ratio, int turns) { if (Context.Target?.Alive == true) { Context.Target.ShieldRatio = ratio; Context.Target.ShieldTurns = turns; } }
    public void SetLinkTurns(int turns) { if (Context.Source != null) Context.Source.LinkTurns = turns; if (Context.Target != null) Context.Target.LinkTurns = turns; }
    public void SetTargetDamageMultiplier(float multiplier) { if (Context.Target?.Alive == true) Context.Target.DamageTakenMultiplier = multiplier; }
    public void AddGrudgeStacksToOpponents(int stacks) { foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) unit.GrudgeStacks += stacks; }
    public void SetCeasefireOnOpponents(int turns) { foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) unit.CeasefireTurns = turns; }
    public void IncreaseNextEnemyCardCost(int amount) { foreach (var held in Context.OpponentDeck.Hand) held.RuntimeCostModifier += amount; }
    public int RefillHand(int targetCount) => Draw(Math.Max(0, targetCount - Context.OwnerDeck.Hand.Count));
    public void SetRandomActionPoints()
    {
        // 规则：50% 恢复当前回合至上限，50% 独立标记下回合行动力归零。
        var isPlayer = Context.OwnerDeck.OwnerId == "player";
        var rng = Context.State.Random;
        if (rng.Next(2) == 0)
        {
            // 50%：恢复当前回合至上限
            if (isPlayer) Context.State.PlayerActionPoints = BattleState.DefaultActionPoints;
            else Context.State.EnemyActionPoints = BattleState.DefaultActionPoints;
        }
        else
        {
            if (isPlayer) Context.State.PlayerZeroNextTurnActionPoints = true;
            else Context.State.EnemyZeroNextTurnActionPoints = true;
        }
    }
    public void CounterPassiveSet()
    {
        if (Context.State.CurrentPassiveEvent is not { SubjectCard: CardInstance subject } ctx) return;
        Context.State.RemovePassive(subject);
        Context.State.InvalidatedPassives.Add((ctx.SubjectOwnerId, ctx.SubjectSlotIndex, subject));
        ApplyExposePenalty(subject.Definition, ctx.SubjectOwnerId);
    }
    public int RedirectToAdjacent()
    {
        var ctx = Context.State.CurrentPassiveEvent; if (ctx == null || ctx.AttackTargetSlot < 0) return -1;
        var candidates = ctx.AliveAllySlots.Where(slot => slot != ctx.AttackTargetSlot).ToList();
        if (candidates.Count == 0) return -1;
        ctx.RedirectSlot = candidates[Context.State.Random.Next(candidates.Count)];
        return ctx.RedirectSlot;
    }
    public void CopyResolvedCard()
    {
        var subject = Context.State.CurrentPassiveEvent?.SubjectCard; if (subject == null) return;
        var copy = new CardInstance(subject.Definition, Context.OwnerDeck.OwnerId) { RuntimeCostOverride = Param(Context.Card.Definition, "cost", 0), ExileAtTurnEnd = true, IsTemporaryCopy = true, FaceUp = Context.OwnerDeck.OwnerId == "player" };
        if (Context.OwnerDeck.Hand.Count >= DeckState.HandLimit) Context.OwnerDeck.Exile(copy); else Context.OwnerDeck.Hand.Add(copy);
    }
    public void SummonDelayedRabbit()
    {
        var ctx = Context.State.CurrentPassiveEvent;
        var slotIndex = ctx?.SubjectSlotIndex ?? -1;
        if (slotIndex < 0) return;
        Context.State.QueueSummon(ctx?.SubjectOwnerId ?? "ai", slotIndex, new UnitState { Name = "兔子", Type = "无职业", Hp = 30, MaxHp = 30, Attack = 0, Star = 0, CanAttack = false, CanRetaliate = false, CardTargetable = false, CountsForOutcome = false, TriggersHeroDeath = false });
        var card = Context.Card.Definition;
        Context.State.Schedule(Param(card, "delay_rounds", 3), () => { foreach (var unit in OpposingUnits.Where(unit => unit.Alive && unit.CountsForOutcome)) unit.Hp = Math.Max(0, unit.Hp - Param(card, "damage", 25)); });
    }
    public void ReviveTargetReducedMaxHp()
    {
        if (Context.Target == null || Context.Target.Alive) return;
        Context.Target.MaxHp = Math.Max(1, (int)Math.Floor(Context.Target.MaxHp * .8));
        Context.Target.Hp = Context.Target.MaxHp;
    }
    public void FreeUnansweredAttack(int amount) => DamageRandomEnemy(amount);
    public int GetSourceAttack() => Context.Source?.Attack ?? Context.Target?.Attack ?? 0;
    public int GetTargetStar() => Context.Target?.Star ?? 0;
    public int GetCardParamInt(string key, int fallback = 0) => Param(Context.Card.Definition, key, fallback);
    public float GetCardParamFloat(string key, float fallback = 0f)
    {
        var def = Context.Card.Definition;
        if (def.effect_params.TryGetValue(key, out Godot.Variant value) && value.VariantType == Godot.Variant.Type.Float) return value.AsSingle();
        return fallback;
    }

    private static float ParamFloat(CardDefinition card, string key, float fallback) => card.effect_params.TryGetValue(key, out Godot.Variant value) && value.VariantType == Godot.Variant.Type.Float ? value.AsSingle() : fallback;

    public void ResolveCardEffect(string handler)
    {
        var card = Context.Card.Definition;
        switch (handler) {
            case "STEAL_TEMPORARY": StealRandomOpponentCard(); break;
            case "STAR_UP": if (Context.Target != null) Context.Target.Star = Math.Min(6, Context.Target.Star + 1); break;
            case "HEAL_PERCENT": if (Context.Target != null) HealTarget((int)MathF.Round(Context.Target.MaxHp * ParamFloat(card, "max_hp_ratio", .15f))); break;
            case "CANCEL_NEXT_ACTIVE": break;
            case "DEAL_DAMAGE": DamageTarget(Param(card, "damage", 25)); break;
            case "DISCARD_DRAW_AP":
                var discardCount = Math.Min(Param(card, "discard_up_to", 3), Context.OwnerDeck.Hand.Count(cardInHand => cardInHand != Context.Card));
                if (!Context.State.PreventsDiscard(Context.OwnerDeck.OwnerId)) foreach (var held in Context.OwnerDeck.Hand.Where(cardInHand => cardInHand != Context.Card).Take(discardCount).ToList()) Context.OwnerDeck.Discard(held);
                Draw(Param(card, "draw", 2));
                if (Context.OwnerDeck.OwnerId == "player") Context.State.PlayerActionPoints += Param(card, "ap_gain", 2); else Context.State.EnemyActionPoints += Param(card, "ap_gain", 2);
                break;
            case "CONSUME_AP_REFUND_NEXT":
                if (Context.OwnerDeck.OwnerId == "player") { var x = Context.State.PlayerActionPoints; Context.State.PlayerActionPoints = 0; Context.State.PlayerNextTurnActionPointsOverride = x + Param(card, "next_turn_set_offset", 1); }
                else { var x = Context.State.EnemyActionPoints; Context.State.EnemyActionPoints = 0; Context.State.EnemyNextTurnActionPointsOverride = x + Param(card, "next_turn_set_offset", 1); }
                break;
            case "APPLY_DAMAGE_ATTACK_BUFF": if (Context.Target != null) { Context.Target.DamageTakenMultiplier = 1f + ParamFloat(card, "damage_taken_increase", .2f); var gain = (int)MathF.Round(Context.Target.Attack * ParamFloat(card, "attack_increase", .25f)); Context.Target.Attack += gain; Context.Target.AttackRestore += gain; } break;
            case "EXTRA_ATTACK_BUFF": if (Context.Target != null) { var gain = (int)MathF.Round(Context.Target.Attack * ParamFloat(card, "attack_increase", .1f)); Context.Target.Attack += gain; Context.Target.AttackRestore += gain; Context.Target.ExtraAttacksRemaining += Param(card, "extra_attacks", 1); } break;
            case "CONSUME_AP_DRAW_REFUND":
                if (Context.OwnerDeck.OwnerId == "player") { var x = Context.State.PlayerActionPoints; Context.State.PlayerActionPoints = x + Param(card, "current_ap_gain_offset", 3); Context.State.PlayerNextTurnBonus += Param(card, "next_turn_ap_delta", -2); Draw(Math.Max(0, x + Param(card, "draw_offset", -1))); }
                else { var x = Context.State.EnemyActionPoints; Context.State.EnemyActionPoints = x + Param(card, "current_ap_gain_offset", 3); Context.State.EnemyNextTurnBonus += Param(card, "next_turn_ap_delta", -2); Draw(Math.Max(0, x + Param(card, "draw_offset", -1))); }
                break;
            case "SELECT_FROM_PILES":
                foreach (var selected in Context.OwnerDeck.DrawPile.Take(Param(card, "count", 2)).ToList()) { Context.OwnerDeck.DrawPile.Remove(selected); selected.Zone = CardInstance.ZoneKind.Hand; Context.OwnerDeck.Hand.Add(selected); }
                break;
            case "SILENCE": if (Context.Target != null) Context.Target.CeasefireTurns = Param(card, "rounds", 2) + 1; break;
            case "TRUE_DAMAGE": if (Context.Target != null) { var amount = (int)MathF.Round(Context.Target.MaxHp * ParamFloat(card, "max_hp_ratio", .4f)); Context.Target.Hp = Math.Max(0, Context.Target.Hp - amount); } break;
            case "ONGOING_SHIELD": foreach (var unit in FriendlyUnits.Where(unit => unit.Alive && unit.CountsForOutcome)) unit.ShieldPoints += (int)MathF.Round(unit.MaxHp * ParamFloat(card, "shield_ratio", .05f)); break;
            case "PREVENT_DISCARD": break;
            case "DRAW_ON_PLACE_AND_NEXT": Draw(Context.State.CurrentPassiveEvent?.EventKey == "NEXT_ALLY_TURN_STARTED" ? Param(card, "next_turn_draw", 1) : Param(card, "immediate_draw", 2)); break;
            case "REVIVE_WITH_PENALTY": if (Context.Target != null && !Context.Target.Alive) { Context.Target.Hp = Math.Max(1, (int)MathF.Round(Context.Target.MaxHp * ParamFloat(card, "hp_ratio", .4f))); Context.Target.Star = Math.Max(0, Context.Target.Star - Param(card, "star_reduction", 1)); Context.Target.DeathHandled = false; } break;
            case "HARD_CHOICE": if (Context.OpponentDeck.Hand.Count > 0 && !Context.State.PreventsDiscard(Context.OpponentDeck.OwnerId)) DiscardOpponentHand(1); else { var target = OpposingUnits.Where(unit => unit.Alive).OrderBy(_ => Context.State.Random.Next()).FirstOrDefault(); if (target != null) target.Attack = Math.Max(0, target.Attack - (int)MathF.Round(target.Attack * ParamFloat(card, "attack_ratio", .1f))); } break;
            case "INCREASE_NEXT_COST": foreach (var held in Context.OpponentDeck.Hand) held.RuntimeCostModifier += Param(card, "cost_increase", 1); break;
            case "GAMBLE_AP": SetRandomActionPointsV2(); break;
            case "HEAL_CLEANSE": HealTarget(Param(card, "heal", 20)); break;
            case "CANCEL_PENDING_EFFECT": case "CANCEL_DAMAGE": case "CANCEL_DRAW": case "SKIP_ENEMY_BATTLE_PHASE": Cancel(); break;
            case "DAMAGE_STAR_ALL": DamageTarget(Param(card, "damage", 15)); break;
            case "APPLY_SHIELD": if (Context.Target != null) { Context.Target.ShieldRatio = Context.Target.Star >= Param(card, "star_required", 4) ? .5f : .2f; Context.Target.ShieldTurns = 1; } break;
            case "LINK_RESONANCE": if (Context.Source != null && Context.Target != null) { Context.Source.LinkTurns = 1; Context.Target.LinkTurns = 1; } break;
            case "ZERO_HAND_COSTS": ZeroOtherHandCosts(); break;
            case "APPLY_DAMAGE_HEAL_AMPLIFY": if (Context.Target != null) Context.Target.DamageTakenMultiplier = 1.1f; break;
            case "FREE_UNANSWERED_ATTACK": DamageRandomEnemy(Context.Source?.Attack ?? Context.Target?.Attack ?? 0); break;
            case "PREPAY_AND_DISCARD": PrepayAllActionPointsAndDiscardOpponent(); break;
            case "APPLY_GRUDGE": foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) { var stacks = Param(card, "stacks", 3); var penalty = Math.Max(0, (int)MathF.Round(unit.Attack * ParamFloat(card, "attack_reduction_per_stack", .05f))); unit.GrudgeStacks += stacks; unit.GrudgeAttackPenaltyPerStack = Math.Max(unit.GrudgeAttackPenaltyPerStack, penalty); unit.Attack = Math.Max(0, unit.Attack - penalty * stacks); } break;
            case "FORCE_MUTUAL_ATTACK": ForceOpponentsToAttack(); break;
            case "APPLY_CEASEFIRE": foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) unit.CeasefireTurns = Param(card, "silence_turns", 2); break;
            case "RANDOM_CROSS_ATTACK": RandomCrossAttack(); break;
            case "TEMP_RANDOM_CLASS": TemporarilyRandomizeOpponentClass(); break;
            case "COUNTER_PASSIVE_SET": CounterPassiveSet(); break;
            case "REVIVE_REDUCED_MAX_HP": if (Context.Target != null && !Context.Target.Alive) { Context.Target.MaxHp = Math.Max(1, (int)Math.Floor(Context.Target.MaxHp * .8)); Context.Target.Hp = Context.Target.MaxHp; } break;
            case "TARGETED_HARD_CHOICE": DiscardOpponentHand(1); break;
            case "NEXT_ENEMY_CARD_COST_UP": foreach (var held in Context.OpponentDeck.Hand) held.RuntimeCostModifier += 1; break;
            case "REFILL_HAND": Draw(Math.Max(0, 5 - Context.OwnerDeck.Hand.Count)); break;
            case "REDIRECT_TO_ADJACENT": RedirectToAdjacent(); break;
            case "SWAP_CLASS_AND_ATTACK": TemporarilySwapOpposingStats(); break;
            case "DISCARD_EQUAL_DRAW": DiscardOtherHand(); break;
            case "GAMBLE_ACTION_POINTS": SetRandomActionPoints(); break;
            case "COPY_RESOLVED_CARD": CopyResolvedCard(); break;
            case "SUMMON_DELAYED_RABBIT": SummonDelayedRabbit(); break;
        }
    }

    private void SetRandomActionPointsV2()
    {
        var player = Context.OwnerDeck.OwnerId == "player";
        if (Context.State.Random.NextDouble() < ParamFloat(Context.Card.Definition, "success_chance", .5f))
        {
            if (player) Context.State.PlayerActionPoints = BattleState.DefaultActionPoints; else Context.State.EnemyActionPoints = BattleState.DefaultActionPoints;
        }
        else if (player) Context.State.PlayerNextTurnBonus -= 3; else Context.State.EnemyNextTurnBonus -= 3;
    }

    private static bool HasExposeConsequences(CardDefinition exposedPassive) =>
        Param(exposedPassive, "expose_ap_bonus", 0) > 0
        || (exposedPassive.effect_params.TryGetValue("expose_penalty_ratio", out Godot.Variant ratio) && ratio.VariantType == Godot.Variant.Type.Float)
        || exposedPassive.handler_key == "TARGETED_HARD_CHOICE";

    private void ApplyExposePenalty(CardDefinition exposedPassive, string exposedOwnerId)
    {
        if (!HasExposeConsequences(exposedPassive)) return;
        var deck = exposedOwnerId == "player" ? Context.State.PlayerDeck : Context.State.EnemyDeck;
        if (Param(exposedPassive, "expose_ap_bonus", 0) > 0)
        {
            if (exposedOwnerId == "player") Context.State.PlayerNextTurnBonus += Param(exposedPassive, "expose_ap_bonus", 0);
            else Context.State.EnemyNextTurnBonus += Param(exposedPassive, "expose_ap_bonus", 0);
        }
        if (exposedPassive.effect_params.TryGetValue("expose_penalty_ratio", out Godot.Variant ratioValue) && ratioValue.VariantType == Godot.Variant.Type.Float)
        {
            var units = exposedOwnerId == "player" ? Context.State.PlayerUnits : Context.State.EnemyUnits;
            foreach (var unit in units.Where(unit => unit.Alive)) unit.MaxHp = Math.Max(1, (int)Math.Floor(unit.MaxHp * (1f - ratioValue.AsSingle())));
        }
        if (exposedPassive.handler_key == "TARGETED_HARD_CHOICE" && deck.Hand.Count > 0)
            deck.Discard(deck.Hand[Context.State.Random.Next(deck.Hand.Count)]);
    }

    public void ForceOpponentsToAttack()
    {
        var units = OpposingUnits.Where(unit => unit.Alive).OrderBy(_ => Context.State.Random.Next()).Take(2).ToList(); if (units.Count < 2) return;
        var firstAttack = units[0].Attack; var secondAttack = units[1].Attack; units[0].Hp = Math.Max(0, units[0].Hp - secondAttack); units[1].Hp = Math.Max(0, units[1].Hp - firstAttack);
    }
    private static int Param(CardDefinition card, string key, int fallback) => card.effect_params.TryGetValue(key, out Godot.Variant value) && value.VariantType == Godot.Variant.Type.Int ? value.AsInt32() : fallback;
}
