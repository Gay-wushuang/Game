-- 非暴力不合作；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
discard_opponent_hand(1)
log_card("非暴力不合作：效果已结算。")
