-- 重甲；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
local threshold = get_card_param_int("star_required", 4)
local ratio = 0.2
if get_target_star() >= threshold then ratio = 0.5 end
set_target_shield(ratio, 1)
log_card("重甲：效果已结算。")
