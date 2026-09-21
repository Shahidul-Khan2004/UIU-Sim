-- Departments catalog + one active university journey per player.
-- players remains identity-only; gameplay progression lives on player_saves.

CREATE TABLE departments (
    id          UUID PRIMARY KEY,
    code        VARCHAR(32) NOT NULL,
    name        VARCHAR(255) NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_departments_code UNIQUE (code)
);

INSERT INTO departments (id, code, name, created_at) VALUES
    ('a1000000-0000-4000-8000-000000000001', 'CSE', 'Computer Science and Engineering', NOW()),
    ('a1000000-0000-4000-8000-000000000002', 'BBA', 'Business Administration', NOW());

CREATE TABLE player_saves (
    id                   UUID PRIMARY KEY,
    player_id            UUID NOT NULL,
    role                 VARCHAR(32) NOT NULL,
    department_id        UUID NOT NULL,
    university_id        VARCHAR(64),
    semester             INTEGER NOT NULL DEFAULT 1,
    current_day          INTEGER NOT NULL DEFAULT 1,
    admission_completed  BOOLEAN NOT NULL DEFAULT FALSE,
    last_saved_at        TIMESTAMPTZ,
    created_at           TIMESTAMPTZ NOT NULL,
    updated_at           TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_player_saves_player_id UNIQUE (player_id),
    CONSTRAINT fk_player_saves_player
        FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    CONSTRAINT fk_player_saves_department
        FOREIGN KEY (department_id) REFERENCES departments(id),
    CONSTRAINT chk_player_saves_role
        CHECK (role IN ('STUDENT', 'FACULTY')),
    CONSTRAINT chk_player_saves_semester
        CHECK (semester >= 1),
    CONSTRAINT chk_player_saves_current_day
        CHECK (current_day >= 1)
);
