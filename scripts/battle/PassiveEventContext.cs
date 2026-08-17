public sealed class PassiveEventContext
{
    public required string EventKey { get; init; }
    public CardInstance? SubjectCard { get; init; }
    public int SubjectSlotIndex { get; init; } = -1;
    public string SubjectOwnerId { get; init; } = "";
    public UnitState? AttackTarget { get; init; }
    public int AttackTargetSlot { get; init; } = -1;
    public int[] AliveAllySlots { get; init; } = [];
    public int RedirectSlot { get; set; } = -1;
}
