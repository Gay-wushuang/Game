-- 风暴；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("INCREASE_NEXT_COST")
log_card("风暴：效果已结算。")
