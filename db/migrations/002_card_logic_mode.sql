USE law_evolution;

SET @has_logic_mode = (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema = 'law_evolution' AND table_name = 'cards' AND column_name = 'logic_mode'
);
SET @logic_sql = IF(@has_logic_mode = 0,
  'ALTER TABLE cards ADD COLUMN logic_mode ENUM(''BUILTIN'', ''LUA'') NOT NULL DEFAULT ''BUILTIN'' AFTER artwork_key, ADD COLUMN lua_script VARCHAR(255) NULL AFTER logic_mode',
  'SELECT 1'
);
PREPARE logic_statement FROM @logic_sql;
EXECUTE logic_statement;
DEALLOCATE PREPARE logic_statement;

INSERT IGNORE INTO schema_migrations(version) VALUES ('002_card_logic_mode');
