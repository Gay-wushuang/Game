using Godot;

/// <summary>
/// 卡牌的组合式规则数据。组件只描述意图，不能直接修改 BattleState；
/// 实际合法性、时序和原子效果仍由 C# 与受限 Lua 入口控制。
/// </summary>
public sealed class CardComponentSet
{
    public CardSourceComponent Source { get; init; } = new();
    public CardCostComponent Cost { get; init; } = new();
    public CardTargetComponent Target { get; init; } = new();
    public CardTriggerComponent Triggers { get; init; } = new();
    public CardEffectComponent Effect { get; init; } = new();
    public CardLifecycleComponent Lifecycle { get; init; } = new();
    public CardLimitComponent Limits { get; init; } = new();
    public bool IsExplicit { get; init; }
}

public sealed class CardSourceComponent
{
    public string Selector { get; init; } = "NONE";
}

public sealed class CardCostComponent
{
    public string Mode { get; init; } = "FIXED";
    public int BaseCost { get; init; }
}

public sealed class CardTargetComponent
{
    public string Key { get; init; } = "NONE";
}

public sealed class CardTriggerComponent
{
    public string[] Keys { get; init; } = [];
}

public sealed class CardEffectComponent
{
    public string HandlerKey { get; init; } = "";
    public Godot.Collections.Dictionary Parameters { get; init; } = new();
}

public sealed class CardLifecycleComponent
{
    public const string Discard = "DISCARD";
    public const string Exile = "EXILE";
    public string OnResolve { get; init; } = Discard;
}

public sealed class CardLimitComponent
{
    public int CooldownTurns { get; init; }
}
