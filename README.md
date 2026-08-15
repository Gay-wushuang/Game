# 法则演进

基于 Godot 4.7 .NET 与 C# 的卡牌战斗训练场。当前版本用于验证英雄部署、职业克制、攻击与反伤、锦囊牌、升星、AI 回合和测试模式。

## 技术栈

- Godot 4.7 .NET
- C# / .NET 8
- Compatibility 渲染器

## 运行

使用 Godot 4.7 .NET 打开根目录的 `project.godot`，运行主场景即可。

命令行编译：

```powershell
dotnet build .\法则演进.csproj -warnaserror
```

## 目录

- `data/`：英雄、锦囊牌和怪物资源
- `scenes/`：训练场及可复用 UI 场景
- `scripts/`：C# 数据对象、战斗状态和 UI 逻辑
- `tests/`：训练场交互冒烟测试
- `docs/`：玩法规格、角色设计和工作栈基准

项目工程规范以 `docs/工作栈规定.md` 为准。
