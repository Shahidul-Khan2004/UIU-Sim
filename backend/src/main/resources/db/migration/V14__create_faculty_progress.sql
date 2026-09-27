CREATE TABLE faculty_progress (
    player_id UUID PRIMARY KEY,
    reputation INTEGER NOT NULL DEFAULT 50,
    computer_used BOOLEAN NOT NULL DEFAULT FALSE,
    office_entered BOOLEAN NOT NULL DEFAULT FALSE,
    classroom_scanned BOOLEAN NOT NULL DEFAULT FALSE,
    lecture_completed BOOLEAN NOT NULL DEFAULT FALSE,
    lecture_left BOOLEAN NOT NULL DEFAULT FALSE,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT fk_faculty_progress_player FOREIGN KEY (player_id)
        REFERENCES players(id) ON DELETE CASCADE,
    CONSTRAINT chk_faculty_progress_reputation CHECK (reputation >= 0 AND reputation <= 100)
);
