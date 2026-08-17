USE law_evolution;

UPDATE cards SET logic_mode = 'LUA' WHERE logic_mode <> 'LUA';
ALTER TABLE cards
  MODIFY COLUMN logic_mode ENUM('LUA') NOT NULL DEFAULT 'LUA';

INSERT IGNORE INTO schema_migrations(version) VALUES ('003_all_cards_lua');
