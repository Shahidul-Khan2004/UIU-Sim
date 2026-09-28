-- Widen course_id CHECKs from V11/V12 (inline unnamed CHECKs become {table}_{column}_check in PostgreSQL/H2)
-- to allow BBA Semester 1 courses alongside existing CSE course IDs.

ALTER TABLE player_course_enrollments DROP CONSTRAINT IF EXISTS player_course_enrollments_course_id_check;
ALTER TABLE player_course_enrollments
    ADD CONSTRAINT player_course_enrollments_course_id_check
    CHECK (course_id IN ('ICS', 'ENGLISH', 'DM', 'IB', 'POA', 'BBA-ENGLISH'));

ALTER TABLE player_assessment_results DROP CONSTRAINT IF EXISTS player_assessment_results_course_id_check;
ALTER TABLE player_assessment_results
    ADD CONSTRAINT player_assessment_results_course_id_check
    CHECK (course_id IN ('ICS', 'ENGLISH', 'DM', 'IB', 'POA', 'BBA-ENGLISH'));

ALTER TABLE faculty_course_assignments DROP CONSTRAINT IF EXISTS faculty_course_assignments_course_id_check;
ALTER TABLE faculty_course_assignments
    ADD CONSTRAINT faculty_course_assignments_course_id_check
    CHECK (course_id IN ('ICS', 'ENGLISH', 'DM', 'IB', 'POA', 'BBA-ENGLISH'));

ALTER TABLE course_materials DROP CONSTRAINT IF EXISTS course_materials_course_id_check;
ALTER TABLE course_materials
    ADD CONSTRAINT course_materials_course_id_check
    CHECK (course_id IN ('ICS', 'ENGLISH', 'DM', 'IB', 'POA', 'BBA-ENGLISH'));
