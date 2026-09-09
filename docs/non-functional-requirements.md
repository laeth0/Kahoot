# Non-Functional Requirements — Kahoot-like Platform

> Living specification. Keep this file synchronized with every architectural / quality change.
> The verbatim original brief is preserved in [`Kahoot-like Platform.md`](./Kahoot-like%20Platform.md).
> Functional requirements live in [`functional-requirements.md`](./functional-requirements.md).

---

## NFR-1 Scale & Performance

Primary target: reliably support at least **500 concurrent players in one live game** without losing answers, duplicating scores, corrupting state, or becoming unacceptably slow.

Design for:

- 500 concurrent SignalR connections in one game.
- 500 near-simultaneous answer submissions (~1 second window).

Targets (to be validated by load tests, not assumed):

| Metric | Target |
|---|---|
| Normal API p95 | < 300 ms |
| Answer submission p95 (under expected load) | < 500 ms |
| Error rate | < 1 % |
| Lost accepted answers | 0 |
| Duplicate scores | 0 |
| Invalid duplicated answers | 0 |
| Inconsistent game state | 0 |
| Question broadcast delivery | hundreds of ms under expected load |

---

## NFR-2 Concurrency & Idempotency

- One accepted answer per `(gameId, questionId, participantId)`, enforced at both the application layer and a database unique constraint. The database is the final integrity boundary.
- Simultaneous duplicate submissions: exactly one succeeds; the loser gets an idempotent response, no extra answer or score.
- Optimistic concurrency on game state (PostgreSQL `xmin`); concurrent transitions fail safely and are resolved idempotently.
- Idempotent host/player operations: submit answer, start game, start question, end question, next question, end game.
- Keep transactions short; avoid N+1 queries and unnecessary round trips on the hot answer path.

---

## NFR-3 Server-Authoritative Design

The backend is the source of truth. Clients never decide: answer correctness, whether an answer was on time, the official timer, points earned, current game state, current question, whether a player already answered, the leaderboard, whether a question is open, or whether the game ended. Client timers are display-only, driven by the server's `questionEndsAt`.

---

## NFR-4 Security

### NFR-4.1 Host login (best practices)

- Passwords hashed with a purpose-built password hash (BCrypt, work factor ≥ 12; Argon2id acceptable). Never plaintext, never a fast/general-purpose hash.
- Access tokens: short-lived JWT (minutes), signed with a secret from configuration.
- Refresh tokens: opaque, long-lived, **stored only as a hash**; single-use with rotation; reuse of a rotated token revokes the chain.
- Login and refresh endpoints are rate-limited and return generic failure messages (no user enumeration).
- Tokens, password hashes, and secrets are never logged.

### NFR-4.2 Authorization

- Every host operation requires authentication and game-ownership authorization.
- Players cannot call host operations, submit answers for another player/game/question, or answer a future/closed question.

### NFR-4.3 Input & transport

- Server-side validation and sanitization of all input (PIN, handle name, quiz/question content).
- Payload size limits; answer-spam protection.
- CORS restricted to the known frontend origins.
- HTTPS in production; proxy/forwarded-headers aware.
- Secrets only via environment variables / configuration providers, never in source control. No secret is exposed to the frontend, in logs, errors, URLs, or the health endpoint.

### NFR-4.4 Rate limiting

- Sensible limits on `/join` and answer operations.
- Limits distinguish malicious repetition from a legitimate burst of 500 distinct players.

---

## NFR-5 Reliability & Startup

- Pending EF Core migrations are applied on startup by a hosted service; failure fails startup (fail fast), runs async with the startup cancellation token, and stays out of `Program.cs`.
- Seeding runs as a separate hosted service after migration, is idempotent, and skips (with a warning) when its configuration is absent — it never blocks startup on its own.
- Handle and document: duplicate messages/requests, host double-click, simultaneous transitions, network loss, reconnect-after-answer, DB timeout/slowness, invalid/expired PIN, answer after / exactly at deadline, backend restart, mass reconnection.

---

## NFR-6 Data Integrity

- Appropriate primary keys, foreign keys, unique constraints, check constraints, and indexes in PostgreSQL.
- Each non-obvious index is justified by the query pattern it serves (documented in `backend/projectSchema.dbml`).
- Schema changes go through EF Core migrations only; `projectSchema.dbml` is updated in the same change. Existing migrations are immutable.

---

## NFR-7 Observability

- Structured logging with `gameId`, `participantId`, `questionId`, `connectionId`, `requestId` where relevant. No passwords, tokens, or secrets in logs.
- Metrics: active games/players, SignalR connections, answers submitted/accepted/rejected, duplicate answers, late answers, answer-processing duration, DB query duration, broadcast duration, reconnections, state-transition failures.

---

## NFR-8 Architecture

- Modular monolith. No microservices.
- Clean Architecture layering: `Kahoot.Domain` (anemic entities + enums), `Kahoot.Application` (use cases, state machine, scoring, validation), `Kahoot.Infrastructure` (EF Core, persistence, hosted services, security implementations), `Kahoot.Api` (controllers, SignalR hubs, pipeline). Business logic stays out of controllers and hubs.
- No global mutable application state as the sole source of truth; state that must survive restarts or scaling lives in PostgreSQL.
- Async / non-blocking I/O throughout; cancellation and timeouts propagated; no unbounded retries.

---

## NFR-9 Deployment

- Backend → Railway (single replica initially), containerised, environment-based configuration, correct port binding, PostgreSQL connection via env var, production CORS, HTTPS/proxy awareness, `/health` endpoint, structured logs.
- Frontend → Vercel, API/SignalR URLs via environment variables, separate development and production environments, no hard-coded backend URLs.
- Database → PostgreSQL (Railway PostgreSQL preferred).
- All required environment variables documented.

---

## NFR-10 Scaling Strategy

- Start with one backend replica. Do **not** add Redis or extra replicas pre-emptively.
- Run the required load tests first; if one replica fails, identify and measure the bottleneck before scaling.
- Only when multiple replicas are justified by measurements: introduce Redis + a SignalR backplane for shared state and broadcasts. PostgreSQL remains the durable source of truth.
- Keep the architecture free of coupling that would make future horizontal scaling hard.

---

## NFR-11 EF Core Rules

- `AsNoTracking()` for read-only queries; project only needed columns; avoid `Include` chains on hot paths.
- Async DB APIs with `CancellationToken`; no sync-over-async.
- `SaveChangesAsync` at intentional use-case boundaries; explicit transactions only when multiple writes must be atomic.
- Expected constraint / concurrency failures are converted to `Result` / `ProblemDetails`, never leaking provider messages.

---

## NFR-12 Verification & Testing (later phase)

- No automated tests are added until explicitly requested, but all production code stays testable: logic separated from infrastructure, explicit dependencies, deterministic behaviour, `TimeProvider` instead of `DateTime.UtcNow`.
- When requested, cover: scoring, state transitions, validation, deadline calculation, DB constraints, duplicate-answer prevention, joining, SignalR flows, authorization, and 50→750 user load tests.
