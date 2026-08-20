# Battle UI 返工执行清单

> 适用提交：`82abddf9d7ff58328004102bf81f051d35239119`  
> 目标：将当前“完成部分几何重排、但交互与组件化未完成”的 TrainingArena UI，按 `BattleUILayoutSpec.md` 与 `battle_ui_master_1920x1080.svg` 继续完成。  
> 
>权威顺序：
> 
>1. `BattleUILayoutSpec.md` —— UI 布局与交互规则权威源  
> 2. `battle_ui_master_1920x1080.svg` —— 1920×1080 几何参考  
> 3. 高保真参考图 —— 视觉风格参考  
> 4. 当前代码 —— 需要被修正的现状，不得反过来覆盖规格
> 
>当前已确认：82ab 已完成 1920×1080 基础 viewport、TopBar/LeftSidebar/Center/RightSidebar 的大体重排、224×228 Slot 尺寸、卡牌 3:4 比例调整；但 RightPanel、Hero/Enemy 右键详情、BattleSlot 组件化、真正的 HandFan、低信息熵左栏、响应式验收等仍未完成。  
> 当前提交还混入 `HAND_EMPTY`、`ACTION_POINTS_ZERO`、`BEFORE_DRAW` 等战斗逻辑修改，后续 UI 返工必须避免继续混入玩法改动。

---

# Phase 0：冻结范围，防止再次把 UI 和玩法混在一起

## 0.1 建立返工分支

- [ ] 从当前可运行提交创建独立 UI 修复分支。
- [ ] 建议名称：`fix/battle-ui-master-completion`。
- [ ] 记录当前基线 commit：`82abddf9d7ff58328004102bf81f051d35239119`。
- [ ] 不在本轮顺手实现新卡牌、新英雄、新被动、新 AI 行为。

## 0.2 明确本轮禁止修改项

以下文件/模块只有在“为 UI 读取数据而必须增加只读接口”时才允许最小修改：

- [ ] `BattleState`
- [ ] `BattleRules`
- [ ] `CardResolver`
- [ ] Lua / CardApi
- [ ] AI 决策
- [ ] 胜负条件
- [ ] 英雄技能规则
- [ ] 被动触发规则
- [ ] 抽牌规则
- [ ] AP 规则

禁止本轮继续新增：

- [ ] `HAND_EMPTY` 逻辑
- [ ] `ACTION_POINTS_ZERO` 逻辑
- [ ] `BEFORE_DRAW / AFTER_DRAW` 逻辑
- [ ] 其它卡牌结算补丁

## 0.3 处理 82ab 中混入的玩法修改

建议二选一：

### 推荐方案 A：保留成果，但拆成独立 commit

- [ ] 将 82ab 中 UI 无关的战斗逻辑修改识别出来。
- [ ] 将被动事件链 / AP / 抽牌逻辑独立整理成单独 commit。
- [ ] 将 UI 返工后续提交保持为纯 UI。

建议提交信息：

```text
fix: complete passive trigger lifecycle
```

### 方案 B：暂时不拆历史 commit

如果不想改历史：

- [ ] 保留 82ab 不动。
- [ ] 从现在开始保证后续 UI commit 不再碰玩法逻辑。
- [ ] 在 PR / 开发日志中明确记录 82ab 是“UI + gameplay mixed commit”。

**Phase 0 验收：**

- [ ] 后续 diff 中不再出现新的战斗规则修改。
- [ ] 可以单独审查 UI 代码，不被玩法改动淹没。

---

# Phase 1：修正 viewport 与 UI 验收环境

当前 `project.godot` 已将逻辑 viewport 改为 `1920×1080`，但窗口 override 仍为 `1280×720`。这会让“Master 1920×1080”验收变得混乱。

## 1.1 建立 Master 验收运行方式

- [ ] 调试/验收时能明确运行在 `1920×1080`。
- [ ] 不依赖默认 1280×720 override 来判断 1920 布局。
- [ ] 如果保留 override，增加明确的测试启动方式或调试配置。

## 1.2 建立四档截图基线

必须最终能测试：

- [ ] `1920×1080`
- [ ] `1600×900`
- [ ] `1366×768`
- [ ] `1280×720`

## 1.3 暂时不要做最终美术

本阶段使用：

- [ ] ColorRect
- [ ] PanelContainer
- [ ] 临时 Label
- [ ] 占位头像
- [ ] 占位角色 Sprite

不要先花时间做：

- [ ] 正式背景
- [ ] 最终卡框
- [ ] 最终字体
- [ ] 最终职业色
- [ ] 最终动画曲线

**Phase 1 验收：**

- [ ] 1920×1080 下能准确观察 Master 布局。
- [ ] UI 调试尺寸可重复切换。
- [ ] 没有把缩放结果误认为原生 1920 布局。

---

# Phase 2：先冻结新版根节点结构

82ab 已经搭出了大体布局，但节点结构还不足以承载后续 RightPanel 和 BattleSlot。

## 2.1 保留已有大框架

保留并校正：

```text
BattleUI / TrainingArena
├─ TopBar
├─ MainRow
│  ├─ LeftSidebar
│  ├─ CenterColumn
│  │  ├─ Battlefield
│  │  └─ HandArea
│  └─ RightSidebar
```

- [ ] `TopBar` 约 64 px 逻辑安全区。
- [ ] `LeftSidebar` 约 240 px。
- [ ] `RightSidebar` 约 352 px。
- [ ] `CenterColumn` 约 1280 px。
- [ ] `Battlefield` 约 700 px。
- [ ] `HandArea` 约 268 px。
- [ ] 左右栏不能因内容变化自动撑宽。
- [ ] Battlefield 不能被左右栏覆盖。
- [ ] HandArea 不能覆盖 BattleSlot 关键 HP/状态区域。

## 2.2 顶部从“厚面板”向轻量 HUD 过渡

逻辑上保留约 64 px 顶部安全区域，但视觉上不要一整条厚重不透明栏。

目标：

```text
左上：地图 / Round
中上：当前阶段提示
右上：AP / 日志 / 设置
```

- [ ] 移除或弱化全宽不透明 Panel 背景。
- [ ] 信息块使用轻量底板或半透明小组件。
- [ ] 中央阶段提示不再侵入 Battlefield。
- [ ] 不在 Battlefield 中增加常驻教学大框。

## 2.3 增加 BackgroundLayer 与 BattleFxLayer

建议：

```text
BackgroundLayer
└─ BattleBackground

Battlefield
├─ BattleSlot...
└─ BattleFxLayer
```

- [ ] `BattleBackground` 与 HUD 解耦。
- [ ] `BattleFxLayer` 独立于角色和 HUD。
- [ ] `Announcement` 若保留，应作为短时演出，不是常驻信息。

**Phase 2 验收：**

- [ ] 根节点职责清楚。
- [ ] 后续切右栏详情不会重排中央区域。
- [ ] 后续替换背景不需要重做 HUD。

---

# Phase 3：优先完成 RightSidebar —— 当前最高优先级缺口

这是当前最明确的功能错误：卡牌右键仍打开独立 `CardDetailDialog`，英雄右键无反应。

## 3.1 删除 CardDetailDialog 的“详情展示职责”

当前场景仍保留：

```text
CardDetailDialog : AcceptDialog
```

必须改为：

- [ ] 卡牌右键不再调用 `Popup()` / `PopupCentered()` / `AcceptDialog`。
- [ ] 卡牌详情不创建独立 Window。
- [ ] 若 `CardDetailDialog` 无其它用途，删除节点和相关调用。
- [ ] 若仍需其它特殊 Modal，用不同职责命名，避免复用为常规详情。

注意：

`HeroBagDialog`、`PileDialog`、`StarChoiceDialog` 等业务弹窗是否保留，应根据其业务语义单独判断；本阶段只强制取消“常规 Card/Hero/Enemy 详情使用独立弹窗”。

## 3.2 重构 RightSidebar 节点结构

从当前：

```text
RightContent
├─ CommanderOverview
├─ RightSpacer
└─ Actions
```

改为：

```text
RightSidebar
└─ RightMargin
   └─ RightContent
      ├─ ContentHost : Control   # 固定 320×500
      │  ├─ CommanderOverview
      │  └─ DetailView
      ├─ RightSpacer
      └─ CommandArea
         ├─ SkillButton
         ├─ CancelButton
         └─ EndTurnButton
```

要求：

- [ ] `ContentHost` 固定尺寸。
- [ ] `CommanderOverview` 与 `DetailView` anchors full rect。
- [ ] 两者完全同位。
- [ ] 任何详情内容不能撑开 RightSidebar。
- [ ] `CommandArea` 永远固定，不随详情切换移动。

## 3.3 实现 RightPanelMode

新增统一状态：

```csharp
enum RightPanelMode
{
    CommanderOverview,
    CardDetail,
    HeroDetail,
    EnemyDetail
}
```

- [ ] 默认 `CommanderOverview`。
- [ ] `CardDetail` 使用同一 `DetailView`。
- [ ] `HeroDetail` 使用同一 `DetailView`。
- [ ] `EnemyDetail` 使用同一 `DetailView`。
- [ ] 不要分别创建三个 Popup。

## 3.4 CommanderOverview 默认内容

在正式美术未完成时先用占位资产：

```text
我方玩家 / 指挥官
头像或半身占位
名称
阵营 / 身份
少量状态

VS

敌方玩家 / 首领
头像或半身占位
名称
阵营 / 身份
少量状态
```

- [ ] 默认右栏不是空白。
- [ ] 不放大段技能说明。
- [ ] 不放长 Buff 列表。
- [ ] 不放完整战斗日志。

## 3.5 CardDetail 内容

- [ ] 卡名
- [ ] 费用
- [ ] 类型
- [ ] 目标
- [ ] 完整效果
- [ ] 关键词解释
- [ ] 卡图可作为小尺寸固定预览
- [ ] 动态文字由程序生成

## 3.6 HeroDetail 内容

- [ ] 英雄名
- [ ] 职业
- [ ] HP / MaxHP
- [ ] ATK
- [ ] 星级
- [ ] 技能
- [ ] 被动
- [ ] 队长能力
- [ ] 当前状态

## 3.7 EnemyDetail 内容

- [ ] 名称
- [ ] 职业 / 类型
- [ ] HP / MaxHP
- [ ] ATK
- [ ] 状态
- [ ] 已公开意图
- [ ] 已公开技能

## 3.8 详情关闭与锁定

统一行为：

```text
右键对象 -> 锁定详情
右键另一个对象 -> 切换详情对象
关闭按钮 -> CommanderOverview
取消选择 -> CommanderOverview
Esc -> CommanderOverview（若当前有详情）
```

- [ ] Hover 不自动打开完整详情。
- [ ] 右键查看详情不改变当前左键选择状态。
- [ ] 右键查看详情不触发出牌。
- [ ] 右键查看详情不改变攻击目标。
- [ ] 关闭详情后恢复默认玩家 VS 展示。

**Phase 3 验收：**

- [ ] 卡牌右键不再弹独立窗口。
- [ ] 英雄右键有反应。
- [ ] 敌人右键有反应。
- [ ] 三类详情都只在固定右栏出现。
- [ ] 切详情时 Battlefield/HandArea/CommandArea 一像素都不被推动。
- [ ] Esc / Cancel / Close 行为一致。

---

# Phase 4：真正重构 BattleSlot，而不是只把 Button 改成 224×228

当前 `unit_slot.tscn` 仍本质上是一个 Button，只修改了 `custom_minimum_size = 224×228`。这不足以支撑正式战场交互。

## 4.1 建立可复用 BattleSlot

建议将现有 UnitSlot 升级或新建为：

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
├─ ActionPreview
├─ SelectionHighlight
├─ TargetHighlight
└─ InteractionArea
```

- [ ] 10 个槽位全部实例化同一个组件。
- [ ] 敌我差异通过数据 / Theme / Side 参数决定。
- [ ] 不复制两套不同实现。

## 4.2 InteractionArea 统一输入

这是修复“英雄右键无反应”的关键。

- [ ] `InteractionArea` 覆盖整个可交互 Slot。
- [ ] 左键：保持现有选择 / 部署 / 攻击目标逻辑。
- [ ] 右键：只发出 DetailRequested。
- [ ] 子节点（HP、Sprite、Status）不要吞掉右键。
- [ ] 必要时设置合适 `MouseFilter`。

推荐事件：

```csharp
DetailRequested(UnitSlot slot, UnitState unit)
```

或：

```csharp
DetailRequested(UnitState unit, BattleSide side)
```

## 4.3 CharacterAnchor

正式规则：

- [ ] 角色素材 128×160。
- [ ] 主体建议 ≤112×144。
- [ ] Bottom Center。
- [ ] 统一虚拟地面基准线。
- [ ] Idle/Attack/Hit/Defeat 不改变角色锚点。
- [ ] 敌我镜像不能导致位置漂移。

## 4.4 HP 与状态

- [ ] HPBar 固定位置。
- [ ] HPText 固定位置。
- [ ] 状态图标固定容器。
- [ ] 常显建议最多 4 个。
- [ ] 超出显示 `+N`。
- [ ] 不围绕角色随机摆状态图标。

## 4.5 PassiveCardSlot

- [ ] 每个部署英雄有 1 个伏牌槽。
- [ ] 显示完整卡背。
- [ ] 保持 3:4。
- [ ] 视觉尺寸约 72×96，必要时按槽位空间适配。
- [ ] 不用普通 Buff icon 替代伏牌。
- [ ] 敌方伏牌不得因 Hover 泄露隐藏信息。

## 4.6 选择 / 目标高亮

- [ ] 当前选中英雄高亮。
- [ ] 当前攻击目标高亮。
- [ ] 卡牌合法目标高亮。
- [ ] 非法目标弱化。
- [ ] 高亮效果不改变 Slot 几何尺寸。

**Phase 4 验收：**

- [ ] 10 个 Slot 共用一个组件。
- [ ] 任意英雄身体、HP、状态区域右键都能打开正确详情。
- [ ] 左键原有战斗交互不被右键破坏。
- [ ] 角色 Bottom Center 不漂。
- [ ] 伏牌与状态显示统一。

---

# Phase 5：真正完成 HandFan

当前 `HandFan.cs` 只把卡牌宽度改成 `height × 0.75`，主要解决了 3:4；并没有真正实现扇形、旋转和 Hover 动效。

## 5.1 支持 1～8 张手牌

- [ ] 不能硬编码仅 4/5 张。
- [ ] 1 张时居中。
- [ ] 2～4 张间距较宽。
- [ ] 5～6 张标准间距。
- [ ] 7～8 张自动收紧。
- [ ] 不越出 HandArea。

## 5.2 实现轻弧形

推荐：

```text
center = (count - 1) / 2
d = index - center

rotation_deg = clamp(d * 4.0, -12, 12)
y_offset = d * d * 3.5
```

- [ ] 左侧卡轻微逆时针。
- [ ] 中间卡接近 0°。
- [ ] 右侧卡轻微顺时针。
- [ ] y_offset 形成轻弧，不做夸张半圆。

## 5.3 Hover

Hover 卡：

- [ ] rotation → 0°
- [ ] y 上移约 18 px
- [ ] scale → 1.05
- [ ] z_index → 当前最高
- [ ] 左右邻牌排开约 18～36 px
- [ ] 使用短 Tween
- [ ] MouseExit 平滑回位

禁止：

- [ ] Hover 打开大型独立卡牌窗口。
- [ ] Hover 自动锁定 RightSidebar 完整详情。
- [ ] Hover 改变战斗选择状态。

## 5.4 右键卡牌

- [ ] 只调用/发出 `ShowCardDetail`。
- [ ] 不调用旧 `CardDetailDialog`。
- [ ] 不触发左键选择。
- [ ] 不触发拖拽。

## 5.5 Drag

- [ ] 拖拽时 rotation → 0。
- [ ] 保持严格 3:4。
- [ ] z_index 置顶。
- [ ] 合法目标高亮。
- [ ] 非法区域弱化。
- [ ] 取消时平滑回位。
- [ ] 成功使用后播放短出牌动画。

## 5.6 移除常驻操作说明

当前：

```text
锦囊手牌 · 左键选择，右键查看详情
```

正式版建议：

- [ ] 新手教程阶段可显示。
- [ ] 普通战斗隐藏“左键选择，右键查看详情”。
- [ ] 只保留极简“手牌”或完全不显示标题。

**Phase 5 验收：**

- [ ] 1～8 张全部正常。
- [ ] 手牌确实有轻弧旋转，而非直线排列。
- [ ] Hover 上浮、回正、放大、排开。
- [ ] 右键只影响右栏详情。
- [ ] Drag 不破坏 3:4。

---

# Phase 6：降低 LeftSidebar 信息熵

82ab 只是把旧底部信息搬到左边，当前仍有明显“文字说明栏”问题。

当前类似：

```text
战场信息
我方阵营
VS
敌方阵营
行动点 / 与顶栏同步
英雄卡包 / 3
抽牌堆 / 0
弃牌堆 / 0
锦囊总卡包
特殊资源预留
```

目标改成：

```text
[我方徽记]
   VS
[敌方徽记]

[AP图标] 3/5
[英雄牌库图标] 3
[抽牌图标] 26
[弃牌图标] 2
[战术牌图标] 43
```

## 6.1 删除无价值常驻文字

- [ ] 删除“战场信息”标题，除非视觉测试证明必要。
- [ ] 删除“与顶栏同步”。
- [ ] 删除“特殊资源预留”常驻占位文字。
- [ ] 无特殊资源时 Reserved 内容彻底隐藏。
- [ ] 保留栏位宽度，不因内容隐藏改变战场宽度。

## 6.2 图标 + 数字

- [ ] HeroDeck → 图标 + count。
- [ ] DrawPile → 图标 + count。
- [ ] DiscardPile → 图标 + count。
- [ ] TotalCards → 图标 + count。
- [ ] AP → 图标/菱形点 + 数字。
- [ ] Faction → 徽记/头像简化展示。

## 6.3 Tooltip 承担说明

Hover 图标显示：

```text
英雄牌库
剩余 3
```

或：

```text
弃牌堆
当前 2 张
```

- [ ] 常驻 UI 只保留识别所需信息。
- [ ] 解释文字进入 Tooltip。
- [ ] 不为了“怕玩家看不懂”重新把长文字塞回来。

**Phase 6 验收：**

- [ ] 第一眼看到数字和图标，而不是文字列表。
- [ ] 左栏明显比 82ab 更低信息熵。
- [ ] 不需要阅读说明也能找到主要资源。

---

# Phase 7：优化 TopBar 为“存在但不挡视野”

本阶段属于对 Master 的视觉升级，不改变信息职责。

## 7.1 保留的信息

- [ ] 地图 / 战斗名
- [ ] Round
- [ ] 当前阶段
- [ ] AP
- [ ] 战斗日志
- [ ] 设置

## 7.2 去除厚重感

- [ ] 不使用一整条高不透明 Panel。
- [ ] 信息块尽量贴边、半透明、轻边框。
- [ ] 中央只显示短阶段信息。
- [ ] 不重复显示 LeftSidebar 已经清楚表达的信息。
- [ ] 不让顶栏成为画面的第一视觉中心。

**Phase 7 验收：**

- [ ] 玩家视线优先落在 Battlefield。
- [ ] 顶部仍能快速找到 Round/AP/设置。
- [ ] 整体比 82ab 更接近参考图的开阔感。

---

# Phase 8：清理旧 UI 路径和重复逻辑

完成新结构后，必须清旧代码，否则会出现“新 UI 和旧 Popup 同时存在”的问题。

## 8.1 清理旧 Card Detail

- [ ] 查找 `CardDetailDialog`。
- [ ] 查找 `PopupCentered` / `Popup` 调用。
- [ ] 删除常规 Card Detail 的旧调用路径。
- [ ] 确认右键卡牌只有一个详情入口。

## 8.2 清理旧 Hero/Enemy 详情路径

- [ ] 查找是否存在旧 Hero Modal。
- [ ] 查找旧 Enemy Preview/Detail。
- [ ] 全部统一到 RightSidebar。

## 8.3 清理重复输入

- [ ] CardTile 不要一套右键。
- [ ] UnitSlot 不要另一套右键。
- [ ] TrainingArena 不要再按节点类型到处手写详情弹窗。
- [ ] 统一通过 `DetailRequested` → `RightSidebar`。

## 8.4 TrainingArena 瘦身

TrainingArena 只负责：

- [ ] 战斗流程协调
- [ ] 数据绑定
- [ ] 当前选择状态
- [ ] 调用 UI 组件公开接口

TrainingArena 不直接负责：

- [ ] BattleSlot 内部 HP 排版
- [ ] StatusIcon 排列
- [ ] CharacterSprite 偏移
- [ ] HandFan 单卡坐标数学
- [ ] RightSidebar 内部可见性细节
- [ ] Tooltip 布局

**Phase 8 验收：**

- [ ] 常规详情只有一个入口。
- [ ] 不存在新旧 UI 双轨。
- [ ] TrainingArena.cs 不继续膨胀成所有 UI 的内部实现者。

---

# Phase 9：接入真实 UI 数据

只有组件结构稳定后再做。

## 9.1 CommanderOverview

- [ ] 玩家名字从真实 Profile/当前测试数据获取。
- [ ] 敌方指挥官从关卡/AI 配置获取。
- [ ] 头像没有正式素材时用占位，不要硬编码未来正式内容。

## 9.2 CardDetail

- [ ] 从 CardDefinition / CardInstance 获取数据。
- [ ] Runtime cost 使用当前真实费用。
- [ ] 动态数值不得写死到图片。
- [ ] 关键词从程序数据生成。

## 9.3 HeroDetail / EnemyDetail

- [ ] 绑定 UnitState。
- [ ] 显示实时 HP。
- [ ] 显示实时 ATK。
- [ ] 显示当前 Star。
- [ ] 显示实时 Status。
- [ ] 不复制一份 UI 专用战斗状态。

## 9.4 LeftSidebar

- [ ] AP 使用真实当前 AP。
- [ ] HeroDeck count 使用真实数据。
- [ ] Draw/Discard count 实时更新。
- [ ] TotalCards 定义明确，避免显示含义不明的数字。

**Phase 9 验收：**

- [ ] UI 是 BattleState/DeckState 的视图，不成为第二状态源。
- [ ] 数值改变后所有显示同步。
- [ ] 不出现同一数据在两个控件显示不同值。

---

# Phase 10：视觉资源接入

结构和交互验收通过后才进入正式美术。

## 10.1 BattleBackground

- [ ] 使用日式赛璐璐世界观氛围 + 像素化战场转译。
- [ ] 背景不抢角色。
- [ ] 中央 5v5 站位区域保持清楚。
- [ ] 不把 Slot、HP、按钮画死在背景 PNG。

## 10.2 BattleSlot 美术

- [ ] 平台/地面标记弱于角色主体。
- [ ] 空位不要是巨大黑框。
- [ ] 敌我通过轻量色彩/纹样区分。
- [ ] 选择和合法目标高亮清楚但不过曝。

## 10.3 Commander 头像/半身像

- [ ] 日式赛璐璐源设计。
- [ ] 右栏默认态使用头像/半身像，不用超大整身立绘。
- [ ] 不抢中央 Battlefield。

## 10.4 卡牌

正式卡牌：

- [ ] 192×256。
- [ ] 严格 3:4。
- [ ] 插图与卡框分层。
- [ ] 动态文字由程序绘制。
- [ ] Hover 不生成另一张巨大卡面。

## 10.5 像素素材导入

- [ ] Nearest。
- [ ] Mipmap Off。
- [ ] 避免平滑缩放破坏像素边缘。
- [ ] 整数倍率优先。

**Phase 10 验收：**

- [ ] 美术替换不破坏结构。
- [ ] 战场仍然是最高视觉中心。
- [ ] UI 仍然低信息熵。

---

# Phase 11：响应式与分辨率验收

每个尺寸都要真实运行，不接受“理论上 Anchor 没问题”。

## 11.1 1920×1080

- [ ] Top HUD 不厚重。
- [ ] LeftSidebar 约 240。
- [ ] RightSidebar 约 352。
- [ ] CenterColumn 不被压缩。
- [ ] Battlefield 五个槽完整。
- [ ] Enemy/Player 两排 X 对齐。
- [ ] HandArea 完整。
- [ ] 8 张手牌不越界。
- [ ] Right Detail 不移动中央区。

## 11.2 1600×900

- [ ] 无重叠。
- [ ] 左右栏不撑爆。
- [ ] 手牌正常收紧。
- [ ] 字体不出现严重截断。

## 11.3 1366×768

- [ ] 无重叠。
- [ ] Slot 保持可读。
- [ ] 右栏按钮全部可见。
- [ ] Hover 卡不超出屏幕。

## 11.4 1280×720

- [ ] 无重叠。
- [ ] 不因 Left/Right Sidebar 造成 Battlefield 无法使用。
- [ ] Card 保持 3:4。
- [ ] Bottom Center 稳定。
- [ ] 右栏详情可完整查看或可滚动，不撑布局。

## 11.5 截图存档

每次正式验收保存：

```text
ui_1920x1080.png
ui_1600x900.png
ui_1366x768.png
ui_1280x720.png
```

**Phase 11 验收：**

- [ ] 四张截图齐全。
- [ ] 每张都与 Master 对照。
- [ ] 偏差有明确记录，不用“差不多”代替验收。

---

# Phase 12：交互回归测试

## 12.1 Card

- [ ] 左键选卡正常。
- [ ] 右键详情正常。
- [ ] Hover 正常。
- [ ] Drag 正常。
- [ ] 右键不改变左键选择。
- [ ] 查看详情不消耗 AP。
- [ ] 查看详情不打出卡牌。

## 12.2 Hero

- [ ] 左键选择我方英雄正常。
- [ ] 右键打开 HeroDetail。
- [ ] 点 HP/状态/角色身体都能触发统一右键。
- [ ] 右键不改变攻击选择。

## 12.3 Enemy

- [ ] 左键目标选择正常。
- [ ] 右键打开 EnemyDetail。
- [ ] 右键不确认攻击。
- [ ] 右键不结算卡牌。

## 12.4 RightSidebar

- [ ] 默认 CommanderOverview。
- [ ] CardDetail 正常。
- [ ] HeroDetail 正常。
- [ ] EnemyDetail 正常。
- [ ] 关闭恢复 CommanderOverview。
- [ ] Cancel 恢复 CommanderOverview。
- [ ] Esc 恢复 CommanderOverview。
- [ ] 切换期间按钮位置不动。

## 12.5 HandFan

- [ ] 1 张。
- [ ] 2 张。
- [ ] 4 张。
- [ ] 5 张。
- [ ] 6 张。
- [ ] 8 张。
- [ ] Hover 最左牌。
- [ ] Hover 中间牌。
- [ ] Hover 最右牌。
- [ ] 拖出后取消。
- [ ] 拖出后成功使用。

**Phase 12 验收：**

- [ ] 所有 UI 交互不改变原有战斗规则。
- [ ] 不出现点击穿透。
- [ ] 不出现右键同时触发左键路径。
- [ ] 不出现旧 Popup。

---

# Phase 13：代码与提交质量检查

## 13.1 UI 组件职责

目标：

```text
TrainingArena
    负责协调

BattleSlot
    负责单个战位显示与输入

HandFan
    负责手牌布局与动画

CardTile
    负责单张卡视觉和输入

RightSidebar
    负责详情状态与显示

BattleBackground / BattleFxLayer
    负责场景与演出层
```

- [ ] 不让 TrainingArena 包办所有控件内部逻辑。
- [ ] 不在 CardTile 里直接操作 RightSidebar 节点路径。
- [ ] 不在 BattleSlot 里直接修改 BattleState。
- [ ] 组件通过信号/事件和公开接口协作。

## 13.2 删除死代码

- [ ] 无用 Popup。
- [ ] 无用旧节点路径。
- [ ] 无用旧 1280 wireframe 引用。
- [ ] 无用临时说明 Label。
- [ ] 无用 Debug print。

## 13.3 Commit 拆分建议

推荐按阶段提交：

```text
refactor(ui): add fixed right sidebar detail host
refactor(ui): make battle slots reusable and right-clickable
feat(ui): implement dynamic hand fan interactions
refactor(ui): reduce battle sidebar information density
feat(ui): add battle background and fx layers
test(ui): add battle hud interaction and resolution checks
style(ui): apply battle visual theme
```

禁止再次形成：

```text
feat: UI + passive + card rules + AI + tests + art all in one commit
```

---

# Phase 14：最终 Master 验收表

只有全部通过才允许标记“Battle UI Master 完成”。

## A. 结构

- [ ] Top HUD 轻量。
- [ ] LeftSidebar 稳定。
- [ ] Battlefield 无常驻 UI 遮挡。
- [ ] 10 个 BattleSlot 组件化。
- [ ] HandArea 独立。
- [ ] RightSidebar 固定。
- [ ] BackgroundLayer 独立。
- [ ] BattleFxLayer 独立。

## B. RightPanel

- [ ] 默认双方指挥官。
- [ ] CardDetail 固定右栏。
- [ ] HeroDetail 固定右栏。
- [ ] EnemyDetail 固定右栏。
- [ ] 无常规独立详情 Window。
- [ ] 详情关闭恢复默认态。

## C. BattleSlot

- [ ] CharacterAnchor。
- [ ] HP。
- [ ] Status。
- [ ] PassiveCardSlot。
- [ ] SelectionHighlight。
- [ ] TargetHighlight。
- [ ] InteractionArea。
- [ ] Bottom Center 稳定。

## D. HandFan

- [ ] 1～8 张。
- [ ] 轻弧。
- [ ] Rotation。
- [ ] Hover 上浮。
- [ ] Hover 回正。
- [ ] Scale 1.05。
- [ ] 邻牌排开。
- [ ] Drag。
- [ ] 3:4。

## E. 低信息熵

- [ ] 左栏图标 + 数字。
- [ ] 说明进 Tooltip。
- [ ] 无“与顶栏同步”等开发说明。
- [ ] 无常驻“左键选择，右键详情”教学文字。
- [ ] 卡面不塞长规则。

## F. 响应式

- [ ] 1920×1080。
- [ ] 1600×900。
- [ ] 1366×768。
- [ ] 1280×720。

## G. 代码边界

- [ ] 本轮 UI 没有修改玩法规则。
- [ ] UI 不成为 BattleState 的第二状态源。
- [ ] 新旧详情系统没有并存。
- [ ] TrainingArena 没继续承担所有组件内部细节。

---

# Phase 15：完成定义（Definition of Done）

只有同时满足以下条件，本轮才算完成：

- [ ] 卡牌右键不再弹独立窗口。
- [ ] 英雄右键正常。
- [ ] 敌人右键正常。
- [ ] 三类详情统一进入 RightSidebar。
- [ ] 10 个 BattleSlot 真正组件化。
- [ ] HandFan 真正实现轻弧、旋转、Hover、排开和 Drag。
- [ ] LeftSidebar 达到低信息熵。
- [ ] 顶部 HUD 不再形成厚重遮挡感。
- [ ] Battlefield 成为画面第一视觉中心。
- [ ] 1920 / 1600 / 1366 / 1280 四档通过。
- [ ] 提交中没有继续混入玩法修改。
- [ ] 输出最终节点树。
- [ ] 输出四档截图。
- [ ] 输出未完成项；若有任何 P0/P1 项未完成，不得宣称“UI 重构完成”。

---

# 给 Codex 的执行规则

将本文件交给 Codex 时附加以下要求：

```text
从 Phase 0 开始按顺序执行。
如果现有实现与本清单冲突，以 BattleUILayoutSpec.md 与本清单为准，并报告冲突。
```

---

# 推荐执行顺序摘要

```text
Phase 0  冻结 UI 范围 / 隔离玩法修改
   ↓
Phase 1  建立 1920×1080 与四档验收环境
   ↓
Phase 2  冻结根 UI 结构 / Background / FX Layer
   ↓
Phase 3  RightSidebar 状态机与 Card/Hero/Enemy 详情
   ↓
Phase 4  BattleSlot 组件化与统一右键输入
   ↓
Phase 5  真正实现 HandFan
   ↓
Phase 6  LeftSidebar 降信息熵
   ↓
Phase 7  Top HUD 轻量化
   ↓
Phase 8  删除旧 Popup / 重复 UI 路径
   ↓
Phase 9  接入真实数据
   ↓
Phase 10 正式视觉资源
   ↓
Phase 11 四档分辨率验收
   ↓
Phase 12 交互回归
   ↓
Phase 13 代码清理与 Commit 拆分
   ↓
Phase 14 Master 总验收
   ↓
Phase 15 Definition of Done
```
