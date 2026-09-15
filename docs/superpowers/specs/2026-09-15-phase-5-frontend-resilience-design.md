# Phase 5 Frontend Resilience Design

## Goal

Make live gameplay converge on authoritative server state, make degraded operation visible and recoverable, meet the confirmed accessibility and privacy requirements, and make supported toolchains and production bundle limits reproducible.

## Constraints

- Participant counts must never be derived by incrementing or decrementing client state.
- Presence version and participant count updates must be atomic and committed with the corresponding presence state change.
- Realtime notifications must be published only after the database transaction commits successfully.
- The game session owns a durable monotonic `PresenceVersion`.
- No `.test` or `.spec` files and no unit or integration testing dependencies will be added.
- The frontend will use a native system font stack and make no runtime font requests.
- Existing user changes and unrelated dirty-worktree files must remain untouched.
- Backend model changes require a forward-only migration and synchronized DBML schema.
- C# and TypeScript source files must contain no comments.

## Authoritative Presence State

Add `PresenceVersion` to `GameSession` as a non-negative durable counter. A committed participant presence transition updates the participant row and increments the owning game session version within one database transaction. While holding the game-session row lock, the operation calculates the absolute count of connected, non-removed participants. The returned presence snapshot therefore represents the same committed transaction as the participant mutation.

The application contract will expose a presence payload containing:

- `participantCount`
- `presenceVersion`
- `reason`
- the affected participant identity and current participant representation when needed by the host UI

The supported reasons are `Joined`, `Reconnected`, `Disconnected`, and `Removed`. A new `ParticipantPresenceChanged` SignalR event replaces count-changing interpretations of `ParticipantJoined` and `ParticipantLeft`. Removal still sends a direct removal notification to the affected connection before eviction so the client can clear its session and stop reconnecting. The general presence event is broadcast after the transaction commits and group membership has been updated.

Joining and reconnecting remain distinct protocol operations. A reconnect publishes `Reconnected`, never `Joined`. A stale disconnect that does not match the participant's current connection ID changes neither participant state nor the game-session version and publishes no presence event.

`HostGameStateResponse` and `PlayerGameStateResponse` include the authoritative participant count and presence version. Host state queries return the full participant collection alongside those values. Player reconnect uses the same count definition without exposing other participant details.

## Client Convergence

Each live-game hook stores the latest accepted presence version and the server-provided participant count. An event with a version less than or equal to the accepted version is ignored. An event exactly one version ahead is applied. An event more than one version ahead indicates a sequence gap and triggers authoritative synchronization instead of applying partial local assumptions.

Host synchronization uses the existing authenticated game-state REST endpoint. Player synchronization invokes the authenticated-by-session SignalR reconnect operation. Every successful hub reconnection also performs authoritative synchronization before normal live state is considered current.

The host participant map may apply participant details from an accepted contiguous presence event, but the displayed participant count always comes from the event or state response. A full host resynchronization replaces both the participant map and authoritative count/version together.

## Polling, Cancellation, and Degraded Operation

Replace the host answered-count interval with one recursive timeout scheduled only after the previous request settles. Requests accept an `AbortSignal`; cleanup or a game-state transition aborts the active request and cancels the next timeout. No effect cleanup writes React state.

State, results, and leaderboard synchronization share a bounded retry policy. Failures retain safe last-known data, record the last successful synchronization time, and retry with bounded exponential delays. Repeated failures mark the view degraded and expose a manual retry action. A successful authoritative response clears degraded state and resets backoff.

The connection banner represents both transport state and data freshness. A disconnected transport remains blocking because live actions are unsafe. A connected but stale data state uses a non-blocking warning that explains that the last-known state is displayed and offers synchronization retry. Player and host pages surface this state consistently and announce meaningful changes through their existing live regions.

## Authentication Refresh Coordination

Use one module-level refresh coordinator to coalesce all concurrent refresh requests. The Axios 401 retry path, authentication bootstrap, scheduled refresh, visibility refresh, and host SignalR token acquisition all use this coordinator. Callers do not start independent refresh operations.

Visibility and timer callbacks explicitly settle their promises. Cleanup cancels only browser resources and never sets component state. Failed refresh follows the existing logout/session-expired behavior without creating unhandled promise rejections.

## Endpoint Timeout Policy

Keep the shared Axios client timeout at 10 seconds for normal API calls. Image uploads use an explicit 60-second timeout and accept an `AbortSignal`. `ImageUploadField` owns the active upload controller, aborts an obsolete upload before a replacement, and aborts on unmount. Cancellation does not display a misleading upload failure.

## Motion and Accessibility

Wrap the application in `MotionConfig` with `reducedMotion="user"` and `LazyMotion` with `domAnimation`. Replace the three full `motion` element imports with `m` so unused layout, drag, and projection features are not loaded eagerly. Components use static opacity and transform values when reduced motion is requested. Existing CSS animation fallbacks remain disabled by the reduced-motion media query.

Convert the large game PIN surface to MUI `ButtonBase`. It has an accessible copy label, visible keyboard focus treatment, button semantics, and the same pointer affordance. Copy success or failure is announced through an accessible status region. Failure text does not repeat the PIN or join URL.

Remove session-flow, game-PIN, and join-URL console logging. Clipboard failures present sanitized UI feedback. Detailed ErrorBoundary logging is development-only; production emits no sensitive render details because no telemetry sink is configured.

## Font Privacy

Remove Google Fonts preconnect and stylesheet links from `index.html`. Replace Inter-first theme and CSS tokens with the native system UI stack:

`system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`

No font files or font runtime requests are introduced.

## Toolchain Reproducibility

Add a root `global.json` for the .NET 10.0.1xx SDK feature band with latest-patch roll-forward and prerelease disabled. Add a repository-local tool manifest pinning `dotnet-ef` to the inspected EF runtime version, 10.0.12. Pin backend SDK and ASP.NET runtime container tags to compatible exact patches.

Require Node 22 with a minimum patch of 22.13.0 in `frontend/package.json`, record `22.13.0` in `frontend/.nvmrc`, and use the matching Node 22.13 Docker line. The patch is one above the initial 22.12 baseline because the resolved ESLint 10 toolchain requires 22.13 or newer. Pin the backend build container to the published `10.0.302` SDK image and the runtime container to `10.0.12`; the root `global.json` continues to constrain local development to the requested 10.0.1xx feature band. There is no repository CI workflow to update.

## Dead Code and Dependencies

Before deletion, verify each candidate with repository-wide text searches, route and dynamic-import inspection, TypeScript compilation, and the production build. Current inspection shows:

- `HostDashboard.tsx` has no route or import.
- `useImageUpload.ts` has no consumer.
- `App.css` has no runtime import; its README entry must be removed with the file.
- direct `zod` has no source import.
- validation constants are actively consumed and must remain.
- `@types/canvas-confetti` is compile-time-only and belongs in `devDependencies`.

Unused barrel files are removed only when their underlying component remains directly imported and the barrel itself has no static or dynamic consumer. Any candidate whose dynamic usage cannot be disproven remains in place. Only the npm package manager regenerates `package-lock.json`.

## Bundle Budget

The clean baseline contains approximately 1,073,921 bytes of JavaScript and 358,677 gzip bytes. The approximately 227 KB chunk named after `useServerCountdown` contains 316 sources; its dominant contents are full Motion projection and gesture features, SignalR, canvas-confetti, and MUI LinearProgress. The hook name is a chunk-assignment artifact rather than evidence that the hook is large.

After adopting `LazyMotion` and removing dead dependencies, record the new clean-build baseline. Add a deterministic post-build script that fails when either total gzip JavaScript or the largest gzip JavaScript chunk exceeds a modest documented margin above that baseline. Source maps are excluded from the production budget. The ordinary production build runs this check so regressions cannot silently bypass it.

## Verification

Verification will not add unit or integration tests. It will include:

- repository-wide reference and dynamic route/import searches before and after deletions
- `dotnet tool restore`
- `dotnet restore` and Release build using the pinned feature band
- EF migration listing and pending-model/schema consistency checks
- DBML validation
- frontend clean install under Node 22.13
- frontend formatting, linting, TypeScript production build, and bundle budget check
- Docker image builds for pinned frontend and backend toolchains
- browser keyboard and reduced-motion smoke checks across login, join, lobby, question, results, leaderboard, and host controls
- forced disconnect/reconnect checks that verify absolute participant count convergence
- slow and failed state/results/leaderboard requests that verify no overlapping requests, visible degraded state, bounded retry, and manual recovery
- visibility-change refresh checks with browser unhandled-rejection monitoring
- production network inspection confirming no Google Fonts request and console inspection confirming no sensitive game/session detail
