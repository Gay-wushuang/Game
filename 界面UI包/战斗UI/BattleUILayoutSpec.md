# Battle UI Layout Spec

> **文档状态：Battle UI Master 实现规格**  
> **基准分辨率：1920 × 1080（16:9）**  
> **配套几何文件：`battle_ui_master_1920x1080.svg`**  
> **用途：指导 Codex / Trae / Godot 开发者将当前 TrainingArena 调试 UI 重构为正式战斗 HUD。**
>
> **权威关系：** `BattleUILayoutSpec.md` = 实现规则权威源；`battle_ui_master_1920x1080.svg` = 几何参考；高保真 PNG = 视觉风格参考。  
> 不允许从旧 TrainingArena 的现有比例反推新版布局。

---

## 1. 核心目标

新版战斗 UI 固定为五个视觉区：

```text
TopBar
LeftSidebar | Battlefield | RightSidebar
            | HandArea    |
```

必须满足：

- 顶栏轻量，不形成厚重 HUD；
- 左侧将阵营与牌堆资源压缩成稳定竖栏；
- 中央 5v5 战场无遮挡；
- 底部手牌采用动态扇形 / 轻弧形；
- 右侧默认显示我方与敌方玩家 / 指挥官；
- 右键卡牌、英雄或敌人时，右栏切换为详情；
- 右栏切换详情不得改变 Battlefield 或 HandArea 几何尺寸；
- 正式 UI 必须由 Godot 原生 `Control` / `Container` 组件化实现，不允许把整张 SVG/PNG 当成不可交互 UI。

---

# 2. Master Geometry

```text
Canvas      1920 × 1080
SafeArea    x=16 y=16 w=1888 h=1048
```

主要区域：

```text
TopBar
x=16 y=16 w=1888 h=64

LeftSidebar
x=16 y=88 w=240 h=976

CenterColumn
x=264 y=88 w=1280 h=976

RightSidebar
x=1552 y=88 w=352 h=976

Battlefield
x=264 y=88 w=1280 h=700

HandArea
x=264 y=796 w=1280 h=268
```

水平间距：

```text
LeftSidebar -> CenterColumn = 8 px
CenterColumn -> RightSidebar = 8 px
```

---

# 3. TopBar

只允许显示：

- 地图 / 战斗名称；
- Round；
- 当前阶段 / 操作引导；
- AP；
- 战斗日志入口；
- 设置入口。

禁止：

- 英雄长技能说明；
- 卡牌规则正文；
- 大尺寸角色立绘；
- 常驻大段战斗日志。

基准区域：

```text
StageInfo  x=28   y=24 w=500 h=48
PhaseInfo  x=540  y=24 w=760 h=48
AP         x=1312 y=24 w=180 h=48
Log        x=1504 y=24 w=180 h=48
Settings   x=1696 y=24 w=196 h=48
```

`PhaseInfo` 是当前阶段提示的唯一常驻权威位置，例如：

```text
部署阶段：请选择一名英雄并部署
自由行动：可出锦囊、使用技能或普通攻击
请选择攻击目标
敌方回合
```

不要再在中央战场放一块常驻教学框。

---

# 4. LeftSidebar

定位：**随时可见、低频变化的战局资源摘要。**

顺序固定：

```text
FactionPanel
ActionPoints
HeroDeckCount
DrawPileCount
DiscardPileCount
TotalCardCount
ReservedSpecialResource
```

允许显示：

- 我方 / 敌方阵营徽记；
- AP；
- 英雄牌库数量；
- 抽牌堆；
- 弃牌堆；
- 战术牌总数；
- Boss 或章节特殊资源。

禁止：

- 卡牌规则正文；
- 英雄技能正文；
- 完整战斗日志；
- 大角色立绘；
- 会向中央展开并遮挡 Battlefield 的面板。

`ReservedSpecialResource` 无内容时隐藏内部元素，但左栏宽度不得改变。

---

# 5. Battlefield

## 5.1 固定 5v5

```text
EnemySlots  = 5
PlayerSlots = 5
```

两排必须使用完全相同的 X 坐标。

## 5.2 槽位几何

```text
SlotWidth  = 224 px
SlotHeight = 228 px
SlotGap    = 16 px
```

敌方：

```text
E1 x=304
E2 x=544
E3 x=784
E4 x=1024
E5 x=1264
y=172
```

我方：

```text
P1 x=304
P2 x=544
P3 x=784
P4 x=1024
P5 x=1264
y=476
```

## 5.3 Battlefield 优先级

`Battlefield` 是全屏最高视觉优先级区域。

禁止：

- 左右栏覆盖英雄；
- 手牌覆盖关键 HP / 状态；
- 详情面板侵入战场；
- 长文本常驻悬浮在角色上方。

允许：

- 攻击轨迹；
- 粒子；
- 伤害数字；
- 技能演出；
- 选择 / 目标高亮。

这些动态内容进入 `Battlefield_FXSafeZone`。

## 5.4 BattleSlot 组件

每个战位使用独立 `BattleSlot.tscn`：

```text
BattleSlot : Control
├─ Platform
├─ CharacterAnchor
│  └─ CharacterSprite / AnimatedSprite2D
├─ HpBar
├─ HpText
├─ ClassIcon
├─ StatusIconContainer
├─ PassiveCardSlot
├─ SelectionHighlight
├─ TargetHighlight
└─ InteractionArea
```

正式角色：

```text
画布 128×160 px
主体 <=112×144 px
透明 PNG
Bottom Center
统一虚拟地面基准线
```

`TrainingArena` / `BattleController` 不允许直接管理角色图片内部像素偏移、状态图标排列、HP 条具体样式等组件细节。

## 5.5 被动伏牌

每名已部署英雄拥有 1 个被动槽。

显示：

```text
完整卡背
严格 3:4
目标视觉尺寸约 72×96 px
```

不得替换成普通 Buff 图标。

## 5.6 状态图标

- 固定在统一位置；
- 建议常显最多 4 个；
- 超出后显示 `+N`；
- 不允许围绕角色随机散布。

---

# 6. HandArea

基准：

```text
x=264 y=796 w=1280 h=268
```

支持 1~8 张手牌。

## 6.1 动态扇形算法

```text
n = hand.Count
center = (n - 1) / 2.0
d = index - center
```

推荐旋转：

```text
rotation_deg = clamp(d * 4.0, -12.0, 12.0)
```

推荐纵向弧度：

```text
y_offset = d * d * 3.5
```

建议基础间距：

```text
1~4 张：180~210 px
5~6 张：150~175 px
7~8 张：118~145 px
```

必须根据 `HandArea` 当前宽度动态收缩，禁止只写死 5 张卡的位置。

## 6.2 Hover

```text
rotation -> 0 deg
position.y -= 18 px
scale -> 1.05
z_index -> 当前最高
```

邻牌向两侧排开约：

```text
18~36 px
```

禁止 Hover 后在鼠标旁再弹出一张大型卡。

## 6.3 Drag

拖拽时：

```text
rotation -> 0 deg
保持 3:4
z_index = top
```

并且：

- 合法目标高亮；
- 非法区域弱化；
- 取消时平滑回位；
- 出牌成功播放出牌动画。

---

# 7. RightSidebar

右栏采用**状态切换**，不是永久说明栏。

## 7.1 状态机

推荐：

```csharp
enum RightPanelMode
{
    CommanderOverview,
    CardDetail,
    HeroDetail,
    EnemyDetail
}
```

默认：

```text
CommanderOverview
```

## 7.2 CommanderOverview

默认显示：

```text
我方玩家 / 指挥官
VS
敌方玩家 / 首领
```

每个角色块只保留：

- 头像 / 半身像；
- 名称；
- 等级 / 身份；
- 阵营；
- 1~2 条短状态摘要。

禁止在默认态塞完整技能说明和长 Buff 列表。

## 7.3 DetailView

右键：

```text
卡牌 -> CardDetail
英雄 -> HeroDetail
敌人 -> EnemyDetail
```

`DetailView` 与 `CommanderOverview` 必须完全同位：

```text
x=1568 y=136 w=320 h=500
```

因此切换详情时：

- 不改变 RightSidebar 宽度；
- 不推动 Battlefield；
- 不触发 HandArea 重排。

### CardDetail

```text
卡名
费用
类型
目标
完整效果
关键词解释
```

### HeroDetail

```text
英雄名
职业
HP / ATK
技能
被动
队长能力
状态
```

### EnemyDetail

```text
名称
职业 / 类型
HP / ATK
状态
已公开意图
已公开技能
```

## 7.4 锁定逻辑

Hover：只做轻量视觉高亮。  
右键：锁定详情。

详情保持直到：

- 右键另一个对象；
- 关闭详情；
- 点击取消选择；
- 场景主动关闭。

---

# 8. Right Command Area

固定按钮：

```text
UseHeroSkill     x=1568 y=676 w=320 h=92
CancelSelection x=1568 y=784 w=320 h=92
EndTurn          x=1568 y=892 w=320 h=156
```

规则：

- 永远固定；
- 不被详情内容撑开；
- 不因右栏模式切换而移动；
- `EndTurn` 是主要流程按钮，层级高于普通辅助按钮。

---

# 9. Godot 推荐节点树

```text
BattleUI : Control
├─ BackgroundLayer : Control
│  └─ BattleBackground : TextureRect
├─ TopBar : MarginContainer
│  └─ TopBarContent
│     ├─ StageInfo
│     ├─ PhaseInfo
│     ├─ ActionPointDisplay
│     ├─ BattleLogButton
│     └─ SettingsButton
├─ LeftSidebar : PanelContainer
│  └─ MarginContainer
│     └─ VBoxContainer
│        ├─ FactionPanel
│        ├─ ActionPointPanel
│        ├─ HeroDeckPanel
│        ├─ DrawPilePanel
│        ├─ DiscardPilePanel
│        ├─ TotalCardsPanel
│        └─ ReservedPanel
├─ Battlefield : Control
│  ├─ EnemySlots : Control
│  │  ├─ EnemySlot01 : BattleSlot
│  │  ├─ EnemySlot02 : BattleSlot
│  │  ├─ EnemySlot03 : BattleSlot
│  │  ├─ EnemySlot04 : BattleSlot
│  │  └─ EnemySlot05 : BattleSlot
│  ├─ PlayerSlots : Control
│  │  ├─ PlayerSlot01 : BattleSlot
│  │  ├─ PlayerSlot02 : BattleSlot
│  │  ├─ PlayerSlot03 : BattleSlot
│  │  ├─ PlayerSlot04 : BattleSlot
│  │  └─ PlayerSlot05 : BattleSlot
│  └─ BattleFxLayer : Control
├─ HandArea : Control
│  └─ HandFanController : Control
└─ RightSidebar : PanelContainer
   └─ MarginContainer
      └─ VBoxContainer
         ├─ ContentStack : Control
         │  ├─ CommanderOverview
         │  └─ DetailView
         └─ CommandArea
            ├─ HeroSkillButton
            ├─ CancelButton
            └─ EndTurnButton
```

---

# 10. 响应式规则

Master：

```text
1920×1080
```

同为 16:9 时必须验证：

```text
2560×1440
1600×900
1366×768
1280×720
```

原则：

- 优先整体比例缩放；
- 使用 Anchor / Container 维持相对结构；
- 左右栏不能因为文本变长自动撑宽；
- Slot 不允许无限横向拉伸；
- 卡牌永远保持 3:4；
- 角色 Bottom Center 不漂移。

低于 1280×720 时需要专项适配，不在本 Master 的强制范围内。

---

# 11. Z 层级建议

```text
0   Background
10  Battlefield Platforms
20  Characters
30  HP / Status / Passive
40  Battlefield Selection FX
50  Hand Cards
60  Dragging Card
70  Left / Right / Top HUD
80  Context UI
90  Modal
100 Debug Overlay
```

---

# 12. 输入规则

### 左键

```text
选择
确认
拖拽
使用
选择攻击目标
```

### 右键

```text
查看 / 锁定详情
```

### Esc

按优先级：

```text
关闭 Modal
关闭右栏详情锁定
取消当前选择
打开暂停 / 设置
```

一个 Esc 不得同时触发多层行为。

---

# 13. 迁移阶段

## UI-1：静态布局

只做：

- TopBar；
- LeftSidebar；
- Battlefield 空区域；
- HandArea 空区域；
- RightSidebar；
- 响应式 Anchor / Offset。

禁止修改：

- BattleState；
- CardResolver；
- Lua；
- 英雄技能；
- AI；
- 胜负规则。

## UI-2：BattleSlot

实现：

- 10 个 `BattleSlot`；
- CharacterAnchor；
- HP；
- 状态；
- 伏牌；
- 选择 / 目标高亮。

## UI-3：HandFan

实现：

- 扇形；
- Hover；
- 排开；
- Drag；
- 回位。

## UI-4：RightPanel

实现：

```text
CommanderOverview
<->
CardDetail / HeroDetail / EnemyDetail
```

## UI-5：接入真实数据

最后再接：

- BattleState；
- DeckState；
- CardData；
- HeroData；
- BattleEvent。

---

# 14. Coding Agent 强制约束

1. `BattleUILayoutSpec.md` 是布局权威源。  
2. `battle_ui_master_1920x1080.svg` 是几何参考。  
3. 高保真参考 PNG 只用于视觉风格。  
4. 不允许把整张 SVG / PNG 作为不可交互背景代替 UI。  
5. 使用 Godot 原生 `Control` / `Container`。  
6. 如果当前任务仅为布局，禁止修改战斗规则。  
7. Battlefield 不得被左右栏或手牌常驻覆盖。  
8. RightSidebar 切换详情时不能改变中央布局尺寸。  
9. 手牌必须动态扇形计算，不允许只硬编码五张牌。  
10. 完成后必须提供节点树、实际截图和规格偏差报告。

---

# 15. 可直接给 Codex / Trae 的 Prompt

```text
任务：按照 Battle UI Master 规格重构 TrainingArena 的战斗 UI。

权威输入：
1. BattleUILayoutSpec.md —— 布局与交互规则唯一权威源。
2. battle_ui_master_1920x1080.svg —— 1920×1080 几何位置参考。
3. 高保真参考 PNG —— 只用于视觉层级和美术风格参考。

本轮仅处理 UI 静态布局与响应式结构。

禁止：
- 修改 BattleState；
- 修改卡牌规则；
- 修改 Lua；
- 修改英雄技能；
- 修改 AI；
- 修改胜负条件；
- 将整个 SVG/PNG 作为不可交互背景图代替 Godot UI。

要求：
- 使用 Godot 原生 Control / Container。
- 基准分辨率 1920×1080。
- 同比例 1600×900、1366×768、1280×720 不崩。
- TopBar 轻量化。
- LeftSidebar 为稳定资源栏。
- Battlefield 为最高视觉优先级。
- 5 个敌方槽与 5 个我方槽使用相同 X 坐标。
- HandArea 预留动态扇形手牌。
- RightSidebar 默认显示双方玩家/指挥官，右键后切换详情；切换不得改变布局尺寸。
- 不添加规格外的新功能。

完成后：
1. 运行项目；
2. 输出实际节点树；
3. 截取 1920×1080 截图；
4. 截取 1280×720 截图；
5. 对照 SVG 检查主要区域尺寸与比例；
6. 报告所有与规格存在的偏差。
```

---

# 16. Master 验收标准

## 1920×1080

- [ ] TopBar 高约 64 px；
- [ ] LeftSidebar 宽 240 px；
- [ ] RightSidebar 宽 352 px；
- [ ] CenterColumn 宽 1280 px；
- [ ] Battlefield 高约 700 px；
- [ ] HandArea 高约 268 px；
- [ ] 5 个敌方位水平对齐；
- [ ] 5 个我方位水平对齐；
- [ ] 两排 Slot 使用相同 X；
- [ ] 角色主体不被常驻 UI 遮挡；
- [ ] 手牌不覆盖关键 HP / 状态；
- [ ] 右栏详情切换不影响中央区域。

## 响应式

- [ ] 1600×900 不重叠；
- [ ] 1366×768 不重叠；
- [ ] 1280×720 不重叠；
- [ ] 卡牌始终 3:4；
- [ ] 左右栏不会因文本撑宽；
- [ ] BattleSlot 的 Bottom Center 不漂移。

## 交互

- [ ] Hover 卡牌上浮；
- [ ] Hover 卡牌恢复为 0°；
- [ ] 邻牌排开；
- [ ] Drag 保持 3:4；
- [ ] 右键详情显示在固定右栏；
- [ ] 关闭详情恢复 CommanderOverview。

---

# 17. 当前不在 Master 范围内

本规格暂不冻结：

- UI 最终字体；
- 卡框最终花纹；
- 最终职业色；
- 稀有度外框；
- 具体战斗背景；
- 双方玩家头像最终美术；
- 动画曲线精确时长；
- 手机竖屏；
- 非 16:9 的完整重排；
- PvP 专用额外 HUD。
