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
        var applied = Math.Min(target.Hp, Math.Max(0, before.Amount)); target.Hp -= applied;
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
        var cards = Context.OwnerDeck.Hand.Where(card => card != Context.Card).ToList();
        foreach (var card in cards) Context.OwnerDeck.Discard(card);
        return cards.Count;
    }

    public int DiscardOpponentHand(int count)
    {
        var amount = Math.Min(Math.Max(0, count), Context.OpponentDeck.Hand.Count);
        for (var index = 0; index < amount; index++) Context.OpponentDeck.Discard(Context.OpponentDeck.Hand[Context.State.Random.Next(Context.OpponentDeck.Hand.Count)]);
        return amount;
    }

    public bool StealRandomOpponentCard()
    {
        if (Context.OpponentDeck.Hand.Count == 0) return false;
        var card = Context.OpponentDeck.Hand[Context.State.Random.Next(Context.OpponentDeck.Hand.Count)];
        Context.OpponentDeck.Hand.Remove(card); Context.OwnerDeck.Hand.Add(card); card.OwnerId = Context.OwnerDeck.OwnerId; card.Zone = CardInstance.ZoneKind.Hand; card.FaceUp = Context.OwnerDeck.OwnerId == "player"; card.RuntimeCostOverride = 0; card.ReturnToOriginalOwnerDiscardAtTurnEnd = true; return true;
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
        var target = living[Context.State.Random.Next(living.Count)]; var original = target.Type; var classes = new[] { "先锋", "刺客", "斥候", "祭司" };
        target.Type = classes[Context.State.Random.Next(classes.Length)]; Context.State.Schedule(1, () => target.Type = original);
    }

    public void TemporarilySwapOpposingStats()
    {
        var friendly = FriendlyUnits.Where(unit => unit.Alive).ToList(); var opposing = OpposingUnits.Where(unit => unit.Alive).ToList(); if (friendly.Count == 0 || opposing.Count == 0) return;
        var first = friendly[Context.State.Random.Next(friendly.Count)]; var second = opposing[Context.State.Random.Next(opposing.Count)]; var firstType = first.Type; var firstAttack = first.Attack; var secondType = second.Type; var secondAttack = second.Attack;
        first.Type = secondType; first.Attack = secondAttack; second.Type = firstType; second.Attack = firstAttack;
        Context.State.Schedule(1, () => { first.Type = firstType; first.Attack = firstAttack; second.Type = secondType; second.Attack = secondAttack; });
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
        // 规则：50% 恢复至上限（3），50% 下回合归零（当前清零）。
        var max = 3;
        var value = Context.State.Random.Next(2) == 0 ? max : 0;
        if (Context.OwnerDeck.OwnerId == "player") Context.State.PlayerActionPoints = value; else Context.State.EnemyActionPoints = value;
    }
    public void CounterPassiveSet()
    {
        var ctx = Context.State.CurrentPassiveEvent; var subject = ctx?.SubjectCard; if (subject == null || ctx == null) return;
        Context.State.RemovePassive(subject);
        Context.State.InvalidatedPassives.Add((ctx.SubjectOwnerId, ctx.SubjectSlotIndex, subject));
        ApplyExposePenalty(subject.Definition, ctx.SubjectOwnerId);
    }
    public int RedirectToAdjacent()
    {
        var ctx = Context.State.CurrentPassiveEvent; if (ctx == null || ctx.AttackTargetSlot < 0) return -1;
        var candidates = new System.Collections.Generic.List<int>();
        if (ctx.AliveAllySlots.Contains(ctx.AttackTargetSlot - 1)) candidates.Add(ctx.AttackTargetSlot - 1);
        if (ctx.AliveAllySlots.Contains(ctx.AttackTargetSlot + 1)) candidates.Add(ctx.AttackTargetSlot + 1);
        if (candidates.Count == 0) return -1;
        ctx.RedirectSlot = candidates[Context.State.Random.Next(candidates.Count)];
        return ctx.RedirectSlot;
    }
    public void CopyResolvedCard()
    {
        var subject = Context.State.CurrentPassiveEvent?.SubjectCard; if (subject == null) return;
        var copy = new CardInstance(subject.Definition, Context.OwnerDeck.OwnerId) { RuntimeCostOverride = Param(Context.Card.Definition, "cost", 0), ExileAtTurnEnd = true, FaceUp = Context.OwnerDeck.OwnerId == "player" };
        Context.OwnerDeck.Hand.Add(copy);
    }
    public void SummonDelayedRabbit()
    {
        var ctx = Context.State.CurrentPassiveEvent;
        var slotIndex = ctx?.SubjectSlotIndex ?? -1;
        if (slotIndex < 0) return;
        Context.State.QueueSummon(ctx?.SubjectOwnerId ?? "ai", slotIndex, new UnitState { Name = "兔子", Type = "无职业", Hp = 1, MaxHp = 1, Attack = 0, Star = 1 });
        var card = Context.Card.Definition;
        Context.State.Schedule(Param(card, "delay_turns", 3), () => { foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) unit.Hp = Math.Max(0, unit.Hp - Param(card, "damage", 20)); });
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

    public void ResolveCardEffect(string handler)
    {
        var card = Context.Card.Definition;
        switch (handler) {
            case "STEAL_TEMPORARY": StealRandomOpponentCard(); break;
            case "STAR_UP": if (Context.Target != null) Context.Target.Star = Math.Min(6, Context.Target.Star + 1); break;
            case "HEAL_CLEANSE": HealTarget(Param(card, "heal", 20)); break;
            case "CANCEL_PENDING_EFFECT": case "CANCEL_DAMAGE": case "CANCEL_DRAW": case "SKIP_ENEMY_BATTLE_PHASE": Cancel(); break;
            case "DAMAGE_STAR_ALL": DamageTarget(Param(card, "damage", 15)); break;
            case "APPLY_SHIELD": if (Context.Target != null) { Context.Target.ShieldRatio = Context.Target.Star >= Param(card, "star_required", 4) ? .5f : .2f; Context.Target.ShieldTurns = 1; } break;
            case "LINK_RESONANCE": if (Context.Source != null && Context.Target != null) { Context.Source.LinkTurns = 1; Context.Target.LinkTurns = 1; } break;
            case "ZERO_HAND_COSTS": ZeroOtherHandCosts(); break;
            case "APPLY_DAMAGE_HEAL_AMPLIFY": if (Context.Target != null) Context.Target.DamageTakenMultiplier = 1.1f; break;
            case "FREE_UNANSWERED_ATTACK": DamageRandomEnemy(Context.Source?.Attack ?? Context.Target?.Attack ?? 0); break;
            case "PREPAY_AND_DISCARD": PrepayAllActionPointsAndDiscardOpponent(); break;
            case "APPLY_GRUDGE": foreach (var unit in OpposingUnits.Where(unit => unit.Alive)) unit.GrudgeStacks += Param(card, "stacks", 5); break;
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
