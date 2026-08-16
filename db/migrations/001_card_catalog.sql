CREATE DATABASE IF NOT EXISTS law_evolution
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

USE law_evolution;

CREATE TABLE IF NOT EXISTS schema_migrations (
  version VARCHAR(64) PRIMARY KEY,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS cards (
  card_id VARCHAR(64) PRIMARY KEY,
  design_code VARCHAR(32) NOT NULL UNIQUE,
  name VARCHAR(100) NOT NULL,
  card_kind ENUM('ACTIVE', 'PASSIVE') NOT NULL,
  cost_mode ENUM('FIXED', 'MAX_AP', 'ALL_CURRENT', 'FORMULA') NOT NULL DEFAULT 'FIXED',
  base_cost SMALLINT NULL,
  cost_params JSON NULL,
  target_key VARCHAR(64) NOT NULL DEFAULT 'NONE',
  rarity TINYINT NOT NULL,
  rules_text TEXT NOT NULL,
  artwork_key VARCHAR(255) NULL,
  handler_key VARCHAR(64) NOT NULL,
  params_json JSON NULL,
  designer_notes TEXT NULL,
  is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
  content_version INT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT chk_cards_rarity CHECK (rarity BETWEEN 1 AND 5),
  CONSTRAINT chk_cards_cost CHECK (
    (cost_mode = 'FIXED' AND base_cost IS NOT NULL AND base_cost >= 0)
    OR (cost_mode <> 'FIXED' AND base_cost IS NULL)
  )
);

CREATE TABLE IF NOT EXISTS card_tags (
  card_id VARCHAR(64) NOT NULL,
  tag_key VARCHAR(64) NOT NULL,
  sort_order SMALLINT NOT NULL DEFAULT 0,
  PRIMARY KEY (card_id, tag_key),
  CONSTRAINT fk_card_tags_card FOREIGN KEY (card_id)
    REFERENCES cards(card_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS card_effects (
  effect_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  card_id VARCHAR(64) NOT NULL,
  sequence_no SMALLINT NOT NULL DEFAULT 1,
  handler_key VARCHAR(64) NOT NULL,
  target_key VARCHAR(64) NOT NULL DEFAULT 'NONE',
  params_json JSON NULL,
  condition_json JSON NULL,
  branch_group VARCHAR(64) NULL,
  branch_key VARCHAR(64) NULL,
  probability DECIMAL(6,5) NULL,
  UNIQUE KEY uq_card_effect_sequence (card_id, sequence_no),
  CONSTRAINT fk_card_effects_card FOREIGN KEY (card_id)
    REFERENCES cards(card_id) ON DELETE CASCADE,
  CONSTRAINT chk_effect_probability CHECK (probability IS NULL OR probability BETWEEN 0 AND 1)
);

CREATE TABLE IF NOT EXISTS card_triggers (
  trigger_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  card_id VARCHAR(64) NOT NULL,
  sequence_no SMALLINT NOT NULL DEFAULT 1,
  event_key VARCHAR(64) NOT NULL,
  source_scope ENUM('ALLY', 'ENEMY', 'ANY') NOT NULL DEFAULT 'ANY',
  condition_json JSON NULL,
  priority SMALLINT NOT NULL DEFAULT 100,
  counterable BOOLEAN NOT NULL DEFAULT TRUE,
  auto_reveal BOOLEAN NOT NULL DEFAULT TRUE,
  post_trigger_zone ENUM('DISCARD', 'SET', 'EXILE') NOT NULL DEFAULT 'DISCARD',
  UNIQUE KEY uq_card_trigger_sequence (card_id, sequence_no),
  CONSTRAINT fk_card_triggers_card FOREIGN KEY (card_id)
    REFERENCES cards(card_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS card_upgrades (
  card_id VARCHAR(64) NOT NULL,
  upgrade_level SMALLINT NOT NULL,
  cost_override SMALLINT NULL,
  params_override JSON NULL,
  rules_text_override TEXT NULL,
  PRIMARY KEY (card_id, upgrade_level),
  CONSTRAINT fk_card_upgrades_card FOREIGN KEY (card_id)
    REFERENCES cards(card_id) ON DELETE CASCADE
);

INSERT IGNORE INTO schema_migrations(version) VALUES ('001_card_catalog');
