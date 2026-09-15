# Phase 5 Frontend Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make live gameplay converge on durable server-authoritative presence state while adding visible failure recovery, accessibility/privacy fixes, reproducible toolchains, verified dead-code removal, and an enforced production bundle budget.

**Architecture:** Each committed participant presence mutation locks the owning game session, updates participant state, increments `PresenceVersion`, calculates the absolute connected/non-removed count, and commits before returning a broadcastable payload. React hooks accept only contiguous newer versions, resynchronize after reconnects or gaps, and expose stale last-known state through the existing connection banner. Cross-cutting auth, upload, motion, font, toolchain, dead-code, and bundle changes remain within their existing frontend/backend boundaries.

**Tech Stack:** .NET 10.0.1xx, EF Core 10.0.12, PostgreSQL, MediatR, ASP.NET Core SignalR, React 19, TypeScript 6, Vite 8, MUI 9, Motion 13, Axios 1.20, Node 22.13.

**Spec:** `docs/superpowers/specs/2026-09-15-phase-5-frontend-resilience-design.md`

## Global Constraints

- Do not add `.test` or `.spec` files, test projects, test dependencies, unit tests, or integration tests.
- Displayed participant counts come only from server state or realtime payloads.
- Persist each participant state change, `PresenceVersion`, and resulting participant count in one transaction.
- Publish realtime events only after the transaction commits.
- Do not modify existing migrations; add one forward-only migration and synchronize `backend/projectSchema.dbml`.
- Do not add comments to C# or TypeScript source files.
- Use the native system font stack and make no external font request.
- Preserve unrelated dirty-worktree changes.
- Delete a file or dependency only after repository-wide static, route, dynamic-import, and build verification.

---

### Task 1: Pin the Runtime and Tooling Baseline

**Files:**
- Create: `global.json`
- Create: `.config/dotnet-tools.json`
- Create: `frontend/.nvmrc`
- Modify: `frontend/package.json`
- Modify: `frontend/Dockerfile`
- Modify: `backend/Dockerfile`

**Interfaces:**
- Produces: .NET SDK feature band `10.0.1xx`, local `dotnet-ef` `10.0.12`, Node `22.13.0`, and compatible exact Docker build/runtime images.

- [ ] **Step 1: Record the installed and package baselines**

Run:

```bash
dotnet --version
node --version
rg -n 'Microsoft.EntityFrameworkCore.*Version=' backend/src --glob '*.csproj'
```

Expected: installed SDK `10.0.112`, EF packages `10.0.12`, and the current shell may report Node 20 while Docker supplies the required Node 22 line.

- [ ] **Step 2: Add the .NET SDK and local tool manifests**

Create `global.json` with feature-band-compatible latest-patch behavior:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Create `.config/dotnet-tools.json` with `dotnet-ef` `10.0.12` and command `dotnet-ef`.

- [ ] **Step 3: Pin Node and Docker versions**

Add this engine contract to `frontend/package.json`:

```json
"engines": {
  "node": ">=22.13.0 <23",
  "npm": ">=10 <11"
}
```

Write `22.13.0` to `frontend/.nvmrc`. Change the frontend build image to `node:22.13-alpine`. Change backend build/runtime images to the published `mcr.microsoft.com/dotnet/sdk:10.0.302` and `mcr.microsoft.com/dotnet/aspnet:10.0.12` tags while the root SDK policy remains on the 10.0.1xx feature band.

- [ ] **Step 4: Restore the pinned local tool**

Run:

```bash
dotnet tool restore
dotnet tool run dotnet-ef --version
```

Expected: Entity Framework Core command-line tools `10.0.12`.

### Task 2: Persist and Atomically Mutate Authoritative Presence

**Files:**
- Modify: `backend/src/Kahoot.Domain/Games/GameSession.cs`
- Modify: `backend/src/Kahoot.Application/Games/Common/GameContracts.cs`
- Modify: `backend/src/Kahoot.Application/Games/Presence/AttachParticipantConnectionCommand.cs`
- Modify: `backend/src/Kahoot.Application/Games/Presence/AttachParticipantConnectionCommandHandler.cs`
- Modify: `backend/src/Kahoot.Application/Games/Presence/DetachParticipantConnectionCommand.cs`
- Modify: `backend/src/Kahoot.Application/Games/Presence/DetachParticipantConnectionCommandHandler.cs`
- Modify: `backend/src/Kahoot.Application/Games/RemoveParticipant/RemoveParticipantCommand.cs`
- Modify: `backend/src/Kahoot.Application/Games/RemoveParticipant/RemoveParticipantCommandHandler.cs`
- Modify: `backend/src/Kahoot.Application/Games/GetHostGameState/GetHostGameStateQueryHandler.cs`
- Modify: `backend/src/Kahoot.Application/Games/Reconnect/ReconnectParticipantCommandHandler.cs`
- Modify: `backend/src/Kahoot.Infrastructure/Persistence/Configurations/GameSessionConfiguration.cs`
- Modify: `backend/projectSchema.dbml`
- Create: `backend/src/Kahoot.Infrastructure/Persistence/Migrations/20260915110000_AddGameSessionPresenceVersion.cs`
- Create: `backend/src/Kahoot.Infrastructure/Persistence/Migrations/20260915110000_AddGameSessionPresenceVersion.Designer.cs`
- Modify: `backend/src/Kahoot.Infrastructure/Persistence/Migrations/KahootDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `GameSession.PresenceVersion: long`.
- Produces: `ParticipantPresenceResponse(int ParticipantCount, long PresenceVersion, string Reason, GameParticipantResponse Participant)`.
- Produces: `ParticipantPresenceMutationResponse(ParticipantPresenceResponse Presence, bool Changed)`.
- Produces: `AttachParticipantConnectionCommand(Guid GameId, Guid ParticipantId, string ConnectionId, string Reason)` returning `ParticipantPresenceMutationResponse`.
- Produces: `DetachParticipantConnectionCommand(Guid GameId, Guid ParticipantId, string ConnectionId)` returning nullable `ParticipantPresenceMutationResponse`.
- Extends: host/player state responses with `ParticipantCount` and `PresenceVersion`.

- [ ] **Step 1: Add the durable model and explicit contract constants**

Add `public long PresenceVersion { get; set; }` to `GameSession`, configure a default of zero, and add the matching `bigint [not null, default: 0]` DBML column.

Add exact reason values `Joined`, `Reconnected`, `Disconnected`, and `Removed` in `ParticipantPresenceReasons`, plus the presence response records. Extend `HostGameStateResponse` and `PlayerGameStateResponse` with authoritative count/version fields.

- [ ] **Step 2: Make attach atomic and idempotent for the same connection**

In one explicit transaction:

1. Lock the game-session row with `FromSqlInterpolated($"SELECT * FROM game_sessions WHERE id = {command.GameId} FOR UPDATE")`.
2. Load the non-removed participant scoped to that game.
3. If the stored connection already equals the incoming connection, query current authoritative count and return `Changed = false` without incrementing.
4. Otherwise update `ConnectionId`/`LastSeenAt`, increment `game.PresenceVersion`, save, query connected/non-removed count, and commit.
5. Construct the response only from the committed participant and game values.

- [ ] **Step 3: Make detach atomic and stale-disconnect safe**

Use the same game-session-first lock order. Match participant ID, game ID, and exact connection ID. A mismatch returns success with no presence mutation, does not increment the version, and causes no event. A match clears the connection, increments `PresenceVersion`, saves, counts, commits, and returns a `Disconnected` payload.

- [ ] **Step 4: Make removal atomic with presence state**

Authorize the current host, lock the owned game-session row, then load the participant. For a new removal, capture the old connection ID, set removal fields, increment `PresenceVersion`, save, count, and commit. Return both the connection ID and committed `Removed` presence payload. An already removed participant returns no new presence payload.

- [ ] **Step 5: Project authoritative state for host and player**

Host state projects `PresenceVersion` and calculates connected/non-removed `ParticipantCount` from the database. Player reconnect projects `PresenceVersion` and calculates the same count. Keep full participant data only in the host response.

- [ ] **Step 6: Generate and inspect the forward-only migration**

Run:

```bash
dotnet tool run dotnet-ef migrations add AddGameSessionPresenceVersion --project backend/src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj --startup-project backend/src/Kahoot.Api/Kahoot.Api.csproj --output-dir Persistence/Migrations
git diff -- backend/src/Kahoot.Infrastructure/Persistence/Migrations backend/projectSchema.dbml
```

Rename only the newly generated migration files and their generated migration identifier to `20260915110000_AddGameSessionPresenceVersion` so the new migration sorts after the existing `20260915100000_QuestionImagesOnly` migration. Expected: one non-null `bigint` column with default `0`, snapshot update, no edits to earlier migrations, and no unrelated schema changes.

### Task 3: Publish Committed Presence Through SignalR

**Files:**
- Modify: `backend/src/Kahoot.Api/Realtime/IGameClient.cs`
- Modify: `backend/src/Kahoot.Api/Realtime/GameNotifier.cs`
- Modify: `backend/src/Kahoot.Api/Realtime/GameHub.cs`
- Modify: `backend/src/Kahoot.Api/Controllers/GamesController.cs`
- Modify: `docs/realtime-protocol.md`
- Modify: relevant load-test SignalR event name handling only if repository-wide search proves it consumes the replaced events

**Interfaces:**
- Consumes: committed `ParticipantPresenceResponse` from Task 2.
- Produces: `IGameClient.ParticipantPresenceChanged(ParticipantPresenceResponse presence)`.
- Preserves: direct `ParticipantRemoved(Guid participantId)` for the kicked client notification.

- [ ] **Step 1: Replace delta-style broadcast contracts**

Remove general `ParticipantJoined` and `ParticipantLeft` client methods. Add `ParticipantPresenceChanged`. Keep targeted `ParticipantRemoved` so the evicted client can clear its player session.

- [ ] **Step 2: Broadcast only committed payloads**

Change `GameNotifier` to accept a completed presence payload and broadcast it to player and host groups. `ParticipantRemovedAsync` first notifies and evicts the captured connection, then broadcasts the already committed presence payload. No notifier performs database work or synthesizes counts.

- [ ] **Step 3: Connect join and reconnect flows**

Pass `Joined` from `JoinGame` and `Reconnected` from `Reconnect` into the atomic attach command. Broadcast only when `Changed` is true. For reconnect, copy the attach response count/version into the returned `PlayerGameStateResponse`, ensuring the caller receives state including its committed connection.

- [ ] **Step 4: Connect disconnect and removal flows**

Pass the game ID to detach. Broadcast only when the exact connection produced a committed mutation. Update the removal controller to notify only when the command returns a new committed presence payload.

- [ ] **Step 5: Document ordering and recovery semantics**

Update `docs/realtime-protocol.md` with the new payload fields, reasons, version comparison, gap recovery, reconnect behavior, count definition, targeted removal event, and post-commit publication guarantee.

- [ ] **Step 6: Build the backend before frontend contract work**

Run:

```bash
dotnet build backend/Kahoot.slnx -c Release --no-restore
```

Expected: zero errors and zero warnings.

### Task 4: Converge Frontend Presence and Surface Degraded State

**Files:**
- Modify: `frontend/src/realtime/events.ts`
- Modify: `frontend/src/hooks/useGameHubConnection.ts`
- Modify: `frontend/src/hooks/usePlayerGame.ts`
- Modify: `frontend/src/hooks/useHostGame.ts`
- Modify: `frontend/src/api/hostGameService.ts`
- Modify: `frontend/src/components/ConnectionStatusBanner/ConnectionStatusBanner.tsx`
- Modify: `frontend/src/pages/PlayerGamePage/PlayerGamePage.tsx`
- Modify: `frontend/src/pages/HostGamePage/HostGamePage.tsx`

**Interfaces:**
- Consumes: `ParticipantPresenceResponse` and count/version state contracts from Task 3.
- Produces: `DataSyncState` with `status: 'current' | 'stale'`, safe message, and last-success timestamp.
- Produces: `retrySync()` from both live-game hooks.

- [ ] **Step 1: Mirror server presence and state contracts**

Add the four-reason TypeScript union and presence payload. Extend player and host state contracts with `participantCount` and `presenceVersion`. Replace event handlers for joined/left broadcasts with `ParticipantPresenceChanged`.

- [ ] **Step 2: Remove state writes from hub cleanup**

Keep the local cancellation flag and idempotently stop the connection in cleanup, but remove `setConnection(null)` from the cleanup function. Keep connection state changes in start/close/retry paths while guarding callbacks from a cancelled effect.

- [ ] **Step 3: Implement player version convergence**

Store count/version from every successful reconnect response. Ignore event versions at or below the current version. Apply count only for the next contiguous version. On a gap, invoke one coalesced reconnect synchronization; on hub reconnection, synchronize before clearing stale status. Preserve last-known gameplay state on transient failure and expose retry.

- [ ] **Step 4: Implement host version convergence**

Replace the participant map, participant count, and version together from each successful REST state response. Apply participant details only for a contiguous new presence event, but always use its server count. A gap or hub reconnection calls one coalesced `getState` refresh.

- [ ] **Step 5: Replace overlapping polling**

Replace `setInterval` with a timeout scheduled after each `getState` settles. Pass an `AbortSignal` through `hostGameService.getState`. Abort the request and cancel the timeout on unmount or when leaving `QuestionActive`. Use bounded delays of 2, 4, 8, and 15 seconds after consecutive failures.

- [ ] **Step 6: Make results and leaderboard recovery visible**

Use the same abortable request ownership and bounded retry policy for missing question results and leaderboard data. After two consecutive synchronization failures, expose stale status while keeping the last safe state. Manual retry cancels delay, resets the retry counter, and immediately synchronizes.

- [ ] **Step 7: Extend the connection banner and pages**

Add non-blocking stale-state presentation with a warning icon, `role="status"`, last-known-state copy, and a `Sync Now` button. Keep the disconnected transport modal blocking. Wire both pages to their hook's data-sync state and retry operation.

- [ ] **Step 8: Run frontend static checks**

Run:

```bash
npm --prefix frontend run format
npm --prefix frontend run lint
npm --prefix frontend run build
```

Expected: clean TypeScript build and no lint errors.

### Task 5: Centralize Auth Refresh and Make Uploads Cancellable

**Files:**
- Modify: `frontend/src/api/authService.ts`
- Modify: `frontend/src/api/axiosClient.ts`
- Modify: `frontend/src/context/AuthProvider.tsx`
- Modify: `frontend/src/hooks/useTokenRefresh.ts`
- Modify: `frontend/src/realtime/gameHub.ts`
- Modify: `frontend/src/api/uploadService.ts`
- Modify: `frontend/src/components/ImageUploadField/ImageUploadField.tsx`

**Interfaces:**
- Produces: one coalesced `authService.refresh()` promise for every refresh caller.
- Produces: async host SignalR access-token factory through the same coordinator.
- Produces: `uploadService.uploadImage(file: File, signal?: AbortSignal)` with a 60-second timeout.

- [ ] **Step 1: Coalesce refresh in the auth service**

Move refresh ownership to one module-level in-flight promise in `authService`. The promise calls `/auth/refresh`, updates memory once, broadcasts once, and clears itself in `finally`. Bootstrap, provider refresh, Axios 401 recovery, and SignalR token acquisition all call this operation.

- [ ] **Step 2: Remove the duplicate Axios refresh queue**

Remove `isRefreshing`, `pendingRequestsQueue`, and `processPendingQueue`. For a retryable 401, await the configured coordinator, set the new token, and replay that request once. Concurrent callers naturally await the same auth-service promise.

- [ ] **Step 3: Explicitly settle scheduled and visibility refreshes**

Make timer and visibility handlers call a local async function that awaits `refreshSession` and handles its boolean result. Invoke it with an explicit rejection handler. Cleanup only clears the timer and listener.

- [ ] **Step 4: Refresh host SignalR tokens through the coordinator**

Add an auth-service method that returns the current valid token or performs the coalesced refresh when expiry is within the established margin. Use its promise as the host `accessTokenFactory`.

- [ ] **Step 5: Add upload-specific cancellation and timeout**

Pass `signal` and `timeout: 60_000` only on the image-upload request. In `ImageUploadField`, own one `AbortController`, abort before replacement and on unmount, and suppress user-facing failure for `CanceledError` while resetting the file control safely.

- [ ] **Step 6: Re-run frontend static checks**

Run format, lint, and build. Expected: no unhandled promise lint findings, no stale effect dependencies, and a successful production build.

### Task 6: Apply Motion, Keyboard, Logging, and Font Fixes

**Files:**
- Modify: `frontend/src/main.tsx`
- Modify: `frontend/src/components/LeaderboardCard/LeaderboardCard.tsx`
- Modify: `frontend/src/components/LeaderboardCard/LeaderboardStandingsRow.tsx`
- Modify: `frontend/src/components/LeaderboardPodium/PodiumSlot.tsx`
- Modify: `frontend/src/components/GamePinDisplay/GamePinDisplay.tsx`
- Modify: `frontend/src/components/ErrorBoundary/AppErrorBoundary.tsx`
- Modify: `frontend/src/utils/clipboard.ts`
- Modify: `frontend/src/hooks/useHostGame.ts`
- Modify: `frontend/index.html`
- Modify: `frontend/src/styles/tokens.css`
- Modify: `frontend/src/theme/typography.ts`

**Interfaces:**
- Produces: application-wide `MotionConfig reducedMotion="user"` and `LazyMotion features={domAnimation}`.
- Produces: semantic PIN copy button and sanitized accessible status feedback.
- Produces: `system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif` font stack.

- [ ] **Step 1: Configure lazy, preference-aware motion**

Wrap `App` under `LazyMotion` and `MotionConfig` in the root render. Change the three animated components from `motion.div` to `m.div`. Use `useReducedMotion` where delayed or spatial animation parameters need static alternatives.

- [ ] **Step 2: Make the PIN display a real button**

Replace the clickable `Box` with `ButtonBase`, keep the visual layout, add `aria-label="Copy game PIN"`, a minimum target size, and a theme-visible `:focus-visible` outline. Keep the separate Copy PIN button because both controls are explicit and keyboard operable.

- [ ] **Step 3: Sanitize copy feedback and production logging**

Remove PIN, URL, environment, origin, and session-flow console calls. Copy failures display generic recovery text without echoing secrets. Clipboard utility failures return structured results without console output. ErrorBoundary logs detailed error objects only inside `import.meta.env.DEV`.

- [ ] **Step 4: Remove external fonts**

Delete Google Fonts preconnect and stylesheet elements. Replace Inter-first declarations in theme typography and CSS tokens with the approved native stack.

- [ ] **Step 5: Verify production artifacts contain no font hosts or sensitive logs**

Run:

```bash
npm --prefix frontend run build
rg -n 'fonts.googleapis.com|fonts.gstatic.com|Game session state loaded|Copy PIN action|Copy Join Link action' frontend/dist frontend/src frontend/index.html
```

Expected: no match.

### Task 7: Remove Only Verified Dead Code and Establish the Bundle Budget

**Files:**
- Delete after verification: `frontend/src/pages/HostDashboard/HostDashboard.tsx`
- Delete after verification: `frontend/src/hooks/useImageUpload.ts`
- Delete after verification: `frontend/src/App.css`
- Delete after verification: unused barrel files proven to have no static/dynamic consumers
- Modify: `frontend/README.md`
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Create: `frontend/scripts/check-bundle-budget.mjs`

**Interfaces:**
- Produces: production `build` script that runs a deterministic gzip JavaScript budget check.

- [ ] **Step 1: Verify every deletion candidate repository-wide**

Run exact filename, symbol, route, and lazy-import searches across tracked source and documentation. Confirm routes import concrete current pages and none resolve through a candidate barrel. Record that validation constants are used and retain them.

- [ ] **Step 2: Remove only proven dead files**

Delete the three confirmed files and only barrels that have no consumer. Remove the obsolete `App.css` tree entry from the frontend README. Do not delete any actively imported component, hook, constant, or barrel.

- [ ] **Step 3: Correct dependency classification**

Move `@types/canvas-confetti` to `devDependencies`. Remove direct `zod` because no tracked source imports it. Regenerate only the lockfile with:

```bash
npm --prefix frontend install --package-lock-only
```

- [ ] **Step 4: Measure the optimized clean build**

Build without source maps, gzip every emitted JavaScript file, and record total gzip bytes and largest gzip chunk. Confirm LazyMotion removed unused projection/gesture content from the previous shared chunk where supported by Motion's feature bundle.

- [ ] **Step 5: Add the deterministic budget check**

Create `frontend/scripts/check-bundle-budget.mjs` using Node built-ins only. It reads `dist/assets/*.js`, calculates gzip sizes with `node:zlib`, and exits nonzero when total or largest chunk exceeds constants set to the post-change baseline plus modest headroom. Update `build` to run `vite build` followed by this checker.

- [ ] **Step 6: Verify build impact after deletions and budget enforcement**

Run clean install, format check, lint, and build. Search the emitted bundle for deleted module names and `zod`. Expected: successful build and passing budget.

### Task 8: Validate Migration, Realtime Recovery, Accessibility, and Final Scope

**Files:**
- Modify only if needed for an observed Phase 5 defect: files already listed above
- Modify: `.wolf/STATUS.md`
- Append: `.wolf/memory.md`
- Modify only for a newly learned project rule: `.wolf/cerebrum.md`
- Append only for an encountered build/runtime defect: `.wolf/buglog.json`

**Interfaces:**
- Consumes: all Phase 5 deliverables.
- Produces: evidence that the approved acceptance criteria pass without unrelated changes.

- [ ] **Step 1: Run backend schema and build checks**

Run:

```bash
dotnet tool restore
dotnet restore backend/Kahoot.slnx
dotnet build backend/Kahoot.slnx -c Release --no-restore
dotnet tool run dotnet-ef migrations list --project backend/src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj --startup-project backend/src/Kahoot.Api/Kahoot.Api.csproj
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj --startup-project backend/src/Kahoot.Api/Kahoot.Api.csproj
npx -y -p @dbml/cli dbml2sql backend/projectSchema.dbml --postgres
```

Expected: restore/build success, new migration listed, no pending model changes, valid DBML.

- [ ] **Step 2: Run clean frontend and bundle checks under pinned Node**

Use the frontend Docker build or a Node 22.13 environment for `npm ci`, format check, lint, and build. Expected: the bundle budget passes and no source maps are emitted.

- [ ] **Step 3: Exercise realtime count/version scenarios**

Run the application against PostgreSQL and exercise join, reconnect, stale disconnect, host removal, forced transport reconnect, duplicate event, and simulated event-gap flows. Verify versions increase only for committed state changes, stale disconnect emits nothing, all clients converge to the REST/reconnect count, and no event precedes commit.

- [ ] **Step 4: Exercise failure recovery scenarios**

Delay and fail host state, results, and leaderboard requests. Confirm requests never overlap, the last-known UI remains, stale state appears after repeated failure, retry is bounded, manual retry recovers, and cleanup aborts outstanding work.

- [ ] **Step 5: Exercise browser accessibility and privacy scenarios**

Use a real browser at representative desktop and mobile widths. Complete login, join, lobby, question, results, leaderboard, and host controls by keyboard. Emulate reduced motion and inspect animations. Confirm PIN focus visibility and status announcements, no external font request, no sensitive production console output, and no unhandled visibility-refresh rejection.

- [ ] **Step 6: Inspect the final diff and update OpenWolf handoff**

Run:

```bash
git status --short
git diff --check
git diff --stat
git diff -- backend/src frontend/src frontend/package.json frontend/package-lock.json global.json .config frontend/.nvmrc backend/projectSchema.dbml docs/realtime-protocol.md docs/superpowers
```

Confirm every changed line is Phase 5 work, earlier migrations remain byte-for-byte untouched by this task, unrelated dirty files are preserved, and the OpenWolf status/memory accurately record verification and remaining limitations.
