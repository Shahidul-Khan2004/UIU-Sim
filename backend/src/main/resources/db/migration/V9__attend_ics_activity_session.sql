-- Extend current-day activities for ATTEND_ICS lecture sessions.
-- PENDING remains absence of a row. IN_PROGRESS covers an active lecture.
-- Milestone progress and pause-aware timing live on the same activity row.

ALTER TABLE player_day_activities
    DROP CONSTRAINT chk_player_day_activities_status;

ALTER TABLE player_day_activities
    ADD CONSTRAINT chk_player_day_activities_status
        CHECK (status IN ('COMPLETED', 'MISSED', 'IN_PROGRESS'));

ALTER TABLE player_day_activities
    ADD COLUMN milestone_seconds INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN session_started_at TIMESTAMPTZ,
    ADD COLUMN session_paused_at TIMESTAMPTZ,
    ADD COLUMN accumulated_active_ms BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN early_leave_penalty_applied BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE player_day_activities
    ADD CONSTRAINT chk_player_day_activities_milestone_seconds
        CHECK (milestone_seconds IN (0, 30, 60, 90));

ALTER TABLE player_day_activities
    ADD CONSTRAINT chk_player_day_activities_accumulated_active_ms
        CHECK (accumulated_active_ms >= 0);
