using System;

public sealed class CardResolver : IDisposable
{
    private readonly BuiltinCardResolver _builtin = new();
    private readonly LuaCardRuntime _lua = new();
    public bool LuaAvailable => _lua.Available;
    public bool ValidateLua(string path, out string error) => _lua.ValidateScript(path, out error);
    public void ReloadLua() => _lua.Reload();
    public bool CanResolveBuiltin(string handlerKey) => _builtin.CanResolve(handlerKey);

    public bool Resolve(CardExecutionContext context, out string error)
    {
        error = "";
        return context.Card.Definition.logic_mode switch {
            "LUA" => _lua.Resolve(context, out error),
            "BUILTIN" => ResolveBuiltin(context, out error),
            _ => Fail($"未知逻辑模式：{context.Card.Definition.logic_mode}", out error),
        };
    }
    private bool ResolveBuiltin(CardExecutionContext context, out string error)
    {
        if (_builtin.Resolve(context)) { error = ""; return true; }
        return Fail($"未注册内置处理器：{context.Card.Definition.handler_key}", out error);
    }
    private static bool Fail(string message, out string error) { error = message; return false; }
    public void Dispose() => _lua.Dispose();
}
