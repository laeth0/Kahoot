# Observability and Verification

## Purpose

Make performance and correctness measurable and define the evidence required to
accept the system at production load.

## NFR-7: Observability

- Logs must be structured and include `gameId`, `participantId`, `questionId`,
  `connectionId`, and `requestId` when relevant.
- Logs must not contain passwords, password hashes, access tokens, refresh tokens,
  participant session tokens, or configuration secrets.
- Metrics must cover active games, reserved players, connected players, SignalR
  connections, connection failures, answers submitted, accepted, duplicated,
  rejected, and late, answer-processing duration, database-query duration,
  broadcast duration and failures, reconnections, and state-transition failures.
- Latency metrics must expose at least p50, p95, and p99 so averages cannot hide
  slow players.
- Capacity tests must also record application, PostgreSQL, and proxy CPU, memory,
  connection, and saturation signals.
- Alerts and dashboards must distinguish liveness, readiness, dependency failure,
  elevated latency, elevated error rate, and lost real-time delivery.

## NFR-12: Verification and Testing

- Production code must remain testable through separated business logic, explicit
  dependencies, deterministic behavior, and `TimeProvider` instead of direct
  `DateTime.UtcNow` use.
- Automated functional tests are added only when separately requested, but this
  constraint does not remove the requirement to run the existing load and
  integrity verification tooling.
- When automated coverage is authorized, it must cover scoring, state transitions,
  validation, deadline calculation, database constraints, duplicate-answer
  prevention, joining, SignalR flows, authorization, and load profiles from 50
  through 750 users.
- Load tests must use the real REST endpoints, SignalR hub protocol, PostgreSQL
  database, and production proxy topology.
- Every load-test run must fail when a required latency or error threshold fails.
- Every run must fail on any lost accepted answer, duplicate answer, duplicate
  score, duplicate participant, cross-session delivery, or inconsistent game
  state.
- A successful lower-capacity run is a regression signal only; it must not be
  reported as proof of the 500-player requirement.

## Required Release Evidence

The release report must identify:

- application revision and configuration;
- deployment topology and resource limits;
- scenario, duration, and user count;
- SignalR connection success and handshake p95;
- question-delivery p50, p95, and p99;
- API and answer-submission p50, p95, and p99;
- unexpected error rate and business-rejection counts;
- database-integrity query results;
- resource saturation and the first observed bottleneck; and
- whether every applicable non-functional requirement passed.
