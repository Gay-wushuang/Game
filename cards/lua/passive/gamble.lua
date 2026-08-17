-- 赌；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("GAMBLE_ACTION_POINTS")
log_card("赌：效果已结算。")
