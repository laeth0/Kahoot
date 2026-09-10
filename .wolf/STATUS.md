---
description: session handoff, regenerate with /handoff when a quest finishes
budget_tokens: 1000
---
# STATUS — kahoot

> Single source of truth for resuming work. Read this FIRST when starting a session.
> Update this file at the end of every work phase so the next `/clear` resumes in 1 read.
> Last updated: 2026-09-10 (Phase 7 complete & stack verified)

---

## ✅ Done

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

---

## 🚀 Next phase

**Status:** All planned phases (Phase 1 through Phase 7) are 100% complete and verified. Ready for production deployment on Railway / Docker Compose.

---

## 📁 Active architecture

- **Backend:** .NET 10 Clean Architecture, EF Core 10, Npgsql, PostgreSQL 17, MediatR, FluentValidation, SignalR.
- **Frontend:** React 19 + TypeScript + Vite, Material UI v9, Emotion, React Router DOM 7, Axios, SignalR.
- **Patterns:** Strictly Light Theme (`#00629B` IEEE Ocean Blue, `#0284C7` Radar Cyan, `#F4F8FC` canvas, `#09131F` text), centralized routing, custom hooks, accessible landmarks, zero comments across `frontend/src` and `backend/src`.
