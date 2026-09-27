CREATE TABLE faculty_course_progress (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL,
    course_code VARCHAR(32) NOT NULL,
    completed BOOLEAN NOT NULL DEFAULT FALSE,
    completed_at TIMESTAMPTZ,
    CONSTRAINT fk_faculty_course_progress_player FOREIGN KEY (player_id)
        REFERENCES players(id) ON DELETE CASCADE,
    CONSTRAINT uq_faculty_course_progress UNIQUE (player_id, course_code),
    CONSTRAINT chk_faculty_course_progress_course CHECK (course_code IN ('ICS', 'DM'))
);

CREATE INDEX idx_faculty_course_progress_player ON faculty_course_progress (player_id);

ALTER TABLE faculty_progress
    ADD COLUMN active_course_code VARCHAR(32);
