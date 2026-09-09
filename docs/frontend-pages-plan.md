# Frontend Pages Plan

> Living specification for the frontend. Source of truth: [`functional-requirements.md`](./functional-requirements.md),
> [`non-functional-requirements.md`](./non-functional-requirements.md), [`realtime-protocol.md`](./realtime-protocol.md),
> the original brief [`Kahoot-like Platform.md`](./Kahoot-like%20Platform.md), and the four API controllers
> (`AuthController`, `QuizzesController`, `GamesController`, `UploadsController`) plus `GameHub` / `IGameClient`.
>
> This document is a build roadmap, not an implementation. Feed it to Gemini one phase at a time.

---

## 1. Context & Ground Rules

### Stack (already installed — do not swap)

- React 19 + TypeScript + Vite (client-rendered SPA, no SSR).
- Material UI v9 (`@mui/material`, `@mui/icons-material`, Emotion) — **the only** component library.
- React Router DOM 7 — centralised routes in `src/routes/routes.tsx` with nested layouts + `ProtectedRoute`.
- Axios — single client `src/api/axiosClient.ts`; feature services in `src/api/*`.
- `@microsoft/signalr` — real-time client (not yet wired).
- Zod — form/schema validation.
- **Light theme only.** IEEE Ocean Blue `#00629B` primary, Radar Cyan `#0284C7` secondary, `#F4F8FC` canvas,
  `#09131F` text. No dark mode, no theme switcher. Use theme tokens, not hard-coded hex, in new code.

### Roles

| Role | Auth | Devices | Notes |
|---|---|---|---|
| **Public** | none | any | Landing, join entry, login, 404. |
| **Host** | username + password → JWT access + rotating refresh | desktop / laptop / projector | Authors quizzes, runs live games. Owns only their own quizzes/games. |
| **Player** | none, ever | mobile-first | Joins with PIN + nickname; identity is an opaque `sessionToken`. |

### Environment config (no hard-coded URLs)

- `VITE_API_URL` — REST base, e.g. `https://api.example.com/api` (already used by `axiosClient`).
- `VITE_SIGNALR_URL` — hub origin; the client appends `/hubs/game`.
- Everything in the bundle is public. No secrets client-side.

### Backend surface this plan targets

**REST (`/api`)** — from the three controllers:

- `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` → `AuthenticationResponse` (`hostId`, `username`,
  `accessToken`, `accessTokenExpiresAt`, `refreshToken`, `refreshTokenExpiresAt`). Logout needs the **refresh token in the body**.
- `GET /quizzes` → `QuizSummaryResponse[]` (`id`, `title`, `description`, `isPublished`, `questionCount`).
- `POST /quizzes` → `201 { id }`; `GET /quizzes/{id}` → `QuizDetailResponse` (with `questions[]` → `choices[]`).
- `PUT /quizzes/{id}` (un-publishes), `DELETE /quizzes/{id}` (blocked once ever played → `409`),
  `POST /quizzes/{id}/publish` (validates → `409` if invalid).
- `POST /quizzes/{id}/questions`, `PUT /quizzes/{id}/questions/{questionId}`, `DELETE /quizzes/{id}/questions/{questionId}`,
  `PUT /quizzes/{id}/questions/order` (body `orderedQuestionIds[]`).
- `POST /uploads/images` — `multipart/form-data`, field `file` → `201 { url }` (relative). Server checks magic bytes;
  default allow JPEG/PNG/WebP/GIF, max 5 MB.
- `POST /games` (body `{ quizId }`) → `201 CreateGameResponse` (`gameId`, `pin`, `status` — **already `Lobby`**,
  confirmed in `CreateGameCommandHandler`).
- `GET /games/{id}` → `HostGameStateResponse` (`gameId`, `pin`, `quizTitle`, `status`, `currentQuestionIndex`,
  `totalQuestions`, `currentQuestionStartedAt`, `currentQuestionEndsAt`, `answeredCount`, `participants[]`).
- `GET /games/{id}/questions/{questionId}/results` → `QuestionResultsResponse`.
- `GET /games/{id}/leaderboard` → `LeaderboardResponse`.
- `POST /games/{id}/start` | `/advance` | `/end-question` | `/leaderboard` | `/end` — host game control; each broadcasts.
- `DELETE /games/{id}/participants/{participantId}` → `204` (idempotent, broadcasts).
- `POST /games/join` (body `{ pin, nickname }`) → `200 JoinGameResponse` (`gameId`, `participantId`, `sessionToken`,
  `nickname`, `status`).

**SignalR hub `/hubs/game`** — envelope `RealtimeResponse<T> = { success, data, error:{ code, description } }`, never throws:

| Client → Server | Auth | Returns |
|---|---|---|
| `JoinGame(pin, nickname)` | none | `RealtimeResponse<JoinGameResponse>` |
| `Reconnect(sessionToken)` | none | `RealtimeResponse<PlayerGameStateResponse>` |
| `SubmitAnswer(questionId, selectedChoiceId)` | session token from connection state | `RealtimeResponse<AnswerAckResponse>` (`accepted`, `alreadyAnswered`) |
| `JoinAsHost(gameId)` | host JWT via `?access_token=` query string | `RealtimeResponse<bool>` |

| Server → Client (`IGameClient`) | Group | Payload |
|---|---|---|
| `ParticipantJoined` | players + host | `GameParticipantResponse` |
| `ParticipantLeft` | host | `participantId` |
| `ParticipantRemoved` | players + host | `participantId` |
| `QuestionStarted` | players | `PlayerQuestionResponse` (**no correct answer**) |
| `QuestionStartedForHost` | host | `HostQuestionResponse` (**includes `correctChoiceId`**) |
| `QuestionEnded` | players + host | `QuestionResultsResponse` |
| `LeaderboardUpdated` | players + host | `LeaderboardResponse` |
| `GameEnded` | players + host | `LeaderboardResponse` (final) |

**Game state machine** (`GameStatus`): `Created → Lobby → QuestionActive → QuestionResults → Leaderboard → QuestionActive → … → Finished`.
Invalid transitions are rejected server-side (`409 Game.InvalidStateTransition`). The client mirrors this only to
disable buttons — it is never authoritative.

### Assumptions & backend gaps to confirm with the backend owner

1. **Game creation opens the lobby.** `POST /games` returns status `Lobby`; there is no separate "open lobby" call.
2. **`HostGameStateResponse` has no current-question content** (no text/choices/`correctChoiceId`, no `quizId`).
   On a host mid-question browser reload, the active question cannot be fully re-rendered from REST alone.
   *Frontend mitigation:* persist the last `QuestionStartedForHost` payload in `sessionStorage` keyed by `gameId`
   and rehydrate from it. *Recommended backend enhancement:* include current question detail + `quizId` in
   `HostGameStateResponse`. Flagged, not assumed.
3. **No "sessions for a quiz" / history endpoint.** The quiz editor cannot list past or active runs of a quiz.
   Each "Start game" simply creates a new independent session.
4. **Logout needs the refresh token.** The client must persist the refresh token (today only the access token is
   persisted) — addressed in Phase 1.
5. **Player state is hub-only.** Players have no REST state endpoint; `/play/:gameId` hydrates via `Reconnect(sessionToken)`.
6. **Canonical player join flow:** REST `POST /games/join` → store `sessionToken` → open socket → `Reconnect(sessionToken)`
   to enter the SignalR group and hydrate. Do **not** also call hub `JoinGame` after a REST join (it re-runs
   `JoinGameCommand` and fails with `Game.NicknameTaken`). Hub `JoinGame` is the fallback path only.
7. **Rate limits are player-friendly but real:** `/games/join` token bucket (60 burst + 30 / 10 s), auth 10 / 5 min,
   global 240 burst + 120 / 30 s, hub `SubmitAnswer` 5 / rolling 3 s → `Game.TooManyAnswerAttempts`. Surface `429`
   with a "try again in a moment" message; never retry-storm.

---

## 2. Route Map

| Route | Role | Page | Layout | Indexable | Phase |
|---|---|---|---|---|---|
| `/` | Public | Landing Page | `RootLayout` | yes | 1 |
| `/join` | Public → Player | Join Game Page | `RootLayout` | yes | 4 |
| `/play/:gameId` | Player | Player Game Page (state-driven: lobby → question → answer → results → leaderboard → finished) | `GameLayout` (player density) | `noindex` | 4–6 |
| `/login` | Public → Host | Host Login Page | `AuthLayout` | yes | 1 |
| `/host` | Host | → redirect to `/host/quizzes` | — | `noindex` | 2 |
| `/host/quizzes` | Host | Quiz Library Page (host home) | `RootLayout` | `noindex` | 2 |
| `/host/quizzes/new` | Host | Create Quiz Page | `RootLayout` | `noindex` | 2 |
| `/host/quizzes/:quizId` | Host | Quiz Editor Page (details, questions, choices, publish; question editor is a drawer, not a route) | `RootLayout` | `noindex` | 2 |
| `/host/game/:gameId` | Host | Host Game Page (state-driven: lobby → active → results → leaderboard → finished) | `GameLayout` (projector density) | `noindex` | 3, 5, 6 |
| `*` | Public | Not Found Page (exists) | `RootLayout` | `noindex` | 1 |

Notes:

- The existing `/host/dashboard` route is **renamed** to `/host/quizzes`; `/host` redirects to it. The current
  `HostDashboard` component (sample-data cards) becomes the real Quiz Library Page in Phase 2.
- Question / choice management is a **drawer or full-screen `Dialog` inside the Quiz Editor Page**, optionally deep-linked
  via `?question=<id>` search param. It is not its own route (per "avoid unnecessary pages").
- "Start a new game session" is a **confirm dialog** on a quiz card/editor that calls `POST /games` and navigates to
  `/host/game/:gameId`. Not its own route.
- Deep links: `/join?pin=123456` (from a shared link) prefills and skips straight to the nickname step.
  `/play/:gameId` and `/host/game/:gameId` are refresh-safe (rehydrate from `sessionToken` / `GET /games/{id}`).

---

## 3. Layouts

| Layout | Status | Used by | Content |
|---|---|---|---|
| `RootLayout` | exists | public + host management pages | Sticky app bar (brand, "Join Game", host login / dashboard / logout), skip link, `<main>`, footer. |
| `AuthLayout` | exists | `/login` | Centered card, brand, no nav. |
| `GameLayout` | **new (Phase 3/4)** | `/host/game/:gameId`, `/play/:gameId` | Full-height, no marketing nav/footer, persistent `ConnectionStatusBanner` region, one `<main>` with a single primary heading per phase. Accepts `density="projector" | "player"`: projector = very large type, high contrast, host-only chrome; player = mobile chrome, thumb-reachable actions, safe-area padding. |

Metadata: one shared metadata boundary (small hook/component, no new library) sets `<title>` + `<meta description>` per
route and emits `noindex` for every non-public route in the table above.

---

## 4. Shared Components (build in Phase 0 unless noted)

### 4.1 State & feedback primitives

- `LoadingState` — skeleton/spinner with accessible label; variants: page, section, inline, list-rows.
- `EmptyState` — icon + heading + body + optional CTA (no quizzes, no players yet, no questions).
- `ErrorState` — message + Retry; variants: `notFound`, `forbidden`, `server`, `network`, `rateLimited`.
- `InlineFieldError` — field-level validation text wired to `aria-describedby` / `aria-invalid`.
- `ConfirmDialog` — reused for: remove player, end game, delete quiz, publish, discard edits.
- `ConnectionStatusBanner` — SignalR lifecycle: `connected` (hidden), `reconnecting` (amber, non-blocking),
  `disconnected` (red, blocking overlay with manual Retry). Polite live-region announcements.
- `LiveRegion` — shared polite/assertive announcer for async outcomes (answer accepted, player removed, question started).
- `AppErrorBoundary` — one per route subtree; resets on route/`gameId` change; reports to the project error hook.
- `MetadataManager` — the single `<title>`/description/robots boundary.

### 4.2 Game components (host + player)

- `ServerCountdown` — renders remaining time from `endsAt` (server clock). Display-only. Computes an initial
  client↔server skew from the first payload and never lets the bar drive game logic. Reduced-motion friendly.
- `QuestionMedia` — question/choice image with reserved aspect ratio (no layout shift), `alt`, lazy below the fold,
  composes absolute URL from `VITE_API_URL` origin + relative stored path.
- `ChoiceButton` / `ChoiceGrid` — Kahoot-style answer tiles for 2–6 choices. **Non-color cues:** distinct shape +
  letter per slot (not color alone). States: idle, selected, submitting, locked (answered), correct, incorrect,
  disabled (closed). Used by player answer, host question display, and both results views.
- `AnswerDistributionBar` / `QuestionResultsChart` — per-choice answer counts (bar), correct choice marked with an
  icon + label. MUI-composed or lightweight inline SVG (no chart library unless the user approves one).
- `AnsweredCounter` — "X of Y answered" with a subtle progress ring (host).
- `LeaderboardList` — ranked rows (rank, nickname, score). Player variant highlights the "you" row and rank delta;
  host variant shows top N + total count. Virtualised when long.
- `PodiumView` — top-3 emphasis for the finished state.
- `GamePinDisplay` — very large PIN, copy-to-clipboard, full join URL, optional QR (QR needs a library decision;
  default to URL + PIN text, QR as an enhancement).
- `PlayerCountBadge` — live participant count.
- `ParticipantTile` / `ParticipantGrid` — nickname, connection dot, overflow menu → Remove. **Virtualised / windowed**
  for 500 players; updates via a keyed `Map`, never a full re-sort per event.
- `GamePhaseIndicator` — compact stepper of `Lobby → Active → Results → Leaderboard → Finished` (host).
- `HostGameControls` — Start game, Next question, End question, Show leaderboard, End game. Each button enabled only
  for states its transition allows; disabled buttons keep an accessible reason. Double-click safe (idempotent backend,
  but also debounce + pending state).
- `WaitingScreen` — "Waiting for the host…", "Waiting for other players…", "Get ready…" (player).
- `AnswerFeedbackScreen` — after submit: `accepted` / `alreadyAnswered` / (post-reveal) `correct` + points /
  `incorrect` / `tooLate` / `rejected`. Points animate up; reduced-motion static.
- `GameFinishedScreen` — final rank, score, podium, "Leave" (player) / "Back to quiz" + "Start new session" (host).
- `KickedNotice` — removed player: explanation + "you cannot rejoin this game with the same nickname" + link home.

### 4.3 Quiz-authoring components (Phase 2)

- `QuizForm` — title (required), description (optional). Zod.
- `QuestionForm` — text (≤ 500), optional image, time limit (5–300 s), points (≥ 0), 2–6 choices,
  exactly one correct. Zod mirrors `QuestionValidationRules`.
- `ChoiceEditorRow` — text (≤ 300) and/or image (at least one), "mark correct" radio (single-select across the set).
- `ImageUploadField` — wraps `POST /uploads/images`; client-side type/size pre-check mirroring the server
  (JPEG/PNG/WebP/GIF, 5 MB); preview; progress; clear; stores the returned relative URL.
- `PublishChecklist` — live view of FR-3.2 rules (every question valid, one correct choice, choice has text or image,
  time in range, points ≥ 0); explains exactly why Publish is blocked; maps `409` publish errors to the offending item.
- `ReorderableQuestionList` — up/down buttons **and** drag (drag needs a single-pointer alternative); persists via
  `PUT /quizzes/{id}/questions/order`.
- `QuizCard` — summary tile: title, question count, published chip, actions (Edit, Publish/Unpublished, Start game, Delete).
- `QuizEditLockNotice` — shown when editing is blocked because a non-finished session exists (`409 Quiz.InUse`).

### 4.4 Hooks

- `useApi` — generic request lifecycle (`idle | loading | success | error`), abortable, no stale overwrite.
- `useGameHubConnection` — one `HubConnection` per mounted game page: builds the connection to
  `${VITE_SIGNALR_URL}/hubs/game`, `withAutomaticReconnect`, host passes `accessTokenFactory`, exposes
  `{ status, invoke, on, off }` and lifecycle events. WebSocket transport preferred.
- `usePlayerGame(gameId)` — reducer over a discriminated union keyed by `GameStatus`; seeds from `Reconnect`,
  folds in `QuestionStarted` / `QuestionEnded` / `LeaderboardUpdated` / `GameEnded` / `ParticipantRemoved`;
  tracks `alreadyAnswered`, score, rank. All player gameplay UI derives from this.
- `useHostGame(gameId)` — seeds from `GET /games/{id}`, subscribes via `JoinAsHost`, folds participant + question +
  results + leaderboard events; exposes control actions that call the REST endpoints and reconcile on the broadcast echo.
- `useServerCountdown(endsAt)` — returns `{ secondsLeft, fraction, expired }`, tick via `requestAnimationFrame`,
  pauses on tab hide, resyncs on resume.
- `useSessionToken(gameId)` — read/write the player `sessionToken` in `sessionStorage` (per `gameId`); also stores the
  last host `QuestionStartedForHost` for reload rehydration (gap #2).
- `useTokenRefresh` — silent access-token refresh before `accessTokenExpiresAt`; on refresh failure → clear + `/login`.
- `useQuizzes` / `useQuiz(quizId)` — list/detail with cache + invalidation on mutation.
- `useImageUpload` — upload state for `ImageUploadField`.
- `useClipboard` — copy PIN / join URL with success feedback.

### 4.5 Services & constants (extend `src/api`)

- `api/authService.ts` — exists; add real refresh-token persistence + `logout(refreshToken)`.
- `api/quizService.ts` — list, get, create, update, delete, publish.
- `api/quizQuestionService.ts` — add, update, delete, reorder.
- `api/hostGameService.ts` — createGame, getState, getQuestionResults, getLeaderboard, start, advance, endQuestion,
  showLeaderboard, endGame, removeParticipant.
- `api/uploadService.ts` — uploadImage (multipart).
- `api/gameService.ts` — exists (REST join); keep.
- `realtime/gameHub.ts` — connection factory + typed `invoke` wrappers (`joinGame`, `reconnect`, `submitAnswer`,
  `joinAsHost`).
- `realtime/events.ts` — TypeScript mirrors of every `IGameClient` payload and `RealtimeResponse<T>` /
  `RealtimeError`; a typed `on(event, handler)` map.
- `constants/gameStatus.ts` — `GameStatus` union (`'Created' | 'Lobby' | 'QuestionActive' | 'QuestionResults' |
  'Leaderboard' | 'Finished'`) + `canTransition(from, to)` mirror for button enablement only.
- `constants/validation.ts` — `TIME_LIMIT_MIN=5`, `TIME_LIMIT_MAX=300`, `CHOICES_MIN=2`, `CHOICES_MAX=6`,
  `QUESTION_TEXT_MAX=500`, `CHOICE_TEXT_MAX=300`, `IMAGE_URL_MAX=2048`, `IMAGE_MAX_BYTES=5*1024*1024`,
  `IMAGE_TYPES=['image/jpeg','image/png','image/webp','image/gif']`, `NICKNAME_MAX` (align with server).
- `constants/errorCodes.ts` — map backend `error.code` (`Game.NicknameTaken`, `Game.InvalidPin`, `Game.NotJoinable`,
  `Game.QuestionClosed`, `Game.ParticipantRemoved`, `Quiz.InUse`, …) to friendly copy.

---

## 5. UI State Catalog (applies to every page/view)

Each page section below lists which of these it must implement.

| State | Trigger | Standard treatment |
|---|---|---|
| **Loading** | initial fetch / hub connect | Skeletons for known layout; spinner + label otherwise. Never a bare spinner. |
| **Empty** | 200 with no data | `EmptyState` with a next action (create quiz, add question, "no players yet — share the PIN"). |
| **Validation error** | Zod / server `400 ValidationProblemDetails` | Field-level `InlineFieldError` + `aria-invalid`; error summary on long forms; keep user input. |
| **API error** | non-validation `4xx/5xx` | `ErrorState` with Retry; `problem+json` `detail/title` → message via the axios interceptor. |
| **Unauthorized (401)** | expired/invalid access token | Interceptor clears tokens; redirect to `/login` preserving return URL; toast "session expired". |
| **Forbidden (403)** | host hitting a game/quiz they don't own | `ErrorState` `forbidden`; link back to `/host/quizzes`. |
| **Not found (404)** | bad PIN, missing quiz/game/question | Join: inline "No open game for that PIN". Host: `ErrorState` `notFound`. |
| **Conflict (409)** | invalid state transition, `NicknameTaken`, `PinUnavailable`, `Quiz.InUse`, already published | Contextual inline message; re-sync state from server; never leave a stuck spinner. |
| **Rate limited (429)** | join / answer flood, auth retries | "Too many attempts — wait a few seconds." Disable action + soft cooldown. No auto-retry storm. |
| **Disconnected** | SignalR `onclose` without reconnect | Blocking `ConnectionStatusBanner` overlay + manual Retry; freeze the timer display; keep last known view underneath. |
| **Reconnecting** | SignalR `onreconnecting` | Non-blocking amber banner; inputs disabled; auto-resume; on `onreconnected` re-invoke `Reconnect` / `JoinAsHost` and re-hydrate. |
| **Game/session finished** | `GameEnded` or navigating to a finished game | Switch to Finished view (podium + final scores); disable all gameplay actions; host offered "Start new session". |
| **Kicked / removed** | `ParticipantRemoved` for self | `KickedNotice`; tear down the hub; block rejoin messaging. |
| **Backend restart** | mass reconnect | Reconnect backoff with jitter; on success full re-hydrate; if the game no longer exists → Finished/Not-found fallback. |

---

## 6. Build Phases

### Phase 0 — Foundation & Shared Infrastructure

**Objective**
Stand up the typed API layer, the SignalR client, the cross-cutting hooks, and the shared UI primitives so every later
page composes existing pieces instead of re-deriving contracts, error handling, or connection logic. No user-facing pages.

**Pages**
None.

**Components**

- `MetadataManager`, `AppErrorBoundary`, `LiveRegion`.
- `LoadingState`, `EmptyState`, `ErrorState`, `InlineFieldError`, `ConfirmDialog`.
- `ConnectionStatusBanner` (visual states only; wired in Phase 3/4).
- Services: `quizService`, `quizQuestionService`, `hostGameService`, `uploadService`; `realtime/gameHub.ts`,
  `realtime/events.ts`.
- Constants: `gameStatus`, `validation`, `errorCodes`.
- Hooks: `useApi`, `useGameHubConnection`, `useServerCountdown`, `useClipboard`.
- Extend `axiosClient` interceptors if needed for `429` and `403`; centralise `problem+json` parsing (already partly done).

**Dependencies**
None (builds on the existing `axiosClient`, `AuthContext`, `routes.tsx`).

**Completion criteria**

- All service methods typed against the backend DTOs listed in §1; no `any` on wire types.
- A dev-only harness connects to `${VITE_SIGNALR_URL}/hubs/game`, logs every `IGameClient` event, and round-trips
  `JoinGame` on a locally running backend.
- `npm run lint`, `npm run format:check`, `npm run build` all green.
- Existing pages (`/`, `/login`, `/host/dashboard`, `404`) render unchanged.

---

### Phase 1 — Public Landing & Host Authentication

**Objective**
Orient every visitor and get a host fully authenticated, with the token/session plumbing finished (refresh-token
persistence, silent refresh, return-URL redirect, logout via API).

**Pages**

1. **Landing Page** — `/` — Public
   - **Purpose:** first screen; route players to join and hosts to login; explain the product in one glance.
   - **Sections:** hero (brand, tagline), primary "Enter a game PIN" field/CTA → `/join` (or `/join?pin=`),
     secondary "Host sign in" → `/login`, short feature strip.
   - **States:** Loading (none — static), Validation error (empty/short PIN inline), API error (n/a here).
   - **Actions:** type PIN + continue; go to host login.
   - **Backend / SignalR:** none (PIN is validated on the Join page).
   - **Navigation:** → `/join`, → `/login`. From: app bar brand, 404 page.
   - **Responsive:** mobile-first single column; PIN field thumb-reachable; hero compresses on desktop, no giant empty band.

2. **Host Login Page** — `/login` — Public → Host *(exists — harden)*
   - **Purpose:** authenticate a host with username + password (no email).
   - **Sections:** `AuthLayout` card, username, password (show/hide), submit, error alert, "back to home" link.
   - **States:** Loading (submitting, button spinner, inputs disabled), Validation error (empty fields),
     API error (`401` → generic "invalid username or password", no enumeration), Rate limited (`429` → cooldown message),
     already authenticated (redirect to return URL or `/host/quizzes`).
   - **Actions:** submit credentials; toggle password visibility.
   - **Backend:** `POST /api/auth/login`. On success persist `accessToken`, `refreshToken`, `expiresAt`, `host`;
     schedule `useTokenRefresh`.
   - **Navigation:** on success → `location.state.from` or `/host/quizzes`. → `/` link.
   - **Responsive:** single-column card, 44px targets, password manager friendly (no paste blocking).

3. **Not Found Page** — `*` *(exists)* — verify it links to `/` and `/host/quizzes` and carries `noindex`.

**Components**
`PinEntryForm` (shared with Phase 4), `MetadataManager` wiring for public routes, `useTokenRefresh`,
`AuthContext` extended to hold + persist the refresh token and expose `host.id`.

**Dependencies**
Phase 0.

**Completion criteria**

- Login persists access **and** refresh tokens; refresh token survives reload; `logout()` calls
  `POST /api/auth/logout` with the refresh token then clears state.
- Silent refresh renews the access token before expiry; a failed refresh cleanly logs out to `/login`.
- `ProtectedRoute` redirects unauthenticated hosts to `/login` and returns them to the original URL after login.
- `/` and `/login` have unique titles/descriptions and are indexable; all host routes emit `noindex`.

---

### Phase 2 — Host Quiz Management

**Objective**
Full quiz authoring: list, create, edit, question/choice management, reorder, publish/unpublish, delete — everything a
host needs before running a game.

**Pages**

1. **Quiz Library Page** — `/host/quizzes` — Host *(replaces the sample-data `HostDashboard`)*
   - **Purpose:** the host home; see and manage all owned quizzes; entry point to running a game.
   - **Sections:** header ("Welcome, {username}" + "Create quiz"), quiz grid of `QuizCard`s, per-card actions
     (Edit, Publish state chip, **Start game**, Delete), search/filter (optional).
   - **States:** Loading (card skeletons), Empty (`EmptyState` "Create your first quiz"), API error (Retry),
     Unauthorized (redirect), per-card action pending, delete `409` ("this quiz has been used in a game and is kept
     for history"), Start-game `409` (`QuizNotPublished` → prompt to publish first).
   - **Actions:** open editor; create quiz; publish/unpublish; delete (ConfirmDialog); **start a live game**
     (ConfirmDialog → `POST /games` → navigate to `/host/game/:gameId`).
   - **Backend:** `GET /api/quizzes`; `DELETE /api/quizzes/{id}`; `POST /api/quizzes/{id}/publish`;
     `POST /api/games` `{ quizId }`.
   - **Navigation:** → `/host/quizzes/new`, → `/host/quizzes/:quizId`, → `/host/game/:gameId`. From: app bar "Dashboard",
     `/host` redirect, post-login.
   - **Responsive:** 1-col mobile / 2–3-col desktop grid; actions collapse into an overflow menu on narrow widths.

2. **Create Quiz Page** — `/host/quizzes/new` — Host
   - **Purpose:** create a quiz shell, then go straight to adding questions.
   - **Sections:** `QuizForm` (title, description), Save, Cancel.
   - **States:** Loading (submitting), Validation error (title required, lengths), API error, discard-changes guard.
   - **Actions:** submit → `POST /api/quizzes` → redirect to `/host/quizzes/:id`; cancel → `/host/quizzes`.
   - **Backend:** `POST /api/quizzes`.
   - **Navigation:** → `/host/quizzes/:quizId` on success. From: Quiz Library "Create quiz".
   - **Responsive:** single-column form.

3. **Quiz Editor Page** — `/host/quizzes/:quizId` — Host
   - **Purpose:** edit quiz metadata; manage the ordered question list; manage each question's choices; publish.
   - **Sections:**
     - quiz header: inline-editable title/description, published chip, **Publish** button + `PublishChecklist`,
       Delete, "Start game" (if published).
     - `ReorderableQuestionList`: each row = index, text preview, image thumb, time, points, Edit, Delete, Duplicate (optional).
     - "Add question" → opens **Question Editor drawer/Dialog** (`QuestionForm` + `ChoiceEditorRow` ×2–6 +
       `ImageUploadField`), optionally deep-linked `?question=<id>`.
     - `QuizEditLockNotice` when a non-finished session exists.
   - **States:** Loading (detail skeleton), Not found (`404`), Forbidden (`403`, not owner), Empty (no questions yet →
     prompt to add), Validation error (per FR-3.2, surfaced inline and in `PublishChecklist`),
     Save conflict (`409 Quiz.ConcurrentModification` → reload + merge prompt), Edit-locked (`409 Quiz.InUse`),
     Publish rejected (`409` → checklist points at the failing question), unsaved-changes navigation guard,
     image upload error (type/size/magic-byte `400`).
   - **Actions:** edit metadata (`PUT /quizzes/{id}` — note it **un-publishes**; warn the host), add/edit/delete question,
     reorder, upload image, mark correct choice, publish, delete quiz, start game.
   - **Backend:** `GET /api/quizzes/{id}`; `PUT /api/quizzes/{id}`; `POST|PUT|DELETE /api/quizzes/{id}/questions[...]`;
     `PUT /api/quizzes/{id}/questions/order`; `POST /api/uploads/images`; `POST /api/quizzes/{id}/publish`;
     `DELETE /api/quizzes/{id}`; `POST /api/games`.
   - **Navigation:** ← `/host/quizzes`; → `/host/game/:gameId` on start. Drawer is in-page (no route change) or `?question=`.
   - **Responsive:** list + editor stack on mobile; editor drawer becomes a full-screen `Dialog` under `md`;
     choice grid is 1-col on mobile, 2-col on desktop.

**Components**
`QuizForm`, `QuestionForm`, `ChoiceEditorRow`, `ImageUploadField`, `PublishChecklist`, `ReorderableQuestionList`,
`QuizCard`, `QuizEditLockNotice`, `ConfirmDialog`, `useQuizzes`, `useQuiz`, `useImageUpload`.

**Dependencies**
Phase 0 (services, primitives), Phase 1 (auth, protected routes).

**Completion criteria**

- Create → edit → add ≥ 2 questions with 2–6 choices each and exactly one correct → publish, all against the real API.
- Editing a published quiz visibly flips it back to unpublished and tells the host why.
- `PublishChecklist` blocks Publish until FR-3.2 passes and pinpoints the offending question on a `409`.
- Image upload rejects wrong type/oversize client-side and shows the server message on a `400`.
- Reorder persists and survives reload.
- Delete is blocked with a clear message once the quiz has been used in a game.
- All list/detail/empty/error/validation/forbidden/not-found states implemented.

---

### Phase 3 — Host Game Setup & Lobby (500-player scale)

**Objective**
Launch an independent session from a published quiz, display the PIN, and show players joining in real time at up to
500 participants without jank. Host game control skeleton in place (Start enabled once ≥ 1 player).

**Pages**

1. **Host Game Page** — `/host/game/:gameId` — Host — *Lobby phase in this phase; other phases in 5–6*
   - **Purpose:** the host's live control surface; in `Lobby` state it is the pre-game waiting room.
   - **Layout:** `GameLayout` density `projector`.
   - **Sections (Lobby):**
     - `GamePinDisplay` — huge PIN, copy, join URL (`${origin}/join?pin=<pin>`), optional QR.
     - `PlayerCountBadge` + `ParticipantGrid` (virtualised) with per-tile Remove.
     - `GamePhaseIndicator` (Lobby highlighted).
     - `HostGameControls` — **Start game** (disabled with reason until ≥ 1 connected player), End game.
     - `ConnectionStatusBanner`.
   - **States:** Loading (`GET /games/{id}` + `JoinAsHost`), Not found (`404` → `ErrorState`),
     Forbidden (`JoinAsHost` fails ownership → back to `/host/quizzes`), Empty ("No players yet — share the PIN"),
     Disconnected / Reconnecting (banner; on reconnect re-`JoinAsHost` + `GET /games/{id}`),
     Finished (navigated to an already-finished game → Finished view via `GET /games/{id}/leaderboard`),
     Remove-player pending / `404` (idempotent, treat as success),
     high-volume join burst (batched render, see below).
   - **Actions:** copy PIN / URL; remove a player (ConfirmDialog → `DELETE …/participants/{id}`); Start game
     (`POST /games/{id}/start` — moves to Phase 5 territory); End game (`POST /games/{id}/end`).
   - **Backend / SignalR:** `POST /api/games` (from the previous page); `GET /api/games/{id}`; hub `JoinAsHost(gameId)`
     with `?access_token=`; events `ParticipantJoined`, `ParticipantLeft`, `ParticipantRemoved`;
     `DELETE /api/games/{id}/participants/{participantId}`.
   - **Navigation:** from Quiz Library / Quiz Editor "Start game". Leaving mid-game warns the host (the game keeps
     running server-side; they can return via the same URL).
   - **Responsive:** optimised for large displays; must still be usable on a laptop. Participant grid wraps; PIN stays
     dominant. On a phone (host on the go) it degrades to a single column with a compact PIN header.

**500-player lobby UX requirements**

- Maintain participants as a keyed `Map<participantId, GameParticipantResponse>`; render a **windowed/virtualised** grid
  (candidate dependency: a virtualisation lib — flag for the user; no-dependency fallback: `content-visibility`,
  a capped visible set + search + "and N more", and only animating deltas).
- **Batch** `ParticipantJoined` events: buffer and flush on a ~250 ms timer or `requestAnimationFrame`; never re-sort or
  re-render the whole grid per event.
- Show the aggregate count as the primary signal; the grid is secondary.
- `ParticipantLeft` marks a tile disconnected (dot), does not remove it; `ParticipantRemoved` removes it.

**Components**
`GameLayout`, `GamePinDisplay`, `PlayerCountBadge`, `ParticipantTile`, `ParticipantGrid`, `GamePhaseIndicator`,
`HostGameControls`, `ConnectionStatusBanner`, `useHostGame`, `useGameHubConnection`, `useSessionToken` (host reload
rehydration of the last question payload — used from Phase 5).

**Dependencies**
Phase 0 (hub client, primitives), Phase 1 (host auth for `JoinAsHost`), Phase 2 (a published quiz + Start action).

**Completion criteria**

- "Start game" on a published quiz creates a session, navigates to `/host/game/:gameId`, and shows the PIN.
- A player joining (Phase 4, or a script) appears in the host grid in well under a second.
- A simulated 500-participant join burst keeps the page responsive (no long frames; count accurate; no duplicates).
- Remove player works, broadcasts, and the tile disappears for the host.
- Host browser reload re-hydrates lobby state (`GET /games/{id}`) and re-subscribes (`JoinAsHost`).
- Navigating to a finished/nonexistent game shows the correct fallback, not a spinner.

---

### Phase 4 — Player Join & Lobby

**Objective**
Mobile-first join with PIN + nickname, a waiting lobby, and rock-solid identity/reconnection built on the
`sessionToken` (never the connection id).

**Pages**

1. **Join Game Page** — `/join` — Public → Player
   - **Purpose:** collect a valid PIN and a valid nickname and enter the game.
   - **Sections:** step 1 `PinEntryForm` (numeric, spaced display), step 2 `NicknameEntryForm`, step 3 "Joining…"
     progress. Accepts `?pin=` to start at step 2.
   - **States:** Loading (validating PIN / joining), Validation error (PIN length, nickname empty/too long/charset),
     API error, Not found (`404 Game.InvalidPin` → "No open game for that PIN"),
     Conflict (`409 Game.NicknameTaken` → "That nickname is taken, try another"; `409 Game.NotJoinable` → "This game has
     already started or ended"), Rate limited (`429` → cooldown), Disconnected (can't reach server → Retry).
   - **Actions:** enter PIN → continue; enter nickname → Join.
   - **Backend / SignalR:** `POST /api/games/join` `{ pin, nickname }` → `JoinGameResponse`. Persist `sessionToken`
     (per `gameId`) via `useSessionToken`. Then open the hub and call `Reconnect(sessionToken)` to enter the players
     group and hydrate. (Fallback if REST join is skipped: hub `JoinGame(pin, nickname)`.)
   - **Navigation:** on success → `/play/:gameId`. From: `/`, `/join?pin=` shared link, app bar "Join Game".
   - **Responsive:** full mobile-first; large numeric PIN input (`inputmode="numeric"`), single field per step,
     sticky primary button above the keyboard, safe-area insets.

2. **Player Game Page** — `/play/:gameId` — Player — *Lobby view in this phase; gameplay in 5–6*
   - **Purpose:** the single state-driven surface for the whole player experience.
   - **Layout:** `GameLayout` density `player`.
   - **Sections (Lobby view):** nickname confirmation, "You're in — waiting for the host", player count (from
     `ParticipantJoined`), `ConnectionStatusBanner`, "Leave game".
   - **States:** Loading (`Reconnect` hydrating), Reconnecting / Disconnected (banner + auto/manual retry; on reconnect
     re-`Reconnect`), Unauthorized (n/a — no auth), Not found / stale token (`Reconnect` fails → offer to re-join via
     `/join`), Kicked (`ParticipantRemoved` for self → `KickedNotice`, tear down hub), Game finished
     (`GameEnded` or `Reconnect` returns `Finished` → Finished view), backend restart (reconnect backoff → re-hydrate).
   - **Actions:** wait; leave game (disconnect + clear token + `/`).
   - **Backend / SignalR:** hub `Reconnect(sessionToken)` → `PlayerGameStateResponse`; events `ParticipantJoined`
     (count), `ParticipantRemoved`, `GameEnded`, plus the gameplay events wired in Phase 5.
   - **Navigation:** from Join Game. "Leave" → `/`. No back-to-lobby button (server-driven).
   - **Responsive:** mobile-first; minimal chrome; everything one-handed.

**Reconnection matrix to verify (FR-7):** drop + restore during **lobby**, **active question before answering**,
**active question after answering**, **question results**, **leaderboard** — each must restore identity, current view,
deadline, answered-state, score, and rank, and must **not** create a second participant.

**Components**
`PinEntryForm`, `NicknameEntryForm`, `WaitingScreen`, `ConnectionStatusBanner`, `KickedNotice`, `PlayerCountBadge`,
`usePlayerGame`, `useSessionToken`, `useGameHubConnection`.

**Dependencies**
Phase 0 (hub client, primitives), Phase 1 (`PinEntryForm`, metadata). Pairs with Phase 3 for end-to-end lobby testing.

**Completion criteria**

- PIN + nickname → `/play/:gameId` lobby; the host (Phase 3) sees the join immediately.
- Nickname-taken and game-not-joinable are handled with specific inline copy.
- Browser refresh and a forced network drop both restore the same player (no duplicate on the host grid).
- A removed player sees `KickedNotice` and the app blocks a same-nickname rejoin attempt with the server's message.
- `/join?pin=` deep link jumps straight to the nickname step.

---

### Phase 5 — Live Gameplay (Host + Player)

**Objective**
The real-time question loop: host starts a question, players receive it within hundreds of ms, submit exactly one
answer, the countdown is server-authoritative and display-only, the host sees live answer counts, and the correct
answer is revealed only after the question closes.

**Pages / views** *(extend the two existing state-driven pages)*

1. **Host Game Page** — `/host/game/:gameId` — new views: **Question Active**, **Question Results**
   - **Question Active sections:** question text + `QuestionMedia`, `ChoiceGrid` (host sees `correctChoiceId` from
     `QuestionStartedForHost` — highlighted for the host only, never sent to players), `ServerCountdown` (large),
     `AnsweredCounter` ("X of Y answered"), `HostGameControls` (**End question** now, **Next question** after results),
     `GamePhaseIndicator`.
   - **Question Results sections:** `QuestionResultsChart` (counts per choice), correct choice marked, participant/answer
     totals, `HostGameControls` (**Show leaderboard**, **Next question**, **End game**).
   - **States:** Loading, Disconnected/Reconnecting (freeze countdown; on reconnect `GET /games/{id}` +
     rehydrate last `QuestionStartedForHost` from `sessionStorage` — gap #2; if unavailable, prompt "End question to
     continue"), Conflict (`409 Game.InvalidStateTransition` on double-click → reconcile from the broadcast echo;
     `409 Game.NoMoreQuestions` on Next → guide to End game), control pending (debounced buttons),
     no answers yet (results view with an empty distribution).
   - **Actions:** `POST /games/{id}/start` (first question, from Lobby), `POST /games/{id}/advance` (next question),
     `POST /games/{id}/end-question` (close early / on timeout).
   - **SignalR:** receives `QuestionStartedForHost`, `QuestionEnded`; participant events still folded in.
   - **Responsive:** projector-first — huge question, huge timer, choice grid readable across a room; laptop fallback keeps
     the same hierarchy.

2. **Player Game Page** — `/play/:gameId` — new views: **Question Active**, **Answer Submitted / Waiting**, **Question Results**
   - **Question Active sections:** question text + `QuestionMedia`, `ChoiceGrid` (2–6 tiles, shape+letter cues, **no**
     correctness info), `ServerCountdown` (display-only), "one answer only" hint.
   - **Answer Submitted sections:** `AnswerFeedbackScreen` — `accepted` ("Answer locked in") or `alreadyAnswered`
     (idempotent, same UI), then `WaitingScreen` ("Waiting for other players…"). Choices disabled.
   - **Question Results sections:** reveal correct choice, the player's own choice marked correct/incorrect, points
     earned this question (animated; static under reduced-motion), running score.
   - **States:** Loading, Reconnecting/Disconnected (freeze timer; on reconnect `Reconnect` restores whether the player
     already answered and the current deadline), answer rejected: `Game.QuestionClosed` / server time past
     `questionEndsAt` → "Too late — answer not counted" (deadline is **inclusive** server-side; the client never
     decides this), `Game.NotCurrentQuestion` / `Game.ChoiceNotInQuestion` → generic "couldn't submit, try the current
     question", `Game.TooManyAnswerAttempts` (`429`-like) → "slow down" + brief disable,
     `Game.ParticipantRemoved` → `KickedNotice`.
   - **Actions:** tap one choice → `SubmitAnswer(questionId, selectedChoiceId)`; after an accepted ack the choices are
     locked (UX guard only — the DB is the real guard).
   - **SignalR:** receives `QuestionStarted` (no answer key), `QuestionEnded` (reveal). Client-supplied timestamps are
     never sent for scoring.
   - **Responsive:** mobile-first; choice tiles fill the viewport, thumb-reachable, min 44px; countdown visible without
     scrolling; works at 320px and 200% zoom.

**Components**
`ServerCountdown`, `QuestionMedia`, `ChoiceGrid`/`ChoiceButton`, `AnsweredCounter`, `AnswerFeedbackScreen`,
`QuestionResultsChart`/`AnswerDistributionBar`, `WaitingScreen`, `HostGameControls` (extended),
`usePlayerGame` (question/answer/results branches), `useHostGame` (question/results branches), `useServerCountdown`.

**Dependencies**
Phase 3 (host lobby + controls), Phase 4 (player lobby + hub).

**Completion criteria**

- Host "Start" / "Next" → every connected player renders the question within hundreds of ms.
- Countdown is driven only by `endsAt`; killing the client clock or editing it does not change acceptance.
- Exactly one answer per player is enforced in the UI; a duplicate submit returns `alreadyAnswered` and the UI stays
  consistent (no second selection, no double feedback).
- An answer after the deadline is clearly rejected; an answer at the deadline is accepted (server decides).
- Players never receive `correctChoiceId` before `QuestionEnded`; the host does (host-only highlight).
- Host answered-count updates live; the results chart matches `QuestionResultsResponse`.
- Double-clicking host controls never corrupts the view (idempotent + reconciled).
- Reconnection during active-question (before and after answering) restores the correct state.

---

### Phase 6 — Results, Leaderboard & Game End

**Objective**
Between-question standings and the final results screen, with strict per-session isolation: every run of a quiz has its
own participants, answers, scores, and leaderboard, keyed by `gameId`.

**Pages / views** *(extend the two state-driven pages)*

1. **Host Game Page** — `/host/game/:gameId` — new views: **Leaderboard**, **Finished**
   - **Leaderboard sections:** `LeaderboardList` (top N + total), `GamePhaseIndicator`, `HostGameControls`
     (**Next question**, **End game**).
   - **Finished sections:** `PodiumView` (top 3), full final `LeaderboardList`, "Back to quiz" (→ `/host/quizzes/:quizId`
     if `quizId` is known, else `/host/quizzes`) and **"Start new session"** (→ `POST /games` again → new
     `/host/game/:gameId`). Controls disabled.
   - **States:** Loading (`GET /games/{id}/leaderboard` on reload), Disconnected/Reconnecting, Conflict on transition,
     Finished reached from any earlier state via `GameEnded`, empty leaderboard (no scoring yet → show zeros).
   - **Actions:** `POST /games/{id}/leaderboard` (show standings), `POST /games/{id}/advance` (next question),
     `POST /games/{id}/end` (finish).
   - **SignalR:** `LeaderboardUpdated`, `GameEnded`.
   - **Responsive:** projector-first ranked list; finished podium scales down to laptop/phone.

2. **Player Game Page** — `/play/:gameId` — new views: **Leaderboard**, **Finished**
   - **Leaderboard sections:** the player's rank + score prominent, rank delta since last question, top few others,
     `WaitingScreen` ("Next question soon…").
   - **Finished sections:** `GameFinishedScreen` — final rank, final score, podium context, "Play again" (→ `/join`) /
     "Leave" (→ `/`). Session token cleared.
   - **States:** Loading (`Reconnect` returns `Leaderboard`/`Finished` with a `leaderboard` snapshot),
     Reconnecting/Disconnected, Finished from any state via `GameEnded`, removed player (`KickedNotice`).
   - **Actions:** none during Leaderboard (server-driven); on Finished: play again / leave.
   - **SignalR:** `LeaderboardUpdated`, `GameEnded`.
   - **Responsive:** mobile-first; "your rank" is the hero element.

**Per-session isolation (FR-5a) — frontend obligations**

- Every hook (`usePlayerGame`, `useHostGame`) is keyed by the `gameId` route param; the `AppErrorBoundary` and hub
  connection reset when `gameId` changes.
- No participant/answer/score/leaderboard data is ever read from `localStorage` or a module-level cache across games;
  the player `sessionToken` is stored per `gameId` and cleared on finish/leave.
- Running the same quiz again produces a brand-new `gameId`, PIN, and empty state — the UI must show nothing carried
  over from a previous run.

**Components**
`LeaderboardList`, `PodiumView`, `GameFinishedScreen`, `WaitingScreen`, `HostGameControls` (final),
`usePlayerGame`/`useHostGame` (leaderboard/finished branches).

**Dependencies**
Phase 5.

**Completion criteria**

- Leaderboard appears after each question on both host and players; the player sees their own rank and delta.
- End game moves every connected client to Finished; the podium and final scores match `LeaderboardResponse`.
- Reconnecting during Leaderboard / Finished restores the snapshot (`PlayerGameStateResponse.leaderboard`).
- Immediately starting a second session of the same quiz shows a fresh lobby with zero carried-over state.
- Host can get from Finished back to the quiz and into a new session in two clicks.

---

### Phase 7 — Resilience, Errors, Accessibility & Polish

**Objective**
Make every state in the catalog real and correct, pass WCAG 2.2 AA on every screen, tune projector vs mobile, and
validate 500-player performance.

**Scope (cross-cutting, all pages)**

- **Global HTTP handling:** consistent `401` (clear + `/login` + toast), `403` (forbidden state), `404`, `409`
  (reconcile from server), `429` (cooldown, no retry storm), `5xx` (`ErrorState` + Retry). One place, not per-page.
- **Connection resilience:** `onreconnecting` / `onreconnected` / `onclose` handling with backoff + jitter; on
  reconnect always re-invoke `Reconnect` (player) or `JoinAsHost` + `GET /games/{id}` (host) and re-hydrate; freeze
  timers while disconnected; "game no longer exists" → Finished/Not-found fallback (covers backend restart & mass
  reconnection).
- **Idempotency UX:** debounce every host control and the player submit; reconcile against the broadcast echo; a
  double-tap never produces a double effect in the UI.
- **Accessibility pass:** one primary `<h1>` per phase; logical heading order; landmarks; keyboard path through every
  control (choices, controls, dialogs); visible focus never hidden behind sticky headers; focus moved to the phase
  heading on view change (not on trivial state changes); polite live-region announcements for "question started",
  "answer accepted", "results in", "player removed", "you were removed"; non-color cues on every choice/result/status;
  AA contrast in the light theme; ~44px targets; `prefers-reduced-motion` disables score/countdown animation;
  `inputmode`/`autocomplete` on PIN and nickname; no paste-blocking on login.
- **Responsive/display pass:** projector density for `/host/game/*` (legible across a room, no host-only chrome that
  could leak the correct answer onto a shared screen — confirm the correct-answer highlight is host-screen-only and
  clearly separated), mobile density for `/play/*` (320px, 200% zoom, safe areas, one-handed), laptop fallback for host.
- **Performance pass (NFR-1):** verify the 500-participant lobby and the answer-results rendering — windowed lists,
  batched participant events, memoised rows, no full re-sort per event, no request waterfalls, `Reconnect` storms
  spread with jitter.
- **SEO pass:** `/`, `/join`, `/login` have unique titles + descriptions + self-referential canonicals and crawlable
  `<a href>` navigation; every host/play route is `noindex`; `robots.txt` + minimal sitemap for the three public routes.

**Pages**
None new — hardening only.

**Components**
`ConnectionStatusBanner` (final behaviour), `LiveRegion` wiring, global error interceptor finalisation, `GameLayout`
density refinement.

**Dependencies**
Phases 0–6.

**Completion criteria**

- Every row of the UI State Catalog (§5) has a defined, tested treatment on every page that can reach it.
- Keyboard-only and screen-reader passes complete on `/`, `/login`, `/join`, `/play/:gameId` (all phases),
  `/host/quizzes`, `/host/quizzes/:quizId`, `/host/game/:gameId` (all phases).
- axe + Lighthouse a11y run clean on each above; `eslint-plugin-jsx-a11y` (or configured a11y lint) green.
- Reconnection verified in all five FR-7 situations for the player and on host reload in every phase.
- A 500-participant simulation shows no dropped/duplicated participants or answers in the UI and no long frames.
- `npm run lint`, `npm run format:check`, `npm run build` green; production build sanity-checked (not just dev).

---

## 7. Final Page Inventory

| # | Page | Route | Role | Layout | Phases | Key backend / SignalR |
|---|---|---|---|---|---|---|
| 1 | Landing Page | `/` | Public | `RootLayout` | 1 | — |
| 2 | Host Login Page | `/login` | Public → Host | `AuthLayout` | 1 | `POST /auth/login`, `/auth/refresh`, `/auth/logout` |
| 3 | Not Found Page | `*` | Public | `RootLayout` | 1 | — |
| 4 | Quiz Library Page | `/host/quizzes` | Host | `RootLayout` | 2 | `GET /quizzes`, `POST /quizzes/{id}/publish`, `DELETE /quizzes/{id}`, `POST /games` |
| 5 | Create Quiz Page | `/host/quizzes/new` | Host | `RootLayout` | 2 | `POST /quizzes` |
| 6 | Quiz Editor Page (+ Question Editor drawer) | `/host/quizzes/:quizId` | Host | `RootLayout` | 2 | `GET/PUT/DELETE /quizzes/{id}`, `.../questions[...]`, `.../questions/order`, `POST /uploads/images`, `POST /quizzes/{id}/publish`, `POST /games` |
| 7 | Host Game Page (Lobby → Active → Results → Leaderboard → Finished) | `/host/game/:gameId` | Host | `GameLayout` (projector) | 3, 5, 6 | `POST /games`, `GET /games/{id}`, `GET /games/{id}/leaderboard`, `GET /games/{id}/questions/{qId}/results`, `POST /games/{id}/{start\|advance\|end-question\|leaderboard\|end}`, `DELETE /games/{id}/participants/{pId}`, hub `JoinAsHost`; events `ParticipantJoined/Left/Removed`, `QuestionStartedForHost`, `QuestionEnded`, `LeaderboardUpdated`, `GameEnded` |
| 8 | Join Game Page | `/join` | Public → Player | `RootLayout` | 4 | `POST /games/join`; hub `JoinGame` (fallback) |
| 9 | Player Game Page (Lobby → Question → Answer → Results → Leaderboard → Finished) | `/play/:gameId` | Player | `GameLayout` (player) | 4, 5, 6 | hub `Reconnect`, `SubmitAnswer`; events `QuestionStarted`, `QuestionEnded`, `LeaderboardUpdated`, `GameEnded`, `ParticipantJoined`, `ParticipantRemoved` |

Redirect-only: `/host` → `/host/quizzes`.

**Deliberately not separate pages** (modal / drawer / panel / component instead): Question editor, Choice editor,
"Start a new game session" confirm, "Remove player" confirm, "End game" confirm, "Publish" confirm + checklist,
answer-submitted feedback, disconnected/reconnecting overlays, kicked notice, leaderboard between questions.

---

## 8. Recommended Implementation Order

1. **Phase 0 — Foundation.** Types, services, `realtime/*`, hooks, primitives, metadata + error boundary. Nothing
   visible ships, but everything after this is composition.
2. **Phase 1 — Landing + Login.** Simplest real pages; finishes auth/session plumbing that every host page needs.
3. **Phase 2 — Quiz Management.** Pure REST CRUD, no real-time; produces the published quiz that Phase 3 needs.
4. **Phase 3 — Host Lobby.** First real-time page, but only the join/lobby slice; proves the hub client and the
   500-player rendering strategy before gameplay logic piles on.
5. **Phase 4 — Player Join + Lobby.** Pairs with Phase 3 for the first full host↔player loop (join → appears → removed →
   reconnect).
6. **Phase 5 — Live Gameplay.** The hard real-time core: question broadcast, one-answer submission, server countdown,
   reveal. Builds directly on 3 + 4.
7. **Phase 6 — Results, Leaderboard, End.** Completes the game loop and locks in per-session isolation.
8. **Phase 7 — Resilience & Polish.** Harden every state, accessibility, projector/mobile, and 500-user performance.

Rule of thumb honoured: static/simple pages (Landing, Login, Quiz CRUD) before anything touching SignalR; within
real-time work, lobby before gameplay, gameplay before results, and resilience last.

---

## 9. Appendix — Feeding this to Gemini

When you ask Gemini to generate a page, give it, per page:

- This file's §1 (stack, roles, backend surface, assumptions) + §3 (layouts) + §4 (shared components) + §5 (state
  catalog) as fixed context.
- The single page's block from the relevant phase (purpose, sections, states, actions, backend/SignalR, navigation,
  responsive).
- The exact DTOs it touches (copy the record shapes from `Kahoot.Application.Games.Common` / `Quizzes.Common` /
  `Authentication.Common`).
- The constraints: MUI v9 only, light theme + IEEE palette tokens, React Router 7, requests only through
  `src/api/*` and `src/realtime/*`, WCAG 2.2 AA, mobile-first for `/play/*`, projector-first for `/host/game/*`,
  no new dependencies without asking.

Ask for one page (or one phase view) at a time, wired to the shared components and hooks named here rather than
re-inventing them.
