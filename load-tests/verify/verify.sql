-- Out-of-band correctness verification, straight against PostgreSQL.
-- This is the *definitive* check for the integrity invariants — k6 only sees the
-- API surface. Run it after a load scenario, optionally scoped to one game:
--
--   psql "$DATABASE_URL" -v game="'<game-session-uuid>'" -f verify/verify.sql
--   psql "$DATABASE_URL" -f verify/verify.sql                       -- all games
--
-- Every query below should return ZERO rows on a healthy system. Any row is a
-- violation of non-functional-requirements.md.

\set ON_ERROR_STOP on
\if :{?game}
\else
  \set game 'NULL'
\endif

\echo '== 1. Duplicate accepted answers: >1 answer row per (game, question, participant) =='
SELECT game_session_id, question_id, participant_id, COUNT(*) AS answer_rows
FROM   answers
WHERE  (:game IS NULL OR game_session_id = :game)
GROUP  BY game_session_id, question_id, participant_id
HAVING COUNT(*) > 1;

\echo '== 2. Duplicate / lost score: participant.total_score <> SUM(points_awarded) of their answers =='
SELECT p.id AS participant_id,
       p.game_session_id,
       p.total_score,
       COALESCE(SUM(a.points_awarded), 0) AS sum_points_awarded
FROM   participants p
LEFT   JOIN answers a ON a.participant_id = p.id
WHERE  (:game IS NULL OR p.game_session_id = :game)
GROUP  BY p.id, p.game_session_id, p.total_score
HAVING p.total_score <> COALESCE(SUM(a.points_awarded), 0);

\echo '== 3. Score out of range: total_score negative, or above the max points of any single answered question =='
SELECT p.id AS participant_id, p.game_session_id, p.total_score
FROM   participants p
WHERE  (:game IS NULL OR p.game_session_id = :game)
  AND (p.total_score < 0
       OR p.total_score > COALESCE((
            SELECT SUM(q.points)
            FROM   questions q
            WHERE  q.id IN (SELECT DISTINCT a.question_id FROM answers a WHERE a.participant_id = p.id)
          ), 0));

\echo '== 4. Answer references a choice that is not part of its question =='
SELECT a.id AS answer_id, a.question_id, a.selected_choice_id
FROM   answers a
LEFT   JOIN choices c ON c.id = a.selected_choice_id AND c.question_id = a.question_id
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  c.id IS NULL;

\echo '== 5. is_correct flag on an answer disagrees with the question''s correct choice =='
SELECT a.id AS answer_id, a.question_id, a.selected_choice_id, a.is_correct
FROM   answers a
JOIN   choices c ON c.id = a.selected_choice_id
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  a.is_correct <> c.is_correct;

\echo '== 6. points_awarded > 0 on an answer that is not correct =='
SELECT a.id AS answer_id, a.question_id, a.points_awarded, a.is_correct
FROM   answers a
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  a.is_correct = false
  AND  a.points_awarded <> 0;

\echo '== 7. Cross-session bleed: answer whose question does not belong to the game''s quiz =='
SELECT a.id AS answer_id, a.game_session_id, a.question_id
FROM   answers a
JOIN   game_sessions gs ON gs.id = a.game_session_id
JOIN   questions q ON q.id = a.question_id
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  q.quiz_id <> gs.quiz_id;

\echo '== 8. Cross-session bleed: answer.participant belongs to a different game_session =='
SELECT a.id AS answer_id, a.game_session_id AS answer_game, p.game_session_id AS participant_game
FROM   answers a
JOIN   participants p ON p.id = a.participant_id
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  p.game_session_id <> a.game_session_id;

\echo '== 9. Duplicate nickname within a game (case-insensitive, non-removed) =='
SELECT game_session_id, lower(nickname) AS nick, COUNT(*)
FROM   participants
WHERE  is_removed = false
  AND (:game IS NULL OR game_session_id = :game)
GROUP  BY game_session_id, lower(nickname)
HAVING COUNT(*) > 1;

\echo '== 10. Answer accepted well after its question''s natural deadline (start + time_limit) =='
-- Uses the natural deadline, not current_question_ends_at, because an early
-- `end-question` legitimately moves current_question_ends_at earlier. A small
-- grace covers clock jitter; anything past it is a genuinely late accept.
SELECT a.id AS answer_id, a.submitted_at, gs.current_question_started_at, q.time_limit_seconds
FROM   answers a
JOIN   game_sessions gs ON gs.id = a.game_session_id
JOIN   questions q ON q.id = a.question_id
WHERE  (:game IS NULL OR a.game_session_id = :game)
  AND  gs.current_question_id = a.question_id
  AND  gs.current_question_started_at IS NOT NULL
  AND  a.submitted_at > gs.current_question_started_at
                        + make_interval(secs => q.time_limit_seconds)
                        + interval '2 seconds';

\echo 'If every section above printed "(0 rows)", the integrity invariants held.'
