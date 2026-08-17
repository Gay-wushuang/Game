-- 多就是坏；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("CANCEL_DRAW")
log_card("多就是坏：效果已结算。")
