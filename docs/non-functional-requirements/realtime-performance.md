# Real-Time Performance

## Purpose

Define the responsiveness, reliability, and isolation required from SignalR
during live gameplay at the supported player count.

## NFR-1.1: SignalR Connections

- The system must sustain **500 concurrent SignalR player connections in one
  game** for the duration of a normal game.
- At least **99%** of connection attempts must complete successfully under the
  supported load.
- SignalR negotiation and handshake latency must be below **3 seconds at p95**.
- Connection establishment must be paced and tested through the production
  reverse proxy; bypassing the proxy does not prove production capacity.
- Connections and messages from one game must never enter another game's group.

## NFR-1.2: Live Event Delivery

- Question broadcasts must reach connected, eligible players within **500 ms at
  p95** and **1 second at p99**, measured from the server's broadcast timestamp
  to client receipt after correcting for clock skew.
- No connected eligible player may miss the current-question event during a
  successful capacity run.
- Each state-changing event must carry enough authoritative state for clients to
  render correctly without incrementing or guessing counts locally.
- Presence events must carry absolute server-calculated reserved-seat and
  connected-player counts.
- The server-provided `questionEndsAt` value is the official deadline. Client
  timers are display-only.
- Slow or disconnected clients must not block the host command or delay delivery
  to healthy clients indefinitely.

## NFR-1.3: Answer and Reconnection Responsiveness

- Answer processing must complete within **500 ms at p95** under the supported
  load, including the 500-player answer burst.
- A reconnect operation must restore authoritative participant and game state
  within **3 seconds at p95**.
- Reconnection must not create a duplicate participant, duplicate answer, or
  duplicate score.
- A player reconnecting after answering must receive state that shows the answer
  was already accepted and must not be able to score again.
- Late, duplicate, closed-question, future-question, and unauthorized submissions
  must be rejected consistently without affecting other players.

## NFR-5.1: Delivery Semantics and Recovery

- A command succeeds once its authoritative state change is durably committed.
- SignalR fan-out occurs only after the commit and is best-effort. A fan-out
  failure must be logged and measured, but must not make a committed command
  appear rolled back.
- SignalR events are not a durable queue. Hosts resynchronize through REST and
  players through the `Reconnect` operation.
- The system must handle transient network loss, reconnect-after-answer, backend
  restart, and mass reconnection without corrupting authoritative state.

## Verification

- Use the real SignalR negotiation, WebSocket handshake, hub protocol, group
  membership, and hub invocations in load tests; a raw socket test is insufficient.
- Measure connection success, handshake latency, broadcast delivery latency,
  broadcast failures, reconnect latency, reconnect failures, and session-isolation
  violations.
- Passing a lower-concurrency test is useful as a regression check but does not
  satisfy the 500-connection release requirement.
