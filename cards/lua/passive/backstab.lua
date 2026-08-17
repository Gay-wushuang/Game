-- 背刺；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("REDIRECT_TO_ADJACENT")
log_card("背刺：效果已结算。")
