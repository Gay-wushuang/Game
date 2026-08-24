/// <summary>
/// V2 正式卡牌只通过受限 Lua 入口结算。该兼容壳保留给 CardResolver，
/// 防止旧存档中的 BUILTIN 标记意外回退到已经删除的 V1 规则。
/// </summary>
public sealed class BuiltinCardResolver
{
    public bool CanResolve(string handlerKey) => false;

    public bool Resolve(CardExecutionContext context) => false;
}
