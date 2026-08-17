-- 操纵世界：行动力读取和结束回合仍由 C# 状态机控制，Lua 只组合卡牌行为。
prepay_and_discard_opponent()
log_card("操纵世界：预付当前行动力、随机弃置敌方手牌，并记录下回合返还。")
