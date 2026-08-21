# 主界面流程交互框架

本文档面向后续参与本项目的开发者（整合、扩展、调试），说明“主界面交互逻辑”模块的架构、设计约束与扩展方式。

## 1. 模块定位

本模块只负责**交互逻辑**，不制造任何视觉：

- 所有界面视觉来自 `界面UI包/`（主美设计源，svg/xcf）转换出的运行时素材 `assets/ui/*.svg`
- 交互层是**纯透明点击区**（hover 时仅白色 8% 覆盖 + 手型光标），按钮文字、色块、装饰一律不做
- 修改任何界面视觉前，请先与设计侧确认；素材源文件（`界面UI包/`）**永不直接修改**

## 2. 环境

| 项 | 值 |
|---|---|
| 引擎 | Godot 4.7 .NET（Compatibility 渲染器） |
| 语言 | C# / .NET 8（严禁新增 GDScript） |
| 视口 | 1920×1080（stretch: canvas_items / expand） |
| 设计稿 | 1280×720（720p）→ 运行时所有坐标按 **×1.5** 换算 |

## 3. 架构总览

```
Autoload
├── SceneRouter    (scripts/ui/SceneRouter.cs)
│    场景路由 / 返回栈 / 异步加载入口 / 跨场景转场黑幕
└── SystemNotice   (scenes/components/system_notice.tscn)
     全局提示弹窗（"敬请期待"等），点外部/Esc 关闭

scenes/components
├── hot_button.tscn      纯透明交互按钮（hover 白8% + 手型）
├── player_bar.tscn      底栏透明交互（仅右下角"设置"透明按钮）
└── system_notice.tscn   见上

scenes/*.tscn            每个界面一个场景（主美素材铺底 + 透明交互层）
scripts/ui/*.cs          每个界面一个脚本（仅事件接线与状态）
```

### 3.1 SceneRouter（路由核心）

| 成员 | 说明 |
|---|---|
| `Scenes.*` 静态常量 | 所有场景路径集中管理，禁止散落字符串 |
| `GoTo(path)` | 压栈当前场景并切换（返回键靠栈回溯） |
| `Back()` | 返回上一场景（按链路，非回主菜单） |
| `LoadAndEnter(path)` | 进加载界面，异步加载目标场景（战斗专用） |
| `DoTransition(packed)` | 常驻黑幕转场（见 §6） |

### 3.2 界面脚本约定

- `_Ready()` 只做两件事：绑定按钮事件、初始状态
- 场景内可交互元素命名：`%BackButton`、`%StartButton` 等 `unique_name_in_owner`
- 未实现功能统一回调 `SystemNotice.Instance.Show("敬请期待")`

## 4. 场景与交互矩阵

| 场景 | 入口 | 返回 | 交互 |
|---|---|---|---|
| main_menu | 启动场景 | — | 开始→模式选择；招募→商店；研发→研发中心；备战→卡组选择；退出→Quit |
| mode_select | 主界面·开始 | →主界面 | 故事模式→选关；挑战/竞技场/pvp→敬请期待 |
| level_select | 模式·故事 | →模式选择 | 关卡1→地图；2~5→敬请期待；存档卡 保存/删除→敬请期待 |
| map_ui | 选关·关卡1 | →选关 | 左侧信息窗第一占位块→战前准备；四按钮（上一页/下一页→敬请期待） |
| prepare_ui | 地图·入口/准备 | →地图 | 8 卡单选（toggle 高亮）；开始：未选卡→提示，已选→加载｜战斗 |
| loading_ui | 战前·开始 | — | 环形进度 = 战斗场景真实加载进度（无返回） |
| shop_ui | 主界面·招募 | →主界面 | 仅返回/设置（栏目交互待素材） |
| lab_ui | 主界面·研发 | →主界面 | 仅返回/设置（模块交互待素材） |
| deck_select | 主界面·备战 | →主界面 | 仅返回/设置（卡组模块交互待素材） |
| deck_ui | （暂无入口，素材就绪后接入） | →卡组选择 | 3 tab 分类 / 10 卡点选计数 / 预览 |
| settings_ui | 底栏·设置 | →上一界面 | 5 个设置 tab 切换内容；本页禁用设置按钮 |

> `training_arena.tscn`（战斗）由战斗侧维护，本模块只负责"加载并进入"。

## 5. 素材与坐标约定

### 5.1 目录职责

```
界面UI包/<界面>/*.svg(xcf)   ← 主美设计源，只读
assets/ui/*.svg              ← 运行时素材（转换产物，可随源更新重新生成）
assets/ui/*.png              ← 历史运行时位图（已被 svg 取代，待设计确认后清理，勿删）
```

### 5.2 SVG 运行时渲染链（重要）

Godot 的 SVG 导入器（ThorVG）**不渲染 `<text>` 元素**，直接使用设计稿 svg 会导致全部文字丢失（图形正常）。因此运行时素材必须经过文字转轮廓处理：

```bash
python tools/svg_text_to_path.py 界面UI包/<界面>/<name>.svg assets/ui/<name>.svg
# 依赖：fontTools（安装于 F:\dev\python-libs\svgtext，加载需 PYTHONPATH）
```

转换特性：
- `<text>` → `<path>`（fill-rule="evenodd"，修正 SimSun 复合字形反向子路径导致的笔画挖空）
- 正确处理 `transform="matrix(...)"` 与 `x/y` 定位、`text-anchor` 居中、H/V 单坐标命令
- 字体映射（本机无 Adobe/Arial 专属字体时的替代）：

| 设计字体 | 运行时替代 |
|---|---|
| Microsoft YaHei / 粗体 | msyh.ttc / msyhbd.ttc |
| Arial / Arial-BoldMT | arial.ttf / arialbd.ttf |
| AdobeSongStd-Light-GBpc-EUC-H | simsun.ttc |

> 字形以替代字体渲染，与设计字体存在细微差异，已与设计侧确认接受。

### 5.3 坐标换算

设计稿为 720p，视口 1080p：`运行时坐标 = 设计稿坐标 × 1.5`。
交互区位置应取自设计稿 svg 的**实际元素矩形**（非目测），并让主美素材与交互区保持同一换算，避免错位。

### 5.4 底栏

- 底栏视觉已烘在设计稿中，交互层**只放右下角"设置"透明按钮**（`PlayerBar`）
- 版本号 `v0.1.0` 锚定设计稿 `(0, 628)` 属设计铁律，任何界面不得移动

### 5.5 占位图 error.png

- 位置：`assets/ui/error.png`（100×100，来源为设计侧交付的占位图，已随素材导入）
- 用途：交互层中需要图片展示但暂无正式素材的位置，一律平铺 `error.png` 占位（如存档卡图区、卡牌插画区）
- 使用方式：`TextureRect`，`expand_mode = 1` + `stretch_mode = 1`（平铺）
- 注意：素材就绪替换后，连同该纹理引用一并移除，不要在场景里留下无引用的占位节点

## 6. 加载与转场机制

```
战前准备·开始
  └─ SceneRouter.LoadAndEnter(Battle)
       ├─ PendingLoad = "res://scenes/training_arena.tscn"
       └─ 切 loading_ui
LoadingUi._Ready
  ├─ LoadThreadedRequest(pending)（未缓存时）
  └─ _Process 按 LoadThreadedGetStatus 驱动环形进度（真实进度）
进度 100% → 停留 0.4s
  └─ SceneRouter.DoTransition(packed)
       ├─ 淡入黑幕 0.3s（黑幕挂在 SceneRouter，跨场景存活）
       ├─ ChangeSceneToPacked（新场景 _Ready 耗时帧被黑幕覆盖，无白帧）
       ├─ 新场景就绪（2 帧）→ 停留 0.15s
       └─ 淡出黑幕 0.3s
```

> 黑幕必须挂在 autoload（SceneRouter），挂在场景内会随旧场景销毁而暴露切换空白帧。

## 7. 新增界面接入步骤

1. 主美提供设计稿 → 放 `界面UI包/<界面>/`
2. 转换：`python tools/svg_text_to_path.py <源svg> assets/ui/<name>.svg`
3. 清缓存重导入：删除 `assets/ui/<name>.svg.import` 与 `.godot/imported/ 对应产物`，然后 `Godot --headless --import`
4. 新建 `scenes/<name>.tscn`：`Bg(TextureRect, 素材) + 透明交互层 + PlayerBar`
5. 新建 `scripts/ui/<Name>.cs`：绑定事件；返回键一律 `SceneRouter.Instance.Back()`
6. 在 `SceneRouter.Scenes` 注册路径；在上游界面接线入口
7. 验证：截图工具 + F5 冒烟（§8）

## 8. 验证

```powershell
dotnet build .\法则演进.csproj -warnaserror     # 基线：0 警告 0 错误
```

截图工具（无人工交互冒烟）：

```powershell
Godot_v4.7.1-stable_mono_win64_console.exe --path . res://tests/ui_screenshot_capture.tscn -- --scene=res://scenes/<name>.tscn --output=<out.png>
```

像素级验证可参考本模块开发时的自查习惯：白色文字像素统计、区域常见色检测（工具脚本曾存放于临时目录，模式见 tools/ 历史）。

## 9. 已知事项 / 待办

- [ ] `shop_ui` / `lab_ui` / `deck_select` 的栏目/模块交互**暂缓**：设计侧还没有细分素材，现仅保留返回与设置，素材就绪后按 §7 恢复（原滚动列表/加减/卡组模块代码实现已移除，接口以"敬请期待"占位）
- [ ] `deck_ui`（卡组组合）暂无入口，等卡组选择交互恢复后接 `编辑` 按钮
- [ ] `assets/ui/*.png` 已被 svg 取代，等设计确认后统一删除（含 `.import` 与导入缓存）
- [ ] 选关字形为标准宋体（SimSun），与设计字体有细微差异（已确认接受）
- [ ] 主场景已切换为 `main_menu.tscn`；战斗/整合侧如需直接调试训练场，可临时改 `project.godot` 的 `run/main_scene`