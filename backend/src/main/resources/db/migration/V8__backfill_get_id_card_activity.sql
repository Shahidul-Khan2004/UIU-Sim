-- Backfill GET_ID_CARD COMPLETED for existing admitted saves that already have an ID card.
-- PENDING remains absence of a row; New Game clears rows via deleteSave.

INSERT INTO player_day_activities (
    id,
    player_id,
    activity_id,
    day_number,
    status,
    outcome,
    aura_delta,
    reputation_delta,
    resolved_at,
    created_at
)
SELECT
    gen_random_uuid(),
    ps.player_id,
    'GET_ID_CARD',
    GREATEST(ps.current_day, 1),
    'COMPLETED',
    'COMPLETED',
    0,
    0,
    NOW(),
    NOW()
FROM player_saves ps
WHERE ps.id_card_issued = TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM player_day_activities pda
      WHERE pda.player_id = ps.player_id
        AND pda.activity_id = 'GET_ID_CARD'
  );
