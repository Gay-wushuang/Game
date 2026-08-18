using Godot;
using System;
using System.Collections.Generic;

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
        globals.Set("cancel", Callable.From(api.Cancel));
        globals.Set("set_target_star", Callable.From<int>(amount => { api.SetTargetStar(amount); }));
        globals.Set("set_target_shield", Callable.From<float, int>((ratio, turns) => { api.SetTargetShield(ratio, turns); }));
        globals.Set("set_link_turns", Callable.From<int>(turns => { api.SetLinkTurns(turns); }));
        globals.Set("set_target_damage_multiplier", Callable.From<float>(m => { api.SetTargetDamageMultiplier(m); }));
        globals.Set("add_grudge_stacks_to_opponents", Callable.From<int>(stacks => { api.AddGrudgeStacksToOpponents(stacks); }));
        globals.Set("set_ceasefire_on_opponents", Callable.From<int>(turns => { api.SetCeasefireOnOpponents(turns); }));
        globals.Set("increase_next_enemy_card_cost", Callable.From<int>(amount => { api.IncreaseNextEnemyCardCost(amount); }));
        globals.Set("refill_hand", Callable.From<int>(count => { api.RefillHand(count); }));
        globals.Set("set_random_action_points", Callable.From(api.SetRandomActionPoints));
        globals.Set("revive_target_reduced_max_hp", Callable.From(api.ReviveTargetReducedMaxHp));
        globals.Set("force_opponents_to_attack", Callable.From(api.ForceOpponentsToAttack));
        globals.Set("free_unanswered_attack", Callable.From<int>(amount => { api.FreeUnansweredAttack(amount); }));
        globals.Set("get_source_attack", Callable.From(api.GetSourceAttack));
        globals.Set("get_target_star", Callable.From(api.GetTargetStar));
        globals.Set("counter_passive_set", Callable.From(api.CounterPassiveSet));
        globals.Set("redirect_to_adjacent", Callable.From(api.RedirectToAdjacent));
        globals.Set("copy_resolved_card", Callable.From(api.CopyResolvedCard));
        globals.Set("summon_delayed_rabbit", Callable.From(api.SummonDelayedRabbit));
        globals.Set("discard_opponent_hand", Callable.From<int>(count => { api.DiscardOpponentHand(count); }));
        globals.Set("discard_other_hand", Callable.From(api.DiscardOtherHand));
        globals.Set("get_card_param_int", Callable.From<string, int>((key, fallback) => api.GetCardParamInt(key, fallback)));
        globals.Set("get_card_param_float", Callable.From<string, float>((key, fallback) => api.GetCardParamFloat(key, fallback)));
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

    public bool ValidateSandboxIsolation(out string error)
    {
        error = "";
        if (_state == null) { error = "LuaState 不可用"; return false; }
        const string script = """
            local function must_fail(name, fn)
              local ok = pcall(fn)
              if ok then error(name .. ' unexpectedly succeeded') end
            end
            if os then must_fail('os.execute', function() os.execute('') end) end
            if io then must_fail('io.open', function() io.open('test') end) end
            must_fail('require', function() require('os') end)
            if FileAccess then must_fail('FileAccess', function() FileAccess.open('x', 1) end) end
            if Engine then must_fail('Engine', function() return Engine.get_main_loop() end) end
            """;
        var result = _state.Call("do_string", script, "res://tests/lua_sandbox_smoke.lua");
        var resultObject = result.AsGodotObject();
        if (resultObject?.IsClass("LuaError") == true) { error = resultObject.ToString(); return false; }
        return true;
    }
    public void Reload(IEnumerable<CardDefinition>? cardsToValidate = null)
    {
        // 先创建新的 LuaState，验证所有卡牌脚本通过后再替换旧状态。
        var previous = _state;
        GodotObject? next = null;
        if (ClassDB.ClassExists("LuaState"))
        {
            try { next = ClassDB.Instantiate("LuaState").AsGodotObject(); }
            catch { next = null; }
        }
        if (next == null)
        {
            // 新状态创建失败，保留旧状态用于回滚。
            return;
        }

        // 可选：使用新状态验证所有卡牌脚本。如果任何脚本失败，释放新状态并保留旧状态。
        if (cardsToValidate != null)
        {
            var temp = _state;
            _state = next;
            var allValid = true;
            foreach (var card in cardsToValidate)
            {
                if (string.IsNullOrWhiteSpace(card.lua_script)) continue;
                if (!ValidateScript(card.lua_script, out _))
                {
                    allValid = false;
                    break;
                }
            }
            _state = temp; // 恢复旧状态

            if (!allValid)
            {
                // 脚本验证失败：释放新状态，保留旧状态，不做任何变更。
                next.Dispose();
                return;
            }
        }

        // 所有验证通过，正式替换。
        previous?.Dispose();
        _state = next;
    }

    public void Dispose() => _state?.Dispose();
}
