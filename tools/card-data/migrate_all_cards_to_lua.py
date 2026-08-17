#!/usr/bin/env python3
"""Give every card a standalone Lua entry script while keeping atomic rules in C#."""
from __future__ import annotations
import json
from pathlib import Path

seed_path = Path("db/seeds/cards.v1.json")
cards = json.loads(seed_path.read_text(encoding="utf-8"))
for card in cards:
    folder = "active" if card["card_kind"] == "ACTIVE" else "passive"
    script_path = Path("cards/lua") / folder / f'{card["card_id"].removeprefix("card_")}.lua'
    if not script_path.exists():
        script_path.parent.mkdir(parents=True, exist_ok=True)
        script_path.write_text(
            f'-- {card["name"]}；规则入口统一由 Lua 编排，状态修改只能经过 C# Card API。\n'
            f'resolve_card_effect("{card["handler_key"]}")\n'
            f'log_card("{card["name"]}：效果已结算。")\n',
            encoding="utf-8",
        )
    card["logic_mode"] = "LUA"
    card["lua_script"] = "res://" + script_path.as_posix()
seed_path.write_text(json.dumps(cards, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"migrated={len(cards)}")
