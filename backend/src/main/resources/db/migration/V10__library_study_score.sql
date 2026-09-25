-- Optional Library Study stores the submitted normalized score on the current-day row.
-- Reward stays in reputation_delta. Absence of a row still means AVAILABLE.

ALTER TABLE player_day_activities
    ADD COLUMN normalized_score INTEGER;

ALTER TABLE player_day_activities
    ADD CONSTRAINT chk_player_day_activities_normalized_score
        CHECK (normalized_score IS NULL OR (normalized_score >= 0 AND normalized_score <= 100));
