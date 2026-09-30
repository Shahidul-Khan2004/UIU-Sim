-- Optional once-per-day faculty coffee claim (day-scoped against player_saves.current_day).
ALTER TABLE faculty_progress
    ADD COLUMN coffee_claimed_day INTEGER,
    ADD COLUMN coffee_option VARCHAR(64);

ALTER TABLE faculty_progress
    ADD CONSTRAINT chk_faculty_progress_coffee_option
        CHECK (
            coffee_option IS NULL
            OR coffee_option IN (
                'CAPPUCCINO',
                'CAPPUCCINO_COOKIE',
                'CAPPUCCINO_COOKIE_BROWNIE'
            )
        );
