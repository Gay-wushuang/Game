-- 操纵世界；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("CONSUME_AP_DRAW_REFUND")
log_card("操纵世界：效果已结算。")
