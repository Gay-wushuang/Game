-- 揭穿；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("COUNTER_PASSIVE_SET")
log_card("揭穿：效果已结算。")
