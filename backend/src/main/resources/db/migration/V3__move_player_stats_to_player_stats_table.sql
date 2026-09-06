CREATE TABLE player_stats (
    player_id UUID PRIMARY KEY,
    aura INTEGER NOT NULL DEFAULT 50,
    academic_reputation INTEGER NOT NULL DEFAULT 50,
    CONSTRAINT fk_player_stats_player FOREIGN KEY (player_id)
        REFERENCES players(id) ON DELETE CASCADE,
    CONSTRAINT chk_player_stats_aura CHECK (aura >= 0 AND aura <= 100),
    CONSTRAINT chk_player_stats_academic_reputation CHECK (academic_reputation >= 0 AND academic_reputation <= 100)
);

INSERT INTO player_stats (player_id, aura, academic_reputation)
SELECT id, aura, academic_reputation FROM players;

ALTER TABLE players
    DROP CONSTRAINT chk_players_aura;

ALTER TABLE players
    DROP CONSTRAINT chk_players_academic_reputation;

ALTER TABLE players
    DROP COLUMN aura;

ALTER TABLE players
    DROP COLUMN academic_reputation;
