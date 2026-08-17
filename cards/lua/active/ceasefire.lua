-- 止戈；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
set_ceasefire_on_opponents(get_card_param_int("silence_turns", 2))
log_card("止戈：效果已结算。")
