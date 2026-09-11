// REST helpers for the host-facing API (auth + quiz authoring + game control).
// Every call is tagged `scope:'api'` so the "normal API p95 < 300 ms" threshold
// can target just these. Game-control calls are tagged `scope:'api_gamectl'`.

import http from 'k6/http';
import { sleep } from 'k6';

// Setup/verification calls run from a single IP and share the global per-IP
// token bucket (240 burst + 120 / 30 s) with the load itself. A 429 here is
// transient back-pressure, not a failure of the thing under test, so retry a few
// times with a wait long enough for the bucket to refill. Measured load paths
// (hub JoinGame / SubmitAnswer) do NOT use this — their 429s must surface.
const RETRY_ON_429 = Number(__ENV.API_429_RETRIES || 6);

function api(env, method, path, body, token, tags) {
  const params = {
    headers: {
      'Content-Type': 'application/json',
      'ngrok-skip-browser-warning': 'true',
    },
    tags: Object.assign({ scope: 'api' }, tags || {}),
  };
  if (token) params.headers['Authorization'] = `Bearer ${token}`;
  const url = `${env.apiBase}${path}`;
  const payload = body === undefined || body === null ? null : JSON.stringify(body);

  let res;
  for (let attempt = 0; attempt <= RETRY_ON_429; attempt += 1) {
    res = http.request(method, url, payload, params);
    if (res.status !== 429) return res;
    sleep(8 + attempt * 4 + Math.random() * 3);
  }
  return res;
}

// Approx offset (server clock - load-generator clock), in ms, from the HTTP
// `Date` header on /health. Used to correct the question-delivery latency
// estimate when the k6 box and the API run on different clocks (e.g. Windows
// host vs a Linux container). Resolution is ~1 s (the header has no ms), and it
// ignores RTT/2, so treat it as a coarse correction, not a precise sync.
export function serverClockSkewMs(env) {
  try {
    const res = http.get(`${env.origin}/health`, {
      headers: { 'ngrok-skip-browser-warning': 'true' },
      tags: { scope: 'setup', name: 'health' },
    });
    const dateHeader = res.headers['Date'] || res.headers['date'];
    if (!dateHeader) return 0;
    const serverMs = Date.parse(dateHeader);
    if (!Number.isFinite(serverMs)) return 0;
    return serverMs - Date.now();
  } catch (_) {
    return 0;
  }
}

export function expectStatus(res, wanted, label) {
  const ok = Array.isArray(wanted) ? wanted.includes(res.status) : res.status === wanted;
  if (!ok) {
    throw new Error(
      `${label}: expected ${wanted}, got ${res.status} — ${String(res.body).slice(0, 300)}`,
    );
  }
  return res;
}

// The /auth/* endpoints are fixed-window rate limited (10 req / 5 min per IP),
// so a suite that logs in per scenario can exhaust the budget. run-all.js logs
// in ONCE and passes the token via HOST_TOKEN / HOST_ID; honour that here.
export function login(env, creds) {
  if (__ENV.HOST_TOKEN) {
    return { token: __ENV.HOST_TOKEN, hostId: __ENV.HOST_ID || null, raw: { preauthorized: true } };
  }
  let res;
  for (let attempt = 0; attempt < 3; attempt += 1) {
    res = api(env, 'POST', '/auth/login', creds, null, { name: 'auth/login' });
    if (res.status !== 429) break;
    sleep(5);
  }
  if (res.status === 429) {
    throw new Error(
      'login: auth endpoint rate-limited (429; limit is 10 / 5 min per IP). ' +
        'Pass -e HOST_TOKEN=<jwt> (see run-all.js) or wait 5 minutes before retrying.',
    );
  }
  expectStatus(res, 200, 'login');
  const body = JSON.parse(res.body);
  return { token: body.accessToken, hostId: body.hostId, raw: body };
}

export function createQuiz(env, token, title, description) {
  const res = api(env, 'POST', '/quizzes', { title, description: description || null }, token, { scope: 'setup', name: 'quizzes/create' });
  expectStatus(res, 201, 'createQuiz');
  return JSON.parse(res.body).id;
}

// question: { text, timeLimitSeconds, points, choices:[{text,isCorrect}] }
export function addQuestion(env, token, quizId, question) {
  const res = api(env, 'POST', `/quizzes/${quizId}/questions`, question, token, { scope: 'setup', name: 'quizzes/addQuestion' });
  expectStatus(res, 201, 'addQuestion');
  return JSON.parse(res.body).id;
}

export function publishQuiz(env, token, quizId) {
  const res = api(env, 'POST', `/quizzes/${quizId}/publish`, null, token, { scope: 'setup', name: 'quizzes/publish' });
  expectStatus(res, 204, 'publishQuiz');
}

export function getQuiz(env, token, quizId) {
  const res = api(env, 'GET', `/quizzes/${quizId}`, null, token, { scope: 'setup', name: 'quizzes/get' });
  expectStatus(res, 200, 'getQuiz');
  return JSON.parse(res.body);
}

export function createGame(env, token, quizId) {
  const res = api(env, 'POST', '/games', { quizId }, token, { scope: 'setup', name: 'games/create' });
  expectStatus(res, 201, 'createGame');
  const body = JSON.parse(res.body);
  return { gameId: body.gameId, pin: body.pin, status: body.status };
}

export function getHostState(env, token, gameId) {
  const res = api(env, 'GET', `/games/${gameId}`, null, token, { name: 'games/state' });
  expectStatus(res, 200, 'getHostState');
  return JSON.parse(res.body);
}

export function getQuestionResults(env, token, gameId, questionId) {
  const res = api(env, 'GET', `/games/${gameId}/questions/${questionId}/results`, null, token, {
    name: 'games/results',
  });
  expectStatus(res, 200, 'getQuestionResults');
  return JSON.parse(res.body);
}

export function getLeaderboard(env, token, gameId) {
  const res = api(env, 'GET', `/games/${gameId}/leaderboard`, null, token, {
    name: 'games/leaderboard',
  });
  expectStatus(res, 200, 'getLeaderboard');
  return JSON.parse(res.body);
}

function gameControl(env, token, gameId, action, okStatus) {
  const res = api(env, 'POST', `/games/${gameId}/${action}`, null, token, {
    scope: 'api_gamectl',
    name: `games/${action}`,
  });
  expectStatus(res, okStatus || 200, `game:${action}`);
  return res.body ? safeJson(res.body) : null;
}

export const startGame = (env, token, gameId) => gameControl(env, token, gameId, 'start', 200);
export const advance = (env, token, gameId) => gameControl(env, token, gameId, 'advance', 200);
export const endQuestion = (env, token, gameId) => gameControl(env, token, gameId, 'end-question', 200);
export const showLeaderboard = (env, token, gameId) => gameControl(env, token, gameId, 'leaderboard', 200);
export const endGame = (env, token, gameId) => gameControl(env, token, gameId, 'end', 200);

export function removeParticipant(env, token, gameId, participantId) {
  const res = api(env, 'DELETE', `/games/${gameId}/participants/${participantId}`, null, token, {
    scope: 'api_gamectl',
    name: 'games/removeParticipant',
  });
  expectStatus(res, [204, 404], 'removeParticipant');
  return res.status;
}

// REST join (anonymous, rate-limited). The hub `JoinGame` is preferred for load;
// this exists for the REST-path tests.
export function restJoin(env, pin, nickname) {
  const res = http.post(`${env.apiBase}/games/join`, JSON.stringify({ pin, nickname }), {
    headers: {
      'Content-Type': 'application/json',
      'ngrok-skip-browser-warning': 'true',
    },
    tags: { scope: 'api_join', name: 'games/join' },
  });
  return res;
}

function safeJson(body) {
  try {
    return JSON.parse(body);
  } catch (_) {
    return null;
  }
}
