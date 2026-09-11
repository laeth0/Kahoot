#!/usr/bin/env node
/*
 * Sequential runner for the k6 load-test suite.
 *
 *   node load-tests/run-all.js [scenario ...] [-- passthrough k6 args]
 *
 * With no scenario names it runs the acceptance set:
 *   connections join-game question-broadcast answer-burst duplicate-answer
 *   reconnection multiple-games ramp
 * (endurance and reconnection-storm are opt-in: name them explicitly.)
 *
 * Config comes from the environment and, if present, load-tests/.env
 * (KEY=VALUE lines). Heavy scenarios still require ALLOW_LOAD_TEST=true — this
 * script refuses to start them otherwise, mirroring the in-script safety gate.
 *
 * Each scenario writes results/<name>-summary.json; this script parses those
 * plus k6's exit code and prints the final PASS/FAIL table.
 */

'use strict';

const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const ROOT = __dirname;
const RESULTS = path.join(ROOT, 'results');
const SCENARIO_DIR = path.join(ROOT, 'scenarios');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// `connections` runs LAST: it is pure connection-establishment traffic and, from
// a single IP, it drains the global per-IP rate-limit bucket for a while, which
// would starve the setup() of whatever scenario ran next.
const ACCEPTANCE = [
  'join-game',
  'question-broadcast',
  'answer-burst',
  'duplicate-answer',
  'reconnection',
  'multiple-games',
  'ramp',
  'connections',
];
const ALL = ACCEPTANCE.concat(['reconnection-storm', 'endurance']);

// Seconds to idle between scenarios so the per-IP token bucket refills before the
// next setup() runs. Read inside main() (after loadDotenv()).
function cooldownSeconds() {
  return Number(process.env.SCENARIO_COOLDOWN || 60);
}

const LABEL = {
  connections: '500 connections',
  'join-game': '500-player join',
  'question-broadcast': 'Question broadcast',
  'answer-burst': '500-answer burst',
  'duplicate-answer': 'Duplicate-answer protection',
  reconnection: '100-player reconnect',
  'multiple-games': '10 x 50 game isolation',
  ramp: '750-user stress test',
  'reconnection-storm': 'Reconnection storm',
  endurance: 'Endurance / soak',
};

function loadDotenv() {
  const f = path.join(ROOT, '.env');
  if (!fs.existsSync(f)) return;
  for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
    const m = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/.exec(line);
    if (!m || line.trim().startsWith('#')) continue;
    let v = m[2];
    if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
      v = v.slice(1, -1);
    }
    if (process.env[m[1]] === undefined) process.env[m[1]] = v;
  }
}

function parseArgs(argv) {
  const scenarios = [];
  const passthrough = [];
  let sawSep = false;
  for (const a of argv) {
    if (a === '--') {
      sawSep = true;
      continue;
    }
    if (sawSep) passthrough.push(a);
    else if (a === 'all') scenarios.push(...ALL);
    else scenarios.push(a);
  }
  return { scenarios: scenarios.length ? scenarios : ACCEPTANCE, passthrough };
}

function k6Bin() {
  return process.env.K6_BIN || 'k6';
}

function runScenario(name, passthrough) {
  const script = path.join(SCENARIO_DIR, `${name}.js`);
  if (!fs.existsSync(script)) {
    return { name, status: 'MISSING', exit: null, summary: null };
  }
  const summaryPath = path.join(RESULTS, `${name}-summary.json`);
  try {
    fs.rmSync(summaryPath, { force: true });
  } catch (_) {
    /* ignore */
  }

  const forwardKeys = [
    'BASE_URL',
    'SIGNALR_URL',
    'HUB_PATH',
    'HOST_USERNAME',
    'HOST_PASSWORD',
    'HOST_TOKEN',
    'HOST_ID',
    'ALLOW_LOAD_TEST',
    'ALLOW_PROD_LOAD_TEST',
    'SAFE_VU_LIMIT',
    'PLAYERS',
    'GAMES',
    'PLAYERS_PER_GAME',
    'RECONNECT_PLAYERS',
    'JOIN_RAMP',
    'CONNECT_RAMP',
    'HOLD_SECONDS',
    'ANSWER_TIME_LIMIT',
    'ENDURANCE_MINUTES',
    'ENDURANCE_PLAYERS',
    'SETTLE_SECONDS',
    'CONFIRM_FRACTION',
    'SIGNALR_SKIP_NEGOTIATION',
    'SIGNALR_CONNECT_RETRIES',
    'API_429_RETRIES',
  ];
  const envFlags = [];
  for (const k of forwardKeys) {
    if (process.env[k] !== undefined) {
      envFlags.push('-e', `${k}=${process.env[k]}`);
    }
  }

  const args = ['run', '--summary-mode', 'compact', '-e', `SUMMARY_DIR=${RESULTS}`, ...envFlags, script, ...passthrough];
  console.log(`\n─── ${name}  (k6 run ... ${name}.js)\n`);
  const res = spawnSync(k6Bin(), args, { stdio: 'inherit', cwd: ROOT, env: process.env });

  let summary = null;
  if (fs.existsSync(summaryPath)) {
    try {
      summary = JSON.parse(fs.readFileSync(summaryPath, 'utf8'));
    } catch (_) {
      /* ignore */
    }
  }
  // k6 exit codes: 0 ok, 99 threshold failed, 10x setup/other error, null = spawn fail
  let status = 'PASS';
  if (res.status === null) status = 'ERROR (k6 not found?)';
  else if (res.status === 99) status = 'FAIL (threshold)';
  else if (res.status !== 0) status = `FAIL (exit ${res.status})`;
  return { name, status, exit: res.status, summary };
}

function metric(summary, name, field, dflt) {
  const m = summary && summary.metrics && summary.metrics[name];
  if (!m || !m.values || m.values[field] === undefined) return dflt;
  return m.values[field];
}

function failedThresholds(summary) {
  const out = [];
  const metrics = (summary && summary.metrics) || {};
  for (const mName of Object.keys(metrics)) {
    const t = metrics[mName].thresholds;
    if (!t) continue;
    for (const expr of Object.keys(t)) {
      if (t[expr] && t[expr].ok === false) out.push(`${mName}: ${expr}`);
    }
  }
  return out;
}

function apiBase() {
  const raw = (process.env.BASE_URL || 'http://localhost:5048/api').replace(/\/+$/, '');
  return /\/api$/.test(raw) ? raw : `${raw}/api`;
}

// One login for the whole suite, then /auth/refresh when the 15-min access token
// nears expiry. Keeps total /auth/* calls well under the 10-per-5-min limit that
// a per-scenario login would blow. Returns { token, hostId } or null on failure
// (scenarios then fall back to their own login()).
async function makeTokenProvider() {
  const base = apiBase();
  const creds = {
    username: process.env.HOST_USERNAME || 'admin',
    password: process.env.HOST_PASSWORD || 'admin',
  };
  let state = null; // { access, refresh, issuedAt }

  async function post(path, body) {
    const r = await fetch(`${base}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'ngrok-skip-browser-warning': 'true',
      },
      body: JSON.stringify(body),
    });
    return { status: r.status, body: r.status === 204 ? {} : await r.json().catch(() => ({})) };
  }

  return async function getToken() {
    const ageMs = state ? Date.now() - state.issuedAt : Infinity;
    try {
      if (!state) {
        const r = await post('/auth/login', creds);
        if (r.status !== 200) throw new Error(`login ${r.status}`);
        state = { access: r.body.accessToken, refresh: r.body.refreshToken, issuedAt: Date.now(), hostId: r.body.hostId };
      } else if (ageMs > 11 * 60 * 1000) {
        const r = await post('/auth/refresh', { refreshToken: state.refresh });
        if (r.status !== 200) throw new Error(`refresh ${r.status}`);
        state = { access: r.body.accessToken, refresh: r.body.refreshToken, issuedAt: Date.now(), hostId: state.hostId };
      }
      return { token: state.access, hostId: state.hostId };
    } catch (e) {
      console.warn(`[run-all] pre-auth failed (${e.message}); scenarios will log in themselves.`);
      return null;
    }
  };
}

async function main() {
  loadDotenv();
  const { scenarios, passthrough } = parseArgs(process.argv.slice(2));

  const heavy = scenarios.some((s) => s !== 'duplicate-answer'); // essentially all
  if (heavy && process.env.ALLOW_LOAD_TEST !== 'true') {
    console.error(
      'Refusing to run the load-test suite without ALLOW_LOAD_TEST=true.\n' +
        'Set it in load-tests/.env or the environment once you have confirmed the target.\n' +
        `Target: BASE_URL=${process.env.BASE_URL || '(default http://localhost:5048/api)'}`,
    );
    process.exit(2);
  }

  fs.mkdirSync(RESULTS, { recursive: true });
  const getToken = await makeTokenProvider();

  const results = [];
  for (let i = 0; i < scenarios.length; i += 1) {
    const name = scenarios[i];
    const cd = cooldownSeconds();
    if (i > 0 && cd > 0) {
      console.log(`\n... cooldown ${cd}s (let the per-IP rate-limit bucket refill) ...`);
      await sleep(cd * 1000);
    }
    if (getToken) {
      const t = await getToken();
      if (t && t.token) {
        process.env.HOST_TOKEN = t.token;
        if (t.hostId) process.env.HOST_ID = t.hostId;
      }
    }
    results.push(runScenario(name, passthrough));
  }

  // ---- Final report ------------------------------------------------------
  const pad = (s, n) => String(s).padEnd(n);
  console.log('\n\n==================  ACCEPTANCE REPORT  ==================\n');
  console.log(pad('Scenario', 30) + pad('Result', 22) + 'Key numbers');
  console.log('-'.repeat(90));
  for (const r of results) {
    const s = r.summary;
    const bits = [];
    if (s) {
      const conns = metric(s, 'signalr_connections', 'count', 0);
      const connFail = metric(s, 'signalr_connection_failures', 'count', 0);
      const accepted = metric(s, 'answers_accepted', 'count', 0);
      const p95 = metric(s, 'answer_submission_duration', 'p(95)');
      const dupA = metric(s, 'duplicate_answer_violations', 'count', 0);
      const dupS = metric(s, 'duplicate_score_violations', 'count', 0);
      const lost = metric(s, 'lost_accepted_answers', 'count', 0);
      const iso = metric(s, 'session_isolation_violations', 'count', 0);
      if (conns) bits.push(`conns=${conns}/${conns + connFail}`);
      if (accepted) bits.push(`accepted=${accepted}`);
      if (p95 !== undefined) bits.push(`ans_p95=${Math.round(p95)}ms`);
      const inv = lost + dupA + dupS + iso;
      bits.push(`invariants_broken=${inv}`);
    }
    console.log(pad(LABEL[r.name] || r.name, 30) + pad(r.status, 22) + bits.join('  '));
    for (const f of failedThresholds(r.summary)) console.log(pad('', 30) + '  ! ' + f);
  }
  console.log('\nPer-scenario JSON: load-tests/results/<scenario>-summary.json');

  const anyFail = results.some((r) => r.status !== 'PASS');
  process.exit(anyFail ? 1 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(2);
});
