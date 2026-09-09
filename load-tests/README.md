# Kahoot-like Platform — k6 load-testing suite

Verifies the non-functional requirements in
[`../docs/non-functional-requirements.md`](../docs/non-functional-requirements.md):

| Requirement | Target |
|---|---|
| 500 concurrent players in one live game | supported, no lost/duplicated data |
| 500 concurrent SignalR connections | all establish reliably |
| ~500 answer submissions in ~1 s | accepted, once each |
| Normal API p95 | < 300 ms |
| Answer submission p95 | < 500 ms |
| Unexpected error rate | < 1 % |
| Lost accepted answers / duplicate scores / duplicate answers / inconsistent state | 0 |

The suite talks to the **real SignalR hub** using the real protocol (negotiate →
WebSocket → JSON handshake → invocations), not a raw socket. It drives host
actions through the real REST controllers.

---

## Prerequisites

* **k6 ≥ v0.52** (tested with Grafana k6 v2.2.0). The SignalR transport uses
  `k6/websockets` (falls back to `k6/experimental/websockets` on older k6 — edit
  the import in `helpers/signalr.js` if your k6 only has the experimental path).
* **Node ≥ 18** — only for `run-all.js` (the sequential runner) and
  `verify/verify-db.mjs`. Plain `k6 run` needs nothing but k6.
* **Docker + Docker Compose v2** — for Option A below (the Railway-sized stack).
* A reachable backend (Compose, bare `dotnet run`, staging, or Railway) **and**
  its PostgreSQL, plus a **host account** (username + password). The backend
  seeds `admin` / `admin` from `backend/src/Kahoot.Api/appsettings.json`
  (`Seeding:Host`) unless `Seeding__Host__*` is overridden.
* `psql` — optional; only the `verify/verify-db.mjs` convenience wrapper needs it
  on `PATH`. Option A instead runs `verify.sql` via `docker compose exec`.

There is **no registration endpoint** — the host account must already exist
(config seed). Players never authenticate.

---

## Environment variables

Pass with `-e KEY=VALUE` on `k6 run`, or put them in `load-tests/.env` (only
`run-all.js` reads that file). See [`.env.example`](.env.example).

| Var | Default | Meaning |
|---|---|---|
| `BASE_URL` | `http://localhost:5048/api` | REST API base, **including** `/api` |
| `SIGNALR_URL` | origin of `BASE_URL` | SignalR host, no path |
| `HUB_PATH` | `/hubs/game` | hub route (`Program.cs`) |
| `HOST_USERNAME` / `HOST_PASSWORD` | `admin` / `admin` | host credentials — override for staging/Railway |
| `ALLOW_LOAD_TEST` | `false` | **must be `true`** for any scenario with peak > `SAFE_VU_LIMIT` |
| `ALLOW_PROD_LOAD_TEST` | `false` | also required if the URL looks like production |
| `SAFE_VU_LIMIT` | `50` | threshold above which the gate applies |
| `PLAYERS` | `500` | players in single-game scenarios |
| `GAMES` / `PLAYERS_PER_GAME` | `10` / `50` | multiple-games scenario |
| `RECONNECT_PLAYERS` | `100` | reconnection scenario |
| `STORM_PLAYERS` / `STORM_ROUNDS` | `PLAYERS` / `3` | reconnection storm |
| `ANSWER_TIME_LIMIT` | `120` | question length (s) for answer/broadcast tests (min 5, max 300) |
| `ENDURANCE_MINUTES` / `ENDURANCE_PLAYERS` | `10` / `200` | endurance scenario |
| `CONNECT_RAMP` / `JOIN_RAMP` | `120s` / `90s` | how long to spread connection/join traffic |
| `SIGNALR_SKIP_NEGOTIATION` | `false` | `true` connects straight to the WebSocket (halves HTTP requests) |
| `SIGNALR_CONNECT_RETRIES` | `4` | retry a rate-limited negotiate / failed upgrade |
| `CLOCK_SKEW_MS` | `0` | subtract from question-delivery latency if load box ≠ server clock |
| `RUN_ID` | `local` | prefix for generated nicknames + summary files |

---

## How test data is created

`helpers/testdata.js` builds everything through the **public host API** in
`setup()` (not counted as measured load):

1. `POST /api/auth/login` with `HOST_USERNAME` / `HOST_PASSWORD`.
2. `POST /api/quizzes` → `POST /api/quizzes/{id}/questions` (4 choices, "Choice A"
   is always correct) → `POST /api/quizzes/{id}/publish`.
3. `GET /api/quizzes/{id}` to read back choice IDs.
4. `POST /api/games` → a fresh PIN per game session.

500 unique players = 500 VUs, each with nickname
`` `${RUN_ID}-${vu.idInTest}-${iter}` `` (globally unique, passes the server's
`^[\p{L}\p{N} _.\-]{2,30}$` rule). No two VUs share a nickname.

Every run creates brand-new quiz + game-session rows, so runs don't collide and
FR-5a session isolation is preserved.

---

## How SignalR is handled

`helpers/signalr.js` implements the wire protocol the app's `@microsoft/signalr`
browser client uses:

* `POST {hub}/negotiate?negotiateVersion=1` (skippable via
  `SIGNALR_SKIP_NEGOTIATION`) → `connectionToken`.
* WebSocket upgrade to `{hub}?id={token}` (`ws://`/`wss://` derived from the
  scheme). Host connections add `?access_token=<jwt>` — the backend reads it from
  the query string for `/hubs/game` (`Program.cs` `OnMessageReceived`).
* Handshake frame `{"protocol":"json","version":1}\x1e`, wait for `{}\x1e`.
* Invocations `{"type":1,"invocationId":..,"target":..,"arguments":[..]}\x1e`,
  completions `type:3`, server events `type:1` (no invocationId), keep-alive
  `type:6` every 10 s (server `ClientTimeoutInterval` is 30 s), close `type:7`.
* Messages are split on the `0x1e` record separator; multiple records per frame
  are handled.

The client is promise-based and yields to the k6 event loop, so a VU can hold a
live connection and still receive `QuestionStarted` / `QuestionEnded` pushes.
**Do not use k6's blocking `sleep()` while a client is open** — use `delay()` /
`waitFor()` from `helpers/signalr.js`.

Hub methods used exactly as the app defines them:
`JoinGame(pin, nickname)`, `Reconnect(sessionToken)`,
`SubmitAnswer(questionId, selectedChoiceId)` (gameId + token come from
per-connection state), `JoinAsHost(gameId)`. Host game-control
(`start` / `advance` / `end-question` / `leaderboard` / `end`) is REST-only, as
in the app.

---

## Running one scenario

```bash
k6 run -e ALLOW_LOAD_TEST=true \
  -e BASE_URL=http://localhost:5048/api \
  -e SIGNALR_URL=http://localhost:5048 \
  load-tests/scenarios/answer-burst.js
```

Each scenario writes `load-tests/results/<scenario>-summary.json` and prints a
compact report block.

| File | Scenario |
|---|---|
| `scenarios/connections.js` | 1 — 500 concurrent SignalR connections |
| `scenarios/join-game.js` | 2 — 500 unique players join one game |
| `scenarios/question-broadcast.js` | 3 — broadcast delivery + group isolation |
| `scenarios/answer-burst.js` | 4 — ~500 answers in ~1 s + state verification |
| `scenarios/duplicate-answer.js` | 5 — duplicate-answer concurrency / idempotency |
| `scenarios/reconnection.js` | 6 — 100 reconnects, identity + state preserved |
| `scenarios/multiple-games.js` | 7 — 10 × 50 games, group isolation |
| `scenarios/ramp.js` | 8 — 50→100→250→500→750 stress |
| `scenarios/reconnection-storm.js` | 9 — mass disconnect/reconnect |
| `scenarios/endurance.js` | 10 — sustained soak (configurable) |

## Running the whole suite

```bash
# put ALLOW_LOAD_TEST=true and the target URLs in load-tests/.env, then:
node load-tests/run-all.js                     # acceptance set (scenarios 1–8)
node load-tests/run-all.js all                 # + storm + endurance
node load-tests/run-all.js answer-burst reconnection
node load-tests/run-all.js -- -e PLAYERS=250   # pass extra args to every k6 run
```

`run-all.js` runs the scenarios sequentially — one login for the whole run
(refreshed as the token ages, so `/auth/*` never rate-limits), a `SCENARIO_COOLDOWN`
idle gap between each so the per-IP bucket refills, `connections` last (it drains
the bucket) — then prints the PASS/FAIL table. Per-scenario JSON lands in
`load-tests/results/`.

---

## How to run — step by step

### Option A (recommended): Docker Compose, sized like one small Railway instance

`load-tests/docker-compose.railway-sim.yml` runs the **same image**
`../docker-compose.yml` builds, but as a self-contained stack tuned to model a
single small Railway deployment: one backend replica, `ASPNETCORE_ENVIRONMENT=Production`,
CPU + memory caps on the API and the database, and Postgres kept off the host
(as on Railway). The API is published on `localhost:5000`.

```bash
# from the repo root

# 1. Build + start the sized stack (first build does a dotnet publish, ~2–4 min)
docker compose -f load-tests/docker-compose.railway-sim.yml up -d --build

#    tune the "instance size" — defaults model a small paid box
#    (API 2 vCPU / 1 GiB, DB 1 vCPU / 1 GiB):
API_CPUS=1 API_MEM=512m DB_CPUS=1 DB_MEM=512m \
  docker compose -f load-tests/docker-compose.railway-sim.yml up -d --build

# 2. Wait for the API (migrations + host seed run on startup)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/health   # expect 200

# 3. Point the tests at it and run
cd load-tests            # so k6 writes results/ and reads .env conventions
cp .env.example .env      # already targets http://localhost:5000 in this repo
node run-all.js                                   # full acceptance set
#   or one scenario:
k6 run -e ALLOW_LOAD_TEST=true -e BASE_URL=http://localhost:5000/api \
       -e SIGNALR_URL=http://localhost:5000 -e SUMMARY_DIR="$PWD/results" \
       scenarios/answer-burst.js

# 4. Watch the "Railway" box while it runs (separate terminal)
docker stats kahoot-railway-sim-backend-1 kahoot-railway-sim-db-1

# 5. Definitive DB integrity check (Postgres has no host port here — exec in)
docker compose -f load-tests/docker-compose.railway-sim.yml exec -T db \
  psql -U postgres -d kahoot -v ON_ERROR_STOP=1 < verify/verify.sql   # every section => (0 rows)

# 6. Tear down (‑v also wipes the DB volume for a clean next run)
docker compose -f load-tests/docker-compose.railway-sim.yml down -v
```

Real Railway: keep the container CPU/RAM close to your Railway service's plan so
the numbers transfer. Because every k6 VU reaches the container through the
Docker gateway, the API's per-IP rate limiter sees one client — the same
single-origin constraint described below.

### Option B: bare `dotnet run` (fastest inner loop, no resource cap)

```bash
docker run -d --name kahoot-loadtest-db -e POSTGRES_DB=kahoot \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5433:5432 postgres:17-alpine
cd backend
ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=kahoot;Username=postgres;Password=postgres" \
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Kahoot.Api --launch-profile http
# -> http://localhost:5048 ; then:  k6 run -e ALLOW_LOAD_TEST=true -e BASE_URL=http://localhost:5048/api ...
```

### Option C: a deployed staging / Railway URL

```bash
k6 run -e ALLOW_LOAD_TEST=true \
  -e BASE_URL=https://<app>.up.railway.app/api \
  -e SIGNALR_URL=https://<app>.up.railway.app \
  -e HOST_USERNAME=... -e HOST_PASSWORD=... \
  load-tests/scenarios/answer-burst.js
```

If `BASE_URL` matches `prod`/`production`/`railway.app`, you also need
`-e ALLOW_PROD_LOAD_TEST=true`. Never commit real credentials — pass them with
`-e` or an untracked `.env`.

### Single load-generator IP — important

The API applies a **global per-IP rate limit** (`RateLimitingExtensions.cs`):
token bucket **240 burst + 120 / 30 s**, `QueueLimit = 0` (excess → immediate
`429`). In a real game, 500 players are 500 IPs; from one k6 box they are **one**
IP. Opening 500 connections there costs 500 (skip-negotiation) – 1000 (with
negotiation) requests against that bucket, so:

* connection scenarios **ramp** the traffic (`CONNECT_RAMP`, `JOIN_RAMP`) and the
  SignalR helper **retries** `429`/upgrade failures with backoff;
* `SIGNALR_SKIP_NEGOTIATION=true` halves the request count;
* for a true instantaneous 500-distinct-client burst, run k6 **distributed**
  (k6 Cloud / multiple agents) or from a host with many source IPs
  (`k6 run --local-ips=...`).

This is a property of the load generator, **not** a backend defect. The
scenarios still validate 500 concurrent *established* connections + a real
single-broadcast answer burst.

---

## Interpreting thresholds

k6 exits **non-zero (99)** if any threshold fails, so a violated requirement
fails the run (and `run-all.js`'s table). Key thresholds:

| Threshold | Requirement |
|---|---|
| `http_req_duration{scope:api}: p(95)<300` | normal API p95 < 300 ms |
| `answer_submission_duration: p(95)<500` | answer p95 < 500 ms |
| `http_req_failed{scope:api}: rate<0.01` and `unexpected_error_rate: rate<0.01` | error rate < 1 % |
| `lost_accepted_answers: count<1` | no lost accepted answers |
| `duplicate_answer_violations: count<1` | no duplicate accepted answers |
| `duplicate_score_violations: count<1` | no duplicate scores |
| `inconsistent_game_state: count<1` | no corrupted game state |
| `session_isolation_violations: count<1` | no cross-game event/score bleed |
| `duplicate_participants: count<1` | reconnection never creates a second player |
| `signalr_connection_success_rate: rate>0.99` | connections establish reliably |

Custom metrics (`Trend` / `Rate` / `Counter` / `Gauge`) are defined in
`helpers/metrics.js`: `signalr_connections`, `signalr_connection_failures`,
`player_join_duration`, `answer_submission_duration`, `answers_accepted`,
`duplicate_answer_violations`, `question_delivery_duration`,
`reconnection_duration`, `unexpected_errors`, …

### How correctness is actually verified (not just acks)

The director side of each scenario re-reads server state after closing a
question:

* `GET /api/games/{id}/questions/{qid}/results` → `answerCount` is the DB-truth
  count of accepted answers; per-choice counts must sum to it.
* `GET /api/games/{id}` → independent `answeredCount` must match; participant
  `totalScore` after one question must be `≤ question.points` (a higher value ⇒
  a score applied twice).
* leaderboard entry count must equal active players, with no duplicate
  participant IDs and monotonic ranks.

For the **definitive** check, run `verify/verify.sql` against PostgreSQL after a
scenario (see below). It directly asserts
`participant.total_score == SUM(answers.points_awarded)`, one answer row per
`(game, question, participant)`, no cross-session rows, etc.

```bash
# scope to the game the scenario logged ("[answer-burst] game <uuid> ...")
DATABASE_URL=postgres://postgres:postgres@localhost:5433/kahoot \
  node load-tests/verify/verify-db.mjs <game-session-uuid>
# or, against a docker container:
PSQL_DOCKER=kahoot-loadtest-db node load-tests/verify/verify-db.mjs
```

Every section should print `(0 rows)`.

---

## Results summary — what you get

`results/<scenario>-summary.json` (raw k6 `data`) plus a printed block with:

```
Concurrent players / SignalR connections established / failed
Answers submitted / accepted / rejected / unexpected failures
Answer submission p50 / p90 / p95 / p99, throughput
API p95, question delivery p95, handshake p95
Error rate, checks passed
Lost accepted answers, duplicate answers, duplicate scores,
inconsistent state, session isolation violations, reconnection failures
```

### What k6 measures vs what you must read elsewhere

**Measured directly by k6** (in `results/*-summary.json` and the printed block):
SignalR connect/handshake time & success rate, player-join time, answer-submission
p50/p90/p95/p99 + throughput, question-delivery time (approx), REST `http_req_*`,
and every correctness invariant that is checkable through the API
(answer counts, per-choice sums, scores, roster, leaderboard, isolation).

**NOT visible to k6 — do not fabricate these; read them from the box:**
container CPU / memory, .NET thread-pool growth & GC, Npgsql pool usage, Postgres
lock waits / slow queries. For **Option A** capture them with:

```bash
docker stats kahoot-railway-sim-backend-1 kahoot-railway-sim-db-1        # live CPU/MEM
docker compose -f load-tests/docker-compose.railway-sim.yml exec db \
  psql -U postgres -d kahoot -c \
  "select state, count(*) from pg_stat_activity where datname='kahoot' group by 1;"
```

For **real Railway**, use the Railway service **Metrics** tab and Railway
Postgres metrics for the same wall-clock window as the run. The endurance
scenario's first-half vs second-half latency split is a drift proxy, not a
substitute for those graphs.

---

## Limitations

* **Single-IP rate limiting** (see above) caps how fast one k6 box can open
  connections; scenarios ramp to compensate. With Option A this is unavoidable —
  every VU shares the Docker gateway IP. True 500-distinct-IP bursts need
  distributed k6 (k6 Cloud / multiple agents / `--local-ips`).
* **Question-delivery latency is approximate**: `client_receive_time −
  (server endsAt − timeLimit)`. `setup()` auto-measures the server⇄load-box clock
  offset from the `/health` `Date` header (≈1 s resolution) and subtracts it;
  override with `-e CLOCK_SKEW_MS`. The relative comparison (all players, one
  broadcast) is the reliable part.
* k6's `experimental/websockets` has no server-push backpressure signal; a VU
  that is CPU-starved could record a late `QuestionStarted` as its own latency.
  Keep VUs-per-core reasonable (≈ ≤ 200/core) or run distributed.
* The suite creates real quizzes/games and does not delete them (quizzes that
  have run a game cannot be deleted by design). Use a disposable database.
* No server-side profiling is included — pair with APM / Railway dashboards.
* `duplicate-answer.js` opens 2 sockets per player to force a real INSERT race;
  it deliberately stays under the hub's 5-per-3 s answer guard, so it proves the
  DB/application idempotency path, not the spam guard.
