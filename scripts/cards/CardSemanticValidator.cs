using System;
using System.Linq;

public static class CardSemanticValidator
{
    public static void Validate(Godot.Collections.Array<CardDefinition> cards)
    {
        Check(cards.Count == CardCatalog.V2ExpectedCount,
            $"V2 数据集应为 {CardCatalog.V2ExpectedCount} 张，实际 {cards.Count} 张");
        Check(cards.Select(card => card.id.ToString()).Distinct().Count() == cards.Count,
            "V2 卡牌 ID 必须唯一");
        Check(cards.All(card => !string.IsNullOrWhiteSpace(card.display_name)),
            "V2 卡牌名称不能为空");
        Check(cards.All(card => !string.IsNullOrWhiteSpace(card.handler_key)),
            "V2 handler_key 不能为空");
        Check(cards.All(card => !string.IsNullOrWhiteSpace(card.target_key)),
            "V2 target_key 不能为空");
        Check(cards.Count(card => card.card_kind == CardDefinition.CardKind.Active) == 15,
            "V2 主动锦囊必须为 15 张");
        Check(cards.Count(card => card.card_kind == CardDefinition.CardKind.Passive) == 15,
            "V2 被动锦囊必须为 15 张");
        Check(cards.Where(card => card.card_kind == CardDefinition.CardKind.Passive)
                .All(card => card.target_kind == CardDefinition.TargetKind.SetGate),
            "V2 被动锦囊必须统一映射到独立战门");
        Check(cards.Where(card => card.cost_mode == "VARIABLE_AP").All(card => card.action_cost == 0),
            "VARIABLE_AP 基础费用必须为 0，并在运行时取当前全部 AP");
        Check(cards.All(card => card.rarity is >= 1 and <= 5),
            "V2 稀有度必须在 1 到 5 之间");
        Check(cards.All(card => card.cooldown_turns >= 0),
            "V2 冷却不能为负数");
        Check(DeckState.HandLimit == 8, "V2 全局手牌上限必须为 8");

        var guide = cards.First(card => card.id.ToString() == "card_resonant_guidance");
        Check(!guide.effect_params["requires_exact_discard"].AsBool(), "迷蒙指引必须允许不足3张时全部弃置");
        var exhaustive = cards.First(card => card.id.ToString() == "card_exhaustive_scheme");
        Check(exhaustive.effect_params.ContainsKey("next_turn_set_offset"), "机关算尽必须直接设置下回合初始AP");
        var artifact2 = cards.First(card => card.id.ToString() == "card_artifact_2");
        Check(artifact2.effect_params["blocked_discard_reasons"].AsString().Contains("TURN_END_CLEANUP", StringComparison.Ordinal), "神器2必须阻止回合结束自动弃牌");

        var rabbit = cards.First(card => card.id.ToString() == "card_wait_for_rabbit");
        Check(rabbit.effect_params["hp"].AsInt32() == 30,
            "守株待兔生成物 HP 必须为 30");
        Check(rabbit.effect_params["attack"].AsInt32() == 0,
            "守株待兔生成物攻击必须为 0");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
