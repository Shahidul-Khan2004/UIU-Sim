ALTER TABLE players
    ADD COLUMN aura INTEGER NOT NULL DEFAULT 50,
    ADD COLUMN academic_reputation INTEGER NOT NULL DEFAULT 50,
    ADD CONSTRAINT chk_players_aura CHECK (aura >= 0 AND aura <= 100),
    ADD CONSTRAINT chk_players_academic_reputation CHECK (academic_reputation >= 0 AND academic_reputation <= 100);
