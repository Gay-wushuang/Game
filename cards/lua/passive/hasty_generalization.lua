-- 以偏概全；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("COPY_RESOLVED_CARD")
log_card("以偏概全：效果已结算。")
