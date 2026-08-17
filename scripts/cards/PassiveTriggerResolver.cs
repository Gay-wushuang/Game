using System.Collections.Generic;
using System.Linq;

public sealed class PassiveTriggerResolver
{
    public IReadOnlyList<BattleState.PlacedPassive> Collect(BattleState state, string ownerId, string eventKey)
    {
        var matches = state.MatchingPassives(ownerId, eventKey).OrderBy(placed => placed.SlotIndex).ToList();
        foreach (var match in matches) state.RemovePassive(match.Card);
        return matches;
    }

    public static bool CancelsEvent(CardDefinition definition) => definition.handler_key is "CANCEL_DAMAGE" or "SKIP_ENEMY_BATTLE_PHASE" or "CANCEL_DRAW";
}
