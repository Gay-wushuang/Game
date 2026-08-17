-- 怨恨；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
add_grudge_stacks_to_opponents(get_card_param_int("stacks", 5))
log_card("怨恨：效果已结算。")
