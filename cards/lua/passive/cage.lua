-- 囚笼；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("SKIP_ENEMY_BATTLE_PHASE")
log_card("囚笼：效果已结算。")
