-- 村好剑；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
free_unanswered_attack(get_source_attack())
log_card("村好剑：效果已结算。")
