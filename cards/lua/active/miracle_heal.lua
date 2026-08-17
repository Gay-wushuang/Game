-- 妙手回春；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
heal_target(get_card_param_int("heal", 20))
log_card("妙手回春：效果已结算。")
