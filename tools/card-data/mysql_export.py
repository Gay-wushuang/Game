#!/usr/bin/env python3
"""Export the MySQL card catalog to deterministic runtime JSON for Godot."""
from __future__ import annotations

import argparse
import json
import os
import subprocess
from pathlib import Path


def query(args: argparse.Namespace, sql: str) -> list[list[str]]:
    command = [args.mysql, "--protocol=tcp", f"--host={args.host}", f"--port={args.port}", f"--user={args.user}", "--default-character-set=utf8mb4", "--batch", "--raw", "--skip-column-names", "law_evolution", "--execute", sql]
    env = os.environ.copy()
    if args.password:
        env["MYSQL_PWD"] = args.password
    result = subprocess.run(command, text=True, encoding="utf-8", env=env, check=True, capture_output=True)
    return [line.split("\t") for line in result.stdout.splitlines() if line]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mysql", default="mysql")
    parser.add_argument("--host", default=os.getenv("LAW_DB_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.getenv("LAW_DB_PORT", "3306")))
    parser.add_argument("--user", default=os.getenv("LAW_DB_USER", "law_dev"))
    parser.add_argument("--password", default=os.getenv("LAW_DB_PASSWORD", ""))
    parser.add_argument("--output", type=Path, default=Path("data/generated/cards.generated.json"))
    args = parser.parse_args()

    rows = query(args, "SELECT card_id,design_code,name,card_kind,cost_mode,IFNULL(base_cost,''),target_key,rarity,rules_text,handler_key,logic_mode,COALESCE(lua_script,''),COALESCE(CAST(params_json AS CHAR),'{}'),COALESCE(designer_notes,'') FROM cards WHERE is_enabled=1 ORDER BY design_code")
    tags = query(args, "SELECT card_id,tag_key FROM card_tags ORDER BY card_id,sort_order,tag_key")
    triggers = query(args, "SELECT card_id,event_key FROM card_triggers ORDER BY card_id,sequence_no")
    tag_map: dict[str, list[str]] = {}
    trigger_map: dict[str, list[str]] = {}
    for card_id, tag in tags: tag_map.setdefault(card_id, []).append(tag)
    for card_id, trigger in triggers: trigger_map.setdefault(card_id, []).append(trigger)
    cards = []
    for row in rows:
        card_id, design_code, name, kind, cost_mode, base_cost, target, rarity, rules, handler, logic_mode, lua_script, params, notes = row
        cards.append({"design_code":design_code,"card_id":card_id,"name":name,"card_kind":kind,"cost_mode":cost_mode,"base_cost":None if base_cost == "" else int(base_cost),"target_key":target,"rarity":int(rarity),"keywords":tag_map.get(card_id,[]),"rules_text":rules,"designer_notes":notes,"handler_key":handler,"logic_mode":logic_mode,"lua_script":lua_script,"trigger_keys":trigger_map.get(card_id,[]),"params":json.loads(params)})
    if len(cards) != 30:
        raise SystemExit(f"Expected 30 enabled cards, got {len(cards)}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(cards, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"exported_cards={len(cards)} output={args.output}")


if __name__ == "__main__":
    main()
