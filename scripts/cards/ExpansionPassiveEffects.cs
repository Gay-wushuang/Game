using System;
using System.Collections.Generic;
using System.Linq;

public static class ExpansionPassiveEffects
{
    public static readonly HashSet<string> Handlers = new(StringComparer.Ordinal) {
        "CHAIN_REACTION", "DELAYED_FUSE", "MIRROR_ILLUSION", "BLOOD_SACRIFICE", "DYING_WISH",
        "OVERLOAD_TURRET", "CORROSIVE_FOG", "SOUL_CONTRACT", "MUTUAL_DESTRUCTION", "SUBSTITUTION",
        "REWIND_TIME", "OPPORTUNISTIC_THEFT", "FINAL_DEFENSE", "ENERGY_SIPHON", "DRAGNET"
    };
    public static readonly HashSet<string> PersistentHandlers = new(StringComparer.Ordinal) { "DELAYED_FUSE", "MIRROR_ILLUSION", "CORROSIVE_FOG" };
    public static readonly HashSet<string> MirrorSafeHandlers = new(StringComparer.Ordinal) {
        "OVERLOAD_TURRET", "CORROSIVE_FOG", "DYING_WISH", "REWIND_TIME", "OPPORTUNISTIC_THEFT", "ENERGY_SIPHON", "DRAGNET"
    };

    public static bool Resolve(CardExecutionContext c, string handler)
    {
        if (!Handlers.Contains(handler)) return false;
        var evt = c.State.CurrentPassiveEvent;
        switch (handler)
        {
            case "CHAIN_REACTION":
                var candidate = c.State.Passives.Where(p => p.OwnerId == c.OwnerDeck.OwnerId && p.Card != c.Card && p.Card.Definition.handler_key != "MIRROR_ILLUSION").OrderBy(p => p.SlotIndex).FirstOrDefault();
                if (candidate != null) candidate.Card.RuntimeFlags.Add("chain_enhanced"); break;
            case "DELAYED_FUSE":
                var left = c.Card.RuntimeInts.GetValueOrDefault("latent", 2) - 1; c.Card.RuntimeInts["latent"] = left;
                if (left <= 0) { foreach (var unit in Opposing(c).Where(u => u.Alive).ToList()) Damage(c, unit, 15, "DELAYED"); c.State.RemovePassive(c.Card); } break;
            case "MIRROR_ILLUSION":
                if (evt?.EventKey == "PASSIVE_SET" && evt.SubjectCard != null && evt.SubjectCard.Definition.card_kind == CardDefinition.CardKind.Passive && MirrorSafeHandlers.Contains(evt.SubjectCard.Definition.handler_key)) c.Card.RuntimeStrings["mirror_instance"] = evt.SubjectCard.InstanceId;
                else c.State.RemovePassive(c.Card); break;
            case "BLOOD_SACRIFICE":
                var sacrifice = c.State.Passives.Where(p => p.OwnerId == c.OwnerDeck.OwnerId && p.Card != c.Card).OrderBy(p => p.SlotIndex).FirstOrDefault();
                if (sacrifice != null && c.Target != null && !c.Target.Alive) { c.State.RemovePassive(sacrifice.Card); c.OwnerDeck.DiscardPlaced(sacrifice.Card); c.Target.Hp = Math.Max(1, (int)MathF.Round(c.Target.MaxHp * .5f)); c.Target.DeathHandled = false; c.Target.HasAttackedThisTurn = false; } break;
            case "DYING_WISH": foreach (var unit in Friendly(c).Where(u => u.Alive)) unit.Hp = Math.Min(unit.MaxHp, unit.Hp + 15); break;
            case "OVERLOAD_TURRET":
                var random = Opposing(c).Where(u => u.Alive).OrderBy(_ => c.State.Random.Next()).FirstOrDefault(); if (random != null) Damage(c, random, 25);
                AddNextBonus(c, -2); break;
            case "CORROSIVE_FOG":
                var count = c.Card.RuntimeInts.GetValueOrDefault("corrosion_count") + 1; c.Card.RuntimeInts["corrosion_count"] = count;
                foreach (var unit in Opposing(c).Where(u => u.Alive).ToList()) Damage(c, unit, count switch { 1 => 3, 2 => 5, _ => 7 }, "CORROSION");
                if (count >= 3) c.State.RemovePassive(c.Card); break;
            case "SOUL_CONTRACT":
                var attacker = evt?.SourceUnit; if (attacker == null) { c.Cancelled = true; break; }
                var life = (int)Math.Ceiling(attacker.Attack * .5); if (attacker.Hp > life) DamageResolution.Apply(new(attacker, attacker, c.Card, life, "LIFE_COST", true, true), life); else c.Cancelled = true; break;
            case "MUTUAL_DESTRUCTION":
                if (evt?.AttackTarget != null && evt.PendingDamage >= evt.AttackTarget.Hp) { var reflected = Math.Min(evt.AttackTarget.Hp, evt.PendingDamage); c.Cancelled = true; if (evt.SourceUnit != null) Damage(c, evt.SourceUnit, reflected, "COUNTER"); else { var victim = Opposing(c).Where(u => u.Alive).OrderBy(_ => c.State.Random.Next()).FirstOrDefault(); if (victim != null) Damage(c, victim, reflected, "COUNTER"); } } break;
            case "SUBSTITUTION":
                var onceKey = $"substitution_used:{c.OwnerDeck.OwnerId}"; if (!c.State.RuntimeFlags.Contains(onceKey)) { c.State.RuntimeFlags.Add(onceKey); c.Cancelled = true; } break;
            case "REWIND_TIME":
                var recovered = c.OwnerDeck.DiscardPile.LastOrDefault(card => card != c.Card); if (recovered != null && c.OwnerDeck.Hand.Count < DeckState.HandLimit) { c.OwnerDeck.DiscardPile.Remove(recovered); recovered.Zone = CardInstance.ZoneKind.Hand; c.OwnerDeck.Hand.Add(recovered); } break;
            case "OPPORTUNISTIC_THEFT":
                c.OpponentDeck.PrepareDrawPile(); var original = c.OpponentDeck.PeekPreparedTop(); if (original != null) AddTemporaryCopy(c, original); break;
            case "FINAL_DEFENSE":
                var last = Friendly(c).Where(u => u.Alive).SingleOrDefault(); if (last != null) { last.Hp = last.MaxHp; var gain = (int)MathF.Round(last.Attack * .5f); last.Attack += gain; last.RuntimeInts["final_defense_bonus"] = gain; last.RuntimeInts["final_defense_turns"] = 2; last.RuntimeFlags.Add("retaliation_immune"); } break;
            case "ENERGY_SIPHON": if (c.OwnerDeck.OwnerId == "player") { c.State.EnemyActionPoints = Math.Max(0, c.State.EnemyActionPoints - 1); c.State.PlayerNextTurnBonus++; } else { c.State.PlayerActionPoints = Math.Max(0, c.State.PlayerActionPoints - 1); c.State.EnemyNextTurnBonus++; } break;
            case "DRAGNET": if (c.OpponentDeck.Hand.Count > 0) { c.Cancelled = true; c.OpponentDeck.Discard(c.OpponentDeck.Hand[c.State.Random.Next(c.OpponentDeck.Hand.Count)]); } break;
        }
        return true;
    }

    private static List<UnitState> Friendly(CardExecutionContext c) => c.OwnerDeck.OwnerId == "player" ? c.State.PlayerUnits : c.State.EnemyUnits;
    private static List<UnitState> Opposing(CardExecutionContext c) => c.OwnerDeck.OwnerId == "player" ? c.State.EnemyUnits : c.State.PlayerUnits;
    private static void AddNextBonus(CardExecutionContext c, int amount) { if (c.OwnerDeck.OwnerId == "player") c.State.PlayerNextTurnBonus += amount; else c.State.EnemyNextTurnBonus += amount; }
    private static void Damage(CardExecutionContext c, UnitState target, int amount, string tag = "NORMAL")
    {
        var result = DamageResolution.Apply(new(c.Source, target, c.Card, amount, tag, false, false, tag != "NORMAL"), amount); c.Log($"{c.Card.Definition.display_name} 对 {target.Name} 造成 {result.FinalHpDamage} 点{tag}伤害");
    }
    private static void AddTemporaryCopy(CardExecutionContext c, CardInstance original)
    {
        var copy = new CardInstance(original.Definition, c.OwnerDeck.OwnerId) { RuntimeCostOverride = 0, IsTemporaryCopy = true, ExileAtTurnEnd = true, FaceUp = c.OwnerDeck.OwnerId == "player", Zone = CardInstance.ZoneKind.Hand };
        if (c.OwnerDeck.Hand.Count >= DeckState.HandLimit) c.OwnerDeck.Exile(copy); else c.OwnerDeck.Hand.Add(copy);
    }
}
