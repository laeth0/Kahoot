import { Trend, Rate, Counter, Gauge } from 'k6/metrics';

// --- SignalR connection lifecycle ----------------------------------------------
export const signalrConnections = new Counter('signalr_connections');
export const signalrConnectionFailures = new Counter('signalr_connection_failures');
export const signalrConnectionSuccessRate = new Rate('signalr_connection_success_rate');
export const signalrConnectionDuration = new Trend('signalr_connection_duration', true);
export const signalrHandshakeDuration = new Trend('signalr_handshake_duration', true);
export const signalrUnexpectedDisconnects = new Counter('signalr_unexpected_disconnects');
export const signalrConnectionsActive = new Gauge('signalr_connections_active');

// --- Player join --------------------------------------------------------------
export const playerJoinDuration = new Trend('player_join_duration', true);
export const playerJoinFailures = new Counter('player_join_failures');
export const playersJoined = new Counter('players_joined');
export const duplicateParticipants = new Counter('duplicate_participants');

// --- Question broadcast ------------------------------------------------------
export const questionDeliveryDuration = new Trend('question_delivery_duration', true);
export const questionDeliveryFailures = new Counter('question_delivery_failures');
export const questionDelivered = new Counter('question_delivered');

// --- Answer submission ------------------------------------------------------
export const answerSubmissionDuration = new Trend('answer_submission_duration', true);
export const answersSubmitted = new Counter('answers_submitted');
export const answersAccepted = new Counter('answers_accepted');
export const answersRejected = new Counter('answers_rejected');
export const unexpectedAnswerFailures = new Counter('unexpected_answer_failures');

// --- Correctness invariants (every one of these MUST stay 0) ----------------
export const duplicateAnswerViolations = new Counter('duplicate_answer_violations');
export const duplicateScoreViolations = new Counter('duplicate_score_violations');
export const lostAcceptedAnswers = new Counter('lost_accepted_answers');
export const inconsistentGameState = new Counter('inconsistent_game_state');
export const sessionIsolationViolations = new Counter('session_isolation_violations');

// --- Reconnection ---------------------------------------------------------
export const reconnectionDuration = new Trend('reconnection_duration', true);
export const reconnectionFailures = new Counter('reconnection_failures');

// --- Catch-all --------------------------------------------------------------
export const unexpectedErrors = new Counter('unexpected_errors');
export const unexpectedErrorRate = new Rate('unexpected_error_rate');

// Record an unexpected failure once, consistently, everywhere.
export function bumpUnexpected(labelTag, extraTags) {
  const tags = Object.assign({}, extraTags, labelTag ? { where: labelTag } : undefined);
  unexpectedErrors.add(1, Object.keys(tags).length ? tags : undefined);
  unexpectedErrorRate.add(true, Object.keys(tags).length ? tags : undefined);
}
export function noUnexpected(extraTags) {
  unexpectedErrorRate.add(false, extraTags && Object.keys(extraTags).length ? extraTags : undefined);
}

// A player arriving after the host already started the question gets
// Game.NotJoinable — that is expected attrition on a ramped join, not a defect.
const BENIGN_JOIN_CODES = ['Game.NotJoinable'];
export function recordJoinFailure(code, where, extraTags) {
  playerJoinFailures.add(1, Object.assign({ reason: code || 'unknown' }, extraTags));
  if (!BENIGN_JOIN_CODES.includes(code)) bumpUnexpected(where || `join:${code}`, extraTags);
}
