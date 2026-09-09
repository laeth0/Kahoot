# Claude Code Prompt — Phase 4: Participant Join & Waiting Lobby

Copy and paste the following prompt directly into **Claude Code** to implement Phase 4.

---

```markdown
You are a Principal Frontend Engineer and UI/UX Pro Max Specialist pair-programming on the IEEEXtreme Kahoot platform.

We are implementing **Phase 4: Participant Join & Waiting Lobby** per `docs/frontend-pages-plan.md` (lines 497–555) and `docs/Kahoot-like-Platform.md`.

---

### 🚨 CRITICAL ARCHITECTURAL CONSTRAINTS & RULES (MANDATORY)

1. **ABSOLUTELY ZERO COMMENTS IN CODE:**
   - There must be **NO** single-line comments (`//`), **NO** multi-line comments (`/* ... */`), **NO** JSDoc (`/** ... */`), and **NO** JSX comments (`{/* ... */}`) in any frontend code written, refactored, or touched.
   - Code must be self-documenting with clean naming and strict TypeScript typing.
   - Before completing your work, run a ripgrep check for `//` and `/*` in `frontend/src` to guarantee 100% compliance.

2. **STRICT LIGHT THEME ONLY:**
   - Strictly light theme (`mode: 'light'`). No dark theme switchers.
   - Palette (from Palestine Section logo):
     - Primary: `#00629B` (IEEE Ocean Blue)
     - Secondary / Accent: `#0284C7` (Radar Cyan)
     - Canvas Background: `#F4F8FC`
     - Surface / Card: `#FFFFFF`
     - Typography: Primary `#09131F`, Secondary `#486581`, Muted `#64748B`, Border `#E2E8F0`
     - Success: `#10B981`, Warning: `#F59E0B`, Error: `#EF4444`

3. **UI/UX PRO MAX PRINCIPLES (Mobile-First for Players):**
   - Touch targets: Minimum 44x44px for all buttons and interactive elements.
   - Typography: Responsive typography (Inter font), minimum 16px input text to prevent mobile iOS zoom.
   - No emoji icons: Use Material UI SVG icons exclusively (e.g., `PlayArrow`, `Person`, `WifiOff`, `CheckCircle`, `Close`).
   - Smooth micro-animations: 150–250ms cubic-bezier transitions, pulsing waiting ring with `transform: scale()`, respecting `prefers-reduced-motion`.
   - Focus rings: Visible high-contrast focus rings for keyboard/accessibility navigation.

4. **BACKEND & SIGNALR INTEGRATION CONTEXT:**
   - Backend runs on `http://localhost:5000` (Docker container `kahoot-backend`).
   - SignalR hub endpoint: `http://localhost:5000/hubs/game`.
   - REST Join: `POST /api/games/join` `{ pin: string, nickname: string }` returns `200 OK` with:
     ```json
     {
       "gameId": "guid",
       "participantId": "guid",
       "sessionToken": "32-byte base64url string",
       "nickname": "string"
     }
     ```
   - SignalR Reconnect (Player):
     - In `GameHub.cs`: `public async Task<RealtimeResponse<PlayerGameStateResponse>> Reconnect(string sessionToken)`
     - Binds the player connection to `game:{gameId}` group and returns `PlayerGameStateResponse`.
   - SignalR Events to handle in Phase 4:
     - `ParticipantJoined(GameParticipantResponse)`: Updates live player count in lobby.
     - `ParticipantRemoved(string participantId)`: If `participantId === myParticipantId`, triggers `KickedNotice` and disconnects.
     - `QuestionStarted(PlayerQuestionResponse)`: Trigger for Phase 5 (navigates or updates state to Active Question).
     - `GameEnded(LeaderboardResponse)`: Handles early termination.
   - Resilient Status Normalization: Backend returns `GameStatus` as integers (`0/Created`, `1/Lobby`, `2/QuestionActive`, `3/QuestionResults`, `4/Leaderboard`, `5/Finished`). Use `normalizeGameStatus(status)` from `src/constants/gameStatus.ts`.

---

### 📋 DETAILED PHASE 4 SPECIFICATION

#### 1. Session Token Storage (`src/hooks/useSessionToken.ts`)
- Creates a helper hook / utilities to persist and retrieve the player session:
  - Storage key: `sessionStorage.getItem(`kahoot_player_session_${gameId}`)`
  - Stored payload: `{ sessionToken: string, participantId: string, nickname: string, gameId: string }`
  - Functions: `saveSession(gameId, data)`, `getSession(gameId)`, `clearSession(gameId)`.
  - Must survive browser reloads on `/play/:gameId`.

#### 2. Nickname Entry Component (`src/components/NicknameEntryForm/NicknameEntryForm.tsx` & `index.ts`)
- Props: `pin: string`, `onSubmit: (nickname: string) => Promise<void> | void`, `isLoading?: boolean`, `serverError?: string | null`, `onBack?: () => void`.
- Validations:
  - Required, trimmed length 2–30 characters.
  - Character counter ("X / 30").
  - Clear error feedback for taken nicknames (`Game.NicknameTaken` -> "That nickname is already taken in this game. Choose another!").
- Visuals: Mobile-first card with clean input, character counter badge, prominent primary CTA button ("Join Game"), and optional "Change PIN" back button.

#### 3. Waiting Screen Component (`src/components/WaitingScreen/WaitingScreen.tsx` & `index.ts`)
- Props: `nickname: string`, `participantCount: number`, `onLeave: () => void`.
- Visuals:
  - "You're in!" celebration header with radar cyan / IEEE blue accent.
  - Prominent participant badge displaying the player's nickname.
  - Animated pulsing radar ring / waiting indicator with smooth 2-second pulse animation.
  - "See your name on screen?" indicator reassuring the player that their nickname is visible to the host.
  - Live participant count badge ("X players in lobby").
  - "Leave Game" button with confirmation dialog.

#### 4. Kicked Notice Component (`src/components/KickedNotice/KickedNotice.tsx` & `index.ts`)
- Props: `onHome: () => void`.
- Visuals:
  - Polite, clear warning card explaining: "You have been removed from this game by the host."
  - Note: "You cannot rejoin this game session with the same nickname."
  - "Return to Home" button navigating to `/`.

#### 5. Player Game Hook (`src/hooks/usePlayerGame.ts`)
- Signature: `usePlayerGame(gameId: string | undefined)`
- Manages full player lifecycle:
  1. Reads `sessionToken` from `useSessionToken(gameId)`.
  2. If no token found, sets state to `noSession` (prompts redirect to `/join`).
  3. Uses `useGameHubConnection(false)` (unauthenticated player hub connection).
  4. Once hub is connected, calls `invokeReconnect(connection, sessionToken)`.
  5. Sets player game state (`nickname`, `status`, `participantId`, `totalScore`, `rank`, `participantCount`).
  6. Subscribes to SignalR events:
     - `ParticipantJoined`: Increments `participantCount`.
     - `ParticipantRemoved`: If ID matches player, sets `isKicked: true` and tears down hub.
     - `QuestionStarted`: Updates status to `QuestionActive` (ready for Phase 5).
     - `GameEnded`: Updates status to `Finished`.
  7. Exposes: `{ playerState, isKicked, isLoading, error, hubStatus, retryHub, leaveGame }`.

#### 6. Refactor Join Game Page (`src/pages/JoinPage/JoinPage.tsx`)
- Supports query param: `/join?pin=123456`
  - If `pin` query param exists and is valid 6-digit number, starts directly on Step 2 (Nickname Entry).
  - If no PIN, starts on Step 1 (`PinEntryForm`).
- Handles Step transitions:
  - Step 1: Enters PIN -> validates -> moves to Step 2.
  - Step 2: Enters nickname -> submits to `POST /api/games/join`.
  - On 404 (`Game.InvalidPin`): Displays "No active game session found for that PIN."
  - On 409 (`Game.NicknameTaken`): Keeps PIN, highlights nickname field with "That nickname is taken. Please pick another."
  - On 409 (`Game.NotJoinable`): Displays "This game has already started or ended."
  - On success: Saves session token to `sessionStorage` and navigates to `/play/${gameId}`.

#### 7. Player Game Page (`src/pages/PlayerGamePage/PlayerGamePage.tsx` & `index.ts`)
- Route: `/play/:gameId`
- Renders:
  - `MetadataManager title="Live Game Lobby - Kahoot" noindex`
  - `ConnectionStatusBanner` for SignalR connection state.
  - If `isKicked`: Renders `KickedNotice`.
  - If `isLoading`: Centered spinner with "Connecting to game lobby...".
  - If `error` or no session: Friendly card with "Game session not found" and "Join a Game" CTA.
  - If in `Lobby` / `Created` state: Renders `WaitingScreen`.
  - If in `QuestionActive` / later phases: Placeholder informing "Question in progress" (to be completed in Phase 5).
- Clean full-screen container with max width 600px (optimized for mobile thumb-friendly experience).

#### 8. Routes Registration (`src/routes/routes.tsx`)
- Mount `/play/:gameId` with `PlayerGamePage`.
- Verify `/join` points to updated `JoinPage`.

---

### 🧪 VERIFICATION & QUALITY CHECKS

Execute these commands in `frontend` before completing the task:
1. `npm run format:check` (must pass without format issues)
2. `npm run lint` (0 errors, 0 warnings)
3. `npm run build` (`tsc -b && vite build` must pass cleanly)
4. Ripgrep audit: `grep -rn "//" src/` and `grep -rn "/\*" src/` (must return NO comments, except URLs).
5. E2E Browser Testing:
   - Host starts game from `/host/quizzes` (or existing session).
   - In a second tab/window, open `/join?pin={PIN}`.
   - Verify PIN pre-filled, enter nickname `Sara_Engineer`.
   - Submit and verify navigation to `/play/{gameId}`.
   - Verify "You're in!" waiting screen renders with pulsing animation.
   - Verify host window instantly shows `Sara_Engineer` in the lobby grid.
   - Refresh player window (`/play/{gameId}`) -> verify player re-authenticates smoothly via `sessionToken` without creating a duplicate tile on the host.
   - In host window, kick `Sara_Engineer` -> verify player tab immediately transitions to `KickedNotice`.
   - Update `.wolf/STATUS.md` and document results.
```

---

*This prompt was generated following the completion of Phase 3, aligned with `docs/frontend-pages-plan.md`, the IEEEXtreme branding design system, and the strict zero-comments requirement.*
