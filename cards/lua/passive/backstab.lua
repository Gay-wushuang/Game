-- 背刺；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
redirect_to_adjacent()
log_card("背刺：效果已结算。")
