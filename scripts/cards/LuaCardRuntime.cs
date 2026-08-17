using Godot;
using System;

public sealed class LuaCardRuntime : IDisposable
{
    private GodotObject? _state;
    public bool Available => _state != null;

    public LuaCardRuntime()
    {
        Reload();
    }

    public bool Resolve(CardExecutionContext context, out string error)
    {
        error = "";
        if (_state == null) { error = "LuaState 不可用"; return false; }
        var path = context.Card.Definition.lua_script;
        if (string.IsNullOrWhiteSpace(path)) { error = "卡牌未配置 lua_script"; return false; }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) { error = $"无法读取 Lua 卡牌脚本：{path}"; return false; }
        var globals = _state.Get("globals").AsGodotObject();
        if (globals == null) { error = "Lua 沙盒全局表不可用"; return false; }
        var api = new CardApi(context);
        globals.Set("heal_target", Callable.From<int>(amount => { api.HealTarget(amount); }));
        globals.Set("damage_target", Callable.From<int>(amount => { api.DamageTarget(amount); }));
        globals.Set("draw_cards", Callable.From<int>(count => { api.Draw(count); }));
        globals.Set("zero_other_hand_costs", Callable.From(api.ZeroOtherHandCosts));
        globals.Set("discard_other_hand", Callable.From(() => { api.DiscardOtherHand(); }));
        globals.Set("discard_opponent_hand", Callable.From<int>(count => { api.DiscardOpponentHand(count); }));
        globals.Set("steal_random_opponent_card", Callable.From(() => { api.StealRandomOpponentCard(); }));
        globals.Set("prepay_and_discard_opponent", Callable.From(api.PrepayAllActionPointsAndDiscardOpponent));
        globals.Set("random_cross_attack", Callable.From(api.RandomCrossAttack));
        globals.Set("temporarily_randomize_opponent_class", Callable.From(api.TemporarilyRandomizeOpponentClass));
        globals.Set("temporarily_swap_opposing_stats", Callable.From(api.TemporarilySwapOpposingStats));
        globals.Set("resolve_card_effect", Callable.From<string>(api.ResolveCardEffect));
        globals.Set("damage_random_enemy", Callable.From<int>(amount => { api.DamageRandomEnemy(amount); }));
        globals.Set("log_card", Callable.From<string>(context.Log));
        var result = _state.Call("do_string", file.GetAsText(), path);
        var resultObject = result.AsGodotObject();
        if (resultObject?.IsClass("LuaError") == true) { error = resultObject.ToString(); return false; }
        return true;
    }

    public bool ValidateScript(string path, out string error)
    {
        error = ""; if (_state == null) { error = "LuaState 不可用"; return false; }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read); if (file == null) { error = $"无法读取：{path}"; return false; }
        var result = _state.Call("load_string", file.GetAsText(), path); var resultObject = result.AsGodotObject();
        if (resultObject?.IsClass("LuaError") == true) { error = resultObject.ToString(); return false; } return true;
    }
    public void Reload() { _state?.Dispose(); _state = ClassDB.ClassExists("LuaState") ? ClassDB.Instantiate("LuaState").AsGodotObject() : null; }

    public void Dispose() => _state?.Dispose();
}
