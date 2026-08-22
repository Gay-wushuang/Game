using System.Collections.Generic;
using System.Linq;

public sealed class PassiveTriggerResolver
{
    public IReadOnlyList<BattleState.PlacedPassive> Collect(BattleState state, string ownerId, string eventKey, PassiveEventContext? context = null)
    {
        var matches = state.MatchingPassives(ownerId, eventKey).OrderBy(placed => placed.SlotIndex).Where(placed => CanTrigger(placed, context, state)).ToList();
        foreach (var match in matches.Where(match => match.Card.Definition.handler_key is not "ONGOING_SHIELD" and not "PREVENT_DISCARD")) state.RemovePassive(match.Card);
        return matches;
    }

    public static bool CanTrigger(BattleState.PlacedPassive placed, PassiveEventContext? context, BattleState state)
    {
        if (context == null) return true;
        return placed.Card.Definition.handler_key switch {
            "REDIRECT_ATTACK" or "REDIRECT_TO_ADJACENT" => HasRedirectTarget(context),
            "SUMMON_DELAYED_RABBIT" => context.EventKey != "ENEMY_SLOT_EMPTY" || context.SubjectSlotIndex >= 0,
            _ => true,
        };
    }

    private static bool HasRedirectTarget(PassiveEventContext context)
    {
        if (context.AttackTargetSlot < 0) return false;
        return context.AliveAllySlots.Any(slot => slot != context.AttackTargetSlot);
    }

    public static bool CancelsEvent(CardDefinition definition, bool cancelledFlag = false) =>
        cancelledFlag;
}
