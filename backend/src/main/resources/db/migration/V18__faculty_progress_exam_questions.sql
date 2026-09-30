-- Day-scoped exam question prep and unprepared-class penalty
-- (compared against player_saves.current_day; not cleared on day advance).
ALTER TABLE faculty_progress
    ADD COLUMN questions_prepared_day INTEGER,
    ADD COLUMN exam_unprepared_penalty_day INTEGER;
