CREATE TABLE player_course_enrollments (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    semester INTEGER NOT NULL CHECK (semester >= 1),
    course_id VARCHAR(32) NOT NULL CHECK (course_id IN ('ICS', 'ENGLISH', 'DM')),
    status VARCHAR(32) NOT NULL CHECK (status IN ('ACTIVE', 'DROPPED_CHEATING')),
    drop_reason VARCHAR(64),
    dropped_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_player_course_semester UNIQUE (player_id, semester, course_id),
    CONSTRAINT chk_course_drop CHECK ((status = 'ACTIVE' AND drop_reason IS NULL AND dropped_at IS NULL)
        OR (status = 'DROPPED_CHEATING' AND drop_reason = 'ACADEMIC_MISCONDUCT' AND dropped_at IS NOT NULL))
);
CREATE TABLE player_assessment_results (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    semester INTEGER NOT NULL CHECK (semester >= 1),
    day_number INTEGER NOT NULL CHECK (day_number >= 1),
    course_id VARCHAR(32) NOT NULL CHECK (course_id IN ('ICS', 'ENGLISH', 'DM')),
    assessment_type VARCHAR(32) NOT NULL CHECK (assessment_type IN ('QUIZ_1', 'MIDTERM', 'QUIZ_2', 'FINAL')),
    state VARCHAR(32) NOT NULL CHECK (state IN ('STARTED', 'COMPLETED', 'MISSED', 'CHEAT_SUCCESS', 'CHEAT_CAUGHT')),
    marks_obtained INTEGER NOT NULL DEFAULT 0,
    max_marks INTEGER NOT NULL,
    aura_delta INTEGER NOT NULL DEFAULT 0,
    academic_reputation_delta INTEGER NOT NULL DEFAULT 0,
    question_index INTEGER NOT NULL DEFAULT 0 CHECK (question_index BETWEEN 0 AND 8),
    attempt_snapshot TEXT,
    question_deadline TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_player_course_assessment UNIQUE (player_id, semester, course_id, assessment_type),
    CONSTRAINT chk_assessment_marks CHECK (marks_obtained BETWEEN 0 AND max_marks AND MOD(marks_obtained, 5) = 0),
    CONSTRAINT chk_assessment_max CHECK ((assessment_type IN ('QUIZ_1', 'QUIZ_2') AND max_marks = 15)
        OR (assessment_type = 'MIDTERM' AND max_marks = 30) OR (assessment_type = 'FINAL' AND max_marks = 40)),
    CONSTRAINT chk_assessment_terminal CHECK ((state = 'STARTED' AND completed_at IS NULL AND question_deadline IS NOT NULL AND attempt_snapshot IS NOT NULL)
        OR (state <> 'STARTED' AND completed_at IS NOT NULL AND question_deadline IS NULL)),
    CONSTRAINT chk_assessment_outcome_marks CHECK ((state NOT IN ('MISSED', 'CHEAT_CAUGHT') OR marks_obtained = 0)
        AND (state <> 'CHEAT_SUCCESS' OR marks_obtained = max_marks)),
    CONSTRAINT chk_assessment_normal_deltas CHECK (state NOT IN ('STARTED', 'COMPLETED', 'MISSED') OR (aura_delta = 0 AND academic_reputation_delta = 0))
);
