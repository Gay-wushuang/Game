using Godot;
using System;
using System.Linq;
using System.Text.Json;

public static class CardCatalog
{
    public const int V2ExpectedCount = 60;
    public const int ExpectedActiveCount = 30;
    public const int ExpectedPassiveCount = 30;

    public static Godot.Collections.Array<CardDefinition> Load(string path = "res://data/generated/cards.generated.json")
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) throw new InvalidOperationException($"无法读取卡牌目录：{path}");
        using var document = JsonDocument.Parse(file.GetAsText());
        var result = new Godot.Collections.Array<CardDefinition>();
        foreach (var row in document.RootElement.EnumerateArray()) result.Add(Parse(row));
        ValidateRuntime(result);
        return result;
    }

    public static void ValidateRuntime(Godot.Collections.Array<CardDefinition> cards)
    {
        if (cards.Count == 0) throw new InvalidOperationException("卡牌目录不能为空");
        var ids = cards.Select(card => card.id.ToString()).ToList();
        if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count()) throw new InvalidOperationException("卡牌 card_id 存在重复");
        var codes = cards.Select(card => card.design_code).ToList();
        if (codes.Count != codes.Distinct(StringComparer.Ordinal).Count()) throw new InvalidOperationException("卡牌 design_code 存在重复");
        foreach (var card in cards)
        {
            if (!card.components.IsExplicit) throw new InvalidOperationException($"{card.display_name} 缺少显式 components");
            if (string.IsNullOrWhiteSpace(card.components.Effect.HandlerKey)) throw new InvalidOperationException($"{card.display_name} 缺少 effect.handler_key");
            if (string.IsNullOrWhiteSpace(card.components.Target.Key)) throw new InvalidOperationException($"{card.display_name} 缺少 target.key");
            if (card.components.Cost.BaseCost < 0) throw new InvalidOperationException($"{card.display_name} 的 cost.base_cost 不能为负数");
            if (card.components.Limits.CooldownTurns < 0) throw new InvalidOperationException($"{card.display_name} 的 limits.cooldown_turns 不能为负数");
            if (card.components.Lifecycle.OnResolve is not CardLifecycleComponent.Discard and not CardLifecycleComponent.Exile)
                throw new InvalidOperationException($"{card.display_name} 的 lifecycle.on_resolve 非法：{card.components.Lifecycle.OnResolve}");
            if (card.logic_mode != "LUA" || string.IsNullOrWhiteSpace(card.lua_script)) throw new InvalidOperationException($"{card.display_name} 缺少独立 Lua 入口");
            if (!FileAccess.FileExists(card.lua_script)) throw new InvalidOperationException($"{card.display_name} 的 Lua 脚本不存在：{card.lua_script}");
        }
    }

    private static CardDefinition Parse(JsonElement row)
    {
        var componentRoot = row.TryGetProperty("components", out var explicitComponents) ? explicitComponents : default;
        var hasComponents = componentRoot.ValueKind == JsonValueKind.Object;
        var cost = Component(componentRoot, "cost");
        var source = Component(componentRoot, "source");
        var target = Component(componentRoot, "target");
        var triggers = Component(componentRoot, "triggers");
        var effect = Component(componentRoot, "effect");
        var lifecycle = Component(componentRoot, "lifecycle");
        var limits = Component(componentRoot, "limits");

        var handler = StringValue(effect, "handler_key", StringValue(row, "handler_key", ""));
        var targetKey = StringValue(target, "key", StringValue(row, "target_key", "NONE"));
        var costMode = StringValue(cost, "mode", StringValue(row, "cost_mode", "FIXED"));
        var baseCost = IntValue(cost, "base_cost", IntValue(row, "base_cost", 0));
        var cooldown = IntValue(limits, "cooldown_turns", IntValue(row, "cooldown", 0));
        var parameters = Property(effect, "params", Property(row, "params"));
        var triggerValues = Property(triggers, "keys", Property(row, "trigger_keys"));
        var parsedParams = new Godot.Collections.Dictionary();
        if (parameters.ValueKind == JsonValueKind.Object)
            foreach (var item in parameters.EnumerateObject()) parsedParams[item.Name] = ToVariant(item.Value);
        var parsedTriggers = triggerValues.ValueKind == JsonValueKind.Array
            ? triggerValues.EnumerateArray().Select(value => value.GetString() ?? "").ToArray()
            : [];
        var parsedComponents = new CardComponentSet {
            IsExplicit = hasComponents,
            Source = new() { Selector = StringValue(source, "selector", "NONE") },
            Cost = new() { Mode = costMode, BaseCost = baseCost },
            Target = new() { Key = targetKey },
            Triggers = new() { Keys = parsedTriggers },
            Effect = new() { HandlerKey = handler, Parameters = parsedParams },
            Lifecycle = new() { OnResolve = StringValue(lifecycle, "on_resolve", CardLifecycleComponent.Discard) },
            Limits = new() { CooldownTurns = cooldown },
        };
        var definition = new CardDefinition {
            id = row.GetProperty("card_id").GetString() ?? "",
            design_code = row.GetProperty("design_code").GetString() ?? "",
            display_name = row.GetProperty("name").GetString() ?? "",
            description = row.GetProperty("rules_text").GetString() ?? "",
            rules_text = row.GetProperty("rules_text").GetString() ?? "",
            designer_notes = row.GetProperty("designer_notes").GetString() ?? "",
            card_kind = row.GetProperty("card_kind").GetString() == "PASSIVE" ? CardDefinition.CardKind.Passive : CardDefinition.CardKind.Active,
            cost_mode = costMode,
            action_cost = baseCost,
            target_key = targetKey,
            target_kind = ParseTarget(targetKey),
            rarity = row.GetProperty("rarity").GetInt32(),
            handler_key = handler,
            logic_mode = row.TryGetProperty("logic_mode", out var logicMode) ? logicMode.GetString() ?? "LUA" : "LUA",
            lua_script = row.TryGetProperty("lua_script", out var luaScript) ? luaScript.GetString() ?? "" : "",
            builtin_effect = LegacyEffect(handler),
            cooldown_turns = cooldown,
            trigger_keys = parsedTriggers,
            effect_params = parsedParams,
            components = parsedComponents,
        };
        foreach (var tag in row.GetProperty("keywords").EnumerateArray()) definition.tags = [.. definition.tags, tag.GetString() ?? ""];
        definition.effect_amount = PrimaryAmount(definition);
        return definition;
    }

    private static JsonElement Component(JsonElement root, string name) => Property(root, name);
    private static JsonElement Property(JsonElement element, string name, JsonElement fallback = default) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : fallback;
    private static string StringValue(JsonElement element, string property, string fallback) =>
        Property(element, property).ValueKind == JsonValueKind.String ? Property(element, property).GetString() ?? fallback : fallback;
    private static int IntValue(JsonElement element, string property, int fallback) =>
        Property(element, property).ValueKind == JsonValueKind.Number ? Property(element, property).GetInt32() : fallback;

    private static CardDefinition.TargetKind ParseTarget(string? value) => value switch {
        "SELECTED_ALLY" => CardDefinition.TargetKind.AllyHero,
        "SELECTED_ENEMY" => CardDefinition.TargetKind.Enemy,
        "ALLY_ENEMY_PAIR" => CardDefinition.TargetKind.AllyEnemyPair,
        "ANY_UNIT" => CardDefinition.TargetKind.AnyUnit,
        "SET_GATE" => CardDefinition.TargetKind.SetGate,
        "SET_SLOT" => CardDefinition.TargetKind.SetSlot,
        "SELECT_CARDS" => CardDefinition.TargetKind.SelectCards,
        "SELECT_CARDS_AND_ENEMIES" => CardDefinition.TargetKind.SelectCardsAndEnemies,
        "SELECT_OPPONENT_DISCARD" => CardDefinition.TargetKind.SelectOpponentDiscard,
        "SELECT_OPPONENT_HAND" => CardDefinition.TargetKind.SelectOpponentHand,
        _ => CardDefinition.TargetKind.None,
    };
    private static CardDefinition.BuiltinEffect LegacyEffect(string handler) => handler switch {
        "STEAL_TEMPORARY" => CardDefinition.BuiltinEffect.StealCard,
        "STAR_UP" => CardDefinition.BuiltinEffect.StarUp,
        "HEAL_CLEANSE" => CardDefinition.BuiltinEffect.Heal,
        "APPLY_DAMAGE_HEAL_AMPLIFY" => CardDefinition.BuiltinEffect.Custom,
        _ => CardDefinition.BuiltinEffect.Custom,
    };
    private static int PrimaryAmount(CardDefinition d)
    {
        foreach (var key in new[] { "heal", "attack", "exp", "damage", "amount", "stacks" })
            if (d.effect_params.TryGetValue(key, out Variant value) && value.VariantType == Variant.Type.Int) return value.AsInt32();
        return 1;
    }
    private static Variant ToVariant(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Number => value.TryGetInt32(out var integer) ? Variant.From(integer) : Variant.From(value.GetDouble()),
        JsonValueKind.True => Variant.From(true), JsonValueKind.False => Variant.From(false),
        JsonValueKind.String => Variant.From(value.GetString() ?? ""),
        _ => Variant.From(value.GetRawText()),
    };
}
