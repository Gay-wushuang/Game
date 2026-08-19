using System;
using System.Collections.Generic;

public sealed class CardResolver : IDisposable
{
    private readonly BuiltinCardResolver _builtin = new();
    private readonly LuaCardRuntime _lua = new();
    public bool LuaAvailable => _lua.Available;
    public bool ValidateLua(string path, out string error) => _lua.ValidateScript(path, out error);
    public bool ValidateSandboxIsolation(out string error) => _lua.ValidateSandboxIsolation(out error);
    public void ReloadLua(IEnumerable<CardDefinition>? cardsToValidate = null) => _lua.Reload(cardsToValidate);
    public bool CanResolveBuiltin(string handlerKey) => _builtin.CanResolve(handlerKey);

    /// <summary>
    /// 自身设置 Cancelled 标志的主动卡牌 handler_key 集合。
    /// 这些卡牌的 Cancelled=true 是预期行为，不应被视为被动取消错误。
    /// </summary>
    private static readonly HashSet<string> SelfCancellingHandlers = new()
    {
        "CANCEL_PENDING_EFFECT",
        "CANCEL_DAMAGE",
        "CANCEL_DRAW",
        "SKIP_ENEMY_BATTLE_PHASE"
    };

    public bool Resolve(CardExecutionContext context, out string error)
    {
        error = "";
        var result = context.Card.Definition.logic_mode switch {
            "LUA" => _lua.Resolve(context, out error),
            "BUILTIN" => ResolveBuiltin(context, out error),
            _ => Fail($"未知逻辑模式：{context.Card.Definition.logic_mode}", out error),
        };
        
        if (result && context.Cancelled)
        {
            // 如果卡牌自身就是取消类卡牌（主动锦囊），Cancelled 是预期行为
            if (SelfCancellingHandlers.Contains(context.Card.Definition.handler_key))
            {
                error = "CANCELLED";  // 保留 CANCELLED 标记供 TrainingArena 检查
                return true;  // 但卡牌执行成功，返回 true
            }
            // 否则视为被动取消，返回错误
            error = "CANCELLED";
            return false;
        }
        
        return result;
    }
    private bool ResolveBuiltin(CardExecutionContext context, out string error)
    {
        if (_builtin.Resolve(context)) { error = ""; return true; }
        return Fail($"未注册内置处理器：{context.Card.Definition.handler_key}", out error);
    }
    private static bool Fail(string message, out string error) { error = message; return false; }
    public void Dispose() => _lua.Dispose();
}
