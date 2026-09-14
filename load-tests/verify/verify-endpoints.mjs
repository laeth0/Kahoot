#!/usr/bin/env node
/*
 * Pre-flight verification script for load-test targets.
 * Validates REST API endpoints, host credentials, SignalR negotiate, and WebSocket handshake.
 *
 * Usage:
 *   node load-tests/verify/verify-endpoints.mjs
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const ENV_FILE = path.join(ROOT, '.env');

function loadDotenv() {
  if (!fs.existsSync(ENV_FILE)) return;
  for (const line of fs.readFileSync(ENV_FILE, 'utf8').split(/\r?\n/)) {
    const m = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/.exec(line);
    if (!m || line.trim().startsWith('#')) continue;
    let v = m[2];
    if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
      v = v.slice(1, -1);
    }
    if (process.env[m[1]] === undefined) process.env[m[1]] = v;
  }
}

loadDotenv();

const targetUrl = (process.env.TARGET_URL || '').replace(/\/+$/, '');
const rawBase = (process.env.BASE_URL || (targetUrl ? `${targetUrl}/api` : 'http://localhost:5048/api')).replace(/\/+$/, '');
const apiBase = /\/api$/.test(rawBase) ? rawBase : `${rawBase}/api`;
const origin = apiBase.replace(/\/api$/, '');
const signalr = (process.env.SIGNALR_URL || (targetUrl ? targetUrl : origin)).replace(/\/+$/, '');
const hubPath = process.env.HUB_PATH || '/hubs/game';

const host = origin.replace(/^https?:\/\//, '').split('/')[0].split(':')[0];
const isLocal = ['localhost', '127.0.0.1', '::1', '0.0.0.0'].includes(host);
const requireProd = process.env.TARGET_PRODUCTION_ONLY === 'true' || process.env.REQUIRE_PROD === 'true';

const hostUsername = process.env.HOST_USERNAME || 'IEEEXtreme Section';
const hostPassword = process.env.HOST_PASSWORD || 'IEEEXtreme@123456789';

console.log('== Pre-flight Target Verification ==');
console.log(`  Origin:       ${origin}`);
console.log(`  API Base:     ${apiBase}`);
console.log(`  SignalR:      ${signalr}${hubPath}`);
console.log(`  Target Host:  ${host} (isLocal=${isLocal})`);
console.log(`  Prod Only:    ${requireProd}`);
console.log('');

if (requireProd && isLocal) {
  console.error(`[FAIL] TARGET_PRODUCTION_ONLY is active, but resolved target is local: ${origin}`);
  process.exit(1);
}

let passed = true;

async function checkStep(name, fn) {
  process.stdout.write(`  [..] ${name}... `);
  const start = Date.now();
  try {
    const detail = await fn();
    const elapsed = Date.now() - start;
    console.log(`PASS (${elapsed}ms)${detail ? ` - ${detail}` : ''}`);
    return true;
  } catch (err) {
    const elapsed = Date.now() - start;
    console.log(`FAIL (${elapsed}ms)`);
    console.error(`       Error: ${err.message}`);
    passed = false;
    return false;
  }
}

// 1. Health check
await checkStep('GET /health', async () => {
  const res = await fetch(`${origin}/health`, { signal: AbortSignal.timeout(5000) });
  if (!res.ok) throw new Error(`HTTP status ${res.status}`);
  const text = (await res.text()).trim();
  if (!/healthy/i.test(text)) throw new Error(`Unexpected body: ${text}`);
  return text;
});

// 2. Host login
let authToken = null;
await checkStep('POST /api/auth/login', async () => {
  const res = await fetch(`${apiBase}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: hostUsername, password: hostPassword }),
    signal: AbortSignal.timeout(5000),
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    throw new Error(`HTTP status ${res.status}: ${body.slice(0, 200)}`);
  }
  const data = await res.json();
  if (!data.accessToken) throw new Error('Missing accessToken in response');
  authToken = data.accessToken;
  return `Host ID: ${data.hostId}`;
});

// 3. Authenticated Quizzes API
await checkStep('GET /api/quizzes (Authorized)', async () => {
  if (!authToken) throw new Error('Skipped (no auth token)');
  const res = await fetch(`${apiBase}/quizzes`, {
    headers: { Authorization: `Bearer ${authToken}` },
    signal: AbortSignal.timeout(5000),
  });
  if (!res.ok) throw new Error(`HTTP status ${res.status}`);
  const list = await res.json();
  return `${Array.isArray(list) ? list.length : 0} quizzes found`;
});

// 4. SignalR Negotiate
await checkStep('POST /hubs/game/negotiate', async () => {
  const url = `${signalr}${hubPath}/negotiate?negotiateVersion=1`;
  const res = await fetch(url, {
    method: 'POST',
    signal: AbortSignal.timeout(5000),
  });
  if (!res.ok) throw new Error(`HTTP status ${res.status}`);
  const data = await res.json();
  if (!data.connectionToken) throw new Error('Missing connectionToken in negotiate response');
  const hasWs = data.availableTransports?.some((t) => t.transport === 'WebSockets');
  if (!hasWs) throw new Error('WebSockets transport not listed in availableTransports');
  return `Token: ${data.connectionToken.slice(0, 8)}..., WebSockets available`;
});

// 5. Native SignalR WebSocket Handshake
await checkStep('WebSocket Handshake (SignalR JSON protocol)', async () => {
  const wsUrl = signalr.replace(/^http:/i, 'ws:').replace(/^https:/i, 'wss:') + hubPath;
  const RS = String.fromCharCode(30);

  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      try { ws.close(); } catch (_) {}
      reject(new Error('WebSocket handshake timed out (5s)'));
    }, 5000);

    const ws = new WebSocket(wsUrl);

    ws.onopen = () => {
      ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS);
    };

    ws.onmessage = (event) => {
      clearTimeout(timeout);
      const msg = typeof event.data === 'string' ? event.data : event.data.toString();
      try { ws.close(); } catch (_) {}
      if (msg.includes('{}')) {
        resolve('Handshake acknowledged ({})');
      } else {
        reject(new Error(`Unexpected handshake response: ${msg.slice(0, 100)}`));
      }
    };

    ws.onerror = (event) => {
      clearTimeout(timeout);
      reject(new Error(`WebSocket connection error: ${event.message || 'unknown'}`));
    };
  });
});

console.log('');
if (passed) {
  console.log('>> All pre-flight checks PASSED. Target is ready for load testing.');
  process.exit(0);
} else {
  console.error('>> One or more pre-flight checks FAILED.');
  process.exit(1);
}
