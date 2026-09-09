---
description: learned preferences, project conventions, and Do-Not-Repeat rules
budget_tokens: 2000
---
# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-09-09

## User Preferences

<!-- How the user likes things done. Code style, tools, patterns, communication. -->

- Build the system phase by phase; backend first, and within the backend the domain layer before anything else. Do not jump ahead to Application/Infrastructure/Api unless asked.
- Roles are **Host** (creates/runs games) and **Participant/Player** (plays). Do NOT rename the entities (kept `Host` / `Participant`). "admin"/"user" is loose conversational wording for the same two roles.
- **No email anywhere in the system.** Host logs in with **username + password**. Participants never log in — they open a shared link, enter a handle name, and wait for the host to start.
- **Frontend Theme & Branding:** Strictly **Light Theme only** (`mode: 'light'`). No dark theme switchers or dark mode variants. Branding and color palette are derived directly from the IEEEXtreme Palestine Section logo (`frontend/src/assets/logo.jpeg`): Primary is IEEE Ocean Blue `#00629B`, secondary accent is Radar Cyan `#0284C7`, background canvas is `#F4F8FC`, text is `#09131F`.
- Requirements are split into `docs/functional-requirements.md` + `docs/non-functional-requirements.md` (living spec, keep synced). `docs/Kahoot-like Platform.md` is the untouched original brief.
- `backend/projectSchema.dbml`: keep it free of `//` comments (user asked). DBML `Note:` annotations are allowed (they render in dbdiagram).

## Key Learnings

- **Project:** kahoot
- **Frontend Theme Configuration:** MUI v9 theme is configured in `frontend/src/theme/` (`palette.ts`, `typography.ts`, `components.ts`, `index.ts`) with tokens in `frontend/src/styles/tokens.css`.
  - Avoid hardcoding text colors directly into typography variants in `typography.ts` so `contrastText` and `color: inherit` function properly on dark-colored containers.
  - Pass flex alignment (`alignItems`, `justifyContent`) on `Stack` and font styles (`fontWeight`) on `Typography` via `sx` for strict TypeScript type checking.
- **Backend:** .NET 10 Clean Architecture, solution `backend/Kahoot.slnx`. `Kahoot.Domain` (pure POCO), `Kahoot.Application` (MediatR 14 CQRS / FluentValidation / Mapster), `Kahoot.Infrastructure` (EF Core 10 + Npgsql + Scrutor + EFCore.NamingConventions snake_case), `Kahoot.Api`.
- **backend/AGENTS.md hard rules:** no comments in C# code; every model change updates `backend/projectSchema.dbml` + a NEW migration same task; migrations immutable. Migrations so far: `20260909075952_InitialCreate`, `20260909082015_RemoveAuditColumnsAddMediaAndPublish`.
- **NOT DDD — anemic domain.** Entities = flat POCOs, `get; set;` only, own `Id = Guid.CreateVersion7()`, NO base classes, NO methods/ctors/computed props. NO auditing (`created_at`/`updated_at` don't exist; order by the v7 `Id`). Only functional timestamps kept. DateTime props have no `Utc` suffix (→ snake_case aligns with DBML); UTC via callers + `timestamptz` `ConfigureConventions`. `Choice.Text` nullable + `Choice/Question.ImageUrl` (image-only choices ok); `Quiz.IsPublished` gates starting a game. Domain also holds `GameStatus` enum + `Common/Result.cs` (`Error` is `record`, un-sealed so `ValidationError` derives).
- **Application — BUILT (feature-sliced, handlers `internal sealed`, validators `public`).** `Common/Messaging` `ICommand(/<T>)`/`IQuery<T>` over `IRequest<Result...>`. Behaviors via `AddOpenBehavior`: `RequestLoggingBehavior` then `ValidationBehavior` (`where TResponse:Result`; builds `Result<T>.Failure` reflectively; emits `ValidationError` w/ per-field dict). Features: `Authentication/{Register,Login,Refresh,Logout}`, `Quizzes/{Create,Update,Delete,Publish,Get,List}` + `Quizzes/Questions/{Add,Update,Delete,Reorder}` (any edit un-publishes; `QuizEditGuard` blocks edits while a non-finished session exists), `Games/{CreateGame,StartGame,StartNextQuestion,EndQuestion,ShowLeaderboard,EndGame,JoinGame,RemoveParticipant,SubmitAnswer,Reconnect,GetHostGameState,GetQuestionResults,GetLeaderboard}`. `Games/GameStateMachine` (static table), `ScoringService` (`base·(1−elapsed/limit/2)`, min half, wrong 0), `LeaderboardService`. Ports (Infra implements): `IApplicationDbContext`, `IJwtTokenService`, `ITokenHasher`, `ISecureTokenGenerator`, `IGamePinGenerator`, `IDbExceptionInterpreter`. `ICurrentUser` is provided by the **API**. `AddApplication` = MediatR + validators + Mapster scan + `TimeProvider.System`.
  - SubmitAnswer hot path: 1 projection query (status+timing+participant+question); inclusive deadline (`now<=endsAt`); INSERT Answer in a txn, catch `uq_answer_participant_question` unique-violation → idempotent `AlreadyAnswered` ack; `ExecuteUpdate` score `+= points` only if `>0`; commit. Host transitions catch `DbUpdateConcurrencyException` (xmin) → `Game.ConcurrentModification`; all double-click idempotent.
- **Infrastructure — BUILT.** `Persistence/{KahootDbContext:IApplicationDbContext, one IEntityTypeConfiguration per entity, DbExceptionInterpreter (Npgsql 23505+ConstraintName), KahootDbContextFactory reads src/Kahoot.Api/appsettings.json}`, `Security/{TokenHasher SHA-256 hex, SecureTokenGenerator 32B base64url, BcryptPasswordHasher wf12}`, `Authentication/JwtTokenService` (`Microsoft.IdentityModel.JsonWebTokens`, HS256), `Games/GamePinGenerator` (6-digit, DB-checked non-finished), `Storage/LocalFileStorage` (dev only — Railway disk ephemeral). `game_sessions` xmin concurrency = shadow `uint "Version"` (not on the entity). `AddInfrastructure` registers `IApplicationDbContext`→`KahootDbContext`, binds `JwtOptions`(`Jwt` section) + `FileStorageOptions`(`FileStorage`) + `HostSeedOptions`(`Seeding:Host`).
- **Startup hosted services** (`Kahoot.Infrastructure/Startup/`): `DatabaseMigrationHostedService` then `DatabaseSeederHostedService`, both `IHostedLifecycleService.StartingAsync` (before Kestrel). Migration failure → startup fails; seeder idempotent, skips+warns if `Seeding:Host` incomplete.
- **API layer — BUILT & e2e-verified vs Postgres 17.** `Kahoot.Api`: `Common/{CurrentUser, HostClaims (shared sub/nameid→Guid), ApiErrorMapping (Error.Code→HTTP: ValidationError→400 VPD; Auth.Forbidden→403; Auth.*→401; *.NotFound|Game.InvalidPin→404; conflict-set→409; else 400), ApiControllerBase (ToActionResult helpers + application/problem+json), GlobalExceptionHandler (IExceptionHandler→500 PD)}`. `Contracts/` = HTTP request records (`SaveQuestionRequest` shared by Add+Update). Controllers (all `ISender`): `AuthController` (login/refresh/logout — **NO register**, slice deleted), `QuizzesController` `[Authorize]`, `GamesController` (host actions `[Authorize]` + `POST /api/games/join` `[AllowAnonymous]`), `UploadsController` now `[Authorize]`. Routes: `POST /api/games/{id}/{start|advance|end-question|leaderboard|end}`, `DELETE .../participants/{pid}`. `Realtime/`: `GameHub : Hub<IGameClient>` (player-facing: `JoinGame`/`Reconnect`/`SubmitAnswer` via `ISender`; `JoinAsHost` `[Authorize]` — ownership-checked via injected `IApplicationDbContext`; all return `RealtimeResponse<T>` envelope, never throw), `IGameClient` (typed events: `ParticipantJoined/Left/Removed`, `QuestionStarted`(players)/`QuestionStartedForHost`(host), `QuestionEnded`, `LeaderboardUpdated`, `GameEnded`), `GameNotifier` (singleton wrapping `IHubContext<GameHub,IGameClient>`; two groups `game:{id}` players + `game:{id}:host`), `GameGroups`. Host game-control is REST-only (hub can't resolve `ICurrentUser` — `HttpContext` null in hub invocations); controllers broadcast via `GameNotifier` after each command succeeds. `Program.cs`: `AddProblemDetails`+`AddExceptionHandler`, JWT `OnMessageReceived` reads `?access_token=` for `/hubs/game`, `MapHub<GameHub>("/hubs/game")`, `UseExceptionHandler` first. Docs: `docs/realtime-protocol.md` rewritten to match.
- **Uploads:** `IFileStorage`/`IImageUploadService` (Application `Common/Storage`, `ImageSignature` magic-byte check) → `LocalFileStorage`; `Kahoot.Api/Controllers/UploadsController` `POST /api/uploads/images` → 201 `{ url }` relative; `Program.cs` static-serves under `PublicBasePath`.
- **Scrutor gotcha:** marker-interface (`I{Transient,Scoped,Singleton}Service`) classes register `AsImplementedInterfaces()` ONLY — inject an abstraction, never the concrete class (bug-003).
- **Game session isolation** (hard req): every run = new `game_sessions` row; participants/answers/scores/leaderboard scoped by `game_session_id`; answer key `(game_session_id, question_id, participant_id)`; prior sessions never read/mutated. Schema + handlers satisfy this.
- **DBML validation:** `npx -y -p @dbml/cli dbml2sql backend/projectSchema.dbml --postgres`. Gotcha: index-setting `note:` strings must not contain escaped `''` (table `Note: '''...'''` blocks are fine).
- **Local machine gotcha:** `postgresql-x64-18` Windows service holds `localhost:5432` and shadows compose's `kahoot-db`; run test Postgres on a different host port (e.g. `5433`).

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
- **[2026-09-09] Frontend Light-Only Theme & Logo Palette:** Configured Material UI theme with palette extracted from `logo.jpeg` (`#00629B` primary, `#0284C7` secondary, `#F4F8FC` canvas, `#09131F` text). Hardcoded light mode only (`mode: 'light'`) to meet user specification. Added typography scale, CSS tokens, and component overrides.
