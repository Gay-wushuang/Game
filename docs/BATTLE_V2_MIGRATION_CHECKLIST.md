# Battle V2 Migration Checklist

> 本文档由 V2 Gap Audit 生成：逐条对照 `docs/战斗流程V2.md` 与当前代码实现。
> 仅审计，不修改代码。迁移阶段依据依赖关系排序，低阶规则先于高阶规则。
>
> 状态说明：
> - ✅ Done：已实现且基本符合 V2
> - ❌ Gap：缺失或与 V2 冲突
> - ⚠️ Partial：部分实现或需细化
> - 🔴 Blocked：依赖前置阶段完成
> - ⏳ Later：明确不在当前 V2 核心范围

---

## 1. Turn / Round 骨架

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 1.1 | Round 由 Player Turn + Enemy Turn 组成 | `_turn` 单计数器，无 Round 概念 | ❌ Gap | `TrainingArena.cs` | Phase 2 |
| 1.2 | Turn Start 确认 BattleEnded | `EndTurn()` 已有 `IsFinished` 守卫，EnemyPhase 也有 | ✅ Done | `TrainingArena.cs` | Phase 2 |
| 1.3 | Turn End 流程固定（弃手牌→清零AP→死亡→胜负→换边） | 当前 `EndTurn()` 有被动触发/AdvanceTurn/抽牌，但无 Turn End 弃手牌 | ❌ Gap | `TrainingArena.cs`, `DeckState.cs` | Phase 2 |
| 1.4 | Round End 流程（状态处理→RoundIndex+1→离开部署期→下一 Player Turn） | 完全未实现 Round 概念 | ❌ Gap | `BattleState.cs`, `TrainingArena.cs` | Phase 2 |

## 2. 行动力 (AP)

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 2.1 | 每 Turn AP = 5 | `_ap = 3`, `BattleState.PlayerActionPoints = 3` | ❌ Gap | `TrainingArena.cs:L18`, `BattleState.cs` | Phase 2 |
| 2.2 | AP 不低于 0 | `EffectiveCost()` 无下限保护 | ⚠️ Partial | `TrainingArena.cs` | Phase 2 |
| 2.3 | AP 不跨 Turn 保留 | `EndTurn()` 重置 `_ap = 3`（应改为5）| ✅ Done (机制) | `TrainingArena.cs:L194` | Phase 2 |
| 2.4 | Turn End 剩余 AP 清零 | `EndTurn()` 在进入下一 Turn 时覆盖 AP 为 3（应改为5），但无明确 Turn End AP=0 结算步骤 | ⚠️ Partial | `TrainingArena.cs:L194` | Phase 2 |
| 2.5 | 卡牌/技能消耗 AP | 使用 `card.CurrentCost()` 扣 `_ap` | ✅ Done | `TrainingArena.cs` | Phase 2 |

## 3. 抽牌与手牌

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 3.1 | 每 Turn Start 抽 4 张 | 开局抽 4 (`_deck.Draw(4)`)，但 Turn Start 只抽补到 5 (`_deck.Draw()` in EndTurn) | ❌ Gap | `TrainingArena.cs:L58,L194`, `DeckState.cs` | Phase 2 |
| 3.2 | 手牌上限 8 张 | 当前上限 5 张（`Hand.Count >= 5`） | ❌ Gap | `TrainingArena.cs:L193` | Phase 2 |
| 3.3 | 超上限牌直接进弃牌堆 | 未实现；`DrawOne()` 在满手牌时拒绝 | ❌ Gap | `DeckState.cs`, `TrainingArena.cs` | Phase 2 |
| 3.4 | Turn End 弃未保留手牌 | 未实现；当前手牌跨 Turn 保留 | ❌ Gap | `DeckState.cs`, `TrainingArena.cs` | Phase 2 |
| 3.5 | 抽牌堆不足时洗回弃牌堆 | `DeckState.Draw()` 已实现：DrawPile 空时自动将 DiscardPile 洗回 DrawPile | ⚠️ Partial | `DeckState.cs:L26` | Phase 2 |
| 3.6 | 双方初始手牌各 4 张 | 当前 `_deck.Draw(4)` + `_aiDeck.Draw(4)` | ✅ Done | `TrainingArena.cs:L58` | Phase 2 |

## 4. 部署系统

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 4.1 | Round 1-5 每方每 Turn 必须部署 1 英雄 | AI 部署条件 `_turn <= 4`，无强制；玩家无 Round 部署窗口限制 | ❌ Gap | `TrainingArena.cs:L201` | Phase 3 |
| 4.2 | Round 6+ 不再部署 | AI 条件 `_turn <= 4` 已隐含，但玩家无对应限制 | ⚠️ Partial | `TrainingArena.cs:L201` | Phase 3 |
| 4.3 | 部署只能选未部署英雄 | HeroBag 排除已部署英雄 | ✅ Done | `TrainingArena.cs:L86` | Phase 3 |
| 4.4 | 必须选合法空位 | `slot.Unit?.Alive == true` 检查 | ✅ Done | `TrainingArena.cs:L96` | Phase 3 |
| 4.5 | 第一部署英雄 = 队长 | `_leaderId` / `_leaderTurns` 已有 | ✅ Done | `TrainingArena.cs:L96` | Phase 3 |
| 4.6 | 新部署英雄当 Turn 可立即行动 | 无 `HasAttackedThisTurn` 限制，等于允许 | ✅ Done (默认) | 后续 Phase 4 | Phase 3 |
| 4.7 | ReserveHeroCount 算存活 | `HasLivingHeroes()` 已实现 | ✅ Done | `BattleState.cs:L106-111` | Done |

## 5. 普通攻击 / 战斗行动

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 5.1 | 每英雄每 Turn 最多攻击 1 次 | `UnitState` 无 `HasAttackedThisTurn` 字段；可重复攻击同一英雄 | ❌ Gap | `UnitState.cs` | Phase 4 |
| 5.2 | 攻击后仍可使用技能 | 无限制 | ✅ Done (默认) | `TrainingArena.cs` | Phase 4 |
| 5.3 | 部署期（Round 1-5）普攻伤害 -2 | `BattleRules` 无此修正；`ConfirmAttack()` 直接用 `CalculateAttackValue` | ❌ Gap | `BattleRules.cs`, `TrainingArena.cs` | Phase 4 |
| 5.4 | 普攻伤害最低 0 | `Math.Max(0, d.Hp - damage)` 只 clamp HP 结果；若 damage 为负值会回血。需验证 `BattleRules` 是否在伤害值本身保证 `finalDamage >= 0` | ⚠️ Partial | `TrainingArena.cs:L104`, `BattleRules.cs` | Phase 4 |
| 5.5 | 攻击合法性检查（目标存活等） | `EnemyChosen()` 有 `slot.Unit?.Alive == true` 检查 | ✅ Done | `TrainingArena.cs:L102` | Phase 4 |
| 5.6 | AttackCommand 标准结算流程 | 分散在 `ConfirmAttack()`/`AiAttack()` 中，无统一 Command 对象 | ⚠️ Partial | `TrainingArena.cs` | Phase 4 |

## 6. 被动锦囊

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 6.1 | 已部署+存活英雄才能绑定 | `TryPlacePassive` 已验证 | ✅ Done | `BattleState.cs:L165-190` | Done |
| 6.2 | 每英雄最多 1 张被动 | `Passives.Exists` 检查已实现 | ✅ Done | `BattleState.cs:L185` | Done |
| 6.3 | 被动默认不消耗 AP | 当前 `PlacePassive` 扣 `_ap -= card.CurrentCost()` | ❌ Gap | `TrainingArena.cs:L186` | Phase 5 |
| 6.4 | 被动槽 = 英雄专属（非公共格） | 已绑定到 Hero Slot | ✅ Done | `TrainingArena.cs:L98` | Phase 5 |
| 6.5 | 触发后默认进弃牌堆 | `TriggerPassive()` 调 `ownerDeck.DiscardPlaced(card)` | ✅ Done | `TrainingArena.cs:L242` | Phase 5 |
| 6.6 | 持续型被动继续保留 | EXILE 与持续保留（Trigger 后仍绑定原 Hero Slot）语义不同。当前 `post_zone=EXILE` 进入 ExilePile，`DiscardPlaced` 进入弃牌堆，均不等于"继续保留在 Passive Slot" | ❌ Gap | `TrainingArena.cs:L242`, `DeckState.cs` | Phase 5 |
| 6.7 | 被动默认对敌方隐藏 | V2 要求"对敌方隐藏"，玩家看到自己的 Passive 是正确行为。需验证 AI Passive 是否对玩家（敌方）隐藏：`FaceUp` 由 `OwnerId == "player"` 控制，AI 卡 `FaceUp=false` → 玩家看到背面 | ✅ Done | `DeckState.cs:L28` | Phase 5 |

## 7. 死亡与胜负

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 7.1 | HP ≤ 0 → Alive = false + HeroDefeated | `UnitState.Alive => Hp > 0`；`FinalizeDeaths()` 已同步 | ✅ Done | `UnitState.cs`, `BattleState.cs:L117-121` | Done |
| 7.2 | 任何 HP 改变后检查死亡 | 部分路径（ConfirmAttack/AiAttack）调 `MarkDefeated()`；Lua/Builtin fallback 可能绕过 | ⚠️ Partial | `TrainingArena.cs`, `CardApi.cs` | Phase 6 |
| 7.3 | BattleEnded 后禁止所有操作 | 大部分入口有 `IsFinished` 守卫 | ✅ Done | `TrainingArena.cs` 多处 | Done |
| 7.4 | 敌方全灭 → PlayerVictory | `EvaluateOutcome()` 已实现 | ✅ Done | `BattleState.cs:L59-83` | Done |
| 7.5 | 我方全灭 → EnemyVictory | 同上 | ✅ Done | `BattleState.cs:L59-83` | Done |
| 7.6 | 双方同时全灭 → Draw | 同上 | ✅ Done | `BattleState.cs:L59-83` | Done |
| 7.7 | BattleEnded 后停止 AI 新行动 | `EnemyPhase` 有 `IsFinished` 检查 | ✅ Done | `TrainingArena.cs:L202` | Done |

## 8. 随机数 (RNG)

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 8.1 | 单 Battle RNG 权威源 | `BattleState.Random` 已统一 | ✅ Done | `BattleState.cs:L15` | Done |
| 8.2 | 开局洗牌使用同一 RNG | `BattleState` 构造时注入双方 Deck | ✅ Done | `BattleState.cs:L33-41` | Done |
| 8.3 | 弃牌堆洗回使用同一 RNG | `DeckState.Draw()` 已实现：DrawPile 空时自动将 DiscardPile 洗回 DrawPile，使用同一 `_random` | ✅ Done | `DeckState.cs:L26` | Phase 2 |
| 8.4 | AI 随机决策使用同一 RNG | `_battle.Random.Next()` 已使用 | ✅ Done | `TrainingArena.cs` 多处 | Done |
| 8.5 | Reset 后 RNG 回到初始 seed | `ResetRandom()` + `ResetTraining()` 已实现 | ✅ Done | `BattleState.cs:L47-57`, `TrainingArena.cs:L58` | Done |
| 8.6 | 相同 seed + 相同操作 → 相同结果 | 已实现（需 Godot 实测验证） | ✅ Done (COMPILE-ONLY) | `BattleOutcomeTest.cs` | Pending GPU |

## 9. 状态唯一来源

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 9.1 | 英雄生命由 BattleState 维护 | `UnitState` 直接被 UI 修改，无 BattleState 中间层 | ❌ Gap | `UnitState.cs`, `TrainingArena.cs` | Phase 6 |
| 9.2 | 手牌由 DeckState 维护 | `DeckState` 维护，但 TrainingArena 直接操作 `_deck.Hand` | ⚠️ Partial | `DeckState.cs`, `TrainingArena.cs` | Phase 6 |
| 9.3 | 行动力由 BattleState 维护 | `BattleState` 有 `PlayerActionPoints`/`EnemyActionPoints`，但 TrainingArena 自己也有 `_ap` | ❌ Gap | `BattleState.cs`, `TrainingArena.cs` | Phase 2 |
| 9.4 | Passive 由 BattleState 维护 | `BattleState.Passives` 是权威源，但仍需与 `DeckState`/`UnitSlot` 同步 | ⚠️ Partial | `BattleState.cs`, `TrainingArena.cs` | Phase 5 |
| 9.5 | UI 不保存权威状态 | UI `UnitSlot` 持有 `UnitState` 引用。需审计：谁有权修改 UnitState？是否存在 BattleState/UnitSlot 双写？是否需要手动双向同步？当前 TrainingArena 直接修改 `unit.Hp`、`unit.Attack` 等字段 | ⚠️ Partial | `UnitSlot.cs`, `TrainingArena.cs` | Phase 6 |

## 10. 卡牌执行一致性

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 10.1 | 主动卡牌使用后进入弃牌堆 | `_deck.Discard(card)` 已实现 | ✅ Done | `TrainingArena.cs` | Phase 6 |
| 10.2 | 卡牌执行失败不残留状态 | 部分路径有回滚（Passive try/commit），卡牌执行无事务 | ❌ Gap | `TrainingArena.cs`, `CardApi.cs` | Phase 6 |
| 10.3 | Lua/Builtin/fallback 统一执行边界 | 三路执行仍并存 | ❌ Gap | `LuaCardRuntime.cs`, `BuiltinCardResolver.cs`, `TrainingArena.cs` | Phase 6 |
| 10.4 | 所有 HP 修改最终触发死亡判定 | 部分路径直接改 HP 后未调 `MarkDefeated()` | ⚠️ Partial | `CardApi.cs`, `TrainingArena.cs` | Phase 6 |

## 11. 职业克制

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 11.1 | 职业克制只有一个权威来源 | `BattleRules` 是唯一权威源 | ✅ Done | `BattleRules.cs` | Done |
| 11.2 | 职业克制关系 | V2 只要求 BattleRules 为唯一权威源，具体关系来自项目职业规则表。当前 `BattleRules` 实现先锋→刺客→斥候→先锋循环，具体数值/关系需与项目职业规则表对照 | ⚠️ Partial | `BattleRules.cs:L6-11`, `docs/职业规则表` | Phase 4 |
| 11.3 | 克制伤害修正 | `CalculateRetaliation()` 有克制/被克制修正 | ✅ Done | `BattleRules.cs:L22-36` | Done |
| 11.4 | UI 层不保存职业克制 | 已实现 | ✅ Done | - | Done |

## 12. 英雄技能

| # | V2 Rule | 当前实现 | 状态 | 涉及文件 | 迁移阶段 |
|---|---------|---------|------|---------|---------|
| 12.1 | 技能默认可在普攻前或后使用 | 无限制 | ✅ Done (默认) | `TrainingArena.cs` | Phase 4 |
| 12.2 | 技能不消耗普攻次数 | 当前无 `HasAttackedThisTurn` 字段。需等 Phase 4 实现攻击次数系统后，明确验证技能调用不修改 `HasAttackedThisTurn` | 🔴 Blocked | `UnitState.cs`, `TrainingArena.cs` | Phase 4 |
| 12.3 | 技能冷却/次数/状态检查 | `Cooldown`/`SkillTurns` 已实现 | ✅ Done | `UnitState.cs`, `TrainingArena.cs` | Phase 4 |
| 12.4 | 技能伤害不受部署期 -2 | 当前技能伤害路径分散，无统一处理 | ⚠️ Partial | `TrainingArena.cs`, `CardApi.cs` | Phase 4 |

---

## 迁移阶段依赖图

```
Phase 2: Turn / Resource
├── Phase 3: Deployment (依赖 Turn 骨架)
│   └── Phase 4: Combat Actions (依赖部署完成)
│       └── Phase 5: Passive V2 (依赖战斗流程)
│           └── Phase 6: Card Execution Closure (依赖所有流程)
└── Phase 6: Card Execution Closure (也依赖 Phase 2)
```

### Phase 2 必须先完成的原因：
- AP=5、Turn Start 抽4、手牌上限8、Turn End 弃手牌 是后续所有规则的资源基础
- Round 概念是部署期（Round 1-5）的前提
- 统一行动力来源消除 TrainingArena._ap vs BattleState.ActionPoints 的双源问题

### Phase 2 完成标志：
- [ ] `BattleState` 正式持有 `RoundIndex`
- [ ] `BattleState` 正式持有 `CurrentTurnOwner` (Player/Enemy)
- [ ] 每 Turn Start: AP=5 → 抽4 → 部署 → 自由行动 → End Turn
- [ ] 手牌上限 8；超上限进弃牌堆
- [ ] Turn End 弃未保留手牌
- [ ] TrainingArena._ap 改为只读引用 BattleState.PlayerActionPoints

---

## 当前已完成但未在 V2 核心范围内（⏳）

| 项目 | 说明 |
|------|------|
| 完整职业克制数值表 | V2 只要求循环关系，具体数值后续补充 |
| 每个英雄具体技能设计 | 不在 V2 核心流程范围 |
| 每张锦囊具体效果 | 已由 Lua/Builtin 实现，待 Phase 6 收口 |
| 动画/镜头表现 | 不属于战斗逻辑范围 |
| PvP 网络同步 | 后续 |
| 主菜单/关卡/卡组/结算 UI | Vertical Slice 阶段 |