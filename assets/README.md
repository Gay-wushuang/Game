# Assets 目录约定

运行时与参考素材按用途分层，文件名统一使用小写 ASCII、数字和下划线。

```text
assets/
├─ art/
│  └─ cards/<card_id>/
│     ├─ illustration.png       # 运行时插图
│     ├─ frame.png              # 运行时无文字、无星级卡框
│     ├─ star_unit.png          # 运行时单枚星标素材
│     └─ reference/             # 不直接用于正式游戏显示的中间稿与成品参考
├─ audio/
│  ├─ bgm/
│  └─ sfx/
├─ fonts/
└─ ui/
```

## 《暴力手段》

- `art/cards/violent_means/illustration.png`：192×244 运行时插图。
- `art/cards/violent_means/frame.png`：192×244 运行时基础卡框。
- `art/cards/violent_means/star_unit.png`：单枚星标来源，由程序按稀有度重复。
- `art/cards/violent_means/reference/composite_reference.png`：旧数值成品参考，不直接投入运行时。
- `art/cards/violent_means/reference/frame_with_text_reference.png`：带文字中间稿。
- `art/cards/violent_means/reference/frame_with_stars_reference.png`：带星标中间稿。

## 《妙手回春》

- `art/cards/miracle_heal/illustration.png`：192×244 运行时插图。
- `art/cards/miracle_heal/frame.png`：192×244 运行时基础卡框。
- `art/cards/miracle_heal/star_unit.png`：单枚星标来源；正式稀有度为 1。
- `art/cards/miracle_heal/reference/composite_reference.png`：AP 1、一星成品参考。

Godot 生成的 `.import` 与 `.godot/` 内容不应提交 Git。
