CREATE TABLE players (
    id              UUID PRIMARY KEY,
    clerk_user_id   TEXT NOT NULL UNIQUE,
    email           TEXT,
    username        TEXT,
    created_at      TIMESTAMPTZ NOT NULL,
    last_login      TIMESTAMPTZ
);
