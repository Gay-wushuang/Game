# Battle UI 返工验收报告

基线：`82abddf9d7ff58328004102bf81f051d35239119`  
返工分支：`codex/fix-battle-ui-master-completion`  
权威规格：`BattleUILayoutSpec.md`、`battle_ui_master_1920x1080.svg`

## 范围边界

本轮只修改战斗 UI 场景、UI 组件、UI 输入协调与 UI 专项测试。没有修改 `BattleState`、`BattleRules`、`CardResolver`、Lua、AI、胜负条件、被动触发、抽牌或 AP 规则。

基线提交中已有的 `HAND_EMPTY` 测试失败仍然存在，本轮未用 UI 提交修补该玩法问题。

## 最终节点树

```text
TrainingArena : Control
├─ BackgroundLayer : Control
│  └─ BattleBackground : ColorRect
├─ Margin
│  └─ Root
│     ├─ TopBar : MarginContainer
│     │  └─ TopContent
│     └─ MainRow
│        ├─ LeftSidebar : PanelContainer
│        │  └─ LeftContent
│        │     ├─ FactionPanel
│        │     ├─ Piles
│        │     └─ ReservedPanel（无内容时隐藏）
│        ├─ CenterColumn
│        │  ├─ Battlefield
│        │  │  ├─ EnemyRow（5 × UnitSlot）
│        │  │  ├─ AllyRow（5 × UnitSlot）
│        │  │  ├─ BattleFxLayer
│        │  │  └─ Announcement
│        │  └─ HandArea
│        │     └─ Hand : HandFan
│        └─ RightSidebar
│           └─ RightContent
│              ├─ ContentHost : BattleRightSidebar
│              │  ├─ CommanderOverview
│              │  └─ DetailView
│              ├─ RightSpacer
│              └─ CommandArea
└─ 业务弹窗（英雄卡包、牌堆、日志、设置、测试编辑器、技能、升星）
```

`UnitSlot` 统一组件结构：

```text
UnitSlot : Control
├─ Platform
├─ CharacterAnchor
│  └─ CharacterCanvas（固定128×160，Bottom Center）
│     ├─ CharacterSprite
│     └─ CharacterPlaceholder
├─ HpBar / HpText
├─ ClassIcon（文字占位）
├─ UnitInfo
├─ StatusIcons（当前为文字占位）
├─ PassiveCardSlot（72×96，3:4卡背占位）
├─ ActionPreview
├─ SelectionHighlight
├─ TargetHighlight
└─ InteractionArea
```

## 已完成

- 1920×1080 逻辑画布；根据实机精修反馈，将三栏从 Master 初稿的 240 / 1280 / 352 调整为 240 / 1328 / 304，降低右栏视觉重量。
- Top HUD 改为轻量 `MarginContainer`，不再使用全宽厚重面板。
- 独立 `BackgroundLayer` 与 `BattleFxLayer`。
- 固定 280×500 的右栏内容宿主及 `CommanderOverview/CardDetail/HeroDetail/EnemyDetail` 状态机；三个操作按钮同步压低高度。
- AP 只在顶部显示，左侧不再重复；右栏指挥官顺序为敌方在上、我方在下，与战场上下排映射一致。
- 卡牌、英雄、敌人详情统一进入右栏；关闭、取消和 Esc 恢复默认总览。
- 删除常规卡牌详情 `AcceptDialog` 路径；业务弹窗保持原职责。
- 10 个战位共用同一组件，统一全槽左键、右键、拖放输入。
- 角色增加固定 128×160 逻辑画布并以 Bottom Center 对齐统一地面线；固定 HP、72×96 伏牌区及选择/目标高亮。
- 战位常驻信息移除 ATK、EXP 和完整职业文字，完整数据进入右栏；战场只保留 HP、职业/星级占位标识、状态和行动预览。
- 敌方详情使用独立公开信息构建路径，不再因敌方数据是 `HeroDefinition` 而泄露完整技能、被动和队长能力。
- 手牌支持 1～8 张动态间距、轻弧旋转、Hover 回正/上浮/1.05 放大、邻牌排开和平滑回位。
- 小卡移除规则摘要及完整规则 Tooltip；完整规则以右栏详情为唯一常规入口。
- 拖拽预览保持 3:4；合法战位高亮、非法战位弱化；放下后沿用现有预览/确认结算入口。
- 左栏改为符号 + 实时数字，说明进入 Tooltip；移除开发占位说明和常驻操作教程。
- 部署、攻击、目标闪烁和锦囊播报动画入口及原有 Tween 时长未替换。

## 自动验收

```text
dotnet build .\法则演进.csproj -warnaserror
结果：0 warning，0 error

Godot 4.7：res://tests/battle_ui_smoke.tscn
结果：CSHARP_BATTLE_UI_SMOKE_OK
```

UI 专项测试覆盖：Master 主要几何、5v5 同列、真实 `CardTile.GuiInput`/`InteractionArea.GuiInput` 右键、敌方隐藏信息、Godot drag 状态与合法 Drop、战斗结束交互禁用、Card/Hero/Enemy 右栏模式、Cancel 恢复、1/2/4/5/6/8 张手牌、3:4、边界、扇形与 Hover 动画。

完整 `training_arena_smoke.tscn` 在进入 UI 交互段前，被基线已有用例 `TestLastCardPlayedTriggersHandEmpty` 阻断：`手牌还剩1张时不应触发HAND_EMPTY`。这不是本轮 UI 代码引入，且按范围冻结规则未修改。

## 四档截图

参考截图由 Godot 4.7 Compatibility 先渲染固定 1920×1080 逻辑画布；低分辨率图片是最近邻缩放输出，只用于核对整体比例，不再作为真实窗口验收证据：

- `ui_1920x1080.png`
- `ui_1600x900.png`
- `ui_1366x768.png`
- `ui_1280x720.png`

输出位于本次 Codex 可视化目录的 `battle-ui-review/`，不写入仓库。

真实窗口验收由 `res://tests/battle_ui_window_smoke.tscn` 分别启动并在运行时设置实际窗口尺寸，验证 `DisplayServer.WindowGetSize()`、1920×1080 逻辑画布以及 Stretch 后按钮视觉/命中位置一致。以下四档均实际运行通过：

```text
BATTLE_UI_WINDOW_SMOKE_OK 1920x1080
BATTLE_UI_WINDOW_SMOKE_OK 1600x900
BATTLE_UI_WINDOW_SMOKE_OK 1366x768
BATTLE_UI_WINDOW_SMOKE_OK 1280x720
```

## 与规格的已知偏差

- 正式战斗背景、指挥官头像、职业图标、状态图标、卡背、卡框和英雄像素立绘尚无最终美术，因此继续使用规格允许的占位样式。
- 左栏图形目前是字体符号占位，正式图标接入后应保持现有固定尺寸与 Tooltip 数据绑定。
- `StatusIcons` 目前仍是文字摘要，并非最终图标容器；`PassiveCardSlot` 当前是严格 72×96 的文字卡背占位，并非正式完整卡背素材。
- 非 16:9 与低于 1280×720 的专项重排不在本 Master 范围。
- 完整玩法冒烟需在独立玩法修复中解决基线 `HAND_EMPTY` 失败后，才能取得全套绿灯；本 UI 专项测试已独立通过。
