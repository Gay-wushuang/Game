-- 守株待兔；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。
resolve_card_effect("SUMMON_DELAYED_RABBIT")
log_card("守株待兔：效果已结算。")
