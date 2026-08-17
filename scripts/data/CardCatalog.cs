using Godot;
using System;
using System.Linq;
using System.Text.Json;

public static class CardCatalog
{
    public const int V1ExpectedCount = 30;

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
            if (card.logic_mode != "LUA" || string.IsNullOrWhiteSpace(card.lua_script)) throw new InvalidOperationException($"{card.display_name} 缺少独立 Lua 入口");
            if (!FileAccess.FileExists(card.lua_script)) throw new InvalidOperationException($"{card.display_name} 的 Lua 脚本不存在：{card.lua_script}");
        }
    }

    private static CardDefinition Parse(JsonElement row)
    {
        var handler = row.GetProperty("handler_key").GetString() ?? "";
        var targetKey = row.GetProperty("target_key").GetString() ?? "";
        var definition = new CardDefinition {
            id = row.GetProperty("card_id").GetString() ?? "",
            design_code = row.GetProperty("design_code").GetString() ?? "",
            display_name = row.GetProperty("name").GetString() ?? "",
            description = row.GetProperty("rules_text").GetString() ?? "",
            rules_text = row.GetProperty("rules_text").GetString() ?? "",
            designer_notes = row.GetProperty("designer_notes").GetString() ?? "",
            card_kind = row.GetProperty("card_kind").GetString() == "PASSIVE" ? CardDefinition.CardKind.Passive : CardDefinition.CardKind.Active,
            cost_mode = row.GetProperty("cost_mode").GetString() ?? "FIXED",
            action_cost = row.GetProperty("base_cost").ValueKind == JsonValueKind.Number ? row.GetProperty("base_cost").GetInt32() : 0,
            target_key = targetKey,
            target_kind = ParseTarget(targetKey),
            rarity = row.GetProperty("rarity").GetInt32(),
            handler_key = handler,
            logic_mode = row.TryGetProperty("logic_mode", out var logicMode) ? logicMode.GetString() ?? "LUA" : "LUA",
            lua_script = row.TryGetProperty("lua_script", out var luaScript) ? luaScript.GetString() ?? "" : "",
            builtin_effect = LegacyEffect(handler),
        };
        foreach (var tag in row.GetProperty("keywords").EnumerateArray()) definition.tags = [.. definition.tags, tag.GetString() ?? ""];
        foreach (var trigger in row.GetProperty("trigger_keys").EnumerateArray()) definition.trigger_keys = [.. definition.trigger_keys, trigger.GetString() ?? ""];
        foreach (var item in row.GetProperty("params").EnumerateObject()) definition.effect_params[item.Name] = ToVariant(item.Value);
        definition.effect_amount = PrimaryAmount(definition);
        return definition;
    }

    private static CardDefinition.TargetKind ParseTarget(string? value) => value switch {
        "SELECTED_ALLY" => CardDefinition.TargetKind.AllyHero,
        "SELECTED_ENEMY" => CardDefinition.TargetKind.Enemy,
        "ALLY_ENEMY_PAIR" => CardDefinition.TargetKind.AllyEnemyPair,
        "ANY_UNIT" => CardDefinition.TargetKind.AnyUnit,
        "SET_SLOT" => CardDefinition.TargetKind.SetSlot,
        _ => CardDefinition.TargetKind.None,
    };
    private static CardDefinition.BuiltinEffect LegacyEffect(string handler) => handler switch {
        "STEAL_TEMPORARY" => CardDefinition.BuiltinEffect.StealCard,
        "STAR_UP" => CardDefinition.BuiltinEffect.StarUp,
        "HEAL_CLEANSE" => CardDefinition.BuiltinEffect.Heal,
        "APPLY_DAMAGE_HEAL_AMPLIFY" => CardDefinition.BuiltinEffect.AddAttack,
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
