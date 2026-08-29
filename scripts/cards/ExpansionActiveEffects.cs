using System;
using System.Collections.Generic;
using System.Linq;

public static class ExpansionActiveEffects
{
    public static readonly HashSet<string> Handlers = new(StringComparer.Ordinal) {
        "DESPERATE_GAMBIT", "CHAIN_SCHEME", "GATHERING_FORCE", "GRAFT_BUFF", "BURN_BOOKS",
        "CICADA_ESCAPE", "FLAME_MARK", "MEND_BROKEN_MIRROR", "HEAVENLY_REINFORCEMENT",
        "BACKLASH_CURSE", "STEAL_DAYLIGHT", "RECUPERATE", "COVERT_PASSAGE", "BREAKER", "UNDERMINE"
    };

    public static bool Resolve(CardExecutionContext c, string handler)
    {
        if (!Handlers.Contains(handler)) return false;
        var source = c.Source ?? Friendly(c).Where(u => u.Alive).OrderByDescending(u => u.Attack).FirstOrDefault();
        var target = c.Target ?? Opposing(c).FirstOrDefault(u => u.Alive && u.CardTargetable);
        switch (handler)
        {
            case "DESPERATE_GAMBIT":
                if (source == null || target == null || source.Hp <= 1) break;
                var ratio = source.Star >= 3 ? .30f : .20f;
                var paid = DamageResolution.Apply(new(source, source, c.Card, (int)MathF.Round(source.Hp * ratio), "LIFE_COST", true, true), (int)MathF.Round(source.Hp * ratio)).FinalHpDamage;
                Damage(c, target, (int)MathF.Round(paid * (source.Star >= 3 ? 2.5f : 2f))); break;
            case "CHAIN_SCHEME":
                c.State.RuntimeInts[$"chain:{c.OwnerDeck.OwnerId}"] = source?.Star >= 4 ? 2 : 1;
                c.State.RuntimeInts[$"chain_keep:{c.OwnerDeck.OwnerId}"] = source?.Star >= 3 ? 1 : 0; break;
            case "GATHERING_FORCE":
                if (source != null) { var gain = (int)MathF.Round(source.Attack * .2f); source.Attack += gain; source.RuntimeInts["charge_bonus"] = source.RuntimeInts.GetValueOrDefault("charge_bonus") + gain; source.RuntimeInts["charge_stacks"] = source.Star >= 3 ? 3 : 2; } break;
            case "GRAFT_BUFF": Graft(source, target, source?.Star >= 4 ? 2 : 1); break;
            case "BURN_BOOKS":
                var selected = SelectedCards(c, c.OwnerDeck.DiscardPile, 3); var enemies = Opposing(c).Where(u => u.Alive).ToList();
                for (var i = 0; i < selected.Count; i++) { c.OwnerDeck.Exile(selected[i]); if (enemies.Count > 0) Damage(c, enemies[SelectedEnemyIndex(c, i, enemies.Count)], source?.Star >= 3 ? 8 : 5); } break;
            case "CICADA_ESCAPE": if (source != null) { source.RuntimeInts["cicada_rounds"] = source.Star >= 5 ? -1 : 2; source.RuntimeInts["cicada_heal_pct"] = source.Star >= 4 ? 30 : 15; } break;
            case "FLAME_MARK":
                foreach (var unit in source?.Star >= 3 ? Opposing(c).Where(u => u.Alive) : target == null ? [] : [target]) unit.RuntimeInts["flame_marks"] = Math.Min(5, unit.RuntimeInts.GetValueOrDefault("flame_marks") + 3); break;
            case "MEND_BROKEN_MIRROR":
                var groups = Friendly(c).Concat(Opposing(c)).Where(u => u.LinkTurns > 0 || u.LinkedEnemy >= 0).ToList(); foreach (var unit in groups) { unit.LinkTurns = 0; unit.LinkedEnemy = -1; Damage(c, unit, 8); } if (groups.Count > 0 && source?.Star >= 3) AddAp(c, 1); break;
            case "HEAVENLY_REINFORCEMENT": c.DeployReserveHero?.Invoke(source?.Star ?? 1); break;
            case "BACKLASH_CURSE": foreach (var unit in Opposing(c).Where(u => u.Alive).ToList()) Damage(c, unit, 15); if (source?.Star < 4) foreach (var unit in Friendly(c).Where(u => u.Alive).ToList()) Damage(c, unit, source?.Star >= 3 ? 4 : 8); break;
            case "STEAL_DAYLIGHT":
                foreach (var original in SelectedCards(c, c.OpponentDeck.DiscardPile.TakeLast(3).ToList(), source?.Star >= 5 ? 3 : source?.Star >= 4 ? 2 : 1)) AddTemporaryCopy(c, original); break;
            case "RECUPERATE": foreach (var unit in Friendly(c).Where(u => u.Alive)) { unit.Hp = Math.Min(unit.MaxHp, unit.Hp + (int)MathF.Round(unit.MaxHp * (source?.Star >= 3 ? .15f : .10f))); if (source?.Star < 4) unit.RuntimeFlags.Add("rest_no_attack"); } break;
            case "COVERT_PASSAGE": foreach (var unit in source?.Star >= 4 ? Friendly(c).Where(u => u.Alive) : source == null ? [] : [source]) { var gain = (int)MathF.Round(unit.Attack * .3f); unit.Attack += gain; unit.RuntimeInts["stealth_bonus"] = unit.RuntimeInts.GetValueOrDefault("stealth_bonus") + gain; unit.RuntimeFlags.Add("stealth"); } break;
            case "BREAKER": Breaker(c, source); break;
            case "UNDERMINE": foreach (var held in SelectedCards(c, c.OpponentDeck.Hand, source?.Star >= 3 ? 2 : 1).ToList()) c.OpponentDeck.Discard(held); break;
        }
        return true;
    }

    private static List<UnitState> Friendly(CardExecutionContext c) => c.OwnerDeck.OwnerId == "player" ? c.State.PlayerUnits : c.State.EnemyUnits;
    private static List<UnitState> Opposing(CardExecutionContext c) => c.OwnerDeck.OwnerId == "player" ? c.State.EnemyUnits : c.State.PlayerUnits;
    private static void Damage(CardExecutionContext c, UnitState target, int amount) => new CardApi(new() { State = c.State, Card = c.Card, OwnerDeck = c.OwnerDeck, OpponentDeck = c.OpponentDeck, Source = c.Source, Target = target, Log = c.Log }).DamageTarget(amount);
    private static void AddAp(CardExecutionContext c, int amount) { if (c.OwnerDeck.OwnerId == "player") c.State.PlayerActionPoints += amount; else c.State.EnemyActionPoints += amount; }
    private static List<CardInstance> SelectedCards(CardExecutionContext c, IEnumerable<CardInstance> pool, int max)
    {
        var list = pool.ToList(); var ids = c.Selection?.SelectedCardIds ?? [];
        return ids.Count > 0 ? ids.Select(id => list.FirstOrDefault(x => x.InstanceId == id)).Where(x => x != null).Cast<CardInstance>().Take(max).ToList() : list.Take(max).ToList();
    }
    private static int SelectedEnemyIndex(CardExecutionContext c, int index, int count) => c.Selection?.EnemySlots.Count > index ? Math.Clamp(c.Selection.EnemySlots[index], 0, count - 1) : index % count;
    private static void AddTemporaryCopy(CardExecutionContext c, CardInstance original)
    {
        var copy = new CardInstance(original.Definition, c.OwnerDeck.OwnerId) { RuntimeCostOverride = 0, IsTemporaryCopy = true, ExileAtTurnEnd = true, FaceUp = c.OwnerDeck.OwnerId == "player", Zone = CardInstance.ZoneKind.Hand };
        if (c.OwnerDeck.Hand.Count >= DeckState.HandLimit) c.OwnerDeck.Exile(copy); else c.OwnerDeck.Hand.Add(copy);
    }
    private static void Graft(UnitState? recipient, UnitState? donor, int count)
    {
        if (recipient == null || donor == null) return;
        foreach (var key in donor.RuntimeInts.Keys.Where(k => k is "charge_stacks" or "flame_marks" or "attack_multiplier_turns").Take(count).ToList()) { recipient.RuntimeInts[key] = donor.RuntimeInts[key]; donor.RuntimeInts.Remove(key); }
    }
    private static void Breaker(CardExecutionContext c, UnitState? source)
    {
        if (source == null || Friendly(c).Count(u => u.Alive) >= Opposing(c).Count(u => u.Alive)) return;
        var usedKey = $"breaker_used:{c.OwnerDeck.OwnerId}"; var first = !c.State.RuntimeFlags.Contains(usedKey); c.State.RuntimeFlags.Add(usedKey);
        foreach (var unit in Friendly(c).Where(u => u.Alive)) { var gain = (int)MathF.Round(unit.Attack * (first ? 1f : .5f)); unit.Attack += gain; unit.RuntimeInts["breaker_bonus"] = gain; unit.RuntimeInts["breaker_rounds"] = first ? 0 : 2; }
    }
}
