using System;
using System.Collections.Generic;

public sealed class BuiltinCardResolver
{
    private readonly Dictionary<string, Action<CardApi, CardDefinition>> _handlers = new(StringComparer.Ordinal) {
        ["HEAL_CLEANSE"] = (api, card) => { api.HealTarget(Param(card, "heal", 20)); var target = api.Context.Target; if (target != null && target.Star >= Param(card, "star_required", 2)) { target.DebuffTurns = 0; target.Attack += target.AttackRestore; target.AttackRestore = 0; } },
        ["DAMAGE_STAR_ALL"] = (api, card) => api.DamageTarget(Param(card, "damage", 15)),
        ["APPLY_DAMAGE_HEAL_AMPLIFY"] = (api, _) => { if (api.Context.Target != null) api.Context.Target.DamageTakenMultiplier = 1.1f; },
        ["APPLY_SHIELD"] = (api, card) => { if (api.Context.Target != null) { api.Context.Target.ShieldRatio = api.Context.Target.Star >= Param(card, "star_required", 4) ? .5f : .2f; api.Context.Target.ShieldTurns = 1; } },
        ["APPLY_GRUDGE"] = (api, card) => { foreach (var unit in api.Context.State.EnemyUnits) if (unit.Alive) unit.GrudgeStacks += Param(card, "stacks", 5); },
        ["APPLY_CEASEFIRE"] = (api, card) => { foreach (var unit in api.Context.State.EnemyUnits) if (unit.Alive) unit.CeasefireTurns = Param(card, "silence_turns", 2); },
    };

    public bool CanResolve(string handlerKey) => _handlers.ContainsKey(handlerKey);
    public bool Resolve(CardExecutionContext context)
    {
        if (!_handlers.TryGetValue(context.Card.Definition.handler_key, out var handler)) return false;
        context.State.Events.Publish(new(BattleEvent.CardPlayed) { Source = context.Source, Target = context.Target, Card = context.Card });
        handler(new CardApi(context), context.Card.Definition);
        context.State.Events.Publish(new(BattleEvent.CardResolved) { Source = context.Source, Target = context.Target, Card = context.Card });
        return true;
    }
    private static int Param(CardDefinition card, string key, int fallback) => card.effect_params.TryGetValue(key, out Godot.Variant value) && value.VariantType == Godot.Variant.Type.Int ? value.AsInt32() : fallback;
}
