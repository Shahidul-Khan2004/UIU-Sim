DELETE FROM course_materials WHERE type = 'PDF';

ALTER TABLE course_materials ADD COLUMN url TEXT;
UPDATE course_materials SET url = url_or_path;
ALTER TABLE course_materials ALTER COLUMN url SET NOT NULL;

ALTER TABLE course_materials DROP COLUMN url_or_path;
ALTER TABLE course_materials DROP COLUMN type;
