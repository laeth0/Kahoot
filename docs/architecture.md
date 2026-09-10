# System Architecture & Technical Design

This document details the end-to-end architecture, communication topology, real-time protocols, concurrency controls, and state machines for the IEEEXtreme Kahoot quiz system.

---

## 1. High-Level Deployment Topology

The system is containerized and orchestrated via Docker Compose, mirroring the single-instance production environment deployed on Railway.

```mermaid
graph TB
    subgraph Client Tier
        BrowserPlayer["Player Mobile/Desktop (Browser)"]
        BrowserHost["Host Projector/Dashboard (Browser)"]
    end

    subgraph Reverse Proxy / Web Serving [kahoot-frontend]
        Nginx["Nginx 1.27 Alpine\n(Port 80 -> Host 3000)\nServes SPA static bundle\nConfigured with HTML5 fallback"]
    end

    subgraph Application Server [kahoot-backend]
        Kestrel[".NET 10 Kestrel Server\n(Port 8080 -> Host 5000)"]
        subgraph Clean Architecture Core
            API["Kahoot.Api (Controllers, Middlewares, Hubs)"]
            App["Kahoot.Application (MediatR CQRS, Validators)"]
            Domain["Kahoot.Domain (Aggregates, State Machines, Scoring)"]
            Infra["Kahoot.Infrastructure (EF Core 10, Auth, Media)"]
        end
    end

    subgraph Data Persistence [kahoot-db]
        Postgres[("PostgreSQL 17 Alpine\n(Port 5432 -> Host 5433)\nACID Persistence, B-Tree Indexes\nForeign Keys with Cascade Deletes")]
        Volume[("Docker Volume: postgres_data")]
    end

    BrowserPlayer -->|HTTP REST / WebSocket| Nginx
    BrowserHost -->|HTTP REST / WebSocket| Nginx
    BrowserPlayer -.->|Direct API/Hub on Host Port 5000| Kestrel
    BrowserHost -.->|Direct API/Hub on Host Port 5000| Kestrel
    Nginx -.->|Production Upstream Proxy| Kestrel
    Kestrel -->|TCP Npgsql Pool / Connection String| Postgres
    Postgres --- Volume
```

---

## 2. HTTP API Interaction Flow

RESTful endpoints handle administrative tasks, authentication lifecycle, quiz management, and out-of-band game queries.

```mermaid
sequenceDiagram
    autonumber
    actor Host as Host User
    actor Player as Player User
    participant API as ASP.NET Core 10 Web API
    participant Auth as JwtProvider & PasswordHasher
    participant DB as PostgreSQL 17 Database

    Note over Host, DB: Host Authentication & Silent Refresh Lifecycle
    Host->>API: POST /api/auth/login (Username, Password)
    API->>DB: Query host by username
    DB-->>API: Host record + salt/hash
    API->>Auth: Verify PBKDF2 hash & Generate JWT Pair
    API->>DB: Insert hashed RefreshToken with expiration
    API-->>Host: 200 OK (AccessToken, RefreshToken, HostSummary)

    Note over Host, DB: Quiz & Question Authoring
    Host->>API: POST /api/quizzes (Title, Description) [Bearer JWT]
    API->>DB: Insert Quiz (Draft)
    Host->>API: POST /api/quizzes/{id}/questions (Text, TimeLimit, Points, Choices)
    API->>DB: Insert Question & Choices (Cascade on Quiz)
    Host->>API: POST /api/quizzes/{id}/publish
    API->>DB: Validate >= 1 question, choices valid -> Set IsPublished = true

    Note over Player, DB: Participant Join Flow
    Player->>API: POST /api/games/join (Pin, Nickname)
    API->>DB: Select Game by PIN with Active Status
    API->>DB: Check Nickname Uniqueness within Game Session
    API->>DB: Insert GameParticipant with UUID SessionToken
    API-->>Player: 200 OK (GameId, ParticipantId, SessionToken, QuizTitle)
```

---

## 3. SignalR Group Isolation & Real-Time Topology

Real-time interactions are delivered via ASP.NET Core SignalR through `GameHub` mounted at `/hubs/game`. Connection multiplexing and isolation are maintained using isolated group subscriptions.

```mermaid
graph TD
    Hub["GameHub (/hubs/game)\nRequires HubConnectionContext"]

    subgraph SignalR Hub Groups
        HostGroup["Host Group: 'game_{gameId}_host'\nHost-only events (e.g. detailed answer stats)"]
        AllGroup["Session Group: 'game_{gameId}'\nBroadcast events (QuestionStarted, QuestionEnded, LeaderboardUpdated)"]
    end

    subgraph Connected Clients
        HostClient["Host Connection (JWT Authorized)\nInvokes: JoinAsHost(gameId)"]
        Player1["Player 1 Connection (Anonymous)\nInvokes: Reconnect(sessionToken)"]
        PlayerN["Player N Connection (Anonymous)\nInvokes: Reconnect(sessionToken)"]
    end

    HostClient -->|Subscribe| Hub
    Player1 -->|Subscribe| Hub
    PlayerN -->|Subscribe| Hub

    Hub -->|Add to Group| HostGroup
    Hub -->|Add to Group| AllGroup

    HostGroup -.->|GameEnded, AnsweredCount Updates| HostClient
    AllGroup -.->|ParticipantJoined, ParticipantRemoved| HostClient
    AllGroup -.->|QuestionStarted, QuestionEnded, LeaderboardUpdated| Player1
    AllGroup -.->|QuestionStarted, QuestionEnded, LeaderboardUpdated| PlayerN
```

---

## 4. Answer Submission and Scoring Pipeline

To handle high-concurrency spikes when questions start, answer submissions execute under strict concurrency controls, time-decay scoring, and idempotency guarantees.

```mermaid
sequenceDiagram
    autonumber
    actor Player as Player Connection
    participant Hub as GameHub (SignalR)
    participant Mediator as MediatR Pipeline
    participant Command as SubmitAnswerCommand
    participant DB as PostgreSQL Transaction
    participant Scoring as Scoring Engine (Domain)

    Player->>Hub: SubmitAnswer(questionId, choiceId)
    Hub->>Mediator: Send(SubmitAnswerCommand)
    Mediator->>Command: Execute with Participant SessionToken

    critical Database Isolation & Unique Verification
        Command->>DB: BEGIN TRANSACTION (ReadCommitted / Row Lock)
        Command->>DB: SELECT * FROM game_participants WHERE session_token = @Token
        Command->>DB: Verify Game.Status == QuestionActive AND Question.Id == CurrentQuestionId
        Command->>DB: Check existing answer for (participant_id, question_id)
        alt Already Answered
            Command-->>Hub: Throw GameRuleException("Already answered")
            Hub-->>Player: AnswerAckResponse (Status: AlreadyAnswered, Points: 0)
        else First Submission
            Command->>Scoring: CalculateScore(isCorrect, answerTimeMs, timeLimitMs, basePoints)
            Note over Scoring: Time-Decay Formula:\nPoints = basePoints * (0.5 + 0.5 * (remainingMs / timeLimitMs))\nZero points if incorrect or expired.
            Scoring-->>Command: Calculated Points
            Command->>DB: INSERT INTO answers (id, participant_id, question_id, choice_id, score, created_at)
            Command->>DB: UPDATE game_participants SET total_score = total_score + @Points
            Command->>DB: COMMIT TRANSACTION
            Command-->>Hub: Success (Points, IsCorrect)
            Hub-->>Player: AnswerAckResponse (Status: Accepted, Points: @Points, IsCorrect: @IsCorrect)
        end
    end
```

---

## 5. Host & Player Game State Machine

Both the host and participant client applications synchronize with the server-side authoritative state machine:

```mermaid
stateDiagram-v2
    [*] --> Created: Host Creates Session (POST /api/games)
    Created --> Lobby: Session Initialized with 6-digit PIN

    state Lobby {
        [*] --> WaitingForPlayers
        WaitingForPlayers --> WaitingForPlayers: Player Joins (ParticipantJoined)
        WaitingForPlayers --> WaitingForPlayers: Host Kicks (ParticipantRemoved)
    }

    Lobby --> QuestionActive: Host Starts Game / Next Question (POST /api/games/{id}/advance)

    state QuestionActive {
        [*] --> CountdownRunning: Server Broadcasts QuestionStarted
        CountdownRunning --> AnswerSubmissions: Players submit choices (SubmitAnswer)
        AnswerSubmissions --> CountdownRunning: Host receives 2s answered count
    }

    QuestionActive --> QuestionResults: Question Timer Expires OR Host Truncates (POST /api/games/{id}/end-question)

    state QuestionResults {
        [*] --> AnswerDistribution: Server Broadcasts QuestionEnded
        AnswerDistribution --> RevealCorrectAnswer: Host displays Bar Chart & Stats
    }

    QuestionResults --> Leaderboard: Host advances to Standings (POST /api/games/{id}/leaderboard)

    state Leaderboard {
        [*] --> ComputeRanks: Recalculate Top Players & Rank Deltas
        ComputeRanks --> DisplayStandings: Broadcast LeaderboardUpdated
    }

    Leaderboard --> QuestionActive: More questions remain (AdvanceQuestion)
    Leaderboard --> Finished: All questions concluded (POST /api/games/{id}/end)

    state Finished {
        [*] --> FinalPodium: 1st, 2nd, 3rd place animation
        FinalPodium --> CompleteLeaderboard: Full player standing table
    }

    Finished --> [*]: Host Closes Session / Player Leaves
```

---

## 6. Component Breakdown & Boundaries

### 6.1 Backend Architecture (.NET 10 Clean Architecture)
- **`Kahoot.Domain`**: Authoritative domain entities (`Host`, `Quiz`, `Question`, `Choice`, `GameSession`, `GameParticipant`, `Answer`). Domain events, invariant validation, and mathematical scoring algorithms.
- **`Kahoot.Application`**: CQRS commands and queries mediated by MediatR. FluentValidation request filters, DTO contracts, and transactional pipeline behaviors.
- **`Kahoot.Infrastructure`**: EF Core DbContext, PostgreSQL relational mappings, PBKDF2 hashing service, JWT bearer token generator, static media disk storage.
- **`Kahoot.Api`**: REST Controllers (`AuthController`, `QuizzesController`, `QuizQuestionsController`, `GamesController`, `MediaController`), global `ApiExceptionMiddleware` mapping domain exceptions to RFC 7807 Problem Details, SignalR `GameHub`, and CORS policies.

### 6.2 Frontend Architecture (React 19 + TypeScript + Vite)
- **API & Data Services Layer (`src/api/`)**: Centralized Axios client with automatic 401 redirect, typed error extraction (`ApiError`), media URL resolution, and REST service wrappers.
- **Real-Time Client Layer (`src/realtime/`, `src/hooks/`)**: Strongly typed SignalR hub wrappers with randomized exponential backoff reconnection, event folding, and state re-hydration.
- **Accessible Design System (`src/theme/`, `src/components/`)**: Strictly light-themed Material UI v9 components customized to IEEE Ocean Blue brand guidelines, WCAG 2.2 AA compliant contrast, non-color visual cues (shapes + letters), ARIA live regions, and `prefers-reduced-motion` listeners.
- **Route Code Splitting (`src/routes/`)**: Dynamic `React.lazy` imports wrapped in a unified `<Suspense>` boundary to guarantee sub-250KB individual asset chunks.
