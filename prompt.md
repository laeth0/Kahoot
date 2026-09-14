# Gemini Production-Readiness Implementation Plan

## Mission

Implement this plan against the current repository. The repository has already been audited; use the findings below as the starting point, but inspect every affected file and its nearby dependencies before editing. The target is a production-ready Azure deployment that reliably supports **500 concurrent players**, including a burst of approximately **500 accepted answers in one second**, plus a **200-player 10-minute endurance run**.

Do not merely produce another review. Make the authorized changes, verify each phase, and record evidence. Do not claim production readiness until every release gate in this document is satisfied.

## Non-negotiable repository rules

- Read and follow the root `AGENTS.md`, `.wolf/OPENWOLF.md`, `.wolf/cerebrum.md`, and every more-specific `AGENTS.md` that governs a file before changing it.
- Preserve the dirty working tree and all unrelated user work. Inspect `git status` and the relevant diffs before every phase. Never reset, checkout, overwrite, stage, or commit unrelated changes.
- Keep changes scoped to this plan. Do not perform dependency upgrades, formatting sweeps, or unrelated refactors.
- Do not edit the two existing EF Core migrations. Create forward-only migrations and update `backend/projectSchema.dbml` for every schema change.
- Do not create new test files or test projects until the owner explicitly authorizes that exception to the repository rule. Extending the existing k6 scenarios and validation scripts is allowed.
- Never print, copy into logs, or put into a patch any credential value discovered in the repository or Git history.
- Do not rotate credentials, purge Git history, force-push, change Azure resources, modify DNS, deploy, or run load tests against a shared/production target without explicit owner approval for that action.
- Use `apply_patch` for deliberate text edits. Inspect the final diff and run proportionate verification after every task.
- Do not weaken security, validation, tests, or acceptance thresholds to make checks pass.
- If a decision gate below is unresolved, stop only the dependent work; continue independent tasks.

## Audited baseline: do not lose existing good work

- Backend: ASP.NET Core/.NET 10 modular monolith, EF Core 10, Npgsql/PostgreSQL, MediatR, FluentValidation, Mapster, SignalR.
- Frontend: React 19, TypeScript, Vite 8, Material UI 9, Axios, SignalR client.
- Deployment: Docker Compose on an Azure VM currently documented as Standard D2s v3 (2 vCPU/8 GiB), with Nginx, PostgreSQL, OpenTelemetry Collector, Prometheus, Loki, Jaeger, Grafana, cAdvisor, node-exporter, postgres-exporter, and blackbox-exporter.
- Preserve and extend the existing OpenTelemetry traces, metrics, logs, dashboards, alert rules, private container networks, retention controls, and correlation behavior. Do not rebuild the observability platform from scratch.
- Keep a single backend replica initially. Do not add Redis, a SignalR backplane, Azure SignalR Service, Kubernetes, or another replica unless post-fix load evidence proves that one process cannot meet the target.
- A clean backend restore/build completed with 0 warnings and 0 errors. `dotnet list package --vulnerable --include-transitive` reported no vulnerable packages.
- EF reported no pending model changes. The globally installed `dotnet-ef` was 10.0.5 while runtime packages were 10.0.12; pin the local toolchain rather than relying on global state.
- A clean frontend `npm ci` followed by `npm run build` passed. Formatting, ESLint, and `npm audit --omit=dev --audit-level=moderate` passed with 0 production vulnerabilities. Local Node 20 was below `@rolldown/plugin-babel`'s supported range; the Docker image uses Node 22, but the repository does not enforce it.
- Both Compose files render when all required variables are explicitly supplied. Rendering production Compose through an incidental developer `.env` is not an acceptable validation method.
- React Doctor reported a low score and 42 diagnostics. Treat that report as triage, not a release oracle: fix the confirmed state-mutation, unstable-key, reduced-motion, accessibility, and bundle/import issues below; do not churn working components solely to eliminate compiler-bailout or complexity warnings. The reported cleanup in `useHostGame` appears to be a false positive after manual inspection.
- The tracked full load-test artifact is a failed acceptance run: 500-player question broadcast had 24 isolation violations; answer burst accepted 496 with about 9.75 s p95; duplicate-answer and reconnect scenarios had invariant violations. A separate status note claiming a passing single answer-burst run does not prove the full suite. Capacity remains **unproven** until Phase 8 passes at the exact release commit.

## Confirmed release blockers

| ID | Severity | Confirmed problem | Required outcome |
|---|---:|---|---|
| SEC-01 | Critical | A tracked server private-key file, tracked non-example environment files, literal credentials in documentation/load tooling, and credential-bearing history exist. | Revoke/rotate first, stop tracking secret-bearing files, fix scanning, then coordinate any history rewrite. |
| SEC-02 | Critical | Host refresh and access tokens are stored in browser `localStorage`; refresh rotation is not atomic and reuse revokes all host sessions rather than a token family. | Secure cookie-based refresh, memory-only access token, CSRF defense, atomic family rotation with race handling. |
| NET-01 | Critical | Outer Nginx only listens on HTTP even though Compose maps 443 and mounts certificate paths. Production URLs, Grafana, JWTs, and passwords are exposed over cleartext. | Functional TLS, HTTP-to-HTTPS redirect, HSTS, secure cookies, and an explicit public origin. |
| GAME-01 | Critical | Player hub group membership is granted before database authorization; removal does not forcibly remove the live connection. | Atomic attach authorization before group membership and server-side eviction on removal. |
| DATA-01 | Critical | Live games and answers reference editable quiz rows, so quiz edits can rewrite historical meaning and referenced deletes can fail. | Immutable per-game question/choice snapshots used by all gameplay and result paths. |
| PERF-01 | Critical | Every accepted answer runs multiple aggregate/anti-join queries; tracked 500-answer results fail badly. | Durable per-question counters and a cheap, race-safe auto-end path. |
| OPS-01 | Critical | There is no proven off-host database/upload backup and restore process; a single VM is a single point of failure. | Owner-approved RPO/RTO tier, encrypted off-host backups, retention, alerts, and a recorded restore drill. |
| TEST-01 | Critical | Load orchestration can fall back to the wrong host/credentials and thresholds permit false passes, including a “750” run with zero answers. | Explicit targets, exact workload assertions, corrected thresholds, and immutable evidence. |
| CFG-01 | High | The config validator checks wrong variable names and excludes environment files/docs/private-key markers, so it reports false secret hygiene. | Hermetic validation and complete tracked-file secret scanning. |
| GAME-02 | High | Join/start/capacity operations are not linearized and no participant limit is enforced. | Atomic 500-player capacity/state enforcement without admitting participants after start. |
| GAME-03 | High | Reconnect emits a joined delta and the player UI starts from count 1, causing participant-count drift. | Absolute server-authoritative counts and documented presence semantics. |
| REL-01 | High | A notification failure after a committed command can return 500, encouraging retries after success. | Commit result remains authoritative; failed fan-out is observed and clients resynchronize. |
| MEDIA-01 | High | Arbitrary external image URLs are accepted; image uploads lack decoded-pixel limits/normalization and lifecycle control; editing clears existing choice images. | Same-origin media only, bounded safe processing/storage, and lossless editor behavior. |
| OPS-02 | High | Health only reports process liveness; production CORS/Client options can silently use development defaults. | Separate live/ready probes and fail-fast production configuration validation. |
| OPS-03 | High | Grafana is publicly reverse-proxied and has no external notification path; some container “health” checks only validate config. | Private admin access, real readiness probes, and external synthetic/alert delivery. |
| INFRA-01 | High | Images/tags are mutable, app containers run as root, Nginx connection headroom is too low, and live-VM builds lack immutable rollback. | Pinned artifacts, least privilege, adequate limits, CI-built releases, health-gated rollback. |

## Owner decision and authorization gates

Record each answer in a short decision log before dependent implementation. Do not invent production values.

### Gate G1 — Public origin and certificate strategy

Ask for the canonical HTTPS origin. **Recommended:** use an owned DNS name and a normal automated ACME certificate. If the owner must use a bare public IP, document that Let's Encrypt IP certificates are short-lived (roughly six days) and require Certbot 5.4+ plus frequent, monitored renewal. Do not leave HTTP as the production answer.

### Gate G2 — Correct-answer cardinality

The functional documents conflict: one section says exactly one correct choice while validators/UI permit one or more. Ask whether questions are single-answer or multi-answer. **Recommended for the current Kahoot-like scoring flow:** exactly one correct choice. Align domain validation, API validation, UI controls, publishing checks, and documentation to the chosen rule.

### Gate G3 — Data durability and availability budget

Ask for RPO, RTO, retention, monthly Azure budget, and acceptable single-VM downtime.

- **Recommended for materially lower operational risk:** Azure Database for PostgreSQL Flexible Server with PITR retention and optional zone-redundant HA, plus managed blob storage for media.
- **Lower-cost accepted-risk option:** keep PostgreSQL on the VM, but require Azure VM Backup plus scheduled PostgreSQL custom-format/logical backups to Blob, encryption, retention, monitoring, and recurring restore drills. A VM snapshot alone is not a database restore strategy.

### Gate G4 — Alert delivery and administrator access

Ask for the Action Group/contact destination and the permitted administration path. **Recommended:** Azure Monitor Standard availability tests and an Action Group; Grafana bound to loopback and reached through Azure Bastion/JIT-approved SSH tunneling or an owner-approved private network.

### Gate G5 — Automated test files

Ask for explicit authorization to add backend integration/concurrency tests and frontend behavior tests. These are strongly recommended for snapshot immutability, refresh-token races, join/start races, duplicate answers, hub authorization, and auth bootstrap. If permission is denied, do not create test files; execute the strongest existing k6/manual verification available and leave any unprovable high-risk gate visibly unresolved.

### Gate G6 — Destructive incident operations

Obtain separate explicit approval for credential rotation in live services, revoking the Azure SSH key, `git-filter-repo`, deleting remote refs, force-pushing rewritten history, and invalidating old clones. Source cleanup can proceed after rotation, but history rewriting must never be automatic.

---

# Execution plan

## Phase 0 — Establish control and contain credential exposure

### Task 0.1 — Snapshot state and create an incident-safe work boundary

**Inspect:** `git status`, `git diff`, `git log`, `.gitignore`, `backend/.gitignore`, `frontend/.gitignore`, `load-tests/.gitignore`, all tracked `.env*` files, `kahoot-server_key.pem`, `cloudflared.md`, `load-tests/run-all.js`.

- [ ] Record the starting commit SHA and the pre-existing modified/untracked files. Do not include secret values.
- [ ] Identify which credential-like files and values are real, which are examples, and which have ever been deployed. Report only file paths and credential categories.
- [ ] Create a release-blocking incident checklist ordered so credentials are revoked/rotated **before** public repository cleanup advertises the exposure.
- [ ] Keep `.env.example`, `.env.production.example`, `frontend/.env.example`, `frontend/.env.production.example`, and `load-tests/.env.example` as placeholder-only templates.
- [ ] Follow the repository-specific ignore rule: root `.gitignore` must contain only `.env`; do not add `.env.*` or exception patterns there.

**Owner-run order after G6 approval:** revoke/replace the exposed Azure VM SSH authorized key; rotate the host password and invalidate refresh-token families; rotate JWT, database, Grafana, monitoring, and any tunnel/cloud credentials that may have been exposed; then clean tracked files; finally coordinate history rewrite and force-push. Require all collaborators and deployment machines to discard old clones/refs after a rewrite.

**Acceptance:** no credential is printed; an owner-readable rotation matrix names credential category, owner, status, and verification without recording values.

### Task 0.2 — Remove current-tree plaintext credentials and unsafe defaults

**Files:** tracked non-example environment files, `kahoot-server_key.pem`, `cloudflared.md`, `load-tests/run-all.js`, `.env.example`, `.env.production.example`, `frontend/.env*.example`, `load-tests/.env.example`, backend appsettings, deployment docs.

- [ ] After credential rotation is confirmed, stop tracking `frontend/.env` and `load-tests/.env` with `git rm --cached`; the root `.gitignore` entry `.env` covers those nested files. Move any still-needed local values from `frontend/.env.production` into an ignored `.env`, then delete the unignored production file rather than leaving it as plaintext in the tree.
- [ ] Remove the tracked private-key file after the matching public key has been revoked. Do not replace it with another key in the repository.
- [ ] Remove literal usernames/passwords and embedded live IP credentials from docs/scripts. Templates must use unmistakable placeholders and document generation requirements.
- [ ] Remove weak connection-string passwords, the live public IP, and development fallbacks from `backend/src/Kahoot.Api/appsettings.json` and `appsettings.Production.json`. Production secrets must be required environment/secret inputs.
- [ ] Change `backend/src/Kahoot.Infrastructure/Persistence/KahootDbContextFactory.cs` to require an explicit design-time connection string instead of a weak hardcoded fallback.
- [ ] Keep secret-bearing runtime files outside version control and use owner-approved Azure secret delivery. Compose interpolation is acceptable only when the environment file is securely provisioned with restricted permissions and never committed.

**Acceptance:** `git ls-files` contains no runtime `.env`, PEM/private key, or secret-bearing results file; tracked examples contain placeholders only; startup and migrations fail with a useful variable-name error when required values are absent.

### Task 0.3 — Replace the false-positive secret/config validator

**Files:** `observability/scripts/validate-config.sh`, Compose files, example files, deployment documentation, CI workflow created later.

- [ ] Correct variable names: production Compose requires `JWT_SIGNING_KEY` and `HOST_SEED_PASSWORD`; do not validate nonexistent `JWT_SECRET_KEY` or `SEEDING_HOST_PASSWORD`.
- [ ] Make Compose validation hermetic: create/use an explicit temporary validation environment and pass `--env-file`; clear inherited values that could leak from a developer root `.env`.
- [ ] Scan every tracked file, including docs and tracked `.env*` files. Do not globally exclude Markdown, README, `.wolf`, or environment files.
- [ ] Detect private-key headers, common cloud/token formats, embedded URL credentials, and literal password assignments. Permit only well-defined placeholders in example templates.
- [ ] Integrate a pinned secret scanner such as Gitleaks in CI and document narrow, reviewed allowlists with reasons. Never emit the matched secret value.
- [ ] Make validation fail closed when required tools/config are missing.

**Acceptance:** add safe temporary canary fixtures during validation to prove detection of a PEM header, an environment password, and a Markdown literal, then remove the fixtures. The validator must fail for each canary and pass the clean tracked tree.

## Phase 1 — Lock contracts and preserve historical truth

### Task 1.1 — Resolve and document product contracts

**Files:** `docs/functional-requirements.md`, `docs/non-functional-requirements.md`, `docs/realtime-protocol.md`, `docs/Kahoot-like-Platform.md`, relevant validators/contracts.

- [ ] Record G2 and make all documents agree on correct-answer cardinality.
- [ ] Define participant capacity as 500 per game and whether only connected/non-removed participants count. **Recommended:** reserve a seat at successful join; removal releases it only before the game begins; disconnect does not release it during a running game.
- [ ] Define question eligibility. **Recommended:** snapshot the eligible participant count when a question starts; later disconnects do not reduce the denominator; removal during play is host-controlled and must adjust an unanswered eligible count atomically only if the contract intentionally allows that.
- [ ] Define presence events as absolute authoritative counts, not client-maintained deltas.
- [ ] Define committed-command semantics: once state commits, fan-out failure cannot turn the command into an apparent rollback; clients recover via REST/reconnect state.
- [ ] Reconcile current docs that disagree about REST join broadcasts and whether `ParticipantLeft` is host-only or all-clients.

**Acceptance:** a single protocol description covers join, attach, reconnect, remove, disconnect, question activation, answer, auto-end, and resynchronization; backend/frontend tasks below implement it exactly.

### Task 1.2 — Add immutable per-game quiz snapshots

**Files:** domain game entities, `KahootDbContext`, `IApplicationDbContext`, persistence configurations, `CreateGame`, `GameQuestionMapper`, `QuestionActivation`, result/leaderboard/state/reconnect handlers, Answer/GameSession configurations, new migration, `backend/projectSchema.dbml`.

- [ ] Add `GameQuestionSnapshot` and `GameChoiceSnapshot` entities owned by a `GameSession`. Store source IDs for traceability plus immutable order, text, normalized media reference, time limit, points, and correct-answer state. Snapshot the quiz title on the game.
- [ ] Make `GameSession.CurrentQuestionId` reference the snapshot question, not the mutable authoring question.
- [ ] Make `Answer` reference snapshot question/choice rows. Retain source IDs only as non-authoritative audit metadata if useful.
- [ ] In `CreateGameCommandHandler`, copy the published quiz and choices into the game within the same transaction. Reject an invalid/unpublished source before creating a partial game.
- [ ] Convert all gameplay reads—activation, host/player reconnect state, submission validation, results, leaderboard, and completed-game queries—to snapshots.
- [ ] Create a new forward-only migration. Backfill existing game sessions from the best currently available quiz rows and remap current-question/answer references. Document that already-mutated historical content cannot be reconstructed perfectly.
- [ ] Update `backend/projectSchema.dbml` and related architecture/protocol docs.
- [ ] Keep quiz editing/deleting behavior independent of finished-game snapshots; no FK from historical data may make legitimate authoring edits throw an unhandled database error.

**Acceptance:** create a game, edit/delete the source quiz after the allowed lifecycle point, and prove the game’s question text, choices, correctness, results, and leaderboard remain unchanged. Migration succeeds on a copy of representative existing data and rolls forward without altering old migrations.

## Phase 2 — Make game concurrency and real-time authorization correct

### Task 2.1 — Linearize join, start, and capacity decisions

**Files:** `JoinGameCommandHandler.cs`, `StartGameCommandHandler.cs`, `CreateGameCommandHandler.cs`, `GameSession` and configuration, shared errors/options, relevant controller mappings.

- [ ] Add validated configuration for maximum participants, default 500.
- [ ] Serialize the small state/capacity critical section using a PostgreSQL transaction plus a row lock or transaction-scoped advisory lock shared by join and start. Do not use an in-memory lock.
- [ ] Within the lock, recheck lobby state, nickname uniqueness/normalization, non-removed seat count, and capacity before inserting.
- [ ] Start must acquire the same lock and transition only after the final join decision, preventing a participant from being inserted after the game starts.
- [ ] Map full capacity and closed lobby to stable ProblemDetails/error codes.
- [ ] Keep the transaction short. Do not perform network calls or notification fan-out while holding the lock.

**Acceptance:** concurrent join/start testing never admits more than 500, never admits after start wins the lock, does not create duplicate normalized nicknames, and completes the 500-player join target without lock timeout or excessive p95.

### Task 2.2 — Replace per-answer aggregates with durable counters

**Files:** `GameSession`, configuration, `QuestionActivation.cs`, `SubmitAnswerCommandHandler.cs`, `TryAutoEndQuestionCommandHandler.cs`, remove/disconnect semantics, `GetHostGameStateQueryHandler.cs`, telemetry, new migration, DBML.

- [ ] Add `CurrentQuestionEligibleCount` and `CurrentQuestionAnsweredCount` (names may follow repository convention) to the session; initialize/reset them atomically when a snapshot question activates.
- [ ] Every accepted answer, including zero-point/wrong answers, must insert the answer and conditionally increment the answered counter in one transaction. Preserve the unique-answer constraint as the final authority.
- [ ] Use a conditional SQL update/concurrency token so simultaneous answers cannot lose increments. A duplicate must not increment the counter.
- [ ] Replace per-answer `COUNT` and anti-join queries with a cheap comparison/conditional state transition. Only one caller may win auto-end and emit the transition.
- [ ] Make host state read the stored counters instead of repeatedly counting answer rows.
- [ ] Apply the Task 1.1 policy to removals/disconnects without introducing negative or inconsistent counters.
- [ ] Add invariants/telemetry for `0 <= answered <= eligible <= capacity` and log identifiers, never tokens.
- [ ] Define numeric limits. **Recommended:** use `long`/PostgreSQL `bigint` for cumulative score and counters where appropriate, use checked scoring arithmetic, and impose reasonable question-point/question-count validation instead of allowing `int.MaxValue` accumulation.

**Acceptance:** the database verification query agrees with stored counters after normal, wrong, duplicate, reconnect, removal, and deadline cases. The final 500-answer scenario meets Phase 8 latency and invariant gates.

### Task 2.3 — Authorize a player before SignalR group membership

**Files:** `backend/src/Kahoot.Api/Realtime/GameHub.cs`, `GameGroups.cs`, attach/detach commands and handlers, `IGameClient.cs`, contracts/errors.

- [ ] Change attach to carry and predicate on participant ID, game ID/pin, session-token hash, not-removed state, and a non-finished game state as required by the protocol.
- [ ] Atomically update the connection only when every predicate matches; return failure when zero rows are affected.
- [ ] In the hub, call the database attach/authorization first. Only then set `Context.Items` and add the connection to the player group.
- [ ] On any later failure, remove group membership, clear context state, and detach only the matching connection ID.
- [ ] Ensure a stale disconnect cannot null a newer reconnect’s connection ID.
- [ ] Keep host group authorization equally explicit and game-scoped.

**Acceptance:** invalid, removed, stale-token, wrong-game, and finished-game connections never enter a player group and never receive a question payload. A stale disconnect cannot evict a new connection.

### Task 2.4 — Evict removed players and fix authoritative presence

**Files:** remove command/handler, `GamesController.cs`, `GameNotifier.cs`, hub/client contracts, reconnect handler, `GameContracts.cs`, frontend realtime events/hooks/pages.

- [ ] When removing a participant, atomically capture/null the current connection ID and mark removal. Then use `IHubContext.Groups.RemoveFromGroupAsync` to evict that exact connection before or as part of the removal fan-out.
- [ ] Guard the reconnect race: do not evict a newer connection that replaced the captured ID.
- [ ] Add/rename an event carrying the absolute authoritative participant count and the reason/version if needed. Include the current count in player reconnect state.
- [ ] Stop broadcasting `ParticipantJoined` for a reconnect and stop client-side `+1/-1` arithmetic.
- [ ] Make kicked clients clear session state and stop reconnect attempts, while server authorization remains the security boundary.

**Acceptance:** repeated disconnect/reconnect does not change the count; host removal immediately blocks further answers and question delivery; all clients converge to the same count after reconnect.

### Task 2.5 — Separate committed state from best-effort notification

**Files:** `GameNotifier.cs`, game controllers, hub auto-end path, request logging/telemetry, frontend resync logic.

- [ ] Return or record notification outcomes without throwing a committed mutation back to the client as a failed command.
- [ ] Emit structured error metrics/logs with game/event/correlation identifiers and no secrets.
- [ ] Preserve unexpected pre-commit failures through the centralized exception handler.
- [ ] Ensure answer acknowledgment reflects durable acceptance even if result/auto-end fan-out fails.
- [ ] On reconnect or a detected sequence gap, clients fetch authoritative state. If event ordering requires it, add a monotonic game-state version and ignore older updates.

**Acceptance:** deliberately stop or fault notification delivery after a successful database commit; the API/hub reports the committed outcome, emits an operational signal, and a reconnect reconstructs correct state without duplicate scoring.

## Phase 3 — Repair authentication, configuration, and API defenses

### Task 3.1 — Make refresh-token rotation atomic and family-scoped

**Files:** `RefreshToken` entity/configuration, refresh/login/logout handlers, auth contracts/errors, `AuthController.cs`, `AuthTokenFactory`, new migration and DBML.

- [ ] Add an indexed token-family identifier and the metadata needed for rotation/revocation. Store only token hashes.
- [ ] In one database transaction, conditionally revoke the presented active token and insert its replacement. Concurrent losers must not leave two valid children.
- [ ] Reuse detection revokes only that family, not every active token owned by the host.
- [ ] Add a narrow refresh-race grace window (for example 10 seconds): a recently replaced token produces a stable retryable `Auth.RefreshRace` result instead of treating a normal multi-tab race as theft. Do not issue another child from the old token.
- [ ] Logout revokes the current family and clears the browser cookie.
- [ ] Add bounded, indexed cleanup of expired/revoked token rows; do not scan/delete unbounded data at request time.

**Acceptance:** concurrent refresh attempts yield one valid successor; family reuse invalidates that family only; other device families remain valid; no raw token is persisted or logged.

### Task 3.2 — Move browser auth to secure boundaries

**Files:** auth controller/contracts, `Program.cs`, frontend `authService.ts`, `axiosClient.ts`, `AuthProvider.tsx`, `useAuth.ts`, `useTokenRefresh.ts`, protected routing, SignalR connection code, deployment env/templates/docs, k6 REST helpers.

- [ ] Set refresh tokens in a `Secure`, `HttpOnly`, appropriately scoped `SameSite` cookie; never return the refresh token in JSON.
- [ ] Keep the host access token in memory only. Remove migration/fallback reads from `localStorage` after a deliberate one-time logout/cleanup path.
- [ ] Protect cookie-authenticated refresh/logout with SameSite plus an exact Origin check and a required custom CSRF header, or the repository’s ASP.NET antiforgery mechanism. Do not use wildcard credentialed CORS.
- [ ] Bootstrap an existing login on page load through the refresh cookie.
- [ ] Implement one single-flight refresh coordinator in Axios: queue concurrent 401s, retry a request at most once, and do not redirect recursively for login/refresh failures.
- [ ] Coordinate tabs with `BroadcastChannel` or the refresh race contract; do not share bearer tokens through persistent storage.
- [ ] Supply the in-memory access token to SignalR and restart/reconnect safely after refresh.
- [ ] Store a player session token at most in `sessionStorage`, not `localStorage`; document the reload/session tradeoff. Continue server-side hashing and authorization.
- [ ] Update k6 helpers to retain `Set-Cookie` and send credentials/CSRF headers without expecting a refresh token body.

**Acceptance:** no host bearer/refresh token remains in Local Storage; cookies are Secure/HttpOnly in production; CSRF attempts from a foreign Origin fail; refresh/login failure cannot create an interceptor loop; two tabs recover from expiry without revoking unrelated sessions.

### Task 3.3 — Add production configuration validation and real health endpoints

**Files:** `Program.cs`, options classes/registration, `appsettings*.json`, Compose health checks, Nginx/blackbox/observability config, docs.

- [ ] Validate JWT signing strength, connection string, public client origin, host seed/bootstrap inputs, file storage, and observability options on startup in Production.
- [ ] Require an absolute HTTPS `Client__BaseUrl`/public origin in Production. Development localhost belongs only in development settings.
- [ ] If production CORS origins are empty/invalid, fail startup; never silently fall back to localhost.
- [ ] Configure production `AllowedHosts` explicitly.
- [ ] Split `/health/live` (process liveness only) and `/health/ready` (database plus selected storage readiness). Keep `/health` as a backward-compatible liveness endpoint if the functional contract requires it.
- [ ] Make Docker deployment dependencies, Nginx upstream checks, and external availability checks use readiness; use liveness for restart decisions so a transient database outage does not create a restart cascade.
- [ ] Add short timeouts and safe failure details; public health responses must not expose connection strings or internals.
- [ ] Replace config-only container health checks for OTel Collector/Loki with real liveness/readiness endpoints or a minimal pinned health client. Do not add a shell to a distroless image just for health checks.

**Acceptance:** production startup fails on every missing/invalid required option; live stays healthy during a simulated dependency outage while ready fails; ready recovers automatically when the dependency returns.

### Task 3.4 — Correct proxy trust, rate limits, and API failure contracts

**Files:** `nginx/default.conf`, `Program.cs`, `RateLimitingExtensions.cs`, `GlobalExceptionHandler.cs`, API error mapping, deployment docs.

- [ ] At Nginx, overwrite client-provided forwarding headers: derive `X-Forwarded-For` from `$remote_addr`, set `X-Forwarded-Proto`, and do not append an untrusted inbound chain.
- [ ] In ASP.NET, set a finite forward limit and trust only the Compose proxy network/known proxy strategy; validate the actual container subnet behavior.
- [ ] Reconcile code and documentation. Current code is far looser than the documented global/auth/join policies.
- [ ] Protect login at approximately 10 attempts per 5 minutes per source plus normalized username where feasible. Design join limits for a 500-player classroom behind one NAT; do not apply a tiny per-IP limit that blocks the event. Add explicit per-game capacity and reasonable global abuse controls.
- [ ] Avoid accidentally double-limiting WebSocket negotiation and join bursts. Validate long-lived upgrade behavior.
- [ ] Return stable ProblemDetails, `Retry-After`, and correlation ID for 429s.

**Acceptance:** spoofed forwarding headers cannot change the limiter identity; authentication brute-force traffic is limited; a legitimate 500-player NAT test can join/connect; 429 behavior is documented and observable.

### Task 3.5 — Make host bootstrap a one-time secure operation

**Files:** `DatabaseSeederHostedService.cs`, `HostSeedOptions.cs`, env templates, deployment/runbook docs.

- [ ] Make bootstrap explicitly enabled and fail closed when enabled without strong inputs.
- [ ] On first deployment, verify creation, then remove/disable bootstrap credentials from the runtime environment.
- [ ] On subsequent boots, do not require or re-log bootstrap material.
- [ ] Document a secure host-password rotation/recovery flow that revokes affected refresh families.

**Acceptance:** a normal restarted production container does not retain or need the seed password; bootstrap cannot overwrite an existing host unexpectedly.

## Phase 4 — Bound and secure media handling

### Task 4.1 — Restrict media references and preserve choice images

**Files:** quiz request/response contracts and validators, question mapping/handlers, `ImageUploadService.cs`, `UploadsController.cs`, frontend `media.ts`, `QuestionFormDialog.tsx`, `ChoiceEditorRow.tsx`, `ImageUploadField.tsx`, CSP config, functional docs.

- [ ] Accept only a server-generated media asset identifier or canonical same-origin `/uploads/<opaque-id>.<allowed-extension>` path. Reject absolute external URLs, protocol-relative values, `data:`, `blob:`, traversal, and unrecognized paths at the API boundary.
- [ ] Make frontend resolution enforce the same allowlist; never turn arbitrary quiz content into a third-party request.
- [ ] Fix question editing so unchanged existing choice images are preserved instead of mapped to `null`.
- [ ] If choice images remain a requirement, provide the existing upload UX for choices and allow text and/or image according to the resolved contract. If removed from scope by the owner, remove the contract field consistently.
- [ ] Fix `QuestionFormDialog`’s state updater so it never mutates an existing choice object. Give draft choices stable client IDs and stop using the array index as the React key.

**Acceptance:** external/traversal URLs fail validation; an edit that changes only question text leaves every existing choice image unchanged; add/reorder/delete operations preserve the correct draft state.

### Task 4.2 — Decode, normalize, store, and retire uploads safely

**Files:** storage interfaces/options/implementation, image signature/upload service, persistence model if using assets, Docker/production config, deployment docs, new migration/DBML where needed.

- [ ] Add a maintained image decoder compatible with pinned .NET only if required; review its license and vulnerability status.
- [ ] Enforce encoded byte limit, decoded pixel/dimension limit, content signature, and allowed output formats. Strip metadata and normalize/re-encode server-side. Reject unsafe/animated GIFs or deliberately process a safe first frame.
- [ ] Generate opaque names server-side; never trust path/extension from the client.
- [ ] Send immutable cache headers only for content-addressed/opaque immutable assets and `nosniff` with an exact content type.
- [ ] Implement the storage choice recorded with G3. **Recommended:** Azure Blob private container accessed using the VM’s system-assigned managed identity and least-privilege Blob Data RBAC. Proxy controlled reads through the same origin or use a deliberately bounded delivery mechanism.
- [ ] Track asset ownership/reference status or implement a safe, bounded orphan cleanup after quiz update/delete. Never delete an object still referenced by another row/snapshot.
- [ ] If local storage is temporarily retained, define disk quota, free-space alerts, off-host backup, restore, and cleanup before release.

**Acceptance:** oversized dimensions, malformed/polyglot content, forbidden types, and path tricks fail safely; normalized output contains no source metadata; referenced assets survive edits/snapshots; orphan cleanup is bounded and observable.

## Phase 5 — Make the frontend resilient, accessible, and reproducible

### Task 5.1 — Consume authoritative real-time state and surface degraded operation

**Files:** `usePlayerGame.ts`, `useHostGame.ts`, `useGameHubConnection.ts`, realtime event types, player/host pages and count components, connection banner.

- [ ] Replace local participant-count deltas and initial `1` with server-provided absolute count/version.
- [ ] Remove state-setting from effect cleanup where it can update an unmounting component; keep resource cleanup idempotent.
- [ ] Replace overlapping `setInterval` polling with non-overlapping recursive scheduling or cancellation-aware requests; abort on unmount/state transition.
- [ ] Do not silently swallow repeated state/results/leaderboard failures. Surface a stale/degraded indicator, retain safe last-known state, apply bounded backoff, and allow retry.
- [ ] Route all refresh behavior through the single auth coordinator. Handle visibility refresh promises explicitly.
- [ ] Ensure endpoint-specific timeout policy: normal calls can retain a short timeout, but image uploads need a justified larger/cancellable timeout.

**Acceptance:** forced disconnects/reconnects converge without count drift; slow responses do not create overlapping polls; repeated API failure is visible and recoverable; no unhandled promise rejection occurs on visibility changes.

### Task 5.2 — Apply confirmed accessibility and privacy fixes

**Files:** `main.tsx`, motion-using components, `GamePinDisplay.tsx`, theme/global styles, logging sites, ErrorBoundary.

- [ ] Configure Framer Motion to honor the user’s reduced-motion preference (`MotionConfig`/`useReducedMotion`) and provide non-motion alternatives; CSS media queries alone do not cover JS animations.
- [ ] Use `LazyMotion`/`m` or targeted imports where it materially reduces the three full Framer Motion imports without obscuring code.
- [ ] Make the game-pin copy control a semantic keyboard-operable button (`ButtonBase` or button), with focus treatment and accessible status feedback.
- [ ] Remove production `console.info`/debug logging that exposes game pins, join URLs, or session flow. Restrict ErrorBoundary console detail to development or send sanitized telemetry.
- [ ] Self-host the Inter font (licensed files checked into an appropriate asset location) or use a system stack; remove runtime Google Fonts requests to support a strict privacy/CSP posture.

**Acceptance:** keyboard-only and reduced-motion smoke checks pass for login, join, lobby, question, results, and host controls; production console contains no game PIN/token/user credential detail; no third-party font request occurs.

### Task 5.3 — Pin toolchains and remove only verified dead code

**Files:** root `global.json`, `.config/dotnet-tools.json`, `frontend/package.json`, `.nvmrc` or `.node-version`, lockfile, verified dead files/dependencies.

- [ ] Pin the supported .NET 10 SDK feature band and local `dotnet-ef` 10.0.12 (or exactly the EF runtime package version after inspection).
- [ ] Require Node 22 with a minimum compatible patch (at least 22.12 for the current Rolldown plugin), record it in `package.json` engines and one standard version file, and use the same exact major/minor in CI/Docker.
- [ ] Verify repository-wide references before removing suspected dead items such as `HostDashboard.tsx`, `useImageUpload.ts`, `App.css`, unused validation constants/barrels, or direct `zod`. Do not remove anything dynamically routed/imported.
- [ ] If verified, move `@types/canvas-confetti` to `devDependencies` and remove unused direct runtime dependencies. Regenerate only the lockfile through the package manager.
- [ ] Add a pragmatic production bundle budget using current clean-build output as a baseline; investigate the approximately 227 kB `useServerCountdown` chunk before setting the cap, because chunk naming may reflect shared imports rather than that hook alone.

**Acceptance:** a clean environment using the pinned versions passes restore/build; no removed item has an import/reference; production bundle stays within the recorded budget.

## Phase 6 — Terminate TLS and harden the Compose deployment

### Task 6.1 — Implement real HTTPS at the public edge

**Files:** `nginx/nginx.conf`, `nginx/default.conf`, `docker-compose.prod.yml`, env templates, frontend Nginx config/Dockerfile if separate, Azure deployment and observability docs.

- [ ] Parameterize the canonical host from G1 without unsafe runtime template substitution.
- [ ] Listen on 80 only to serve ACME validation where needed and redirect all other traffic to the canonical HTTPS origin.
- [ ] Add a real 443 TLS server with mounted certificate/key paths, TLS 1.2/1.3, safe session settings, OCSP behavior appropriate to the issuer, and automated renewal/reload.
- [ ] Prove renewal in staging. If using IP certificates, schedule renewal frequently enough for short-lived certificates and alert well before expiry.
- [ ] Enable HSTS only after HTTPS and renewal are proven. Then set Grafana/auth cookies Secure.
- [ ] Preserve WebSocket upgrade headers and long-running SignalR behavior over `wss://`.
- [ ] Remove every hardcoded `http://<public-ip>` production URL from Compose, appsettings, frontend config, dashboards, blackbox targets, and docs.

**Acceptance:** HTTP redirects to HTTPS; certificate chain/hostname is valid; TLS 1.0/1.1 fail; browser/API/SignalR/Grafana-selected access work via HTTPS; renewal dry-run and expiry alert work.

### Task 6.2 — Add consistent security headers and request bounds

**Files:** outer and frontend Nginx configs, media rules, backend headers if still needed.

- [ ] Apply headers at the effective public edge and avoid conflicting duplicates: CSP, `X-Content-Type-Options`, `Referrer-Policy`, clickjacking protection via `frame-ancestors`, a narrow `Permissions-Policy`, and HSTS after proof.
- [ ] After same-origin media and self-hosted fonts, start CSP with `default-src 'self'`, `object-src 'none'`, `base-uri 'self'`, `frame-ancestors 'none'`, `form-action 'self'`, narrow `img-src`, and `connect-src 'self' wss:`. MUI Emotion may require `style-src 'self' 'unsafe-inline'` unless a nonce architecture is deliberately implemented; never add `unsafe-eval` just to silence a violation.
- [ ] Set per-location body-size limits: small JSON endpoints, explicit image upload maximum, and no unbounded buffering.
- [ ] Increase Nginx `worker_connections`/nofile headroom beyond 1024 because 500 proxied WebSockets consume connections on both sides; start at 4096 or a measured higher value and verify on the VM.
- [ ] Add sensible proxy connect/read/send timeouts without prematurely killing healthy games.

**Acceptance:** CSP report/dev testing shows only intended sources; 500 WebSockets plus HTTP traffic do not exhaust edge connections; oversized requests return controlled 413 responses.

### Task 6.3 — Enforce container least privilege and reliable health

**Files:** `backend/Dockerfile`, `frontend/Dockerfile`, Compose files, service configs, documentation.

- [ ] Use the official .NET non-root `app` user for the API and an unprivileged Nginx pattern/image for the frontend/edge where compatible.
- [ ] Add `no-new-privileges`, drop capabilities, read-only root filesystems, `tmpfs`, PID limits, and graceful stop periods service-by-service after verifying required writes/ports.
- [ ] Do not blindly apply app-container restrictions to PostgreSQL, exporters, or cAdvisor. Document narrow exceptions and remove Jaeger `user: "0:0"` by initializing volume ownership or using the image’s supported non-root UID if possible.
- [ ] Tune resource reservations/limits from measured load on the 2-vCPU/8-GiB VM. Reserve database/admin/exporter connection headroom; configure the application pool explicitly (a measured starting cap such as 64, not the Npgsql default 100) and align PostgreSQL `max_connections`.
- [ ] Add connect/command timeouts and pool-wait telemetry. Do not solve pool pressure by raising every limit without evidence.
- [ ] Pin every production base/third-party image by reviewed immutable digest while retaining a human-readable version comment/document. Add automated update review instead of floating `alpine`, `18-alpine`, or broad `10.0` tags.

**Acceptance:** app/edge containers run non-root, write only to declared locations, pass real health checks, shut down gracefully, and remain within CPU/memory/connection budgets during Phase 8.

### Task 6.4 — Remove public observability administration

**Files:** `docker-compose.prod.yml`, Nginx config, Grafana settings, observability docs/validator.

- [ ] Remove the public `/grafana` proxy unless the owner explicitly selects a separately authenticated private access layer.
- [ ] Bind Grafana to `127.0.0.1:3000` on the VM or only the internal network and document Bastion/JIT/SSH-tunnel access. Update the “no public ports” validator to allow loopback-only bindings but reject `0.0.0.0`/public exposure.
- [ ] Keep Prometheus, Loki, Jaeger, exporters, Collector receivers, PostgreSQL, and Docker/cAdvisor endpoints off public host interfaces.
- [ ] Restrict Grafana anonymous access, rotate admin credentials, set secure cookies, and document role/user lifecycle.

**Acceptance:** external port scanning exposes only approved 80/443 and the owner-approved administrative entry; telemetry APIs and Grafana cannot be reached directly from the internet.

## Phase 7 — Establish Azure durability, delivery, and operations

### Task 7.1 — Implement the selected database/media durability tier

**Files:** Compose/configuration as applicable, storage implementation, `docs/azure-vm-deployment.md`, a concise backup/restore runbook, monitoring config.

- [ ] Implement G3 rather than merely recommending it.
- [ ] For managed PostgreSQL, require TLS, private networking/firewall restrictions, least-privilege application/migration/monitor roles, explicit connection budgets, PITR retention, and HA if required by RTO.
- [ ] For VM PostgreSQL, schedule encrypted PostgreSQL custom-format/logical backups to off-host Blob using managed identity; retain Azure VM Backup as a separate disaster-recovery layer. Define daily/weekly/monthly retention and prune safely.
- [ ] Back up media/object metadata consistently. Document the recovery ordering between database and media.
- [ ] Alert on backup failure, stale backup, storage growth, and restore failure.
- [ ] Perform a restore into an isolated target, run integrity queries and application smoke checks, record duration/data-loss window, then dispose of the isolated target with owner approval.

**Acceptance:** the measured restore meets recorded RPO/RTO; the runbook identifies owners, schedules, retention, encryption, integrity checks, and escalation. Delete/restore drills never touch production in place.

### Task 7.2 — Harden Azure VM and administrative access

**Files:** Azure deployment/security documentation and owner-managed Azure configuration.

- [ ] Give the VM a system-assigned managed identity and grant only required Blob Data roles at the narrowest scope.
- [ ] Limit NSG inbound traffic to 80/443 publicly. Remove open SSH; use Azure Bastion or Defender for Cloud JIT with restricted source/time/port. Disable password SSH and rotate keys.
- [ ] Configure host firewall consistently with NSG and Docker behavior; verify Docker rules do not bypass intended filtering.
- [ ] Enable Azure Update Manager/patch scheduling, disk encryption/default platform encryption as appropriate, boot diagnostics, and VM health alerts.
- [ ] Make clock synchronization, log retention, disk free-space monitoring, and reboot maintenance explicit.

**Acceptance:** Azure effective-security-rule review and an external scan match the documented exposure; administrative access is time/source restricted and auditable.

### Task 7.3 — Build immutable CI/CD and rollback

**Files:** new `.github/workflows/ci.yml`, optional dependency-update config, Dockerfiles/Compose, deployment docs/scripts. Do not add application test files before G5.

- [ ] Pin CI action SHAs and tool versions. Run backend restore/build, vulnerable-package scan, local EF pending-model check, frontend clean install/format/lint/build/audit, hermetic Compose render, secret scan, observability validation, `promtool` rule tests, Docker builds, and a pinned image vulnerability scan.
- [ ] Build once in CI, label images with commit SHA/source metadata, push to the owner-selected registry, sign/attest if available, and deploy by digest. Do not build from `git pull` on the live VM.
- [ ] Separate pull-request validation from an owner-approved production environment. Production deploy requires all release gates, backup confirmation, and immutable image/config references.
- [ ] Generate/review the EF migration script before deploy. With one API replica, the current migration hosted service may remain only after backup and migration lock behavior are verified; document expand/contract compatibility and recovery. Do not silently roll back schema by downgrading an image.
- [ ] Deploy with `docker compose up -d --wait`, verify readiness and smoke paths, then switch/continue traffic. Retain the last known-good image/config digest and a rollback procedure compatible with the new schema.
- [ ] Add automated dependency update PRs with human review; never auto-deploy them.

**Acceptance:** a clean CI run from the release commit produces traceable images; a no-op deployment is reproducible; a controlled application rollback succeeds without data loss; production contains no dirty working-tree build.

### Task 7.4 — Add off-stack detection and actionable alert delivery

**Files:** Azure Monitor configuration/runbook, Grafana provisioning/docs, blackbox targets, alert docs.

- [ ] Use Azure Monitor **Standard** availability tests for public HTTPS landing and `/health/ready`, connected to the G4 Action Group. Do not start a new URL-ping test because that feature retires on 2026-09-30.
- [ ] Route critical Grafana alerts to an owner-approved external contact point or mirror critical infrastructure/app alerts into Azure Monitor so failure of the local Grafana stack is still detectable.
- [ ] Add alerts for certificate expiry/renewal failure, backup age/failure, VM/disk pressure, container restarts/OOM, readiness failure, PostgreSQL connections/pool waits, SignalR connections, answer latency/error/invariant counters, and telemetry-pipeline silence.
- [ ] Every alert needs severity, owner, actionable annotation, dashboard link, runbook, and tested recovery/notification.

**Acceptance:** a controlled failure reaches the external contact within the documented time even when Grafana is unavailable; resolution notifications work; no alert payload contains secrets/tokens.

## Phase 8 — Make performance proof honest and pass the release workload

### Task 8.1 — Repair load-test orchestration and thresholds

**Files:** `load-tests/run-all.js`, `run.sh`, `config/environments.js`, `config/thresholds.js`, scenario/helper files, verifier, README, result-handling rules.

- [ ] Remove all hardcoded credential defaults and remote-to-localhost authentication fallback.
- [ ] Require explicit `BASE_URL`, SignalR URL derived/validated from it, host credentials supplied outside Git, target environment, and a typed production-load confirmation such as `ALLOW_PROD_LOAD_TEST`. Print only sanitized target/parameters.
- [ ] Make scenario names match actual workloads. Keep 500 as the production acceptance target and make 750 a clearly separate exploratory stress test.
- [ ] Align answer thresholds with the NFR: answer processing p95 under 500 ms and p99 under 2 s, with exact documented measurement semantics. Do not leave the current looser p95 under 1 s helper.
- [ ] Replace thresholds such as `count >= 0` and permissive confirmation fractions with required joined/connected/submitted/accepted counts. A scenario with zero samples must fail.
- [ ] Require zero cross-game/isolation violations, zero duplicate scoring, correct database totals/counters, and no hidden HTTP/SignalR setup failure.
- [ ] Fail the suite on a skipped/missing summary, wrong target, any container restart/OOM, or incomplete database verification.
- [ ] Do not track volatile result JSON/log files. Publish an immutable CI/release artifact containing git SHA, timestamp, sanitized environment, VM SKU, image digests, config hash, scenario parameters, raw summaries, and pass/fail.

**Acceptance:** intentionally point auth at an invalid target, set zero submissions, and create an isolation violation; each must make the suite fail. A scenario can never pass with `ans_p95=0` because nothing ran.

### Task 8.2 — Execute staged performance and failure validation

Run only against a disposable, production-shaped environment with owner authorization. Reset through safe migrations/fixtures; never erase a shared database.

- [ ] Smoke: host login/refresh/logout, quiz create/edit/publish, image upload, game create/join/start, answer/results/leaderboard/end, reconnect, kick, and historical snapshot integrity.
- [ ] 500-player join: all expected joins succeed within threshold; capacity rejects 501 deterministically.
- [ ] 500 concurrent SignalR connections: stable through the test with no unexpected disconnect/reconnect storm.
- [ ] Question broadcast: all 500 correct game clients receive exactly the intended event; zero cross-game payloads.
- [ ] 500-answer burst in approximately one second: all valid answers durably accepted once; p95 < 500 ms, p99 < 2 s, invariants zero.
- [ ] Duplicate-answer race: one accepted score per participant/question and counters match rows.
- [ ] Reconnect/removal race: stale disconnect cannot evict the replacement; removed player receives/submits nothing.
- [ ] Ten simultaneous games of 50: no group or state isolation violation.
- [ ] 200-player 10-minute endurance: no memory/connection growth trend, restart, OOM, pool exhaustion, data inconsistency, or material latency degradation.
- [ ] Controlled failures: notification interruption, database transient outage, OTel/Grafana outage, container restart, and certificate/backup alert paths. Verify live/ready behavior and recovery.
- [ ] Watch VM/container, .NET runtime, SignalR, Npgsql pool, PostgreSQL, Nginx, and telemetry metrics throughout. Stop on unsafe saturation rather than damaging the host.

**Acceptance:** one complete run at the exact release commit passes every scenario and produces the immutable artifact. A passing isolated answer-burst or a prose status claim is insufficient.

## Phase 9 — Reconcile documentation and perform final release review

### Task 9.1 — Make documentation describe the implemented system only

**Files:** all active docs, env examples, Compose comments, observability docs, realtime protocol, DBML, load README.

- [ ] Remove stale Railway/Vercel or HTTP/public-Grafana instructions unless clearly archived as non-production history.
- [ ] Use one production environment-file convention consistently. Document permissions and provisioning without including values.
- [ ] Correct PostgreSQL role/database commands to use configured names rather than assuming `postgres` or a nonexistent `kahoot_user`.
- [ ] Replace “zero loss” claims with measured RPO/RTO and restore evidence.
- [ ] Document canonical HTTPS URLs, private Grafana access, certificate renewal, backup/restore, deployment/rollback, secret rotation, bootstrap disablement, load-test safety, and on-call alerts.
- [ ] Update architecture and protocol docs for snapshots, counters, token cookies/families, absolute participant counts, readiness, and notification recovery.

**Acceptance:** an operator using only the runbooks can provision, deploy, verify, back up, restore, rotate, observe, and roll back without guessing or exposing a secret.

### Task 9.2 — Final diff and release gate

- [ ] Inspect `git status`, `git diff --check`, the complete task diff, and generated migration/lockfile changes. Verify every changed line is intentional and no user change was overwritten.
- [ ] Re-run the verification matrix below from a clean environment.
- [ ] Confirm every critical/high finding is fixed or has an explicit owner-accepted exception with expiry and compensating control.
- [ ] Confirm the credential incident checklist is complete; code cleanup alone does not close SEC-01.
- [ ] Record exact commit SHA, image digests, schema version, config hash, backup ID/time, restore-drill evidence, load artifact, and approver.
- [ ] Only then label the release production-ready.

---

# Verification matrix

Use repository scripts where they already exist. Adjust paths only after inspecting actual project conventions.

## Static/backend

```bash
dotnet tool restore
dotnet restore backend/Kahoot.slnx --locked-mode
dotnet build backend/Kahoot.slnx --no-restore
dotnet list backend/Kahoot.slnx package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes --project backend/src/Kahoot.Infrastructure --startup-project backend/src/Kahoot.Api
git diff --check
```

If a locked restore is introduced, intentionally create/update lock files once and review them; do not add `--locked-mode` before the repository can satisfy it.

## Frontend

```bash
npm --prefix frontend ci
npm --prefix frontend run format:check
npm --prefix frontend run lint
npm --prefix frontend run build
npm --prefix frontend audit --omit=dev --audit-level=moderate
```

## Configuration/containers/observability

```bash
bash observability/scripts/validate-config.sh
docker compose --env-file <owner-provisioned-validation-env> -f docker-compose.yml -f docker-compose.prod.yml config --quiet
bash observability/scripts/validate-observability.sh
```

Also run the existing Prometheus rule tests, pinned container-image scan, secret scan, health/readiness checks, external-port scan, certificate validation/renewal dry-run, backup/restore drill, and the corrected k6 suite. Never substitute a developer `.env` implicitly during validation.

## Behavior cases that must be demonstrated

- Invalid/removed/stale player cannot join a SignalR group or receive an event.
- Start/join race is linearizable and capacity is never exceeded.
- Duplicate answer cannot increment score/counter twice.
- All-wrong/zero-point answers still advance the answer counter and auto-end correctly.
- Quiz edits/deletes cannot change a created game’s snapshots/history.
- Notification failure after commit does not return a false rollback.
- Concurrent refresh creates one successor; theft revokes one family; normal multi-tab race recovers.
- Foreign-origin cookie requests fail CSRF checks.
- External media URLs/path tricks/oversized decoded images fail.
- Liveness and readiness diverge correctly during dependency outage.
- 500-player workload and 200-player endurance meet Phase 8 without invariant violations.

# Definition of done

The work is complete only when all of the following are true:

- [ ] All decision gates are recorded and every dependent implementation matches them.
- [ ] Exposed credentials/SSH key are revoked or rotated, current tracked plaintext is removed, scanning is effective, and any approved history cleanup is completed and communicated.
- [ ] Production traffic is HTTPS/WSS only with automated, monitored renewal and secure cookies.
- [ ] Authentication rotation, storage boundaries, CSRF, rate limiting, proxy trust, and bootstrap lifecycle pass their negative cases.
- [ ] Immutable game snapshots, atomic counters, capacity locks, hub authorization, removals, and presence semantics pass concurrency/behavior verification.
- [ ] Database/media backups are off-host and an isolated restore meets documented RPO/RTO.
- [ ] Containers are pinned, least-privileged, health-checked, resource-bounded, and deployed from immutable CI artifacts with a tested rollback.
- [ ] Grafana/telemetry endpoints are private and an off-stack alert path has been exercised.
- [ ] Clean backend/frontend/config/security/image checks pass with pinned toolchains.
- [ ] The complete corrected load suite passes at the release commit; no zero-sample or partial run can report success.
- [ ] Documentation, env examples, DBML, migration state, dashboards/alerts, and runbooks match reality.
- [ ] The final diff contains only intentional scoped changes and preserves pre-existing user work.

# Authoritative implementation references

Use current official documentation and the versions pinned in the repository:

- ASP.NET Core security/data protection/health/forwarded headers: `https://learn.microsoft.com/aspnet/core/`
- Azure Blob authorization with Microsoft Entra ID/managed identity: `https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory`
- Azure VM backup/recovery: `https://learn.microsoft.com/azure/virtual-machines/backup-recovery`
- Azure VM/disk backup and disaster recovery: `https://learn.microsoft.com/azure/virtual-machines/backup-and-disaster-recovery-for-azure-iaas-disks`
- Azure Database for PostgreSQL business continuity/PITR/HA: `https://learn.microsoft.com/azure/postgresql/backup-restore/concepts-business-continuity`
- Azure PostgreSQL hosting choices: `https://learn.microsoft.com/azure/postgresql/configure-maintain/overview-postgres-choose-server-options`
- Azure JIT VM access: `https://learn.microsoft.com/azure/defender-for-cloud/enable-just-in-time-access`
- Secure VM administration: `https://learn.microsoft.com/azure/networking/design-guide/developer-admin-access`
- Azure Update Manager: `https://learn.microsoft.com/azure/update-manager/overview`
- Azure Monitor availability tests: `https://learn.microsoft.com/azure/azure-monitor/app/availability`
- Let's Encrypt IP-certificate general availability: `https://letsencrypt.org/2026/01/15/6day-and-ip-general-availability`
- Certbot short-lived/IP certificate support: `https://letsencrypt.org/2026/03/11/shorter-certs-certbot`

# Required Gemini handoff after execution

Return a concise phase-by-phase report containing:

1. Files changed and why.
2. Migrations and compatibility implications.
3. Decision-gate answers and owner approvals used.
4. Credential incident actions completed versus still owner-blocked, without values.
5. Exact verification commands and results.
6. Load-test artifact metadata and measured thresholds.
7. Backup/restore evidence and measured RPO/RTO.
8. Remaining risks, accepted exceptions, expiry dates, and next owner action.

Do not report “production-ready” while any critical gate, credential rotation, restore proof, HTTPS renewal proof, or complete load suite remains unresolved.
