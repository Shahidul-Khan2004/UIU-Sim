CREATE TABLE faculty_course_assignments (
    id UUID PRIMARY KEY,
    player_id UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    course_id VARCHAR(32) NOT NULL CHECK (course_id IN ('ICS', 'ENGLISH', 'DM')),
    created_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_faculty_course_assignment UNIQUE (player_id, course_id)
);

CREATE TABLE course_materials (
    id UUID PRIMARY KEY,
    course_id VARCHAR(32) NOT NULL CHECK (course_id IN ('ICS', 'ENGLISH', 'DM')),
    title VARCHAR(255) NOT NULL,
    type VARCHAR(16) NOT NULL CHECK (type IN ('PDF', 'LINK')),
    url_or_path TEXT NOT NULL,
    uploaded_by UUID NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_faculty_course_assignments_player ON faculty_course_assignments (player_id);
CREATE INDEX idx_course_materials_course ON course_materials (course_id, created_at);
