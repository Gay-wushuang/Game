using System;
using System.Collections.Generic;

public sealed class BattleEventBus
{
    private readonly Dictionary<BattleEvent, List<Action<BattleEventData>>> _listeners = [];

    public void Subscribe(BattleEvent eventType, Action<BattleEventData> listener)
    {
        if (!_listeners.TryGetValue(eventType, out var listeners)) _listeners[eventType] = listeners = [];
        listeners.Add(listener);
    }

    public void Publish(BattleEventData data)
    {
        if (!_listeners.TryGetValue(data.EventType, out var listeners)) return;
        foreach (var listener in listeners.ToArray()) listener(data);
    }

    public void Clear() => _listeners.Clear();
}
