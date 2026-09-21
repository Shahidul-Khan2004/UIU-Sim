-- Current-day activity outcomes only (no historical tracking).
-- PENDING is represented by the absence of a row for (player_id, activity_id).

CREATE TABLE player_day_activities (
    id                  UUID PRIMARY KEY,
    player_id           UUID NOT NULL,
    activity_id         VARCHAR(64) NOT NULL,
    day_number          INTEGER NOT NULL,
    status              VARCHAR(32) NOT NULL,
    outcome             VARCHAR(64) NOT NULL,
    aura_delta          INTEGER NOT NULL DEFAULT 0,
    reputation_delta    INTEGER NOT NULL DEFAULT 0,
    resolved_at         TIMESTAMPTZ NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_player_day_activities_player_activity
        UNIQUE (player_id, activity_id),
    CONSTRAINT fk_player_day_activities_player
        FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    CONSTRAINT chk_player_day_activities_day_number
        CHECK (day_number >= 1),
    CONSTRAINT chk_player_day_activities_status
        CHECK (status IN ('COMPLETED', 'MISSED'))
);

CREATE INDEX idx_player_day_activities_player_id
    ON player_day_activities (player_id);
