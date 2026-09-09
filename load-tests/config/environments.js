import { fail } from 'k6';

// Resolves the target from BASE_URL / SIGNALR_URL. Defaults to the local dev API
// (http://localhost:5048). Nothing here is environment-specific in source — all
// URLs come from `-e` flags / the process environment (NFR-9).
export function resolveEnv() {
  const rawBase = (__ENV.BASE_URL || 'http://localhost:5048/api').replace(/\/+$/, '');
  const apiBase = /\/api$/.test(rawBase) ? rawBase : `${rawBase}/api`;
  const origin = apiBase.replace(/\/api$/, '');
  const signalr = (__ENV.SIGNALR_URL || origin).replace(/\/+$/, '');
  const host = origin.replace(/^https?:\/\//, '').split('/')[0].split(':')[0];
  const isLocal = ['localhost', '127.0.0.1', '::1', '0.0.0.0'].includes(host);
  const looksProd = /prod|production|railway\.app/i.test(`${apiBase} ${signalr}`) && !isLocal;

  return {
    apiBase,
    origin,
    signalr,
    hubPath: __ENV.HUB_PATH || '/hubs/game',
    host,
    isLocal,
    looksProd,
  };
}

// High-load scenarios call this from setup(). It aborts the whole test (fail()
// in setup) unless the operator has explicitly opted in. This is the
// "don't accidentally nuke production" gate the brief asks for.
export function assertLoadAllowed(env, peakVus) {
  const limit = Number(__ENV.SAFE_VU_LIMIT || 50);
  if (peakVus <= limit) return;

  if (env.looksProd && __ENV.ALLOW_PROD_LOAD_TEST !== 'true') {
    fail(
      `Refusing a ${peakVus}-VU load test against a production-looking target (${env.apiBase}). ` +
        `If this really is a throw-away load environment, set -e ALLOW_PROD_LOAD_TEST=true.`,
    );
  }
  if (__ENV.ALLOW_LOAD_TEST !== 'true') {
    fail(
      `High-load scenario blocked (peak ${peakVus} VUs > SAFE_VU_LIMIT ${limit}). ` +
        `Re-run with -e ALLOW_LOAD_TEST=true after confirming ${env.apiBase} is a load-test target.`,
    );
  }
}

export function hostCredentials() {
  return {
    username: __ENV.HOST_USERNAME || 'admin',
    password: __ENV.HOST_PASSWORD || 'admin',
  };
}

export function intEnv(name, dflt) {
  const v = Number(__ENV[name]);
  return Number.isFinite(v) && v > 0 ? Math.floor(v) : dflt;
}
