# Main Menu 正式美术资源清单

> 文档状态：美术制作基准（2026-08-24）  
> 对应场景：`res://scenes/main_menu.tscn`  
> 参考图：`8c4016756f40635b7423040654390e04.png`（原图 `1672 × 941`，16:9）  
> 游戏逻辑画布：`1920 × 1080`  
> 换算系数：参考图到游戏画布约为 `×1.148`。

## 1. 结论

参考图的构图可以作为正式主界面目标，但它和当前 `main_menu.tscn` 的灰盒布局并不相同。美术应按本文的“目标布局”制作；程序接入时需要同步返工 `main_menu.tscn` 与 `player_bar.tscn` 的布局，不能把新美术硬塞进旧热区。

正式资源必须分层导出。除正式游戏 Logo 外，按钮名称、公告、玩家名称、等级、经验、货币数、版本号和设置文字均由 Godot 动态绘制，不得烘焙进图片。

## 2. 当前运行时布局（仅用于对照）

当前 `main_menu.tscn` 使用以下热区：

| 节点 | 当前坐标 `(x, y)` | 当前尺寸 |
|---|---:|---:|
| `StartButton` | `(120, 330)` | `510 × 81` |
| `RecruitButton` | `(120, 435)` | `510 × 81` |
| `LabButton` | `(120, 540)` | `510 × 81` |
| `PrepareButton` | `(120, 645)` | `510 × 81` |
| `ExitButton` | `(120, 750)` | `510 × 81` |
| `PlayerBar` | `(0, 960)` | `1920 × 120` |
| `SettingsButton`（相对 PlayerBar） | `(1803, 3)` | `117 × 117` |

当前背景 `main_menu_ui.svg` 的原始设计画布为 `1280 × 720`，Godot 将其铺到 `1920 × 1080`。其中已有大量文字转曲，不可继续作为正式成品。

## 3. 参考图目标布局（正式美术基准）

下列值依据 `1672 × 941` 参考图等比换算，并统一到便于制作和布局的整数尺寸。坐标允许程序接入时微调 `±4 px`，资产画布尺寸不变。

| 区域 | 目标坐标 `(x, y)` | 目标尺寸 | 备注 |
|---|---:|---:|---|
| 全屏画布 | `(0, 0)` | `1920 × 1080` | 固定逻辑画布 |
| 全屏装饰外框 | `(0, 0)` | `1920 × 1080` | 透明 PNG |
| Logo 区 | `(56, 34)` | `520 × 150` | 校徽、游戏名、副标题可作为正式 Logo |
| 左侧菜单总面板 | `(44, 190)` | `520 × 610` | 含外框，不含按钮文字 |
| 菜单按钮内容区 | `(76, 220)` | `450 × 98` | 共5个，纵向步进约108px |
| 公告总面板 | `(1210, 108)` | `660 × 630` | 外框与底纹 |
| 公告标题条 | `(1274, 142)` | `530 × 68` | 标题文字动态绘制 |
| 公告正文安全区 | `(1245, 222)` | `590 × 300` | 不制作独立底图也可以 |
| 活动入口行 | `(1245, 536)` | `590 × 174` | 5格 |
| 单个活动入口 | — | `102 × 174` | 建议格间距20px |
| 底部 HUD | `(18, 854)` | `1884 × 208` | 参考图保留18px外框边距 |
| 玩家头像框 | `(58, 882)` | `148 × 148` | 头像内容安全区128×128 |
| 玩家信息区 | `(220, 880)` | `320 × 160` | 名称、等级、经验、编号 |
| 等级/经验信息块 | `(550, 880)` | `210 × 160` | 两行动态信息 |
| 货币信息区 | `(780, 900)` | `850 × 120` | 3种货币 |
| 设置按钮 | `(1700, 892)` | `150 × 150` | 图标建议64×64或80×80 |

五个主菜单按钮的推荐热区：

| 功能 | 目标坐标 | 尺寸 |
|---|---:|---:|
| 开始游戏 | `(76, 220)` | `450 × 98` |
| 招募大厅 | `(76, 328)` | `450 × 98` |
| 研发基地 | `(76, 436)` | `450 × 98` |
| 备战中心 | `(76, 544)` | `450 × 98` |
| 退出游戏 | `(76, 652)` | `450 × 98` |

## 4. 必须交付的美术资源

### A. 场景与整体框架

| 文件建议名 | 交付尺寸 | 格式 | 内容约束 |
|---|---:|---|---|
| `main_menu_background.png` | `1920 × 1080` | PNG/RGB | 天空、学院、庭院、喷泉、植物和固定环境人物；不得含 UI 面板与文字 |
| `main_menu_foreground.png` | `1920 × 1080` | PNG/RGBA | 可选；前景树叶、花瓣、遮挡层，用于轻微视差 |
| `main_menu_outer_frame.png` | `1920 × 1080` | PNG/RGBA | 金色全屏装饰边框，内部透明 |
| `main_menu_vignette.png` | `1920 × 1080` | PNG/RGBA | 可选；边缘压暗和左右 UI 背后的可读性遮罩 |

背景建议保留 `3840 × 2160` 绘制母版，游戏交付版导出为 `1920 × 1080`。

### B. Logo

| 文件建议名 | 交付尺寸 | 格式 | 说明 |
|---|---:|---|---|
| `game_logo.png` | `520 × 150` | PNG/RGBA | 校徽、正式游戏名和固定副标题可以烘焙 |

Logo 主体建议控制在 `500 × 136` 安全区内，四周至少留10px透明边距。

### C. 左侧菜单

| 文件建议名 | 交付尺寸 | 数量 | 说明 |
|---|---:|---:|---|
| `menu_panel.png` | `520 × 610` | 1 | 菜单总外框与暗色底纹 |
| `menu_button_normal.png` | `450 × 98` | 1 | 通用普通状态，不含字 |
| `menu_button_hover.png` | `450 × 98` | 1 | 高亮状态，不改变外轮廓 |
| `menu_button_pressed.png` | `450 × 98` | 1 | 按下状态 |
| `menu_button_disabled.png` | `450 × 98` | 1 | 禁用状态 |
| `menu_button_exit_normal.png` | `450 × 98` | 1 | 可选；退出按钮红色版本 |
| `menu_button_exit_hover.png` | `450 × 98` | 1 | 可选；退出按钮红色高亮 |

菜单图标均使用透明 PNG，建议画布 `64 × 64`：

- `menu_icon_start.png`
- `menu_icon_recruit.png`
- `menu_icon_lab.png`
- `menu_icon_prepare.png`
- `menu_icon_exit.png`

按钮文字使用 `ChillBitmap 7px` 的 Display 角色动态绘制，不得烘焙。

### D. 公告与活动入口

| 文件建议名 | 交付尺寸 | 数量 | 说明 |
|---|---:|---:|---|
| `notice_panel.png` | `660 × 630` | 1 | 完整公告外框和底纹 |
| `notice_header.png` | `530 × 68` | 1 | 红色/粉色标题条，不含“公告”文字 |
| `notice_bell_icon.png` | `32 × 32` | 1 | 公告行图标 |
| `activity_slot_normal.png` | `102 × 174` | 1 | 通用活动入口底板 |
| `activity_slot_hover.png` | `102 × 174` | 1 | 悬停状态 |
| `activity_slot_pressed.png` | `102 × 174` | 1 | 按下状态 |
| `activity_icon_01.png` 至 `activity_icon_05.png` | `64 × 64` | 5 | 具体活动图标，透明 PNG |

公告内容、活动名称、倒计时、红点与未读数量全部动态绘制。

### E. 底部玩家 HUD

| 文件建议名 | 交付尺寸 | 数量 | 说明 |
|---|---:|---:|---|
| `player_hud_background.png` | `1884 × 208` | 1 | HUD 外框、底纹和固定分隔线 |
| `player_avatar_frame.png` | `148 × 148` | 1 | 头像框，中心透明 |
| `player_avatar_default.png` | `128 × 128` | 1 | 默认头像；以后可替换 |
| `level_badge.png` | `48 × 48` | 1 | 等级图标 |
| `exp_icon.png` | `48 × 48` | 1 | 可选；经验图标 |
| `exp_bar_back.png` | `200 × 16` | 1 | 经验条底图 |
| `exp_bar_fill.png` | `200 × 16` | 1 | 经验条填充图，允许横向裁切 |
| `currency_icon_01.png` | `48 × 48` | 1 | 第一货币 |
| `currency_icon_02.png` | `48 × 48` | 1 | 第二货币 |
| `currency_icon_03.png` | `48 × 48` | 1 | 第三货币 |
| `settings_button_normal.png` | `150 × 150` | 1 | 设置入口底板 |
| `settings_button_hover.png` | `150 × 150` | 1 | 设置入口高亮 |
| `settings_button_pressed.png` | `150 × 150` | 1 | 设置入口按下 |
| `settings_icon.png` | `72 × 72` | 1 | 齿轮图标，透明 PNG |

玩家名称、玩家编号、等级、经验数值、货币数值和“设置”文字均动态绘制。

## 5. 中央插画中的小角色

如果角色永远不动，可以直接画入 `main_menu_background.png`。如果角色需要待机、走动、点击反馈或根据玩家已拥有英雄变化，则必须单独交付：

| 资产 | 尺寸 | 约束 |
|---|---:|---|
| 单个主界面角色帧 | `128 × 160` | 透明 PNG，与战斗立绘共用正式规格 |
| 待机动画 | 每帧 `128 × 160` | 4–6帧，相同画布和脚底锚点 |
| 行走动画 | 每帧 `128 × 160` | 6–8帧，相同画布和脚底锚点 |

脚底基线保持在 `y≈150`。若主界面人物只需更小显示，由程序最近邻缩放，不另交付模糊小图。

## 6. 九宫格与安全区

- `menu_button_*`、`notice_panel`、`activity_slot_*`、`player_hud_background` 建议同时提供九宫格边距说明。
- 推荐按钮九宫格边距：左右各32px，上下各24px。
- 推荐公告面板九宫格边距：四边各48px。
- 推荐活动入口九宫格边距：左右18px、上下24px。
- 所有按钮状态必须使用完全一致的画布和轮廓，避免 Hover 时跳动。
- 装饰不得侵入文字安全区；需要压在文字前方的花纹应作为独立前景层交付。

## 7. 色彩与导入要求

- UI 与插画交付为 PNG；有透明内容时使用 RGBA。
- 像素图关闭 mipmap，使用 Nearest 过滤。
- 背景大图如采用非严格像素画风，可单独使用线性过滤；边框、按钮与图标仍使用 Nearest。
- 文件名只使用小写 ASCII、数字和下划线。
- 原始 `.aseprite`、`.psd` 或 `.xcf` 放入各自的 `source/` 子目录，并通过 Git LFS 管理。
- 不要在 PNG 中嵌入玩家数据、公告内容、货币数值或按钮名称。

## 8. 推荐目录

```text
assets/art/ui/main_menu/
├─ background/
│  ├─ main_menu_background.png
│  ├─ main_menu_foreground.png
│  └─ main_menu_vignette.png
├─ frame/
│  └─ main_menu_outer_frame.png
├─ logo/
│  └─ game_logo.png
├─ menu/
│  ├─ menu_panel.png
│  ├─ menu_button_normal.png
│  ├─ menu_button_hover.png
│  ├─ menu_button_pressed.png
│  ├─ menu_button_disabled.png
│  └─ menu_icon_*.png
├─ notice/
│  ├─ notice_panel.png
│  ├─ notice_header.png
│  ├─ notice_bell_icon.png
│  ├─ activity_slot_*.png
│  └─ activity_icon_*.png
├─ player_hud/
│  ├─ player_hud_background.png
│  ├─ player_avatar_frame.png
│  ├─ player_avatar_default.png
│  ├─ level_badge.png
│  ├─ exp_bar_*.png
│  ├─ currency_icon_*.png
│  ├─ settings_button_*.png
│  └─ settings_icon.png
└─ source/
```

## 9. 接入验收

美术资源完成后，程序侧必须完成以下验收：

1. `main_menu.tscn` 按目标坐标重构为 Container/Anchor 布局。
2. `player_bar.tscn` 从当前120px高度调整为约208px视觉区域。
3. 五个菜单按钮热区与新按钮底板完全重合。
4. 公告和五个活动入口使用真实节点，不再烘焙在背景。
5. 玩家 HUD 中所有数据为动态 Label/ProgressBar。
6. 在 `1920×1080`、`1600×900`、`1366×768`、`1280×720` 实际窗口运行检查。
7. 文字不得与图片内旧占位文字重叠。
8. 720p 下按钮点击区域必须与视觉位置一致。

