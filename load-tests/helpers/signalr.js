// Minimal SignalR client for k6, speaking the real protocol the app uses:
//   negotiate (HTTP) -> WebSocket upgrade -> JSON handshake -> invocations.
//
// The app's browser client is `@microsoft/signalr` with the default JSON hub
// protocol and WebSockets transport. This helper reproduces that wire format so
// the load tests exercise the actual hub, not a fake raw socket:
//   * POST {hub}/negotiate?negotiateVersion=1   (unless SIGNALR_SKIP_NEGOTIATION)
//   * ws(s) upgrade to {hub}?id={connectionToken}[&access_token=...]
//   * send  {"protocol":"json","version":1}<RS>   and wait for {}<RS>
//   * invocation   {"type":1,"invocationId":"N","target":..,"arguments":[..]}<RS>
//   * completion   {"type":3,"invocationId":"N","result"|"error":..}<RS>
//   * server event {"type":1,"target":..,"arguments":[..]}<RS>   (no invocationId)
//   * ping         {"type":6}<RS>   (both directions, keep-alive)
//   * close        {"type":7,...}
//
// It is promise-based and yields to the k6 event loop, so a VU can hold a live
// connection open and still receive server pushes (needed for the broadcast and
// answer-burst scenarios). Do NOT use k6's blocking `sleep()` while a client is
// open — use `delay()` / `waitFor()` from this module instead.

import http from 'k6/http';
// k6/websockets is the stable module (k6 >= v1.x); k6/experimental/websockets is
// the deprecated alias. Same browser-like WebSocket API either way.
import { WebSocket } from 'k6/websockets';
import {
  signalrConnections,
  signalrConnectionFailures,
  signalrConnectionSuccessRate,
  signalrConnectionDuration,
  signalrHandshakeDuration,
} from './metrics.js';

const RS = String.fromCharCode(30); // ASCII record separator that frames every SignalR message

export function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Polls `predicate` until it returns truthy or `timeoutMs` elapses.
export async function waitFor(predicate, { timeoutMs = 15000, intervalMs = 100 } = {}) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const result = predicate();
    if (result) return result;
    if (Date.now() >= deadline) return null;
    await delay(intervalMs);
  }
}

function toWsUrl(httpUrl) {
  return httpUrl.replace(/^http:/i, 'ws:').replace(/^https:/i, 'wss:');
}

export class SignalRClient {
  constructor(env, opts = {}) {
    this.origin = env.signalr.replace(/\/+$/, '');
    this.hubPath = env.hubPath || '/hubs/game';
    this.accessToken = opts.accessToken || null;
    this.skipNegotiation =
      opts.skipNegotiation !== undefined
        ? opts.skipNegotiation
        : __ENV.SIGNALR_SKIP_NEGOTIATION === 'true';
    this.connectRetries =
      opts.connectRetries !== undefined
        ? opts.connectRetries
        : Number(__ENV.SIGNALR_CONNECT_RETRIES || 4);
    this.handshakeTimeoutMs = opts.handshakeTimeoutMs || 20000;

    this.handlers = {}; // target -> [fn]
    this.pending = {}; // invocationId -> {resolve, reject}
    this.nextId = 0;
    this.ws = null;
    this.connected = false;
    this.closed = false;
    this.closeInfo = null; // populated on unexpected close
    this._rx = '';
    this._pingTimer = null;
    this._onCloseCb = opts.onClose || null;
  }

  on(target, fn) {
    (this.handlers[target] || (this.handlers[target] = [])).push(fn);
  }

  // Establishes negotiate + socket + handshake, retrying on transient rejects
  // (HTTP 429 from the per-IP rate limiter, upgrade failures). Records the
  // connection lifecycle metrics. Resolves with `this` once handshaken.
  async start() {
    const connectionStartTimeMs = Date.now();
    let lastConnectionError = null;
    for (let attempt = 0; attempt <= this.connectRetries; attempt++) {
      if (attempt > 0) {
        // The per-IP global limiter refills ~4 tokens/s (120 / 30 s). A 429 means
        // wait for a real refill, not a few hundred ms — with jitter so 500 VUs
        // don't retry in lockstep. Other errors get a short backoff.
        const rateLimited = /429|rate-limited/i.test(String(lastConnectionError));
        const backoffMs = rateLimited
          ? 6000 + attempt * 4000 + Math.random() * 5000
          : 400 * attempt + Math.random() * 400;
        await delay(backoffMs);
      }
      try {
        const wsUrl = this.skipNegotiation ? this._directUrl() : this._negotiate();
        await this._openAndHandshake(wsUrl, connectionStartTimeMs);
        signalrConnections.add(1);
        signalrConnectionSuccessRate.add(true);
        signalrConnectionDuration.add(Date.now() - connectionStartTimeMs);
        return this;
      } catch (connectionError) {
        lastConnectionError = connectionError;
        this._teardownSocket();
      }
    }
    signalrConnectionFailures.add(1);
    signalrConnectionSuccessRate.add(false);
    throw new Error(`SignalR connect failed after ${this.connectRetries + 1} attempts: ${lastConnectionError}`);
  }

  _directUrl() {
    let directWsUrl = toWsUrl(`${this.origin}${this.hubPath}`);
    if (this.accessToken) directWsUrl += `?access_token=${encodeURIComponent(this.accessToken)}`;
    return directWsUrl;
  }

  _negotiate() {
    const negotiateEndpointUrl = `${this.origin}${this.hubPath}/negotiate?negotiateVersion=1`;
    const headers = {
      'Content-Type': 'text/plain;charset=UTF-8',
      'ngrok-skip-browser-warning': 'true',
    };
    if (this.accessToken) headers['Authorization'] = `Bearer ${this.accessToken}`;
    const negotiateResponse = http.post(negotiateEndpointUrl, null, { headers, tags: { scope: 'signalr_negotiate' } });
    if (negotiateResponse.status === 429) throw new Error('negotiate rate-limited (429)');
    if (negotiateResponse.status !== 200) throw new Error(`negotiate status ${negotiateResponse.status}: ${String(negotiateResponse.body).slice(0, 200)}`);

    let negotiatePayload;
    try {
      negotiatePayload = JSON.parse(negotiateResponse.body);
    } catch (_) {
      throw new Error(`negotiate body not JSON: ${String(negotiateResponse.body).slice(0, 120)}`);
    }
    if (negotiatePayload.error) throw new Error(`negotiate error: ${negotiatePayload.error}`);
    if (negotiatePayload.url) {
      // redirect response — follow it once
      if (negotiatePayload.accessToken) this.accessToken = negotiatePayload.accessToken;
      let redirectWsUrl = toWsUrl(negotiatePayload.url);
      if (this.accessToken) redirectWsUrl += `${redirectWsUrl.includes('?') ? '&' : '?'}access_token=${encodeURIComponent(this.accessToken)}`;
      return redirectWsUrl;
    }
    const connectionToken = negotiatePayload.connectionToken || negotiatePayload.connectionId;
    let targetWsUrl = `${toWsUrl(`${this.origin}${this.hubPath}`)}?id=${encodeURIComponent(connectionToken)}`;
    if (this.accessToken) targetWsUrl += `&access_token=${encodeURIComponent(this.accessToken)}`;
    return targetWsUrl;
  }

  _openAndHandshake(wsUrl, handshakeStartTimeMs) {
    return new Promise((resolve, reject) => {
      let settled = false;
      const settleHandshake = (resolveOrRejectFn, errorOrResult) => {
        if (settled) return;
        settled = true;
        resolveOrRejectFn(errorOrResult);
      };

      const ws = new WebSocket(wsUrl);
      this.ws = ws;
      ws.binaryType = 'arraybuffer';
      this._handshakeResolve = () => {
        signalrHandshakeDuration.add(Date.now() - handshakeStartTimeMs);
        settleHandshake(resolve, this);
      };
      this._handshakeReject = (handshakeError) => settleHandshake(reject, handshakeError);

      const timeout = setTimeout(() => {
        this._handshakeReject(new Error('handshake timeout'));
        this._teardownSocket();
      }, this.handshakeTimeoutMs);
      this._handshakeTimeout = timeout;

      ws.addEventListener('open', () => {
        try {
          ws.send(`{"protocol":"json","version":1}${RS}`);
        } catch (e) {
          this._handshakeReject(new Error(`handshake send failed: ${e}`));
        }
      });
      ws.addEventListener('message', (ev) => this._onMessage(ev));
      ws.addEventListener('error', (ev) => {
        const msg = ev && (ev.error || ev.message) ? ev.error || ev.message : 'ws error';
        if (!this.connected) this._handshakeReject(new Error(String(msg)));
        else {
          this.closeInfo = String(msg);
          this._failPending(new Error(String(msg)));
        }
      });
      ws.addEventListener('close', () => {
        clearTimeout(this._handshakeTimeout);
        this._stopPing();
        if (!this.connected) this._handshakeReject(new Error('socket closed before handshake'));
        const wasClean = this.closed;
        this.closed = true;
        this._failPending(new Error('connection closed'));
        if (!wasClean && this._onCloseCb) {
          try {
            this._onCloseCb(this.closeInfo);
          } catch (_) {
            /* ignore */
          }
        }
      });
    });
  }

  _onMessage(ev) {
    let text;
    if (typeof ev.data === 'string') text = ev.data;
    else text = bytesToString(ev.data);
    this._rx += text;

    let sep;
    while ((sep = this._rx.indexOf(RS)) >= 0) {
      const raw = this._rx.slice(0, sep);
      this._rx = this._rx.slice(sep + 1);
      if (!raw) continue;
      let msg;
      try {
        msg = JSON.parse(raw);
      } catch (_) {
        continue;
      }
      this._dispatch(msg);
    }
  }

  _dispatch(msg) {
    // First frame after the handshake request has no `type`: `{}` or `{"error":..}`.
    if (!this.connected && msg.type === undefined) {
      clearTimeout(this._handshakeTimeout);
      if (msg.error) {
        this._handshakeReject(new Error(`handshake rejected: ${msg.error}`));
        return;
      }
      this.connected = true;
      this._startPing();
      this._handshakeResolve();
      return;
    }

    switch (msg.type) {
      case 1: {
        // server -> client event (target invocation, no invocationId)
        const fns = this.handlers[msg.target];
        if (fns) for (const fn of fns) safe(fn, msg.arguments || []);
        break;
      }
      case 3: {
        // completion of one of our invocations
        const p = this.pending[msg.invocationId];
        if (!p) break;
        delete this.pending[msg.invocationId];
        if (msg.error) p.reject(new Error(msg.error));
        else p.resolve(msg.result);
        break;
      }
      case 6:
        break; // server keep-alive ping — nothing to do
      case 7: // close
        this.closeInfo = msg.error || 'server close frame';
        try {
          this.ws.close();
        } catch (_) {
          /* ignore */
        }
        break;
      default:
        break;
    }
  }

  // Invoke a hub method that returns a value. Resolves with the raw result
  // (for this app: a RealtimeResponse<T> envelope `{ success, data, error }`).
  invoke(target, ...args) {
    if (!this.connected || this.closed) return Promise.reject(new Error('not connected'));
    const id = String(this.nextId++);
    const frame = `${JSON.stringify({ type: 1, invocationId: id, target, arguments: args })}${RS}`;
    return new Promise((resolve, reject) => {
      this.pending[id] = { resolve, reject };
      try {
        this.ws.send(frame);
      } catch (e) {
        delete this.pending[id];
        reject(e);
      }
    });
  }

  close() {
    this._stopPing();
    this.closed = true;
    clearTimeout(this._handshakeTimeout);
    try {
      if (this.ws) this.ws.close();
    } catch (_) {
      /* ignore */
    }
  }

  _startPing() {
    // Server ClientTimeoutInterval is 30 s; ping well inside it.
    this._pingTimer = setInterval(() => {
      try {
        this.ws.send(`{"type":6}${RS}`);
      } catch (_) {
        this._stopPing();
      }
    }, 10000);
  }

  _stopPing() {
    if (this._pingTimer) {
      clearInterval(this._pingTimer);
      this._pingTimer = null;
    }
  }

  _failPending(err) {
    for (const id of Object.keys(this.pending)) {
      try {
        this.pending[id].reject(err);
      } catch (_) {
        /* ignore */
      }
      delete this.pending[id];
    }
  }

  _teardownSocket() {
    this._stopPing();
    clearTimeout(this._handshakeTimeout);
    try {
      if (this.ws) this.ws.close();
    } catch (_) {
      /* ignore */
    }
    this.ws = null;
    this.connected = false;
    this._rx = '';
    this.pending = {};
  }
}

// Convenience: connect a player and JoinGame in one step. Returns
// { client, join } where `join` is the JoinGameResponse data.
export async function connectAndJoin(env, pin, nickname, connectionOptions = {}) {
  const signalrClient = new SignalRClient(env, connectionOptions);
  await signalrClient.start();
  const joinResponse = await signalrClient.invoke('JoinGame', pin, nickname);
  if (!joinResponse || joinResponse.success !== true) {
    signalrClient.close();
    const errorCode = joinResponse && joinResponse.error ? joinResponse.error.code : 'no-response';
    throw new JoinError(errorCode, `JoinGame rejected: ${errorCode}`);
  }
  return { client: signalrClient, join: joinResponse.data };
}

export class JoinError extends Error {
  constructor(errorCode, errorMessage) {
    super(errorMessage);
    this.code = errorCode;
  }
}

function bytesToString(arrayBuffer) {
  const byteArray = new Uint8Array(arrayBuffer);
  let decodedString = '';
  for (let byteIndex = 0; byteIndex < byteArray.length; byteIndex += 1) {
    decodedString += String.fromCharCode(byteArray[byteIndex]);
  }
  return decodedString;
}

function safe(callbackFunction, callbackArguments) {
  try {
    callbackFunction.apply(null, callbackArguments);
  } catch (_) {
    /* handler errors must not break the read loop */
  }
}
