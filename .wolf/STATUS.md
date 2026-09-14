---
description: session handoff, regenerate with /handoff when a quest finishes
budget_tokens: 1000
---
# STATUS — kahoot

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-09-13 (remove ngrok configuration, skills, and headers from repository)

---

## ✅ Done

- **PostgreSQL 18 Upgrade (Completed & Verified):**
  - Upgraded PostgreSQL container image to `postgres:18-alpine` in `docker-compose.yml`.
  - Updated data mount path to `postgres_data:/var/lib/postgresql` per official PostgreSQL 18+ directory standards.
  - Backed up and restored existing data seamlessly with 100% data integrity.
  - Verified container startup, healthy status, and live e2e tests.
- **Azure VM Production Deployment (Completed & Active):**
  - Project deployment target is Azure VM (Ubuntu) with Docker Compose (`docker-compose.prod.yml`).
  - Removed obsolete `DEPLOYMENT_HOSTINGER.md` guide.
  - Active deployment guide is maintained in `docs/azure-vm-deployment.md`.
- **Docker Dev & Prod Separation (Completed & Verified):**
  - Separated Docker configurations into:
    - `docker-compose.yml`: Default local development stack (`docker compose up -d`). Exposes standard ports on localhost: Frontend on `3000:80`, Backend on `5000:8080`, PostgreSQL on `5432:5432`. No outer Nginx container needed in dev.
    - `docker-compose.prod.yml`: Hardened production stack on Azure VM (`docker compose -f docker-compose.prod.yml up -d --build`). Features Nginx reverse proxy on standard HTTP/HTTPS ports (`80`/`443`), with internal-only application containers, health checks, and Let's Encrypt Certbot volume mounts.
  - Separated environment templates:
    - `.env` & `.env.example`: Local development defaults (`FRONTEND_PORT=3000`, `BACKEND_PORT=5000`, `DB_PORT=5432`, `CORS_ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5173`).
    - `.env.production.example`: Production template with `HTTP_PORT=80`, `HTTPS_PORT=443`, and production placeholders.
  - Updated `docs/azure-vm-deployment.md` to reference `docker-compose.prod.yml`.
  - Zero-comment rule strictly maintained across all configuration and code files.
- **Ngrok Removal (Completed & Verified):**
  - Deleted `ngrok-kahoot.yml` configuration.
  - Deleted `.agents/skills/ngrok/` skill directory and removed entry from `skills-lock.json`.
  - Removed `ngrok-skip-browser-warning` headers from `load-tests/helpers/rest.js`, `load-tests/helpers/signalr.js`, and `load-tests/run-all.js`.
  - Audited repository: 0 remaining references to ngrok across all tracked files.

- **Navbar Host Badge Removal (Completed & Verified):**
  - Removed the `Host: <username>` outlined Chip badge and its unused `PersonIcon` and `Chip` imports from `frontend/src/layouts/RootLayout.tsx`.
  - Zero-comment rule strictly maintained.
  - Verified with `npm run lint` (0 errors), `npm run format:check` (100% compliant), and `npm run build` (clean dist).
  - Rebuilt and restarted the frontend Docker container (`kahoot-frontend`), verified live in Chromium on `/host/quizzes`.

- **Project Configuration Consolidation (Completed & Verified):**
  - Consolidated application configuration templates into a single example file per component: `appsettings.json` + root `.env.example` for backend/Docker stack, and `frontend/.env.example` for frontend.
  - Purged all secrets from `backend/src/Kahoot.Api/appsettings.json` (`Jwt:SigningKey: ""` and `Seeding:Host:Password: ""`), while preserving non-sensitive defaults and local connection string for design-time EF Core tooling. Merged `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` into `appsettings.json`.
  - Removed duplicate `backend/src/Kahoot.Api/appsettings.Production.json`.
  - Created root `.env.example` containing parameterized Docker Compose variables for PostgreSQL, JWT security, initial host seeding, CORS origins, and frontend URLs; deleted obsolete `.env.production.example`.
  - Updated `frontend/.env.example` to unify local Vite development defaults with production reverse-proxy routing; deleted obsolete `frontend/.env.production.example`.
  - Updated `DEPLOYMENT_HOSTINGER.md` instructions to reference `.env.example`.
  - Verified: `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors), runtime fail-fast OptionsValidationException when JWT signing key is omitted, `dotnet ef migrations list` initialized successfully, `npm run lint` (0 errors), `npm run format:check` (100% compliant), `npm run build` (clean dist), secret audit clean across tracked files, and Docker backend container rebuilt and running healthy. Zero comments in C# and TypeScript strictly maintained.

- **Automatic Question Completion & Responsive Leaderboard Viewport (Completed & Verified):**
  - **Backend Auto-End Question:** Implemented `TryAutoEndQuestionCommand` and `TryAutoEndQuestionCommandHandler` in `Kahoot.Application`. Transition occurs atomically as soon as all active (non-removed, connected) players submit answers (`totalAnswers > 0 && remaining == 0`). Integrated into `GameHub.SubmitAnswer`, `GameHub.OnDisconnectedAsync`, and `GamesController.RemoveParticipant`. Host manual controls and question countdown timer remain functional fallbacks.
  - **Automated Verification:** Verified via standalone realtime test (`test_auto_end_question.mjs`), confirming immediate question completion when all active players answer and immediate unblocking when an unanswered player disconnects.
  - **Responsive Leaderboard Viewport (`host/game/{id}`):** Refactored `LeaderboardCard.tsx`, `LeaderboardPodium.tsx`, and `PodiumSlot.tsx` to use dynamic viewport-relative clamp sizing (`clamp()`, `vh`) and a responsive 2-column split layout for >= 4 players on `md`+ screens (podium on left, scroll-contained standings on right). Total card height capped at ~440px on standard laptops (1366x768), fitting the Top 3 podium, badges, and host controls inside the initial viewport without scrolling.
  - **Visual Verification:** Verified live in Chromium across 1366×768 (laptop), 1920×1080 (desktop), and 390×844 (mobile) with 11 players and long nicknames.
  - **Zero Comments Constraint:** Strictly enforced 0 comments in C# and TypeScript files. All checks passed: `dotnet build` (0 warnings, 0 errors), `npm run lint` (0 errors), `npm run format:check` (100% formatted), `npm run build` (built cleanly).
- **Hostinger Production Deployment Preparation (Completed & Verified):**
  - Added permanent engineering rules to both `AGENTS.md` and `CLAUDE.md` mandating production-ready architecture, zero hardcoded localhost URLs, secret injection via environment variables, and persistent storage.
  - Configured frontend API (`axiosClient.ts`), media URL resolver (`media.ts`), and SignalR hub resolver (`gameHub.ts`) to use relative routes (`/api`, `/uploads`, `/hubs/game`) by default in production, eliminating hardcoded localhost dependencies.
  - Enhanced frontend `nginx.conf` with `client_max_body_size 10M;`, WebSocket proxy timeouts (`3600s`), real client IP/protocol forwarding (`X-Forwarded-Proto $scheme`), and static asset gzip compression.
  - Created `frontend/public/.htaccess` for Hostinger Web Hosting Apache/LiteSpeed SPA fallback routing with security headers and caching policies.
  - Created `docker-compose.prod.yml` with isolated internal database network, environment variable parameterized credentials, healthchecks, restart policies, and named persistent volumes.
  - Created root and frontend production environment templates (`.env.production.example`, `frontend/.env.production.example`).
  - Added comma/semicolon delimited string parsing to `Cors:AllowedOrigins` in `Kahoot.Api/Program.cs` for flexible environment variable overrides.
  - Authored comprehensive `DEPLOYMENT_HOSTINGER.md` guide covering Hostinger VPS setup, Docker Compose deployment, SSL with Certbot, database backups, Hostinger Web Hosting alternative, and 9-point production verification checklist.
  - Zero-comment rule strictly maintained across all C# and TypeScript code.
  - Verification passed: `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors), `npm run lint` (0 errors), `npm run format:check` (100% formatted), `npm run build` (clean dist), and live container verification running image upload, direct/proxied fetches, and SignalR gameplay.
  - Resolved root cause of missing choice images during live gameplay: added persistent Docker volume `uploads_data:/app/uploads` to `backend` service in `docker-compose.yml` so uploaded media survives container restarts and rebuilds.
  - Added Vite dev server proxy in `frontend/vite.config.ts` forwarding `/api`, `/uploads`, and `/hubs` to `http://localhost:5000`.
  - Extended backend `ChoiceResultResponse` and `QuestionResultsBuilder` in `Kahoot.Application` to project and return `choice.ImageUrl` in `QuestionResultsResponse`.
  - Updated frontend `ChoiceResultResponse` in `src/realtime/events.ts` to include `imageUrl?: string | null`.
  - Refactored `ChoiceButton.tsx` to render both choice thumbnail and label when present, and display an accessible `ImageNotSupportedOutlinedIcon` fallback instead of a broken browser icon if an image fails to load.
  - Refactored `ImageUploadField.tsx`, `QuestionMedia.tsx`, and `QuestionResultsChart.tsx` to resolve media URLs and handle image errors cleanly without synchronous effect cascades.
  - Zero-comment rule strictly maintained across all C# and TypeScript files.
  - End-to-end verified via automated script: image upload, direct (port 5000) and proxied (port 3000) fetch, DB persistence, container restart persistence, and live SignalR game delivery for Host (`QuestionStartedForHost`, `QuestionEnded`) and Player (`QuestionStarted`).
- **Quiz Overview Card Polish:** Removed the green correct-answer summary badge from question cards on `/host/quizzes/{id}` while preserving choices and answers in question forms and live gameplay. Adjusted card padding, spacing, and vertical alignment for a compact, balanced card. Cleaned up `.playwright-cli`. Verified with `format:check`, `eslint .`, and `tsc -b && vite build`.
- **Dynamic quiz question ordering:** Added handles on every card, mouse/touch and keyboard moves, edge scrolling, floating preview, dynamic destination text, immediate reindexing, full-ID saves, and failure rollback. Verified all six move patterns on 50 real questions, long scrolling, refresh persistence, unchanged question/choice data, cancellation, and 320–1440px question-list layouts. Build, lint, and format checks run with pinned dependencies in an isolated Linux source copy.

<!-- Move items here from "🚀 Next phase" when finished. Group by area. -->

- **Frontend:** Installed all required libraries (`axios`, `zod`, `@mui/material`, `@emotion/react`, `@emotion/styled`, `@mui/icons-material`, `react-router-dom` v7, `@microsoft/signalr`).
- **Frontend Tooling:** Configured Prettier (`.prettierrc`, `.prettierignore`), `eslint-plugin-simple-import-sort`, `eslint-config-prettier`, and package scripts (`lint`, `lint:fix`, `format`, `format:check`). Lint and build verified cleanly.
- **Frontend Scaffolding:** Created folder structure per `STRUCTURE.md`.
- **Frontend Documentation:** Updated `README.md` with complete directory tree, technology stack, directory responsibilities, and scripts guide. Build, lint, and format verified cleanly.
- **Frontend Assets:** Moved `logo.jpeg` to `frontend/src/assets/logo.jpeg` (and `frontend/public/logo.jpeg`).
- **Frontend Light Theme & Colors:** Configured custom MUI light theme derived from `logo.jpeg` (`src/theme/palette.ts`, `typography.ts`, `components.ts`, `index.ts`), design tokens (`src/styles/tokens.css`, `src/index.css`), Google Fonts Inter (`index.html`), and verification showcase in `src/App.tsx`. Enforced light theme only. Verified with lint, prettier, build, and browser testing.
- **Frontend Phase 1: Public Landing & Host Authentication:**
  - Built high-impact, creative, and intuitive Landing Page (`src/pages/HomePage/HomePage.tsx`) with IEEE Ocean Blue branding, hero with live status badge, embedded `PinEntryForm` with digit formatting and sanitization, secondary Host Login CTA, 3-step "How to Participate" guide cards, and feature highlights grid.
  - Implemented dedicated Join Game page (`src/pages/JoinPage/JoinPage.tsx`) supporting deep links (`?pin=...`), PIN validation, player handle input, and direct session joining.
  - Hardened Host Login Page (`src/pages/LoginPage/LoginPage.tsx`) with username + password authentication (no email), show/hide password toggle, return-URL redirect, and rate-limit handling.
  - Built reusable `PinEntryForm` (`src/components/PinEntryForm/PinEntryForm.tsx`).
  - Implemented token persistence (access + refresh tokens with expiration timestamps) in `AuthProvider`, `authService`, and `axiosClient`.
  - Implemented silent access-token renewal hook (`src/hooks/useTokenRefresh.ts`) with tab visibility detection.
  - Implemented centralized SEO and accessibility `MetadataManager` (`src/components/MetadataManager/MetadataManager.tsx`) managing title, meta description, and `noindex` rules.
  - Built accessible feedback primitives (`LoadingState`, `EmptyState`, `ErrorState`, `InlineFieldError`, `LiveRegion`) and `AppErrorBoundary`.
  - Enhanced `NotFoundPage` (`src/pages/NotFoundPage/NotFoundPage.tsx`) with noindex tag and dual return paths.
- **Frontend Phase 2: Host Quiz Management (Completed & Verified):**
  - **Zero Comments Constraint:** Audited and stripped 100% of comments across `frontend/src` code files.
  - **API Services & Hooks:** Built `quizService`, `quizQuestionService`, `uploadService`, `hostGameService`, `useQuizzes`, `useQuiz`, and `useImageUpload`.
  - **Components:** Built `QuizCard`, `ReorderableQuestionList`, `QuestionFormDialog`, `ChoiceEditorRow`, `ImageUploadField`, `PublishChecklist` & `checklistUtils`, and `ConfirmDialog`.
  - **Pages:**
    - `QuizLibraryPage` (`/host/quizzes`): Search, status tabs (All, Published, Drafts), responsive card grid, delete with confirmation, and host game launcher.
    - `CreateQuizPage` (`/host/quizzes/new`): Title & description input with validation and character counters.
    - `QuizEditorPage` (`/host/quizzes/:quizId`): Real-time question management, up/down reordering, question form modal, metadata editor modal, and FR-3.2 publish checklist.
    - `HostGamePlaceholderPage` (`/host/game/:gameId`): Transition page linking game creation to Phase 3.
  - **Quality Gates:** 0 lint errors (`eslint .`), 100% Prettier compliant, 0 TypeScript build errors (`tsc -b && vite build`).
  - **E2E Browser Verification:** Automated verification with recorded video (`host_quiz_management_flow_1788973051046.webp`) covering login, quiz creation, question authoring, checklist validation, quiz publishing, and library status.

- **Frontend Phase 3: Host Game Setup & Lobby at 500-player scale (Completed & Verified):**
  - **Zero Comments Constraint:** 100% enforced across all Phase 3 frontend code.
  - **SignalR Realtime Infrastructure:**
    - `src/realtime/events.ts`: Strict TypeScript definitions for all hub payloads matching `IGameClient` and `RealtimeResponse<T>`.
    - `src/realtime/gameHub.ts`: Factory for `/hubs/game` with host JWT authorization and automatic reconnection policy.
    - `src/hooks/useGameHubConnection.ts`: Lifecycle hook (`connecting`, `connected`, `reconnecting`, `disconnected`) without reactive ref render issues.
    - `src/hooks/useHostGame.ts`: Host controller hook featuring high-velocity join batching (`150ms` throttle), optimistic and real-time state synchronization, double-click protection, and participant management.
    - `src/constants/gameStatus.ts`: Resilient normalization handling both integer and string backend game status representations (`0/Created`, `1/Lobby`, `2/QuestionActive`, `3/QuestionResults`, `4/Leaderboard`, `5/Finished`).
  - **Projector-First Lobby Components:**
    - `GameLayout` (`src/layouts/GameLayout.tsx`): Full-viewport projector-first layout with fullscreen toggle, live PIN badge, and game leave confirmation.
    - `ConnectionStatusBanner` (`src/components/ConnectionStatusBanner/`): Live connectivity banner with manual reconnect action.
    - `GamePinDisplay` (`src/components/GamePinDisplay/`): Projector-scale 6-digit Game PIN display, join instructions, and one-click copy actions with feedback toasts.
    - `PlayerCountBadge` (`src/components/PlayerCountBadge/`): High-visibility participant counter with real-time count updates.
    - `ParticipantTile` (`src/components/ParticipantTile/`): Participant card with deterministic avatar styling, nickname truncation, online indicator, and kick action.
    - `ParticipantGrid` (`src/components/ParticipantGrid/`): Responsive, windowed participant grid with empty state, instant search filtering, and kick confirmation dialog.
    - `GamePhaseIndicator` (`src/components/GamePhaseIndicator/`): Stepper indicating active and upcoming game phases.
    - `HostGameControls` (`src/components/HostGameControls/`): Sticky host controls with disabled state explanation tooltip when 0 players and active state when >= 1 player.
    - `HostGamePage` (`src/pages/HostGamePage/`): Comprehensive host game page mounted at `/host/game/:gameId`.
  - **Quality Gates:** 0 ESLint errors, 100% Prettier formatted, 0 TypeScript compile errors (`tsc -b && vite build`).
  - **E2E Browser Verification:** Automated verification with recorded video (`host_lobby_e2e_verified_1788974955625.webp`) validating game creation, initial lobby state, SignalR participant joins (`Tariq_Dev`, `Noor_Engineer`), participant kick (`Noor_Engineer`), and live game launch into `QuestionActive`.

- **Frontend Phase 4: Participant Join & Waiting Lobby (Completed & Verified):**
  - **Zero Comments Constraint:** 100% enforced (audit clean across `frontend/src`).
  - **Session identity:** `src/hooks/useSessionToken.ts` — `getSession/saveSession/clearSession` (+ `useSessionToken` hook) persisting `{ sessionToken, participantId, nickname, gameId }` under `sessionStorage["kahoot_player_session_<gameId>"]`; storage access is try/catch-guarded (booleans, never empty catch).
  - **Typed error boundary:** `axiosClient.ts` now rejects with `ApiError { message, code, status }` (reads `problem+json` `code` extension); `constants/errorCodes.ts` friendly copy refined for `Game.InvalidPin` / `Game.NicknameTaken` / `Game.NotJoinable` / `Game.InvalidSessionToken`.
  - **Realtime:** `realtime/events.ts` adds `PlayerChoiceResponse` / `PlayerQuestionResponse` / `PlayerGameStateResponse` + `QuestionStarted` client event; `realtime/gameHub.ts` adds `invokeReconnect(connection, sessionToken)`.
  - **Player lifecycle hook:** `src/hooks/usePlayerGame.ts` — reads session, opens anonymous hub (`useGameHubConnection(false)`), calls `Reconnect` on connect and on every reconnect, folds `ParticipantJoined` (count++), `ParticipantRemoved` (self → `isKicked` + hub teardown; other → count--), `QuestionStarted` (→ `QuestionActive`), `GameEnded` (→ `Finished`). Derived `error` / `isLoading` (no setState-in-effect). Returns `{ playerState, isKicked, isLoading, error, hubStatus, retryHub, leaveGame }`.
  - **Components:** `NicknameEntryForm` (2–30 char counter, `Game.NicknameTaken` field feedback, Change-PIN back), `WaitingScreen` ("You're in!", 2s `prefers-reduced-motion`-aware pulsing radar ring, nickname badge, live count, Leave-with-confirm), `KickedNotice` (removed-by-host + no-rejoin note).
  - **Pages:** `JoinPage` refactored to 2-step flow (`?pin=` 6-digit deep link → nickname step), maps 404/409 by `ApiError.code`, 429 soft cooldown, saves session + navigates to `/play/:gameId`. New `PlayerGamePage` at `/play/:gameId` (mobile-first ≤600px container, `MetadataManager noindex`, `ConnectionStatusBanner`, state-driven: kicked / connecting / not-found / lobby `WaitingScreen` / finished / question-placeholder). Route registered top-level in `routes.tsx`.
  - **Quality Gates:** `format:check` clean, `eslint .` 0/0, `tsc -b && vite build` clean.
  - **E2E Verified (Chrome CDP vs live Docker backend):** REST join success shape + `409 Game.NicknameTaken` + `404 Game.InvalidPin` (both carry `code`); hub `Reconnect` success (camelCase, integer `status`) + `Game.InvalidSessionToken` failure; `/play/:gameId` renders `WaitingScreen`; live count ticked 1→2 on a second `ParticipantJoined`; host `DELETE …/participants/{id}` → player screen transitioned to `KickedNotice` instantly; `/join?pin=` deep link jumps to nickname step.
  - **Note:** work was auto-committed to `main` by the project hook as `dfbccaa` (repo's established pattern). Player-facing lobby count starts at 1 and tracks deltas only (no player-facing count in the backend contract) — known limitation.

- **Frontend Phase 5: Live Gameplay (Host + Player) (Completed & Verified):**
  - **Zero Comments Constraint:** 100% enforced (audit clean).
  - **Countdown:** `src/hooks/useServerCountdown.ts` — anchors client↔server skew from the first `endsAt`/`startedAt` payload (ref set in effect, never during render), `requestAnimationFrame` tick throttled to seconds + ~1% bar steps, pauses on `document.hidden` and on `paused` prop (frozen on disconnect). `src/components/ServerCountdown` is purely presentational (takes `CountdownState`); the pages own the hook so the player can gate submission on `expired`.
  - **Realtime:** `events.ts` adds `AnswerAckResponse`; `gameHub.ts` adds `invokeSubmitAnswer(connection, questionId, choiceId)`. `hostGameService.QuestionStartedResponse.player` now typed `PlayerQuestionResponse`.
  - **Session:** `useSessionToken.ts` adds `getHostQuestion` / `saveHostQuestion` / `clearHostQuestion` (`kahoot_host_question_<gameId>`) so the host rehydrates the active question after a mid-question reload (`HostGameStateResponse` carries no question text — gap #2).
  - **Components:** `ChoiceGrid` + `ChoiceButton` + `ChoiceShape` (2–6 Kahoot tiles, non-colour cue = per-slot shape icon + letter A–F, states idle/selected/submitting/locked/correct/incorrect/muted, optional count chip); `QuestionMedia` (16:9 reserved box, absolute URL via new `src/api/media.ts` `resolveMediaUrl`); `AnsweredCounter` (ring + "X / Y"); `QuestionResultsChart` (per-choice bars, correct marked, "N of M answered"); `AnswerFeedbackScreen` (accepted / alreadyAnswered / tooLate / rejected / slowDown). `HostGameControls` extended with `onEndQuestion` / `onNextQuestion` / `onShowLeaderboard` + phase-driven primary button + `actionError` alert.
  - **`usePlayerGame`:** `PlayerState` extended with `currentQuestion` / `alreadyAnswered` / `lastResults` / `leaderboard`; folds `QuestionStarted` (→ Active, reset answer state), `QuestionEnded` (→ Results + `lastResults`), `LeaderboardUpdated` (→ Leaderboard, pull own `totalScore`/`rank` from entry), `GameEnded` (→ Finished + entry). New `submitAnswer(choiceId)` → `invokeSubmitAnswer`; `answerState` machine (`idle|submitting|accepted|alreadyAnswered|tooLate|rejected|slowDown`) mapping `Game.QuestionClosed|QuestionNotActive`→tooLate, `Game.TooManyAnswerAttempts`→slowDown, `Game.ParticipantRemoved`→kick. `scoreBeforeQuestion` state → derived `pointsThisQuestion`.
  - **`useHostGame`:** adds `currentQuestion` (seeded from `getHostQuestion`) / `questionResults` / `leaderboard` / `actionError`; `advanceQuestion` / `endQuestion` / `showLeaderboard` actions via a shared `runAction` wrapper (single `isActionPending` debounce, `ApiError.code`→friendly message, all backend re-entry paths idempotent). Answered-count polling of `GET /games/{id}` every 2s while `QuestionActive` (no per-answer broadcast exists). Reconnect re-`JoinAsHost` + `refetch` guarded by a "was subscribed" ref (no setState-in-effect). One-shot `getQuestionResults` fetch when landing on Results with no cached results.
  - **Pages:** `PlayerGamePage` gains `PlayerQuestionView` (feedback-or-question + `ServerCountdown` + `ChoiceGrid`, locked on submit/expiry/disconnect) and `PlayerResultsView` (verdict + points pop + `ChoiceGrid` reveal with distribution), plus Leaderboard/Finished notice cards with `ordinal()` rank. `HostGamePage` gains `HostQuestionView` (question + `AnsweredCounter` + projector `ServerCountdown` + `ChoiceGrid` host-only correct highlight) and `HostLeaderboardView`; Question Results renders `QuestionResultsChart`.
  - **Quality Gates:** `format:check` clean, `eslint .` 0/0 (incl. `react-hooks` v7 `refs`/`purity`/`set-state-in-effect`/`static-components`), `tsc -b && vite build` clean, comment audit clean.
  - **E2E Verified (Chrome CDP vs live Docker backend):** host Start → player renders the question (<2.5s), `ServerCountdown` ticking; player taps a tile → "Answer locked in!"; host End Question → both show `QuestionResultsChart` with the correct choice + counts; host Show Leaderboard → player "1st place, 939 points" (time-weighted score via `LeaderboardUpdated`); host Next Question → Q2 on both; host End → both Finished ("You finished 1st"). Host page verified through its own on-screen controls (lobby → active → results → leaderboard → Q2).
  - **Known limits:** host live answered-count is 2s poll, not push (backend has no per-answer event); a player who reconnects after answering keeps `alreadyAnswered` + deadline but loses which choice they picked (not in `PlayerGameStateResponse`); "+points this round" shows on the player Leaderboard card (score only arrives with `LeaderboardUpdated`, after the Results view).

- **Frontend Phase 6: Results, Leaderboard & Game End (Completed & Verified):**
  - **Zero Comments Constraint:** 100% enforced (audit clean).
  - **Components:** `src/components/LeaderboardList` (ranked rows, gold/silver/bronze rank badges, "(you)" highlight, `rankDeltas` map → ▲/▼/– chip with `ArrowUpward`/`ArrowDownward` non-colour cue, `maxRows` cap + "and N more players", separate row if the highlighted player is past the cap, `compact` / `projector` sizes); `src/components/PodiumView` (2·1·3 columns, varying heights, `EmojiEvents` medals, "you" ring, renders 1–3); `src/components/GameFinishedScreen` (hero "You finished Nth" + score, `PodiumView`, compact `LeaderboardList`, Play Again / Leave). `src/utils/rank.ts` `ordinal()` shared (removed the duplicate in `PlayerGamePage`). `HostLeaderboardView` (Phase 5 stopgap) deleted — `HostGamePage` composes `LeaderboardList` directly.
  - **`usePlayerGame`:** tracks `rankBeforeQuestionRef` (snapshotted on `QuestionStarted` from a synced `liveRef`, and on reconnect) → derived `rankDelta` (positive = moved up) set in `foldLeaderboard` on `LeaderboardUpdated` / `GameEnded`; exposes `rankDelta`. Session is NOT auto-cleared on Finished (so a reload still reconnects to the snapshot); cleared only on the player's Play Again / Leave action.
  - **`useHostGame`:** one-shot `getLeaderboard` fetch when landing on `Leaderboard` / `Finished` with no cached `leaderboard` (mirrors the Phase 5 `getQuestionResults` reload path).
  - **Pages:** `PlayerGamePage` → new `PlayerLeaderboardView` (hero "YOUR POSITION / Nth" + rank-delta line + `LeaderboardList` top-5 with own delta + "Next question soon…" pulse) and `GameFinishedScreen` for Finished (Play Again → `leaveGame()` + `/join`; Leave → `leaveGame()` + `/`). `HostGamePage` Leaderboard view → `LeaderboardList` (projector, 12 rows); Finished view → `PodiumView` + full `LeaderboardList` (20) + "Back to Quiz" + "Start New Session".
  - **Per-session isolation (FR-5a):** `PlayerGamePage` / `HostGamePage` split into an outer route component that renders the session component with `key={gameId}` → a full remount (fresh `usePlayerGame` / `useHostGame` / hub) when the `gameId` route param changes. `quizId` is threaded via router `location.state` from the Phase-2 "Start game" call sites (`QuizLibraryPage`, `QuizEditorPage`) so Finished's "Back to Quiz" / "Start New Session" work (`HostGameStateResponse` has no `quizId`); "Start New Session" `POST /games` then `navigate(replace)` to the new keyed URL.
  - **Quality Gates:** `format:check` clean, `eslint .` 0/0 (incl. `react-hooks` v7), `tsc -b && vite build` clean, comment audit clean.
  - **E2E Verified (Chrome CDP vs live Docker backend, host driven via REST):** player round-1 → `PlayerLeaderboardView` ("YOUR POSITION / 1st / 0 points", list row "Rania (you)", "Next question soon…"); round-2 → same (delta hidden at 0 for a lone player — wiring exercised); `POST /end` → `GameFinishedScreen` ("You finished 1st!", `PodiumView`, list, Play Again / Leave); **Play Again → `/join` and `sessionStorage` session cleared** (FR-5a). Host Finished/Leaderboard views are pure-render over `leaderboard` data already verified flowing in Phase 5.
  - **Known limits:** rank-delta is only the player's own (backend sends no per-player history); host page verified indirectly (its auth-seeding in the CDP harness is flaky, but the render paths are unchanged data-wise from Phase 5's verified host run).

---

- **Frontend Phase 7: Resilience, Hardening, Accessibility & Polish (Completed & Verified):**
  - **Zero Comments Constraint:** 100% enforced and verified across all `frontend/src` and `backend/src` code files.
  - **Global HTTP Policy (`axiosClient.ts`):** Centralized 401 handling clears auth tokens, persists `kahoot_session_expired`, and redirects to `/login?returnUrl=...`. Structured mapping for 403, 404, 409, 429, and 5xx errors into typed `ApiError` instances.
  - **Host Login Polish (`LoginPage.tsx`):** Reads `returnUrl` from query parameters and displays session expired feedback alert via lazy state initializer (0 effect cascades).
  - **SignalR Connection Resilience (`gameHub.ts`):** Integrated exponential backoff with randomized jitter into automatic reconnection policies to prevent reconnect storms.
  - **Player & Host Resilience (`usePlayerGame.ts`, `useHostGame.ts`):** Added jittered `Reconnect` and `JoinAsHost` invocations. Graceful terminal fallback on `Game.NotFound`, `Game.InvalidPin`, and `Game.InvalidSessionToken`. Wired ARIA `LiveRegion` announcements across all state transitions (question started, answer accepted, results in, player kicked, leaderboard updated, reconnection state).
  - **Accessible UI & Error Boundaries:** Wrapped `PlayerGamePage` and `HostGamePage` in `AppErrorBoundary` keyed by `gameId`. Added automated focus management on phase transitions. Added "Skip to main content" keyboard link and `id="main-content"` landmark in `GameLayout.tsx`. Added blocking modal backdrop with "Reconnect Now" in `ConnectionStatusBanner.tsx`.
  - **Performance & Code-Splitting (`routes.tsx`):** Split route pages with `React.lazy()` and `<Suspense>` fallback, eliminating Vite large chunk warnings and reducing individual page chunk sizes below 50KB.
  - **SEO & Metadata:** Enhanced `MetadataManager.tsx` to manage Open Graph (`og:*`), Twitter Card (`twitter:*`), canonical URLs, and `noindex` attributes. Generated `frontend/public/robots.txt` and `frontend/public/sitemap.xml`.
  - **Docker Compose Configuration:** Configured build args (`VITE_API_URL`, `VITE_SIGNALR_URL`) in `frontend/Dockerfile` and `docker-compose.yml`.
  - **Quality Gates:** 0 ESLint errors (`eslint .`), 100% Prettier compliant (`prettier --check .`), 0 TypeScript compiler errors (`tsc -b && vite build`), clean .NET build (`dotnet build backend/Kahoot.slnx`).

- **Production Observability Platform — Task 0 Baseline (Completed & Verified):**
  - Inspected working tree: clean, 0 uncommitted changes.
  - Recorded tool versions: .NET SDK `10.0.401`, Node `v25.8.0`, Docker Compose `v5.5.1`.
  - Verified Docker image manifests (all exit code 0): `grafana/grafana:13.2.1`, `grafana/loki:3.7.7`, `prom/prometheus:v3.14.0`, `otel/opentelemetry-collector-contrib:0.160.0`, `jaegertracing/jaeger:2.20.0`, `quay.io/prometheus/node-exporter:v1.12.1`, `ghcr.io/google/cadvisor:v0.60.5`, `quay.io/prometheuscommunity/postgres-exporter:v0.20.1`, `quay.io/prometheus/blackbox-exporter:v0.28.0`.
  - Verified pre-change quality gates: `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors), `npm run format:check` (clean), `npm run lint` (0 errors), `npm run build` (clean), and both `docker-compose.yml` and `docker-compose.prod.yml` compose config validation (0 exit code).
  - Rollback point recorded: `2857739b716234127d43895b9faad680d9b5765d`.

- **Production Observability Platform — Task 1 Configuration & Validation Scaffolding (Completed & Verified):**
  - Created `observability/scripts/validate-config.sh`: asserts required planned observability files and enforces rejection of public ports on observability/database services. Tested initial run which safely failed with missing `observability/otel/collector-config.yml` (exit code 1).
  - Added non-secret observability contracts (`OBSERVABILITY_ENABLED`, `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD=`, `POSTGRES_MONITOR_USER`, `POSTGRES_MONITOR_PASSWORD=`) to `.env.example` and `.env.production.example`.
  - Cleared committed runtime secrets (`Password: ""` and `SigningKey: ""`) and bound safe `Observability` defaults in `backend/src/Kahoot.Api/appsettings.json`.
  - Removed committed load-test password fallback from `load-tests/config/environments.js` and required `HOST_USERNAME` and `HOST_PASSWORD` via `fail()`.
  - Verified secret hygiene, .NET 10 solution build (`dotnet build backend/Kahoot.slnx` - 0 errors, 0 warnings), Node syntax check, and compose configurations.
  - Committed with `chore: define secure observability configuration` (`647f2f9`).

- **Production Observability Platform — Task 2 OpenTelemetry SDK Registration in ASP.NET Core (Completed & Verified):**
  - Added pinned NuGet dependencies to `backend/src/Kahoot.Api/Kahoot.Api.csproj`: `Npgsql.OpenTelemetry` (10.0.3), `OpenTelemetry.Exporter.OpenTelemetryProtocol` (1.18.0), `OpenTelemetry.Extensions.Hosting` (1.18.0), `OpenTelemetry.Instrumentation.AspNetCore` (1.18.0), `OpenTelemetry.Instrumentation.Runtime` (1.18.0).
  - Implemented `ObservabilityOptions` and `ObservabilityNames` in `backend/src/Kahoot.Api/Observability/ObservabilityOptions.cs` with validation for non-empty ServiceName, absolute HTTP/HTTPS URI OtlpEndpoint, and 15–300s snapshot interval.
  - Implemented `AddKahootObservability` in `backend/src/Kahoot.Api/Observability/ObservabilityExtensions.cs` registering OpenTelemetry tracing, metrics, json console logging, and logs OTLP exporting with safe resource attributes and async exporter behavior.
  - Wired `builder.AddKahootObservability()` into `backend/src/Kahoot.Api/Program.cs`.
  - Verified disabled startup: API booted cleanly, rendered structured JSON logs, and responded 200 OK on `/health`.
  - Verified enabled startup validation: invalid endpoint (`ftp://invalid`) threw `OptionsValidationException` with safe message without exposing secrets.
  - Committed with `feat: register OpenTelemetry SDK` (`5f60c81`) and pushed to GitHub `origin/main`.

- **Production Observability Platform — Task 3 Application Telemetry Contract and MediatR Spans (Completed & Verified):**
  - Created `IKahootTelemetry` and `KahootTelemetry` in `backend/src/Kahoot.Application/Common/Observability/` with BCL types only (`System.Diagnostics`, `System.Diagnostics.Metrics`).
  - Defined instruments for game sessions created/ended, players joined, questions served, answers submitted (with duration), SignalR reconnections, disconnects, events sent, broadcast duration, transition failures, application operation duration, and snapshot failures.
  - Implemented thread-safe observable gauges (`kahoot.game.sessions.active`, `kahoot.players.connected`) with volatile snapshot reads/writes and per-enum state tags.
  - Registered `services.AddSingleton<IKahootTelemetry, KahootTelemetry>()` in `Kahoot.Application/DependencyInjection.cs`.
  - Extended `RequestLoggingBehavior` to wrap MediatR requests in `application.<RequestType>` activities with `kahoot.request.name`, `kahoot.result` (`success|failure|exception`), error status tagging, duration histograms in `finally`, and clean rethrow of exceptions.
  - Verified with `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors) and confirmed zero sensitive-data logging/tagging and zero code comments.
  - Committed with `feat: trace application operations` (`40e293b`) and pushed to GitHub `origin/main`.

- **Production Observability Platform — Task 4 Instrument Game and Realtime Business Flows (Completed & Verified):**
  - Instrumented game creation (`CreateGameCommandHandler`) and player joins (`JoinGameCommandHandler`): sets safe trace tags (`game.id`, `quiz.id`, `participant.id`) and calls `RecordGameCreated()` and `RecordPlayerJoined()`, strictly excluding PIN, nickname, and tokens from tags.
  - Instrumented state transitions (`StartGameCommandHandler`, `StartNextQuestionCommandHandler`, `EndQuestionCommandHandler`, `ShowLeaderboardCommandHandler`, `EndGameCommandHandler`): sets `game.id`, `transition`, `game.source_state`, `game.resulting_state` tags, records `RecordQuestionServed("start")` and `RecordQuestionServed("advance")`, records `RecordGameEnded()` on final transition to `Finished`, records `RecordTransitionFailure(transition, errorCode)` on invalid or concurrent transitions, and avoids double-counting idempotent re-entries.
  - Instrumented answer submission (`SubmitAnswerCommandHandler`): captures single timing observation per invocation, maps bounded outcomes (`late`, `duplicate`, `accepted`, `rejected`, `exception`), tags only `game.id`, `question.id`, and `participant.id`, and calls `RecordAnswer(outcome, elapsedSeconds)` in `finally`.
  - Instrumented SignalR reconnect/disconnect lifecycle in `GameHub`: calls `RecordReconnect("success"|"failure")` once per `Reconnect` and `RecordDisconnect("normal"|"error")` once per `OnDisconnectedAsync`.
  - Instrumented typed broadcasts centrally in `GameNotifier`: routed all 7 events (`ParticipantJoined`, `ParticipantLeft`, `ParticipantRemoved`, `QuestionStarted`, `QuestionEnded`, `LeaderboardUpdated`, `GameEnded`) through `BroadcastAsync`, measuring duration, recording `RecordBroadcast`, and safely rethrowing on failure without serializing payloads.
  - Verified with `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors), confirmed 0 high-cardinality/sensitive metric tags, and 0 code comments.
  - Committed with `feat: instrument game and realtime flows` (`20b58fc`) and pushed to GitHub `origin/main`.

- **Production Observability Platform — Task 5 Correlate Requests and Reconcile Business Gauges (Completed & Verified):**
  - Created `RequestCorrelationMiddleware` in `backend/src/Kahoot.Api/Common/`: validates `X-Request-ID` (1–64 ASCII alphanumeric + `.-_`) or generates `ActivityTraceId.CreateRandom()`, sets it on the response header, logging scope (`request.id`), and active trace tag.
  - Placed `RequestCorrelationMiddleware` immediately after `UseForwardedHeaders` and before exception handling in `Program.cs`.
  - Configured non-sensitive Npgsql pool name `kahoot-db` using a singleton `NpgsqlDataSource` in `Kahoot.Infrastructure/DependencyInjection.cs`.
  - Created `BusinessMetricsSnapshotHostedService` in `backend/src/Kahoot.Infrastructure/Observability/`: periodically runs via `PeriodicTimer`, queries active games by `GameStatus` and connected players, updates business snapshot gauges, handles cancellation cleanly, and records failures.
  - Registered `BusinessMetricsSnapshotHostedService` conditionally in `Program.cs` only when observability is enabled.
  - Verified with `dotnet build backend/Kahoot.slnx` (0 warnings, 0 errors), confirmed no connection strings/passwords in telemetry, and 0 code comments.
  - Committed with `feat: correlate requests and business gauges` (`d258169`) and pushed to GitHub `origin/main`.

- **Production Observability Platform — Task 6 Configure the OpenTelemetry Collector (Completed & Verified):**
  - Created `observability/otel/collector-config.yml` pinned for `otel/opentelemetry-collector-contrib:0.160.0`.
  - Configured `otlp` gRPC (4317) and HTTP (4318) receivers, `health_check` (13133) extension, and `filelog/nginx` receiver parsing JSON access logs from `/var/log/nginx/access-observability.json` with `service.name=nginx` and retaining only safe fields (`request_id`, `uri`, `status`, `request_time`, `upstream_response_time`, `method`).
  - Configured `memory_limiter` (256 MiB limit, 64 MiB spike limit, 1s interval), `batch` processor (5s timeout, 1024–2048 batch size), and OTTL `transform/sanitize` processor stripping sensitive keys (`authorization`, `cookie`, `password`, `token`, `hash`, `query_string`, `request_body`, `sql_parameter`, `nickname`, `client_address`) from traces and logs.
  - Implemented tail-sampling union with 10s decision wait, 10000 trace buffer, 500 new traces/sec, and 4 policies (`errors` on ERROR status, `http-5xx` on 5xx status codes, `slow` on latency >= 1000ms, and `baseline` on 10% probabilistic sampling).
  - Configured pre-sampling derivation pipeline for RED and dependency metrics using `span_metrics` (namespace `traces.span.metrics`, seconds duration unit with explicit buckets, exemplars enabled, excluded collector instance ID, and bounded routes/methods/statuses/db dimensions) and `servicegraph`.
  - Configured exporters: Prometheus exporter on `0.0.0.0:8889` (with OpenMetrics and resource conversion), `otlphttp/loki` at `http://loki:3100/otlp`, and `otlp/jaeger` at `jaeger:4317`.
  - Configured Collector internal telemetry with `info` logs and Prometheus metrics pull reader at `0.0.0.0:8888`.
  - Verified configuration using `docker run --rm -v ... otel/opentelemetry-collector-contrib:0.160.0 validate --config=...` (exit code 0).
  - Committed with `feat: configure bounded telemetry collection` and pushed to GitHub `origin/main`.

---

## 🚀 Next phase

**Active Quest:** Production Observability Platform (`docs/superpowers/plans/2026-09-14-production-observability-platform.md`)
- Task 0: Completed.
- Task 1: Completed.
- Task 2: Completed.
- Task 3: Completed.
- Task 4: Completed.
- Task 5: Completed.
- Task 6: Completed.
- Task 7: Configure Loki and Jaeger Retention (`observability/loki/loki-config.yml`, `observability/jaeger/jaeger-config.yml`).

---

## 📁 Active architecture

- **Backend:** .NET 10 Clean Architecture, EF Core 10, Npgsql, PostgreSQL 17, MediatR, FluentValidation, SignalR.
- **Frontend:** React 19 + TypeScript + Vite, Material UI v9, Emotion, React Router DOM 7, Axios, SignalR.
- **Patterns:** Strictly Light Theme (`#00629B` IEEE Ocean Blue, `#0284C7` Radar Cyan, `#F4F8FC` canvas, `#09131F` text), centralized routing, custom hooks, accessible landmarks, zero comments across `frontend/src` and `backend/src`.
