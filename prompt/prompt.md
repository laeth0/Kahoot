# Fix Game Lifecycle, Host Disconnect Handling, and Edit Conflicts

I have two related issues in the game system:

## Current Problems

### 1. Player cannot join the game
When a player opens:

```

/join?pin=<game-pin>

```

the frontend shows:

```

A server error occurred. Please try again.

```

Browser console:

```

POST [http://localhost:5000/api/games/join](http://localhost:5000/api/games/join) 500 (Internal Server Error)

```

Investigate the backend join flow and fix the root cause.

---

### 2. Host cannot edit quiz information

When the host edits quiz title/description:

```

PUT /api/quizzes/{id}

```

the API returns:

```

409 Conflict

```

Frontend shows:

```

A conflict occurred with the current game state.

```

The expected business behavior:

- The host should be able to edit quiz information normally when there is no active live game.
- A stale active game/session should not block editing forever.
- The system must correctly know whether the game is actually running.

---

# Required Business Logic

## Game Lifecycle Management

Implement reliable game lifecycle handling:

### When host closes the browser/window:

The game should automatically end.

Examples:
- Host closes browser tab.
- Host refreshes and does not reconnect.
- Host loses connection permanently.

The backend should detect that the host is no longer present and transition:

```

Active Game
|
|
Host disconnected
|
|
End Game / Close Session

```

The game should no longer remain active in the database.

---

## Host Presence Detection

Review the current SignalR/realtime implementation.

Implement a reliable mechanism:

- Track host connection state.
- Detect disconnected host.
- Add timeout/grace period to avoid ending the game during temporary network issues.

Example:

```

Host disconnect
|
|
Wait X seconds
|
|
If host reconnects:
keep game active

Else:
automatically end game

```

The timeout should be configurable.

---

## Cleanup Requirements

When a game ends automatically:

Ensure:

- Game session status changes correctly.
- Active participants are disconnected.
- Realtime clients receive game-ended event.
- Database state is consistent.
- The host can create/start another game normally.

---

# Concurrency / State Validation

Review all current conflict checks.

The current 409 conflict is probably caused by stale game state.

Ensure:

- Only truly active games block quiz modifications.
- Finished/abandoned sessions do not block edits.
- Database state is the source of truth.
- No frontend-only state decides whether editing is allowed.

---

# Investigation Requirements

Before changing code:

Review:

Backend:
- Game session entity/model
- Game lifecycle service
- Host game controller
- SignalR hubs
- Disconnect handling
- Background cleanup jobs (if any)
- Quiz update endpoint

Frontend:
- Host game hooks
- SignalR connection handling
- Browser unload handling
- Edit quiz flow

---

# Verification

Test these scenarios:

## Scenario 1
1. Host starts game.
2. Player joins.
3. Host closes browser.
4. Wait for timeout.
5. Verify game is automatically ended.
6. Verify player receives game-ended event.

## Scenario 2
1. Host starts game.
2. Refresh browser.
3. Reconnect quickly.
4. Verify game remains active.

## Scenario 3
1. End/abandon game.
2. Edit quiz title and description.
3. Save successfully.
4. No 409 conflict.

## Scenario 4
1. Create game.
2. Join with valid PIN.
3. Verify join endpoint succeeds.

Do not only hide errors on the frontend.
Fix the underlying lifecycle and state management problems.
