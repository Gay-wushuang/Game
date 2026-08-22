-- 机关算尽；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("CONSUME_AP_REFUND_NEXT")
log_card("机关算尽：效果已结算。")
