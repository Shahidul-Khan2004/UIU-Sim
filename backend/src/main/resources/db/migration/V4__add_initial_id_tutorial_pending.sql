-- One-time initial ID tutorial flag on mutable gameplay state.
-- Existing players: false (no forced tutorial on next login).
-- New players: default true (set after backfill so inserts receive true).

ALTER TABLE player_stats
    ADD COLUMN initial_id_tutorial_pending BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE player_stats
    ALTER COLUMN initial_id_tutorial_pending SET DEFAULT TRUE;
