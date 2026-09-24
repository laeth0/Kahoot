# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-09-24T17:32:59.375Z
> Files: 131 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `.gitignore` — Git ignore rules (~2 tok)
- `AGENTS.md` — Engineering Standards for AI Coding Agents (~10061 tok)
- `CLAUDE.md` — OpenWolf (~99 tok)
- `docker-compose.yml` — Docker Compose services (~416 tok)
- `skills-lock.json` (~329 tok)

## .githooks/

- `pre-commit` (~161 tok)

## .specify/

- `.gitignore` — Git ignore rules (~105 tok)
- `init-options.json` (~52 tok)
- `integration.json` (~110 tok)

## .specify/integrations/

- `claude.manifest.json` (~372 tok)
- `codex.manifest.json` (~372 tok)
- `speckit.manifest.json` (~439 tok)

## .specify/memory/

- `.constitution-template.json` (~31 tok)
- `constitution.md` — [PROJECT_NAME] Constitution (~597 tok)

## .specify/scripts/powershell/

- `check-prerequisites.ps1` — Consolidated prerequisite checking script (PowerShell) (~1818 tok)
- `common.ps1` — Common PowerShell functions analogous to common.sh (~9362 tok)
- `create-new-feature.ps1` — Create a new feature (~3824 tok)
- `resolve-template.ps1` (~248 tok)
- `setup-plan.ps1` — Setup implementation plan for a feature (~839 tok)
- `setup-tasks.ps1` (~1063 tok)

## .specify/templates/

- `checklist-template.md` — [CHECKLIST TYPE] Checklist: [FEATURE NAME] (~520 tok)
- `constitution-template.md` — [PROJECT_NAME] Constitution (~597 tok)
- `plan-template.md` — Implementation Plan: [FEATURE] (~909 tok)
- `spec-template.md` — Feature Specification: [FEATURE NAME] (~1172 tok)
- `tasks-template.md` — Tasks: [FEATURE NAME] (~2347 tok)

## .specify/workflows/

- `workflow-registry.json` (~109 tok)

## .specify/workflows/speckit/

- `workflow.yml` (~625 tok)

## backend/

- `.dockerignore` — Docker ignore rules (~113 tok)
- `.gitignore` — Git ignore rules (~180 tok)
- `AGENTS.md` — Backend agent instructions (~1174 tok)
- `Dockerfile` — Docker container definition (~278 tok)
- `dotnet-tools.json` (~56 tok)
- `Kahoot.slnx` (~88 tok)
- `projectSchema.dbml` — Declares varchar (~3935 tok)
- `README.md` — Project documentation (~630 tok)

## backend/src/Kahoot.Api/

- `appsettings.Development.json` (~151 tok)
- `appsettings.json` — .NET application settings (~120 tok)
- `appsettings.Production.json` (~116 tok)
- `Kahoot.Api.csproj` (~249 tok)
- `Program.cs` — Application entry point (~377 tok)

## backend/src/Kahoot.Api/Controllers/

- `ApiController.cs` — Controller: ApiController (~659 tok)
- `AuthController.cs` — Controller: AuthController (~346 tok)

## backend/src/Kahoot.Api/Endpoints/

- `HomePageEndpoint.cs` — HomePageEndpoint: MapHomePage (~1180 tok)

## backend/src/Kahoot.Api/HealthChecks/

- `DatabaseHealthCheck.cs` — DatabaseHealthCheck: CheckHealthAsync (~162 tok)

## backend/src/Kahoot.Api/Middleware/

- `GlobalExceptionHandler.cs` — GlobalExceptionHandler: TryHandleAsync (~732 tok)

## backend/src/Kahoot.Api/Properties/

- `launchSettings.json` (~256 tok)

## backend/src/Kahoot.Api/Services/

- `CurrentUser.cs` — Class: CurrentUser (~235 tok)

## backend/src/Kahoot.Application/

- `AssemblyReference.cs` — Class: AssemblyReference (~49 tok)
- `DependencyInjection.cs` — DependencyInjection: AddApplication (~189 tok)
- `Kahoot.Application.csproj` (~218 tok)

## backend/src/Kahoot.Application/Common/Behaviors/

- `ValidationBehavior.cs` — ValidationBehavior: Handle (~318 tok)

## backend/src/Kahoot.Application/Common/Exceptions/

- `PasswordHashingRateLimitedException.cs` — Thrown when the global password hashing concurrency floor (16 active, 50 queued) is exceeded. (~106 tok)
- `UniqueConstraintViolationException.cs` — Represents a database-provider-independent error that occurs when a unique constraint is violated. (~243 tok)

## backend/src/Kahoot.Application/Common/Interfaces/

- `ICurrentUser.cs` — Interface: ICurrentUser (0 members) (~46 tok)
- `IJwtTokenGenerator.cs` — Interface: IJwtTokenGenerator (1 members) (~44 tok)
- `IPasswordHasher.cs` — Interface: IPasswordHasher (0 members) (~111 tok)

## backend/src/Kahoot.Application/Common/Messaging/

- `ICommand.cs` — Interface: ICommand (0 members) (~132 tok)
- `IQuery.cs` — Interface: IQuery (0 members) (~85 tok)

## backend/src/Kahoot.Application/Common/Persistence/

- `IAppDbContext.cs` — DbContext: User, RefreshToken, Quiz, MediaItem, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessionTo... (~272 tok)

## backend/src/Kahoot.Application/Common/Results/

- `Error.cs` — Error: NotFound, Validation, Conflict, Unauthorized + 4 more (~476 tok)
- `ErrorType.cs` — Class: ErrorType (~59 tok)
- `Result.cs` — Result: Success, Failure (~303 tok)
- `ResultT.cs` — Class: Result (~162 tok)

## backend/src/Kahoot.Application/Features/Auth/

- `AuthErrors.cs` — Class: AuthErrors (~157 tok)

## backend/src/Kahoot.Application/Features/Auth/Login/

- `LoginCommand.cs` — Class: LoginCommand (~59 tok)
- `LoginCommandValidator.cs` — Class: LoginCommandValidator (~133 tok)
- `LoginRequest.cs` — Class: LoginRequest (~32 tok)
- `LoginResponse.cs` — Class: LoginResponse (~54 tok)
- `LoginResult.cs` — Class: LoginResult (~38 tok)

## backend/src/Kahoot.Application/Features/Auth/Register/

- `RegisterCommand.cs` — Class: RegisterCommand (~54 tok)
- `RegisterCommandHandler.cs` — Class: RegisterCommandHandler (~773 tok)
- `RegisterCommandValidator.cs` — Class: RegisterCommandValidator (~707 tok)
- `RegisterRequest.cs` — Class: RegisterRequest (~34 tok)
- `RegisterResponse.cs` — Class: RegisterResponse (~34 tok)

## backend/src/Kahoot.Domain/

- `Kahoot.Domain.csproj` (~58 tok)

## backend/src/Kahoot.Domain/Entities/

- `AnswerSubmission.cs` — Class: AnswerSubmission (~125 tok)
- `AnswerSubmissionChoice.cs` — Class: AnswerSubmissionChoice (~71 tok)
- `Choice.cs` — Class: Choice (~84 tok)
- `Game.cs` — Class: Game (~224 tok)
- `GameChoiceSnapshot.cs` — Class: GameChoiceSnapshot (~101 tok)
- `GameCommandIdempotency.cs` — Class: GameCommandIdempotency (~126 tok)
- `GameQuestionSnapshot.cs` — Class: GameQuestionSnapshot (~214 tok)
- `MediaItem.cs` — Class: MediaItem (~168 tok)
- `Participant.cs` — Class: Participant (~199 tok)
- `ParticipantSessionToken.cs` — Class: ParticipantSessionToken (~121 tok)
- `Question.cs` — Class: Question (~108 tok)
- `Quiz.cs` — Class: Quiz (~114 tok)
- `RefreshToken.cs` — Class: RefreshToken (~135 tok)
- `User.cs` — Class: User (~180 tok)

## backend/src/Kahoot.Domain/Enums/

- `GameStatus.cs` — Class: GameStatus (~48 tok)
- `MediaStatus.cs` — Class: MediaStatus (~27 tok)
- `UserRole.cs` — Class: UserRole (~25 tok)
- `UserStatus.cs` — Class: UserStatus (~25 tok)

## backend/src/Kahoot.Infrastructure/

- `AssemblyReference.cs` — Class: AssemblyReference (~50 tok)
- `DependencyInjection.cs` — DependencyInjection: AddInfrastructure (~755 tok)
- `Kahoot.Infrastructure.csproj` (~315 tok)

## backend/src/Kahoot.Infrastructure/Persistence/

- `AppDbContext.cs` — DbContext: User, RefreshToken, Quiz, MediaItem, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessionTo... (~929 tok)
- `DatabaseMigrationService.cs` — DatabaseMigrationService: StartAsync, StopAsync (~179 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Configurations/

- `AnswerSubmissionChoiceConfiguration.cs` — AnswerSubmissionChoiceConfiguration: Configure (~476 tok)
- `AnswerSubmissionConfiguration.cs` — AnswerSubmissionConfiguration: Configure (~738 tok)
- `ChoiceConfiguration.cs` — ChoiceConfiguration: Configure (~437 tok)
- `GameChoiceSnapshotConfiguration.cs` — GameChoiceSnapshotConfiguration: Configure (~497 tok)
- `GameCommandIdempotencyConfiguration.cs` — GameCommandIdempotencyConfiguration: Configure (~482 tok)
- `GameConfiguration.cs` — GameConfiguration: Configure (~783 tok)
- `GameQuestionSnapshotConfiguration.cs` — GameQuestionSnapshotConfiguration: Configure (~818 tok)
- `MediaItemConfiguration.cs` — MediaItemConfiguration: Configure (~560 tok)
- `ParticipantConfiguration.cs` — ParticipantConfiguration: Configure (~816 tok)
- `ParticipantSessionTokenConfiguration.cs` — ParticipantSessionTokenConfiguration: Configure (~532 tok)
- `QuestionConfiguration.cs` — QuestionConfiguration: Configure (~567 tok)
- `QuizConfiguration.cs` — QuizConfiguration: Configure (~407 tok)
- `RefreshTokenConfiguration.cs` — RefreshTokenConfiguration: Configure (~460 tok)
- `UserConfiguration.cs` — UserConfiguration: Configure (~451 tok)

## backend/src/Kahoot.Infrastructure/Security/

- `JwtOptions.cs` — Strongly typed options for JWT access token issuance. (~118 tok)
- `PasswordHasher.cs` — PasswordHasher: HashPasswordAsync, VerifyPasswordAsync, VerifyDummyPasswordAsync, Dispose (~1533 tok)

## backend/test/

- `.gitkeep` — Placeholder to preserve empty test folder in git (~14 tok)

## docs/

- `01-roles-and-access.md` — 01. Roles, Ownership, and Access (~5080 tok)
- `02-authentication.md` — 02. Authentication and Credential Lifecycle (~5396 tok)
- `03-account-management.md` — 03. Platform Account Management and Tenant Lifecycle (~3459 tok)
- `04-quiz-and-question-management.md` — 04. Quiz and Question Authoring (~3815 tok)
- `05-media-management.md` — 05. Media Management and Lifecycle (~3736 tok)
- `06-game-lifecycle.md` — 06. Game Lifecycle and Session State Machine (~4484 tok)
- `07-joining-and-lobby.md` — 07. Player Joining, Lobby Management, and Presence (~3822 tok)
- `08-live-gameplay.md` — 08. Live Gameplay and High-Throughput Ingestion (~3791 tok)
- `09-scoring-and-leaderboards.md` — 09. Scoring Engine and Leaderboards (~2713 tok)
- `10-realtime-and-protocol.md` — 10. Realtime Protocol and SignalR Communication (~3671 tok)
- `11-reconnection.md` — 11. Player Reconnection and State Catch-Up (~2909 tok)
- `12-platform-operations-and-health.md` — 12. Platform Operations, Health Probes, and Worker Orchestration (~3330 tok)
- `13-architecture-and-deployment.md` — 13. Architecture and Deployment (~5596 tok)
- `14-verification-and-testing.md` — 14. Verification and Testing (~9438 tok)

## frontend/

- `AGENTS.md` — React Engineering Standards (~9193 tok)
- `FOLDER_STRUCTURE.md` — Frontend Folder Structure & Architecture Guide (~3655 tok)
