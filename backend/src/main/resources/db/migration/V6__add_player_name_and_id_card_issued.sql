-- Admission display name + ID card issuance flag for world admission flow.

ALTER TABLE player_saves
    ADD COLUMN player_name VARCHAR(128),
    ADD COLUMN id_card_issued BOOLEAN NOT NULL DEFAULT FALSE;

-- Existing admitted journeys already completed the prior placeholder admission path.
UPDATE player_saves
SET id_card_issued = TRUE
WHERE admission_completed = TRUE;
