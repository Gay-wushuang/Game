#!/usr/bin/env python3
"""Import normalized card seed JSON into the development-only MySQL catalog."""
from __future__ import annotations

import argparse
import json
import os
import subprocess
from pathlib import Path


def sql_string(value: object) -> str:
    if value is None:
        return "NULL"
    text = str(value).replace("\\", "\\\\").replace("'", "''")
    return f"'{text}'"


def json_sql(value: object) -> str:
    return f"CAST({sql_string(json.dumps(value, ensure_ascii=False, separators=(',', ':')))} AS JSON)"


def mysql_command(args: argparse.Namespace) -> list[str]:
    command = [args.mysql, "--protocol=tcp", f"--host={args.host}", f"--port={args.port}", f"--user={args.user}", "--default-character-set=utf8mb4"]
    return command


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mysql", default="mysql")
    parser.add_argument("--host", default=os.getenv("LAW_DB_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.getenv("LAW_DB_PORT", "3306")))
    parser.add_argument("--user", default=os.getenv("LAW_DB_USER", "law_dev"))
    parser.add_argument("--password", default=os.getenv("LAW_DB_PASSWORD", ""))
    parser.add_argument("--migration", type=Path, default=Path("db/migrations/001_card_catalog.sql"))
    parser.add_argument("--seed", type=Path, default=Path("db/seeds/cards.v1.json"))
    args = parser.parse_args()

    cards = json.loads(args.seed.read_text(encoding="utf-8"))
    ids = [c["card_id"] for c in cards]
    codes = [c["design_code"] for c in cards]
    if len(cards) != 30 or len(ids) != len(set(ids)) or len(codes) != len(set(codes)):
        raise SystemExit("Seed validation failed: expected 30 unique card_id and design_code values")

    statements = [args.migration.read_text(encoding="utf-8"), "USE law_evolution;", "SET FOREIGN_KEY_CHECKS=0;", "TRUNCATE card_upgrades;", "TRUNCATE card_triggers;", "TRUNCATE card_effects;", "TRUNCATE card_tags;", "TRUNCATE cards;", "SET FOREIGN_KEY_CHECKS=1;"]
    for card in cards:
        base_cost = "NULL" if card["base_cost"] is None else str(int(card["base_cost"]))
        statements.append(
            "INSERT INTO cards(card_id,design_code,name,card_kind,cost_mode,base_cost,target_key,rarity,rules_text,handler_key,params_json,designer_notes) VALUES ("
            + ",".join([
                sql_string(card["card_id"]), sql_string(card["design_code"]), sql_string(card["name"]), sql_string(card["card_kind"]),
                sql_string(card["cost_mode"]), base_cost, sql_string(card["target_key"]), str(int(card["rarity"])), sql_string(card["rules_text"]),
                sql_string(card["handler_key"]), json_sql(card.get("params", {})), sql_string(card.get("designer_notes", "")),
            ]) + ");"
        )
        for order, tag in enumerate(card.get("keywords", []), start=1):
            statements.append(f"INSERT INTO card_tags(card_id,tag_key,sort_order) VALUES ({sql_string(card['card_id'])},{sql_string(tag)},{order});")
        statements.append(
            f"INSERT INTO card_effects(card_id,sequence_no,handler_key,target_key,params_json) VALUES ({sql_string(card['card_id'])},1,{sql_string(card['handler_key'])},{sql_string(card['target_key'])},{json_sql(card.get('params', {}))});"
        )
        trigger_keys = [key for key in card.get("trigger_key", "").split("|") if key]
        for order, trigger in enumerate(trigger_keys, start=1):
            post_zone = "EXILE" if card.get("params", {}).get("post_zone") == "EXILE" else "DISCARD"
            counterable = 0 if card.get("params", {}).get("counterable") is False else 1
            statements.append(
                f"INSERT INTO card_triggers(card_id,sequence_no,event_key,counterable,post_trigger_zone) VALUES ({sql_string(card['card_id'])},{order},{sql_string(trigger)},{counterable},{sql_string(post_zone)});"
            )
    statements.append("SELECT COUNT(*) AS imported_cards FROM cards;")
    env = os.environ.copy()
    if args.password:
        env["MYSQL_PWD"] = args.password
    result = subprocess.run(mysql_command(args), input="\n".join(statements), text=True, encoding="utf-8", env=env, check=True, capture_output=True)
    print(result.stdout.strip())


if __name__ == "__main__":
    main()
