-- 暴力手段；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
damage_target(get_card_param_int("damage", 15))
log_card("暴力手段：效果已结算。")
