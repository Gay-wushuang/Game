-- 拒绝生效；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("CANCEL_PENDING_EFFECT")
log_card("拒绝生效：效果已结算。")
