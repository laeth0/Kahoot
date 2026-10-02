# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-10-01T12:01:09.174Z
> Files: 532 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `.gitignore` — Git ignore rules (~7 tok)
- `AGENTS.md` — Engineering Standards for AI Coding Agents (~10657 tok)
- `CLAUDE.md` — OpenWolf & Engineering Guidelines (~211 tok)
- `docker-compose.yml` — Docker Compose services (~1364 tok)
- `improving.md` — Phase 1 — Make PostgreSQL a Real Production Database (~5650 tok)
- `README.md` — Project documentation (~286 tok)
- `skills-lock.json` (~406 tok)

## .githooks/

- `pre-commit` (~161 tok)

## .github/workflows/

- `backend-ci.yml` — CI: Backend CI (~425 tok)

## backend/

- `.dockerignore` — Docker ignore rules (~113 tok)
- `.editorconfig` — Editor configuration (~56 tok)
- `.gitignore` — Git ignore rules (~180 tok)
- `AGENTS.md` — Backend agent instructions (~4812 tok)
- `Dockerfile` — Docker container definition (~312 tok)
- `dotnet-tools.json` (~56 tok)
- `Kahoot.slnx` (~274 tok)
- `projectSchema.dbml` — Declares varchar (~3991 tok)
- `README.md` — Project documentation (~2584 tok)

## backend/src/Kahoot.Api/

- `appsettings.Development.json` (~467 tok)
- `appsettings.json` — .NET application settings (~491 tok)
- `appsettings.Production.json` (~445 tok)
- `AssemblyReference.cs` — Class: AssemblyReference (~75 tok)
- `Kahoot.Api.csproj` (~417 tok)
- `Program.cs` — Application entry point (~3046 tok)

## backend/src/Kahoot.Api/Controllers/

- `AdminAdministratorsController.cs` — API Controller: AdminAdministratorsController (Get, Post) (~1420 tok)
- `AdminUsersController.cs` — API Controller: AdminUsersController (Get) (~1498 tok)
- `ApiController.cs` — Controller: ApiController (~1250 tok)
- `AuthController.cs` — Controller: AuthController (~3536 tok)
- `GamesController.cs` — API Controller: GamesController (Post) (~4758 tok)
- `ImagesController.cs` — Controller: ImagesController (~2081 tok)
- `QuizzesController.cs` — API Controller: QuizzesController (Post, Get) (~3066 tok)

## backend/src/Kahoot.Api/Endpoints/

- `HomePageEndpoint.cs` — Home Page Endpoint - Serves lightweight HTML landing page at root URL for service discovery and API reference navigation. (~1286 tok)

## backend/src/Kahoot.Api/HealthChecks/

- `DatabaseHealthCheck.cs` — DatabaseHealthCheck: CheckHealthAsync (~329 tok)
- `ReadinessHealthCheck.cs` — ReadinessHealthCheck: CheckHealthAsync (~1721 tok)
- `RedisHealthCheck.cs` — RedisHealthCheck: CheckHealthAsync (~392 tok)
- `StorageHealthCheck.cs` — StorageHealthCheck: CheckHealthAsync (~772 tok)

## backend/src/Kahoot.Api/Middleware/

- `AccessTokenScrubberMiddleware.cs` — AccessTokenScrubberMiddleware: InvokeAsync (~475 tok)
- `GlobalExceptionHandler.cs` — GlobalExceptionHandler: TryHandleAsync (~1940 tok)

## backend/src/Kahoot.Api/Options/

- `CorsOptions.cs` — CORS Configuration Options - Binds allowed frontend origin URLs for browser cross-origin policy enforcement. (~109 tok)

## backend/src/Kahoot.Api/Properties/

- `launchSettings.json` (~256 tok)

## backend/src/Kahoot.Api/ServiceCollectionExtension/

- `CorsInstaller.cs` — CorsInstaller: AddCorsPolicy (~315 tok)
- `JwtAuthenticationInstaller.cs` — JwtAuthenticationInstaller: AddJwtAuthentication (~2489 tok)
- `ObservabilityInstaller.cs` — ObservabilityInstaller: AddObservability, AddObservability (~2526 tok)

## backend/src/Kahoot.Api/Services/

- `CurrentUser.cs` — Class: CurrentUser (~392 tok)

## backend/src/Kahoot.Application/

- `AssemblyReference.cs` — Class: AssemblyReference (~76 tok)
- `DependencyInjection.cs` — DependencyInjection: AddApplication (~230 tok)
- `Kahoot.Application.csproj` (~270 tok)

## backend/src/Kahoot.Application/Common/

- `AccessTokenResult.cs` — The result of generating a JWT access token, carrying the token string alongside its expiration metadata so callers never have to duplicate or gues... (~105 tok)

## backend/src/Kahoot.Application/Common/Behaviors/

- `ValidationBehavior.cs` — ValidationBehavior: Handle (~502 tok)

## backend/src/Kahoot.Application/Common/Exceptions/

- `ForeignKeyConstraintViolationException.cs` — Relational Constraint Abstraction - Database-provider-independent exception representing foreign key constraint violations (~172 tok)
- `PasswordHashingRateLimitedException.cs` — Concurrency Saturation Barrier (AUTH-HASH-001) - Thrown when global Argon2id hashing capacity (16 active, 50 queued) is exhausted (~110 tok)
- `UniqueConstraintViolationException.cs` — Relational Constraint Abstraction - Database-provider-independent exception representing unique constraint collisions (~272 tok)

## backend/src/Kahoot.Application/Common/Interfaces/

- `IAnswerRateLimiter.cs` — Multi-Tier Answer Submission Rate Limiter - Enforces per-socket rolling burst limits and participant-scoped question attempt limits. (~154 tok)
- `ICriticalWorkerFailureTracker.cs` — Critical Worker Failure Tracker - Tracks background worker health and triggers readiness degradation when worker backlogs persist (OPS-WORK-002). (~218 tok)
- `ICurrentUser.cs` — Interface: ICurrentUser (1 members) (~125 tok)
- `IGameCommandIdempotencyService.cs` — Interface: IGameCommandIdempotencyService (2 members) (~284 tok)
- `IGameNotificationService.cs` — Interface: IGameNotificationService (13 members) (~812 tok)
- `IImageStorageService.cs` — Interface: IImageStorageService (4 members) (~300 tok)
- `IJwtTokenGenerator.cs` — Interface: IJwtTokenGenerator (2 members) (~92 tok)
- `ILobbyJoinRateLimiter.cs` — Interface: ILobbyJoinRateLimiter (1 members) (~85 tok)
- `ILoginRateLimiter.cs` — Interface: ILoginRateLimiter (6 members) (~197 tok)
- `IPasswordHasher.cs` — Interface: IPasswordHasher (2 members) (~208 tok)
- `IPinGeneratorService.cs` — Interface: IPinGeneratorService (2 members) (~64 tok)
- `IPlayerPresenceService.cs` — Interface: IPlayerPresenceService (3 members) (~191 tok)
- `ISocketEvictionService.cs` — Interface: ISocketEvictionService (2 members) (~89 tok)
- `ISuspensionFinalizerChannel.cs` — Interface: ISuspensionFinalizerChannel (3 members) (~196 tok)

## backend/src/Kahoot.Application/Common/Messaging/

- `ICommand.cs` — Interface: ICommand (0 members) (~132 tok)
- `IQuery.cs` — Interface: IQuery (0 members) (~85 tok)

## backend/src/Kahoot.Application/Common/Options/

- `BootstrapAdminOptions.cs` — Class: BootstrapAdminOptions (~183 tok)
- `GameJoinOptions.cs` — GameJoinOptions: HasValidOrigin (~352 tok)
- `ImageStorageOptions.cs` — Class: ImageStorageOptions (~675 tok)
- `RefreshTokenOptions.cs` — Refresh Token Lifecycle Configuration - Governs individual token validity and rotation family maximum lifetime (~183 tok)

## backend/src/Kahoot.Application/Common/Pagination/

- `KeysetCursor.cs` — KeysetCursor: Encode, TryDecode (~840 tok)

## backend/src/Kahoot.Application/Common/Persistence/

- `IAppDbContext.cs` — DbContext: User, RefreshToken, Quiz, QuestionImage, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessi... (~958 tok)

## backend/src/Kahoot.Application/Common/Results/

- `Error.cs` — Error: NotFound, Validation, Conflict, Unauthorized + 6 more (~799 tok)
- `ErrorType.cs` — Class: ErrorType (~330 tok)
- `Result.cs` — Result: Success, Failure (~559 tok)
- `ResultT.cs` — Class: Result (~214 tok)

## backend/src/Kahoot.Application/Common/Seeding/

- `ISeeder.cs` — Interface: ISeeder (1 members) (~36 tok)

## backend/src/Kahoot.Application/Features/Admin/

- `AccountErrors.cs` — Class: AccountErrors (~478 tok)

## backend/src/Kahoot.Application/Features/Admin/Administrators/

- `AdministratorResponse.cs` — Class: AdministratorResponse (~193 tok)

## backend/src/Kahoot.Application/Features/Admin/Administrators/CreateAdministrator/

- `CreateAdministratorCommand.cs` — Class: CreateAdministratorCommand (~82 tok)
- `CreateAdministratorCommandHandler.cs` — Class: CreateAdministratorCommandHandler (~1177 tok)
- `CreateAdministratorCommandValidator.cs` — Class: CreateAdministratorCommandValidator (~979 tok)
- `CreateAdministratorRequest.cs` — Class: CreateAdministratorRequest (~45 tok)

## backend/src/Kahoot.Application/Features/Admin/Administrators/ListAdministrators/

- `ListAdministratorsQuery.cs` — Class: ListAdministratorsQuery (~60 tok)
- `ListAdministratorsQueryHandler.cs` — Class: ListAdministratorsQueryHandler (~695 tok)

## backend/src/Kahoot.Application/Features/Admin/Administrators/ReactivateAdministrator/

- `ReactivateAdministratorCommand.cs` — Class: ReactivateAdministratorCommand (~101 tok)
- `ReactivateAdministratorCommandHandler.cs` — ReactivateAdministratorCommandHandler: Handle (~1207 tok)
- `ReactivateAdministratorCommandValidator.cs` — Class: ReactivateAdministratorCommandValidator (~212 tok)
- `ReactivateAdministratorRequest.cs` — Class: ReactivateAdministratorRequest (~82 tok)

## backend/src/Kahoot.Application/Features/Admin/Administrators/SuspendAdministrator/

- `SuspendAdministratorCommand.cs` — Class: SuspendAdministratorCommand (~99 tok)
- `SuspendAdministratorCommandHandler.cs` — SuspendAdministratorCommandHandler: Handle (~1774 tok)
- `SuspendAdministratorCommandValidator.cs` — Class: SuspendAdministratorCommandValidator (~209 tok)
- `SuspendAdministratorRequest.cs` — Class: SuspendAdministratorRequest (~80 tok)

## backend/src/Kahoot.Application/Features/Admin/Users/GetUserById/

- `GetUserByIdQuery.cs` — Class: GetUserByIdQuery (~53 tok)
- `GetUserByIdQueryHandler.cs` — Class: GetUserByIdQueryHandler (~516 tok)
- `GetUserByIdQueryValidator.cs` — Class: GetUserByIdQueryValidator (~128 tok)
- `UserAdminResponse.cs` — Class: UserAdminResponse (~193 tok)

## backend/src/Kahoot.Application/Features/Admin/Users/ListUsers/

- `ListUsersQuery.cs` — Class: ListUsersQuery (~86 tok)
- `ListUsersQueryHandler.cs` — Class: ListUsersQueryHandler (~1140 tok)
- `ListUsersQueryValidator.cs` — Class: ListUsersQueryValidator (~384 tok)
- `ListUsersResponse.cs` — Class: ListUsersResponse (~287 tok)

## backend/src/Kahoot.Application/Features/Admin/Users/ReactivateUser/

- `ReactivateUserCommand.cs` — Class: ReactivateUserCommand (~92 tok)
- `ReactivateUserCommandHandler.cs` — ReactivateUserCommandHandler: Handle (~1273 tok)
- `ReactivateUserCommandValidator.cs` — Class: ReactivateUserCommandValidator (~195 tok)
- `ReactivateUserRequest.cs` — Class: ReactivateUserRequest (~74 tok)

## backend/src/Kahoot.Application/Features/Admin/Users/SuspendUser/

- `SuspendUserCommand.cs` — Class: SuspendUserCommand (~95 tok)
- `SuspendUserCommandHandler.cs` — Class: SuspendUserCommandHandler (~1748 tok)
- `SuspendUserCommandValidator.cs` — Class: SuspendUserCommandValidator (~192 tok)
- `SuspendUserRequest.cs` — Class: SuspendUserRequest (~73 tok)
- `SuspendUserResponse.cs` — Class: SuspendUserResponse (~78 tok)
- `SuspendUserResult.cs` — Class: SuspendUserResult (~36 tok)

## backend/src/Kahoot.Application/Features/Auth/

- `AuthErrors.cs` — Class: AuthErrors (~655 tok)
- `UsernameNormalization.cs` — UsernameNormalization: GetDisplayUsername, GetNormalizedUsername (~168 tok)

## backend/src/Kahoot.Application/Features/Auth/Bootstrap/

- `SystemAdminSeeder.cs` — SystemAdminSeeder: SeedAsync (~1399 tok)

## backend/src/Kahoot.Application/Features/Auth/ChangePassword/

- `ChangePasswordCommand.cs` — Class: ChangePasswordCommand (~56 tok)
- `ChangePasswordCommandHandler.cs` — ChangePasswordCommandHandler: Handle (~1585 tok)
- `ChangePasswordCommandValidator.cs` — Class: ChangePasswordCommandValidator (~466 tok)
- `ChangePasswordRequest.cs` — Class: ChangePasswordRequest (~41 tok)

## backend/src/Kahoot.Application/Features/Auth/Login/

- `LoginCommand.cs` — Class: LoginCommand (~61 tok)
- `LoginCommandHandler.cs` — Class: LoginCommandHandler (~1927 tok)
- `LoginCommandValidator.cs` — Class: LoginCommandValidator (~225 tok)
- `LoginRequest.cs` — Class: LoginRequest (~33 tok)
- `LoginResponse.cs` — Class: LoginResponse (~55 tok)
- `LoginResult.cs` — Class: LoginResult (~40 tok)

## backend/src/Kahoot.Application/Features/Auth/Logout/

- `LogoutCommand.cs` — Class: LogoutCommand (~47 tok)
- `LogoutCommandHandler.cs` — LogoutCommandHandler: Handle (~766 tok)

## backend/src/Kahoot.Application/Features/Auth/LogoutAll/

- `LogoutAllCommand.cs` — Class: LogoutAllCommand (~42 tok)
- `LogoutAllCommandHandler.cs` — LogoutAllCommandHandler: Handle (~903 tok)

## backend/src/Kahoot.Application/Features/Auth/Refresh/

- `RefreshCommand.cs` — Class: RefreshCommand (~51 tok)
- `RefreshCommandHandler.cs` — Class: RefreshCommandHandler (~2265 tok)
- `RefreshRequest.cs` — Class: RefreshRequest (~33 tok)
- `RefreshResponse.cs` — Class: RefreshResponse (~56 tok)
- `RefreshResult.cs` — Class: RefreshResult (~41 tok)

## backend/src/Kahoot.Application/Features/Auth/Register/

- `RegisterCommand.cs` — Class: RegisterCommand (~55 tok)
- `RegisterCommandHandler.cs` — Class: RegisterCommandHandler (~914 tok)
- `RegisterCommandValidator.cs` — Class: RegisterCommandValidator (~882 tok)
- `RegisterRequest.cs` — Class: RegisterRequest (~35 tok)
- `RegisterResponse.cs` — Class: RegisterResponse (~35 tok)

## backend/src/Kahoot.Application/Features/Games/

- `GameControlRequest.cs` — State Transition Control Contract - Encapsulates client idempotency key and OCC version fence for all game control endpoints (~138 tok)
- `GameErrors.cs` — Class: GameErrors (~1521 tok)
- `ParticipantRankMaterializer.cs` — Class: ParticipantRankMaterializer (~466 tok)
- `QuestionResultsMaterializer.cs` — Class: QuestionResultsMaterializer (~1321 tok)

## backend/src/Kahoot.Application/Features/Games/AdvanceQuestion/

- `AdvanceQuestionCommand.cs` — Class: AdvanceQuestionCommand (~71 tok)
- `AdvanceQuestionCommandHandler.cs` — Class: AdvanceQuestionCommandHandler (~2644 tok)
- `AdvanceQuestionCommandValidator.cs` — Class: AdvanceQuestionCommandValidator (~283 tok)
- `AdvanceQuestionResponse.cs` — Class: AdvanceQuestionResponse (~71 tok)

## backend/src/Kahoot.Application/Features/Games/CreateGame/

- `CreateGameCommand.cs` — Class: CreateGameCommand (~51 tok)
- `CreateGameCommandHandler.cs` — Class: CreateGameCommandHandler (~2378 tok)
- `CreateGameCommandValidator.cs` — Class: CreateGameCommandValidator (~129 tok)
- `CreateGameRequest.cs` — Class: CreateGameRequest (~31 tok)
- `CreateGameResponse.cs` — Class: CreateGameResponse (~74 tok)

## backend/src/Kahoot.Application/Features/Games/EndGame/

- `EndGameCommand.cs` — Class: EndGameCommand (~64 tok)
- `EndGameCommandHandler.cs` — Class: EndGameCommandHandler (~2226 tok)
- `EndGameCommandValidator.cs` — Class: EndGameCommandValidator (~275 tok)
- `EndGameResponse.cs` — Class: EndGameResponse (~75 tok)

## backend/src/Kahoot.Application/Features/Games/EndQuestion/

- `EndQuestionCommand.cs` — Class: EndQuestionCommand (~68 tok)
- `EndQuestionCommandHandler.cs` — Class: EndQuestionCommandHandler (~1981 tok)
- `EndQuestionCommandValidator.cs` — Class: EndQuestionCommandValidator (~279 tok)
- `EndQuestionResponse.cs` — Class: EndQuestionResponse (~99 tok)

## backend/src/Kahoot.Application/Features/Games/GetGame/

- `GetGameQuery.cs` — Class: GetGameQuery (~48 tok)
- `GetGameQueryHandler.cs` — Class: GetGameQueryHandler (~822 tok)
- `GetGameResponse.cs` — Class: GetGameResponse (~103 tok)

## backend/src/Kahoot.Application/Features/Games/GetGameParticipants/

- `GetGameParticipantsQuery.cs` — Class: GetGameParticipantsQuery (~81 tok)
- `GetGameParticipantsQueryHandler.cs` — Class: GetGameParticipantsQueryHandler (~1083 tok)
- `GetGameParticipantsQueryValidator.cs` — Class: GetGameParticipantsQueryValidator (~296 tok)
- `GetGameParticipantsResponse.cs` — Class: GetGameParticipantsResponse (~70 tok)
- `ParticipantDto.cs` — Class: ParticipantDto (~61 tok)

## backend/src/Kahoot.Application/Features/Games/GetGameReport/

- `GetGameReportQuery.cs` — Class: GetGameReportQuery (~52 tok)
- `GetGameReportQueryHandler.cs` — Class: GetGameReportQueryHandler (~1455 tok)
- `GetGameReportResponse.cs` — Class: GetGameReportResponse (~113 tok)

## backend/src/Kahoot.Application/Features/Games/GetJoinInfo/

- `GetJoinInfoQuery.cs` — Class: GetJoinInfoQuery (~51 tok)
- `GetJoinInfoQueryHandler.cs` — Class: GetJoinInfoQueryHandler (~661 tok)
- `GetJoinInfoQueryValidator.cs` — Class: GetJoinInfoQueryValidator (~227 tok)
- `GetJoinInfoResponse.cs` — Class: GetJoinInfoResponse (~61 tok)

## backend/src/Kahoot.Application/Features/Games/JoinGame/

- `JoinGameCommand.cs` — Class: JoinGameCommand (~131 tok)
- `JoinGameCommandHandler.cs` — Class: JoinGameCommandHandler (~3156 tok)
- `JoinGameCommandValidator.cs` — Class: JoinGameCommandValidator (~437 tok)
- `JoinGameRequest.cs` — Class: JoinGameRequest (~44 tok)
- `JoinGameResponse.cs` — Class: JoinGameResponse (~125 tok)
- `PlayerNickname.cs` — PlayerNickname: TryNormalize (~577 tok)

## backend/src/Kahoot.Application/Features/Games/Models/

- `GameDtos.cs` — Class: GameDtos (~1797 tok)

## backend/src/Kahoot.Application/Features/Games/RemoveParticipant/

- `RemoveParticipantCommand.cs` — Class: RemoveParticipantCommand (~58 tok)
- `RemoveParticipantCommandHandler.cs` — RemoveParticipantCommandHandler: Handle (~3207 tok)
- `RemoveParticipantCommandValidator.cs` — Class: RemoveParticipantCommandValidator (~198 tok)

## backend/src/Kahoot.Application/Features/Games/Scoring/

- `ScoringEngine.cs` — ScoringEngine: EvaluateCorrectness, CalculatePoints (~400 tok)

## backend/src/Kahoot.Application/Features/Games/ShowLeaderboard/

- `ShowLeaderboardCommand.cs` — Class: ShowLeaderboardCommand (~71 tok)
- `ShowLeaderboardCommandHandler.cs` — Class: ShowLeaderboardCommandHandler (~1997 tok)
- `ShowLeaderboardCommandValidator.cs` — Class: ShowLeaderboardCommandValidator (~283 tok)
- `ShowLeaderboardResponse.cs` — Class: ShowLeaderboardResponse (~75 tok)

## backend/src/Kahoot.Application/Features/Games/StartGame/

- `StartGameCommand.cs` — Class: StartGameCommand (~66 tok)
- `StartGameCommandHandler.cs` — Class: StartGameCommandHandler (~2582 tok)
- `StartGameCommandValidator.cs` — Class: StartGameCommandValidator (~277 tok)
- `StartGameResponse.cs` — Class: StartGameResponse (~68 tok)

## backend/src/Kahoot.Application/Features/Games/SubmitAnswer/

- `SubmitAnswerCommand.cs` — Class: SubmitAnswerCommand (~134 tok)
- `SubmitAnswerCommandHandler.cs` — Class: SubmitAnswerCommandHandler (~4257 tok)
- `SubmitAnswerCommandValidator.cs` — Class: SubmitAnswerCommandValidator (~224 tok)
- `SubmitAnswerRequest.cs` — Answer Submission Request Contract - Encapsulates submitted choice selections for active question. (~69 tok)
- `SubmitAnswerResponse.cs` — Answer Submission Response - Acknowledges answer acceptance and reports whether the submission was an idempotent replay. (~75 tok)

## backend/src/Kahoot.Application/Features/Images/

- `ImageErrors.cs` — Class: ImageErrors (~511 tok)

## backend/src/Kahoot.Application/Features/Images/UploadImage/

- `UploadImageCommand.cs` — Class: UploadImageCommand (~176 tok)
- `UploadImageCommandHandler.cs` — Class: UploadImageCommandHandler (~1405 tok)
- `UploadImageResponse.cs` — Class: UploadImageResponse (~102 tok)

## backend/src/Kahoot.Application/Features/Quizzes/

- `QuizErrors.cs` — Class: QuizErrors (~741 tok)
- `QuizSummaryResponse.cs` — Class: QuizSummaryResponse (~70 tok)

## backend/src/Kahoot.Application/Features/Quizzes/CreateQuiz/

- `CreateQuizCommand.cs` — Class: CreateQuizCommand (~72 tok)
- `CreateQuizCommandHandler.cs` — Class: CreateQuizCommandHandler (~719 tok)
- `CreateQuizCommandValidator.cs` — Class: CreateQuizCommandValidator (~361 tok)
- `CreateQuizRequest.cs` — Class: CreateQuizRequest (~40 tok)

## backend/src/Kahoot.Application/Features/Quizzes/DeleteQuiz/

- `DeleteQuizCommand.cs` — Class: DeleteQuizCommand (~46 tok)
- `DeleteQuizCommandHandler.cs` — DeleteQuizCommandHandler: Handle (~1259 tok)
- `DeleteQuizCommandValidator.cs` — Class: DeleteQuizCommandValidator (~125 tok)

## backend/src/Kahoot.Application/Features/Quizzes/GetQuizById/

- `GetQuizByIdQuery.cs` — Class: GetQuizByIdQuery (~51 tok)
- `GetQuizByIdQueryHandler.cs` — Class: GetQuizByIdQueryHandler (~1160 tok)
- `GetQuizByIdQueryValidator.cs` — Class: GetQuizByIdQueryValidator (~122 tok)
- `QuizDetailsResponse.cs` — Class: QuizDetailsResponse (~183 tok)

## backend/src/Kahoot.Application/Features/Quizzes/ListQuizzes/

- `ListQuizzesQuery.cs` — Class: ListQuizzesQuery (~62 tok)
- `ListQuizzesQueryHandler.cs` — Class: ListQuizzesQueryHandler (~1046 tok)
- `ListQuizzesQueryValidator.cs` — Class: ListQuizzesQueryValidator (~255 tok)
- `ListQuizzesResponse.cs` — Class: ListQuizzesResponse (~53 tok)

## backend/src/Kahoot.Application/Features/Quizzes/Questions/

- `ChoiceRequest.cs` — Class: ChoiceRequest (~37 tok)
- `QuestionResponse.cs` — Class: QuestionResponse (~113 tok)

## backend/src/Kahoot.Application/Features/Quizzes/Questions/AddQuestion/

- `AddQuestionCommand.cs` — Class: AddQuestionCommand (~104 tok)
- `AddQuestionCommandHandler.cs` — Class: AddQuestionCommandHandler (~2193 tok)
- `AddQuestionCommandValidator.cs` — Class: AddQuestionCommandValidator (~1048 tok)
- `AddQuestionRequest.cs` — Class: AddQuestionRequest (~65 tok)

## backend/src/Kahoot.Application/Features/Quizzes/Questions/DeleteQuestion/

- `DeleteQuestionCommand.cs` — Class: DeleteQuestionCommand (~59 tok)
- `DeleteQuestionCommandHandler.cs` — DeleteQuestionCommandHandler: Handle (~1684 tok)
- `DeleteQuestionCommandValidator.cs` — Class: DeleteQuestionCommandValidator (~183 tok)

## backend/src/Kahoot.Application/Features/Quizzes/Questions/UpdateQuestion/

- `UpdateQuestionCommand.cs` — Class: UpdateQuestionCommand (~112 tok)
- `UpdateQuestionCommandHandler.cs` — Class: UpdateQuestionCommandHandler (~2894 tok)
- `UpdateQuestionCommandValidator.cs` — Class: UpdateQuestionCommandValidator (~897 tok)
- `UpdateQuestionRequest.cs` — Class: UpdateQuestionRequest (~82 tok)

## backend/src/Kahoot.Application/Features/Quizzes/ReorderQuestions/

- `ReorderQuestionsCommand.cs` — Class: ReorderQuestionsCommand (~68 tok)
- `ReorderQuestionsCommandHandler.cs` — Class: ReorderQuestionsCommandHandler (~1581 tok)
- `ReorderQuestionsCommandValidator.cs` — Class: ReorderQuestionsCommandValidator (~191 tok)
- `ReorderQuestionsRequest.cs` — Class: ReorderQuestionsRequest (~41 tok)
- `ReorderQuestionsResponse.cs` — Class: ReorderQuestionsResponse (~42 tok)

## backend/src/Kahoot.Application/Features/Quizzes/UpdateQuiz/

- `UpdateQuizCommand.cs` — Class: UpdateQuizCommand (~77 tok)
- `UpdateQuizCommandHandler.cs` — Class: UpdateQuizCommandHandler (~1144 tok)
- `UpdateQuizCommandValidator.cs` — Class: UpdateQuizCommandValidator (~419 tok)
- `UpdateQuizRequest.cs` — Class: UpdateQuizRequest (~40 tok)

## backend/src/Kahoot.Domain/

- `Kahoot.Domain.csproj` (~58 tok)

## backend/src/Kahoot.Domain/Entities/

- `AnswerSubmission.cs` — Class: AnswerSubmission (~372 tok)
- `AnswerSubmissionChoice.cs` — Class: AnswerSubmissionChoice (~172 tok)
- `Choice.cs` — Class: Choice (~242 tok)
- `Game.cs` — Class: Game (~669 tok)
- `GameChoiceSnapshot.cs` — Class: GameChoiceSnapshot (~282 tok)
- `GameCommandIdempotency.cs` — Class: GameCommandIdempotency (~360 tok)
- `GameQuestionSnapshot.cs` — Class: GameQuestionSnapshot (~660 tok)
- `IAuditableEntity.cs` — Interface: IAuditableEntity (0 members) (~172 tok)
- `Participant.cs` — Class: Participant (~612 tok)
- `ParticipantSessionToken.cs` — Class: ParticipantSessionToken (~331 tok)
- `Question.cs` — Class: Question (~361 tok)
- `QuestionImage.cs` — Class: QuestionImage (~371 tok)
- `Quiz.cs` — Class: Quiz (~343 tok)
- `RefreshToken.cs` — Class: RefreshToken (~412 tok)
- `User.cs` — Class: User (~549 tok)

## backend/src/Kahoot.Domain/Enums/

- `GameStatus.cs` — Class: GameStatus (~224 tok)
- `UserRole.cs` — Class: UserRole (~81 tok)
- `UserStatus.cs` — Class: UserStatus (~79 tok)

## backend/src/Kahoot.Infrastructure/

- `AssemblyReference.cs` — Class: AssemblyReference (~78 tok)
- `DependencyInjection.cs` — DependencyInjection: AddInfrastructure (~600 tok)
- `Kahoot.Infrastructure.csproj` (~333 tok)

## backend/src/Kahoot.Infrastructure/Persistence/

- `AppDbContext.cs` — DbContext: User, RefreshToken, Quiz, QuestionImage, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessi... (~1883 tok)
- `AuditableEntityInterceptor.cs` — AuditableEntityInterceptor: SavingChanges (~882 tok)
- `DatabaseMigrationService.cs` — DatabaseMigrationService: StartAsync, StopAsync (~1541 tok)
- `DatabaseOptions.cs` — Class: DatabaseOptions (~280 tok)
- `DatabaseSeeder.cs` — DatabaseSeeder: StartAsync, StopAsync (~946 tok)
- `GameAbandonmentWorker.cs` — Class: GameAbandonmentWorker (~3408 tok)
- `QuestionImageCleanupWorker.cs` — Class: QuestionImageCleanupWorker (~4706 tok)
- `RefreshTokenCleanupWorker.cs` — Class: RefreshTokenCleanupWorker (~2372 tok)
- `SuspensionFinalizerChannel.cs` — SuspensionFinalizerChannel: NotifySuspension, ReadAsync, WaitToReadAsync, TryRead (~811 tok)
- `SuspensionFinalizerWorker.cs` — Class: SuspensionFinalizerWorker (~3213 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Configurations/

- `AnswerSubmissionChoiceConfiguration.cs` — AnswerSubmissionChoiceConfiguration: Configure (~615 tok)
- `AnswerSubmissionConfiguration.cs` — AnswerSubmissionConfiguration: Configure (~942 tok)
- `ChoiceConfiguration.cs` — ChoiceConfiguration: Configure (~564 tok)
- `GameChoiceSnapshotConfiguration.cs` — GameChoiceSnapshotConfiguration: Configure (~634 tok)
- `GameCommandIdempotencyConfiguration.cs` — GameCommandIdempotencyConfiguration: Configure (~609 tok)
- `GameConfiguration.cs` — GameConfiguration: Configure (~1056 tok)
- `GameQuestionSnapshotConfiguration.cs` — GameQuestionSnapshotConfiguration: Configure (~1033 tok)
- `ParticipantConfiguration.cs` — ParticipantConfiguration: Configure (~1105 tok)
- `ParticipantSessionTokenConfiguration.cs` — ParticipantSessionTokenConfiguration: Configure (~677 tok)
- `QuestionConfiguration.cs` — QuestionConfiguration: Configure (~751 tok)
- `QuestionImageConfiguration.cs` — QuestionImageConfiguration: Configure (~675 tok)
- `QuizConfiguration.cs` — QuizConfiguration: Configure (~528 tok)
- `RefreshTokenConfiguration.cs` — RefreshTokenConfiguration: Configure (~628 tok)
- `UserConfiguration.cs` — UserConfiguration: Configure (~662 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Migrations/

- `20260929135314_InitialSchema.cs` — Class: InitialSchema (~11242 tok)
- `20260929135314_InitialSchema.Designer.cs` — <auto-generated /> (~13771 tok)
- `AppDbContextModelSnapshot.cs` — <auto-generated /> (~13744 tok)

## backend/src/Kahoot.Infrastructure/Realtime/

- `GameHub.cs` — GameHub: JoinAsHost, JoinGame (~12614 tok)
- `GameHubFilter.cs` — Class: GameHubFilter (~476 tok)
- `GameNotificationService.cs` — GameNotificationService: PublishQuestionStartedAsync, PublishQuestionEndedAsync, PublishQuestionEndedWithPersonalResultsAsync, PublishLeaderboardUp... (~2621 tok)
- `HostPresenceHeartbeatWorker.cs` — Class: HostPresenceHeartbeatWorker (~1096 tok)
- `HostPresenceService.cs` — HostPresenceService: TryGetConnection, RegisterAsync, RemoveAsync, HasAnyAsync + 4 more (~2345 tok)
- `PlayerPresenceHeartbeatWorker.cs` — Class: PlayerPresenceHeartbeatWorker (~1324 tok)
- `PlayerPresenceService.cs` — PlayerPresenceService: TryGetConnection, HasActiveConnectionAsync, RegisterAsync, RemoveAsync + 9 more (~3748 tok)
- `PlayerSocketEvictionSubscriber.cs` — Class: PlayerSocketEvictionSubscriber (~1350 tok)
- `RealtimeOptions.cs` — Realtime Subsystem Configuration - Defines connection parameters and channel prefix boundaries for Redis pub/sub and presence tracking. (~267 tok)
- `SocketEvictionService.cs` — SocketEvictionService: EvictUserSocketsAsync (~452 tok)
- `SocketEvictionSubscriber.cs` — SocketEvictionSubscriber: StartAsync, StopAsync (~656 tok)
- `UnauthenticatedSocketGuard.cs` — UnauthenticatedSocketGuard: Track, MarkAuthenticated, Remove, AbortAll + 2 more (~1043 tok)

## backend/src/Kahoot.Infrastructure/Security/

- `AnswerRateLimiter.cs` — AnswerRateLimiter: IsRateLimitedAsync (~1157 tok)
- `JwtOptions.cs` — JWT Subsystem Configuration - Declares issuer, audience, symmetric signing key, and access token lifetime boundaries. (~246 tok)
- `JwtTokenGenerator.cs` — JwtTokenGenerator: GenerateAccessToken (~855 tok)
- `LobbyJoinRateLimiter.cs` — LobbyJoinRateLimiter: IsRateLimitedAsync (~1015 tok)
- `LoginRateLimiter.cs` — LoginRateLimiter: IsIpRateLimited, GetUsernameBackoffDelay, RecordFailedAttempt, ResetFailedAttempts + 1 more (~1222 tok)
- `PasswordHasher.cs` — PasswordHasher: HashPasswordAsync, VerifyPasswordAsync, VerifyDummyPasswordAsync, Dispose (~2095 tok)

## backend/src/Kahoot.Infrastructure/ServiceCollectionExtension/

- `PersistenceInstaller.cs` — PersistenceInstaller: AddPersistence (~1740 tok)
- `RealtimeInstaller.cs` — RealtimeInstaller: AddRealtime (~1104 tok)
- `SecurityInstaller.cs` — SecurityInstaller: AddSecurity (~1104 tok)
- `StorageInstaller.cs` — StorageInstaller: AddStorage (~704 tok)

## backend/src/Kahoot.Infrastructure/Services/

- `CriticalWorkerFailureTracker.cs` — CriticalWorkerFailureTracker: ReportSuccess, ReportFailure, HasDegradedBacklog (~1036 tok)
- `GameCommandIdempotencyService.cs` — Class: GameCommandIdempotencyService (~1279 tok)
- `PinGeneratorService.cs` — PinGeneratorService: GeneratePin (~181 tok)

## backend/src/Kahoot.Infrastructure/Storage/

- `ImageStorageService.cs` — ImageStorageService: IsStorageAvailable (~5276 tok)

## backend/test/

- `Directory.Build.props` (~194 tok)

## backend/test/Kahoot.Api.IntegrationTests/

- `Kahoot.Api.IntegrationTests.csproj` (~194 tok)

## backend/test/Kahoot.Api.UnitTests/

- `Kahoot.Api.UnitTests.csproj` (~103 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Admin/

- `AdminAdministratorsControllerTests.cs` — AdminAdministratorsControllerTests: ListAdministrators_Success_DispatchesQueryAndReturnsOk, ListAdministrators_Failure_ReturnsProblemDetails, Creat... (~2354 tok)
- `AdminUsersControllerTests.cs` — AdminUsersControllerTests: ListUsers_WithDefaults_DispatchesQueryAndReturnsOk, ListUsers_WithFilters_DispatchesQueryWithFilters, GetUserById_Succes... (~2601 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Auth/

- `AuthControllerCommandTests.cs` — AuthControllerCommandTests: Register_Success_DispatchesCommandAndReturnsCreated, Register_Failure_ReturnsProblemDetailsWithoutCookieSideEffects, Lo... (~2675 tok)
- `AuthControllerCookieTests.cs` — AuthControllerCookieTests: Login_Success_SetsHardenedRefreshAndCsrfCookiesWithConfiguredLifetime, Refresh_Success_RotatesBothCookiesAndReturnsOnlyP... (~2613 tok)
- `AuthControllerCsrfTests.cs` — AuthControllerCsrfTests: Guard_MissingEmptyWhitespaceOrMultipleCsrfHeader_IsRejected, Guard_CookieAuth_MissingCsrfCookie_IsRejected, Guard_CookieAu... (~4921 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Games/

- `GamesControllerTests.cs` — GamesControllerTests: CreateGame_Success_DispatchesCommandAndReturnsCreated, CreateGame_Failure_ReturnsProblemDetails, GetGameById_Success_Dispatch... (~5342 tok)
- `SubmitAnswerTokenGuardTests.cs` — SubmitAnswerTokenGuardTests: SubmitAnswer_MissingTokenHeaders_Returns401BeforeDbContext, SubmitAnswer_EmptyOrWhitespaceSessionToken_WithoutBearer_R... (~1906 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Images/

- `ImagePathGuardTests.cs` — ImagePathGuardTests: GetUploadsRoot_Returns404ImageNotFound, GetImage_RejectsInvalidFilenameBeforeStorageResolver, GetImage_RejectsRawPathEncodedTr... (~1383 tok)
- `ImageUploadControllerTests.cs` — ImageUploadControllerTests: UploadImage_UnavailableStorageRejectsBeforeFormRead, UploadImage_MapsFormReadFailure_InvalidDataException_Returns400Val... (~6538 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Quizzes/

- `QuizzesControllerTests.cs` — QuizzesControllerTests: CreateQuiz_Success_DispatchesCommandAndReturnsCreatedAtAction, CreateQuiz_Failure_ReturnsProblemDetails, ListQuizzes_WithDe... (~5422 tok)

## backend/test/Kahoot.Api.UnitTests/Features/Shared/

- `ApiControllerFrameworkContractTests.cs` — ApiControllerFrameworkContractTests: Problem_WithDefaultMvcFactoryWithoutActivity_ReturnsMappedResponse, Problem_WithDefaultMvcFactoryAndActivity_R... (~832 tok)
- `ApiControllerTests.cs` — ApiControllerTests: Problem_MapsErrorTypeToHttpStatus, Problem_MapsKnownTitlesAndFallsBackToCode, Problem_PreservesDescriptionInstanceAndErrorTypeU... (~1594 tok)
- `ControllerSecurityMetadataDeclarationTests.cs` — ControllerSecurityMetadataDeclarationTests: Controllers_DeclareApiControllerAttribute, HostControllers_DeclareHostRoleRequirement, AdminControllers... (~1080 tok)

## backend/test/Kahoot.Api.UnitTests/HealthChecks/

- `StorageHealthCheckGuardTests.cs` — StorageHealthCheckGuardTests: CheckHealthAsync_WhenStorageUnavailable_ReturnsDegradedWithoutFileAccess, CheckHealthAsync_WhenStorageThrowsSynthetic... (~866 tok)

## backend/test/Kahoot.Api.UnitTests/Middleware/

- `AccessTokenScrubberMiddlewareTests.cs` — AccessTokenScrubberMiddlewareTests: InvokeAsync_RedactsHubTokenAndPreservesOriginalInItems, InvokeAsync_RedactsNonHubTokenWithoutRetainingCredentia... (~2390 tok)
- `GlobalExceptionHandlerTests.cs` — GlobalExceptionHandlerTests: TryHandleAsync_MapsValidationFailuresByProperty, TryHandleAsync_MapsImagePayloadLimitOnlyAtUploadEndpoint, TryHandleAs... (~4578 tok)

## backend/test/Kahoot.Api.UnitTests/ServiceCollectionExtension/

- `CorsInstallerTests.cs` — CorsInstallerTests: AddCorsPolicy_BindsConfiguredAllowedOrigins, AddCorsPolicy_RegistersFrontendNamedPolicy_WithCredentialsAndAnyHeaderMethod, AddC... (~1087 tok)
- `JwtAuthenticationInstallerTests.cs` — JwtAuthenticationInstallerTests: AddJwtAuthentication_ConfiguresExpectedTokenValidationParameters, AddJwtAuthentication_WithInvalidBase64SigningKey... (~762 tok)
- `JwtBearerEventTests.cs` — JwtBearerEventTests: OnMessageReceived_AtHubPath_ItemsTokenTakesPrecedenceOverQueryToken, OnMessageReceived_AtHubPath_WithoutItemsToken_ExtractsQue... (~4166 tok)
- `ObservabilityInstallerTests.cs` — ObservabilityInstallerTests: AddObservability_RejectsUnsupportedSampler, AddObservability_RejectsInvalidSamplerRatio, AddObservability_AcceptsSuppo... (~1368 tok)

## backend/test/Kahoot.Api.UnitTests/Services/

- `CurrentUserTests.cs` — CurrentUserTests: CurrentUser_NoContextReturnsAnonymousValues, UserId_ReadsNameIdentifierBeforeSub, UserId_InvalidPreferredClaimDoesNotFallBackToVa... (~1241 tok)

## backend/test/Kahoot.Api.UnitTests/TestSupport/

- `ApiConfiguration.cs` — ApiConfiguration: Create (~444 tok)
- `FailOnUseDbContext.cs` — DbContext: User, RefreshToken, Quiz, QuestionImage, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessi... (~563 tok)
- `HttpContextFactory.cs` — HttpContextFactory: Create, CreateWithLogging (~395 tok)
- `RecordingLogger.cs` — RecordingLogger: IsEnabled, Dispose (~474 tok)
- `RecordingSender.cs` — RecordingSender: RespondWithException (~790 tok)
- `StubFormFeature.cs` — StubFormFeature: ReadForm, ReadFormAsync (~397 tok)
- `StubFormFile.cs` — StubFormFile: OpenReadStream, CopyTo, CopyToAsync (~409 tok)
- `StubHostEnvironment.cs` — Class: StubHostEnvironment (~127 tok)
- `StubImageStorageService.cs` — StubImageStorageService: IsStorageAvailable, CompensateFile (~427 tok)
- `TestApiController.cs` — TestApiController: MapProblem (~74 tok)
- `TestProblemDetailsFactory.cs` — TestProblemDetailsFactory: CreateProblemDetails, CreateValidationProblemDetails (~346 tok)
- `TestRequestCookieCollection.cs` — TestRequestCookieCollection: ContainsKey, TryGetValue (~286 tok)
- `TestRequestCookiesFeature.cs` — Class: TestRequestCookiesFeature (~98 tok)
- `TrackingStream.cs` — Class: TrackingStream (~167 tok)

## backend/test/Kahoot.Application.IntegrationTests/

- `Kahoot.Application.IntegrationTests.csproj` (~136 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Admin/

- `AdministrationQueryTests.cs` — AdministrationQueryTests: ListUsers_FiltersAndPaginatesHostAccountsOnly, GetUserById_ReturnsHostMetadataAndHidesSecrets, ListAdministrators_Returns... (~1753 tok)
- `AdministratorConcurrencyTests.cs` — AdministratorConcurrencyTests: TwoAdministratorsSuspendingOneAnother_KeepAnActiveAdministrator (~948 tok)
- `AdministratorManagementTests.cs` — AdministratorManagementTests: CreateAdministrator_ValidCredentials_PersistsAdminAndEnforcesUniqueness, SuspendAdministrator_OneOfTwoAdmins_Suspends... (~1923 tok)
- `BootstrapSeederTests.cs` — BootstrapSeederTests: BootstrapDisabled_DoesNotCreateAdministrator, BootstrapEnabled_ValidCredentials_CreatesSingleAdministratorAndIsIdempotent, Bo... (~1353 tok)
- `HostAccountManagementTests.cs` — HostAccountManagementTests: SuspendUser_ValidHostWithoutUnfinishedGames_SuspendsWithoutFinalizerHint, SuspendUser_WithUnfinishedGame_SetsTerminatio... (~2454 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Auth/

- `AuthConcurrencyTests.cs` — AuthConcurrencyTests: ConcurrentRegistration_EquivalentUsernames_CommitOneAccount, ConcurrentRefresh_SameToken_ReturnsOneReplacementAndOneRace, Ref... (~4278 tok)
- `LoginTests.cs` — LoginTests: Login_ValidCredentials_PersistsHashedRefreshFamily, Login_Twice_CreatesIndependentFamilies, Login_UnknownWrongPasswordOrSuspended_Conce... (~2241 tok)
- `LogoutTests.cs` — LogoutTests: Logout_NullUnknownOrAlreadyRevokedToken_IsIdempotent, Logout_OldRotatedToken_RevokesItsReplacementFamily, LogoutAll_ActiveUser_Revokes... (~1623 tok)
- `PasswordChangeTests.cs` — PasswordChangeTests: ChangePassword_ValidCurrentPassword_CommitsHashAndRevocation, ChangePassword_WrongCurrentPassword_DoesNotMutate, ChangePasswor... (~1388 tok)
- `RefreshTests.cs` — RefreshTests: Refresh_ValidToken_RotatesWithinSameFamily, Refresh_RotatedTokenBeforeTenSeconds_ReturnsRefreshRace, Refresh_RotatedTokenAtTenSeconds... (~3282 tok)
- `RegistrationTests.cs` — RegistrationTests: Register_NewAccount_PersistsActiveHostWithAuditedDefaults, Register_NormalizedDuplicate_ReturnsUsernameUnavailable, Register_Use... (~1121 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Composition/

- `ApplicationCompositionTests.cs` — ApplicationCompositionTests: Composition_UsesProductionContextAndScopedRequestIdentity, MigratedDatabase_RoundTripsNativeEnums, SeparateHarnesses_D... (~2656 tok)
- `ValidationPipelineTests.cs` — ValidationPipelineTests: Send_InvalidRegister_ThrowsValidationExceptionWithoutPersistingUser, Send_InvalidCreateQuiz_ThrowsBeforePersistence, Send_... (~1352 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Concurrency/

- `GameplayConcurrencyTests.cs` — GameplayConcurrencyTests: ConcurrentAdvance_SameId_ReplaysOneTransition, ConcurrentAdvance_DifferentIdsSameVersion_RejectsStaleLoser, ConcurrentDup... (~7739 tok)
- `LobbyConcurrencyTests.cs` — LobbyConcurrencyTests: ConcurrentJoins_ForLastSeat_AdmitOne, ConcurrentJoins_SameNickname_ReserveOneTombstoneName, ConcurrentJoinReplay_SameOperati... (~4066 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Creation/

- `GameCreationTests.cs` — GameCreationTests: CreateGame_PopulatedOwnedQuiz_PersistsCompleteLobbySnapshot, CreateGame_EmptyQuiz_FailsValidationWithoutPersistingGame, CreateGa... (~3024 tok)
- `GamePinCollisionTests.cs` — GamePinCollisionTests: CreateGame_ActivePinCollision_RetriesAndSavesWithSecondPin, CreateGame_FiveCollisions_ReturnsPinUnavailable, CreateGame_Fini... (~2435 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Gameplay/

- `AnswerAdmissionTests.cs` — AnswerAdmissionTests: Submit_TooManyParticipantAttempts_Rejects, Submit_RateKeyDerivation_IgnoresClientQuestionIdBypass (~1761 tok)
- `AnswerSubmissionTests.cs` — AnswerSubmissionTests: Submit_ValidTokenHash_PersistsOneAnswerAndDistinctSelections, Submit_ValidGeneration_UsesCurrentParticipantBinding, Submit_E... (~5307 tok)
- `LeaderboardAndReportTests.cs` — LeaderboardAndReportTests: Leaderboard_UsesDatabaseOrderingAndExcludesRemovedPlayers, EndGameAndReport_UseCommittedSnapshotAndAnswerAggregates (~2504 tok)
- `QuestionResultsTests.cs` — QuestionResultsTests: Submit_LastEligibleAnswer_MaterializesAndClosesExactlyOnce, Submit_AlreadyCommittedAnswer_IsRecoverableReplay, EndQuestion_Wi... (~3130 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Lifecycle/

- `GameCommandReplayTests.cs` — GameCommandReplayTests: Replay_SameCommandIdAndPayload_ReturnsCachedResponseWithoutNewMutations, Replay_SameCommandIdAlteredPayloadOrCommand_Return... (~1970 tok)
- `GameTransitionTests.cs` — GameTransitionTests: Transition_TwoQuestionWorkflow_AdvancesThroughCompleteLifecycle, Transition_QuestionStartedPayloads_AudienceIsolation, Transit... (~4151 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Lobby/

- `GameJoinTests.cs` — GameJoinTests: Join_NewPlayer_PersistsSeatAndHashedSessionToken, Join_NormalizedNicknameCollision_IncludingTombstone_Rejects, Join_AtCapacity_Rejec... (~4133 tok)
- `JoinRecoveryTests.cs` — JoinRecoveryTests: Recover_SameOperationWithoutSocket_ReturnsSameParticipantAndNewToken, Recover_WithActiveSocket_ReturnsEmptyTokenWithoutNewRows, ... (~2798 tok)
- `LobbyQueryTests.cs` — LobbyQueryTests: GetJoinInfo_ReturnsCapacityWithoutParticipantSecrets, GetParticipants_PagesBySeatWithOptionalRemovedRows (~2867 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Games/Participants/

- `ParticipantRemovalTests.cs` — ParticipantRemovalTests: Remove_InLobby_SetsTombstoneRevokesTokensAndPreservesSeatAllocation, Remove_RepeatedRemoval_IsIdempotent, Remove_DuringQue... (~6071 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Images/

- `ImageUploadCompensationTests.cs` — ImageUploadCompensationTests: UploadImage_ForeignKeyViolation_CompensatesAndDeletesFile, UploadImage_UncertainPersistenceFailure_RetainsFileForReco... (~1464 tok)
- `ImageUploadPersistenceTests.cs` — ImageUploadPersistenceTests: UploadImage_ValidPng_PersistsSanitizedFileAndQuestionImageRow, UploadImage_ValidJpegWithExifMetadata_StripsMetadataAnd... (~1949 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Quizzes/

- `QuizMutationTests.cs` — QuizMutationTests: CreateQuiz_WithNullDescriptionAndTrimmedTitle_PersistsRevision1, UpdateQuiz_ValidData_UpdatesMetadataAndIncrementsRevision, Quiz... (~3800 tok)
- `QuizQueryTests.cs` — QuizQueryTests: GetQuizById_OwnedQuiz_ReturnsOrderedQuestionsAndChoices, GetQuizById_CrossTenantOrAnonymous_ReturnsExpectedError, ListQuizzes_Keyse... (~2801 tok)
- `QuizSnapshotConcurrencyTests.cs` — QuizSnapshotConcurrencyTests: QuizEditVsCreateGame_SerializesThroughHostRow, QuizDeleteVsCreateGame_RespectsBothCommitOrders (~2496 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Quizzes/Questions/

- `QuestionImageReferenceTests.cs` — QuestionImageReferenceTests: AttachImage_OwnUnusedImage_ClearsOrphanTimestamp, AttachImage_AbsentForeignOrAlreadyAttachedImage_Rejects, ReplaceOrRe... (~3100 tok)
- `QuestionMutationTests.cs` — QuestionMutationTests: AddQuestion_PersistsOwnedOrderedChoicesAndIncrementsQuizRevision, AddQuestion_AtTwoHundredQuestions_RejectsWithoutPartialRow... (~2997 tok)
- `QuestionOrderingTests.cs` — QuestionOrderingTests: DeleteQuestion_CompactsMiddleOrdering, ReorderQuestions_ReversesExistingSetWithoutUniqueCollision, ReorderQuestions_MissingD... (~2682 tok)

## backend/test/Kahoot.Application.IntegrationTests/Features/Reliability/

- `CancellationTests.cs` — CancellationTests: Request_CancelledWhileWaitingForHeldHostLock_LeavesNoChanges, Request_CancelledBeforeCommit_RollsBackGraph (~1385 tok)
- `PostCommitFailureTests.cs` — PostCommitFailureTests: GameBroadcast_RequestCancelledAfterCommit_UsesIndependentToken, GameBroadcast_ThrowsAfterCommit_StateAndReplayRemainDurable... (~2273 tok)
- `TransactionRollbackTests.cs` — TransactionRollbackTests: UpdateQuestion_BeforeCommitFailure_RestoresDeletedAndInsertedChoices, StartGame_BeforeCommitFailure_RollsBackStateAndIdem... (~2285 tok)

## backend/test/Kahoot.Application.IntegrationTests/TestSupport/

- `AdjustableTimeProvider.cs` — AdjustableTimeProvider: GetUtcNow, SetUtcNow, Advance (~136 tok)
- `ApplicationDependencyFixture.cs` — ApplicationDependencyFixture: InitializeAsync, CreateHarnessAsync, DisposeAsync (~869 tok)
- `ApplicationIntegrationCollection.cs` — Class: ApplicationIntegrationCollection (~73 tok)
- `ApplicationRequestScope.cs` — ApplicationRequestScope: Dispose, DisposeAsync (~337 tok)
- `ApplicationServices.cs` — TestCallerHolder: BuildServiceProvider (~1837 tok)
- `ApplicationTestHarness.cs` — ApplicationTestHarness: CreateRequestScope, ReadDbAsync, ReadDbAsync, DisposeAsync (~1160 tok)
- `ControlledPlayerPresenceService.cs` — ControlledPlayerPresenceService: SetActive, HasActiveConnectionAsync, GetConnectedCountAsync, EvictParticipantAsync (~426 tok)
- `DatabaseSandbox.cs` — DatabaseSandbox: InitializeAsync, DisposeAsync (~1306 tok)
- `FailOnUseImageStorageService.cs` — FailOnUseImageStorageService: IsStorageAvailable, CompensateFile (~271 tok)
- `FaultInjectionAppDbContext.cs` — DbContext: User, RefreshToken, Quiz, QuestionImage, Question, Choice, Game, GameQuestionSnapshot, GameChoiceSnapshot, Participant, ParticipantSessi... (~796 tok)
- `FeatureData.cs` — Class: FeatureData (~1564 tok)
- `RecordingGameNotificationService.cs` — RecordingGameNotificationService: PublishQuestionStartedAsync, PublishQuestionEndedAsync, PublishQuestionEndedWithPersonalResultsAsync, PublishLead... (~1508 tok)
- `RecordingSocketEvictionService.cs` — RecordingSocketEvictionService: EvictUserSocketsAsync (~232 tok)
- `RecordingSuspensionFinalizerChannel.cs` — RecordingSuspensionFinalizerChannel: NotifySuspension, ReadAsync, WaitToReadAsync, TryRead (~306 tok)
- `SequencePinGenerator.cs` — SequencePinGenerator: GeneratePin (~201 tok)
- `TestCaller.cs` — TestCaller: Host, SystemAdmin, Admin (~124 tok)
- `TestCurrentUser.cs` — Class: TestCurrentUser (~117 tok)
- `TestTimeProvider.cs` — TestTimeProvider: GetUtcNow, SetUtcNow, Advance (~122 tok)

## backend/test/Kahoot.Application.IntegrationTests/TestSupport/Concurrency/

- `PostgresLockObserver.cs` — Class: PostgresLockObserver (~604 tok)
- `SaveChangesGate.cs` — SaveChangesGate: SignalReached, Release, AwaitReleaseAsync (~230 tok)
- `SaveChangesGateInterceptor.cs` — Class: SaveChangesGateInterceptor (~247 tok)
- `ScopeConcurrencyGate.cs` — Class: ScopeConcurrencyGate (~106 tok)
- `TransactionCommitGate.cs` — TransactionCommitGate: SignalReached, Release, AwaitReleaseAsync (~232 tok)
- `TransactionCommitInterceptor.cs` — Class: TransactionCommitInterceptor (~452 tok)

## backend/test/Kahoot.Application.IntegrationTests/TestSupport/Faults/

- `CommitFailureInterceptor.cs` — CommitFailureInterceptor: TransactionCommittingAsync, TransactionCommittedAsync (~377 tok)

## backend/test/Kahoot.Application.IntegrationTests/TestSupport/Images/

- `TemporaryImageStorage.cs` — TemporaryImageStorage: GetPhysicalPath, FileExists, Dispose, DisposeAsync (~657 tok)
- `TestHostEnvironment.cs` — Class: TestHostEnvironment (~179 tok)
- `TestImageGenerator.cs` — Class: TestImageGenerator (~389 tok)

## backend/test/Kahoot.Application.UnitTests/

- `Kahoot.Application.UnitTests.csproj` (~71 tok)

## backend/test/Kahoot.Application.UnitTests/Common/Behaviors/

- `ValidationBehaviorTests.cs` — ValidationBehaviorTests: Handle_WhenNoValidatorsRegistered_CallsNextDelegateExactlyOnce, Handle_WhenRequestIsValid_CallsNextDelegateExactlyOnce, Ha... (~1627 tok)

## backend/test/Kahoot.Application.UnitTests/Common/Options/

- `GameJoinOptionsTests.cs` — GameJoinOptionsTests: HasValidOrigin_WithPermittedOrigins_ReturnsTrue, HasValidOrigin_WithRejectedOrigins_ReturnsFalse (~463 tok)

## backend/test/Kahoot.Application.UnitTests/Common/Pagination/

- `KeysetCursorTests.cs` — KeysetCursorTests: EncodeAndTryDecode_RoundTripsSuccessfully, EncodeAndTryDecode_WithTimestampBoundaries_RoundTripsSuccessfully, TryDecode_WhenNull... (~1118 tok)

## backend/test/Kahoot.Application.UnitTests/Common/Results/

- `ErrorTests.cs` — ErrorTests: None_HasEmptyPropertiesAndNoneType, FactoryMethods_CreateExpectedErrorTypeAndPreserveCodeAndDescription (~583 tok)
- `ResultTests.cs` — ResultTests: Success_CreatesSuccessfulResultWithNoneError, Failure_CreatesFailedResultWithSpecifiedError, Constructor_WhenSuccessWithNonNoneError_T... (~1192 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Admin/Administrators/

- `AdministratorLifecycleCommandValidatorTests.cs` — AdministratorLifecycleCommandValidatorTests: SuspendAdministrator_WithValidCommand_Succeeds, SuspendAdministrator_WhenAdministratorIdIsEmpty_FailsW... (~812 tok)
- `CreateAdministratorCommandValidatorTests.cs` — CreateAdministratorCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenUsernameIsEmpty_FailsWithRequiredMessage, Validate_WhenU... (~1393 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Admin/Users/

- `GetUserByIdQueryValidatorTests.cs` — GetUserByIdQueryValidatorTests: Validate_WithValidAccountId_Succeeds, Validate_WhenAccountIdIsEmpty_FailsWithRequiredMessage (~242 tok)
- `ListUsersQueryValidatorTests.cs` — ListUsersQueryValidatorTests: Validate_WithDefaultQuery_Succeeds, Validate_WhenPageSizeIsWithinBounds_Succeeds, Validate_WhenPageSizeIsOutOfBounds_... (~975 tok)
- `UserLifecycleCommandValidatorTests.cs` — UserLifecycleCommandValidatorTests: SuspendUser_WithValidCommand_Succeeds, SuspendUser_WhenAccountIdIsEmpty_FailsWithRequiredMessage, SuspendUser_W... (~840 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Auth/

- `ChangePasswordCommandValidatorTests.cs` — ChangePasswordCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenCurrentPasswordIsEmpty_FailsWithRequiredMessage, Validate_Whe... (~1190 tok)
- `LoginCommandValidatorTests.cs` — LoginCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WithSimplePassword_SucceedsWithoutEnforcingRegistrationComplexity, Validat... (~700 tok)
- `RegisterCommandValidatorTests.cs` — RegisterCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenUsernameIsEmpty_FailsWithRequiredMessage, Validate_WhenUsernameShor... (~1663 tok)
- `UsernameNormalizationTests.cs` — UsernameNormalizationTests: GetDisplayUsername_StripsLeadingAndTrailingSpacesOnly, GetDisplayUsername_PreservesOriginalCasing, GetNormalizedUsernam... (~479 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/

- `GameLifecycleCommandValidatorTests.cs` — GameLifecycleCommandValidatorTests: CreateGameCommandValidator_WithValidQuizId_Succeeds, CreateGameCommandValidator_WhenQuizIdIsEmpty_Fails, Lifecy... (~2106 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/GetGameParticipants/

- `GetGameParticipantsQueryValidatorTests.cs` — GetGameParticipantsQueryValidatorTests: Validate_WithValidQuery_Succeeds, Validate_WithNullCursorAndLimit_Succeeds, Validate_WhenGameIdIsEmpty_Fail... (~914 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/GetJoinInfo/

- `GetJoinInfoQueryValidatorTests.cs` — GetJoinInfoQueryValidatorTests: Validate_WithValidPin_Succeeds, Validate_WhenPinIsEmpty_FailsWithRequiredMessage, Validate_WhenPinIsInvalid_FailsWi... (~387 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/JoinGame/

- `JoinGameCommandValidatorTests.cs` — JoinGameCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenPinIsWithinFourToEightDigits_Succeeds, Validate_WhenPinIsEmpty_Fail... (~1014 tok)
- `PlayerNicknameTests.cs` — PlayerNicknameTests: TryNormalize_WithValidNickname_ReturnsTrueWithTrimmedAndUpperNfkc, TryNormalize_WhenNullOrWhitespace_ReturnsFalse, TryNormaliz... (~988 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/Scoring/

- `ScoringEngineTests.cs` — ScoringEngineTests: EvaluateCorrectness_WhenChoicesMatchExactly_ReturnsTrue, EvaluateCorrectness_IsOrderIndependent, EvaluateCorrectness_IgnoresDup... (~1869 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Games/SubmitAnswer/

- `SubmitAnswerCommandValidatorTests.cs` — SubmitAnswerCommandValidatorTests: Validate_WithValidCommandAndSessionTokenHash_Succeeds, Validate_WithValidCommandAndConnectionGeneration_Succeeds... (~929 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Images/

- `UploadImageCommandHandlerTests.cs` — UploadImageCommandHandlerTests: Handle_WhenCallerNotAuthenticated_ReturnsUnauthorizedError, Handle_WhenContentStreamIsNullStream_ReturnsMissingFile... (~1813 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Quizzes/

- `CreateQuizCommandValidatorTests.cs` — CreateQuizCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WithNullDescription_Succeeds, Validate_WhenTitleIsEmptyOrWhitespace_F... (~744 tok)
- `DeleteQuizCommandValidatorTests.cs` — DeleteQuizCommandValidatorTests: Validate_WithValidQuizId_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage (~238 tok)
- `GetQuizByIdQueryValidatorTests.cs` — GetQuizByIdQueryValidatorTests: Validate_WithValidQuizId_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage (~235 tok)
- `ListQuizzesQueryValidatorTests.cs` — ListQuizzesQueryValidatorTests: Validate_WithDefaultQuery_Succeeds, Validate_WithValidCursorAndPageSize_Succeeds, Validate_WhenPageSizeIsWithinBoun... (~640 tok)
- `ReorderQuestionsCommandValidatorTests.cs` — ReorderQuestionsCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage, Validate_WhenQuesti... (~496 tok)
- `UpdateQuizCommandValidatorTests.cs` — UpdateQuizCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage, Validate_WhenTitleIsEmpty... (~626 tok)

## backend/test/Kahoot.Application.UnitTests/Features/Quizzes/Questions/

- `AddQuestionCommandValidatorTests.cs` — AddQuestionCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage, Validate_WhenTextIsEmpty... (~2598 tok)
- `ChoiceRequestValidatorTests.cs` — ChoiceRequestValidatorTests: Validate_WithValidChoice_Succeeds, Validate_WhenTextIsEmptyOrWhitespace_FailsWithRequiredMessage, Validate_WhenTextExc... (~467 tok)
- `DeleteQuestionCommandValidatorTests.cs` — DeleteQuestionCommandValidatorTests: Validate_WithValidIdentifiers_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage, Validate_WhenQues... (~356 tok)
- `UpdateQuestionCommandValidatorTests.cs` — UpdateQuestionCommandValidatorTests: Validate_WithValidRequest_Succeeds, Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage, Validate_WhenQuestion... (~1356 tok)

## backend/test/Kahoot.Domain.UnitTests/

- `Kahoot.Domain.UnitTests.csproj` (~59 tok)

## backend/test/Kahoot.Domain.UnitTests/Entities/

- `GameTests.cs` — GameTests: NewGame_StartsInCreatedState, NewGame_StartsWithInitialStateVersion, NewGame_StartsSeatNumberingAtOne (~190 tok)
- `QuizTests.cs` — QuizTests: NewQuiz_StartsWithInitialRevision (~82 tok)
- `UserTests.cs` — UserTests: NewUser_DefaultsToHostRole, NewUser_DefaultsToActiveStatus, NewUser_StartsWithInitialTokenSecurityVersion, NewUser_StartsWithInitialRevi... (~273 tok)

## backend/test/Kahoot.Domain.UnitTests/Enums/

- `GameStatusTests.cs` — GameStatusTests: DefinedNames_MatchCanonicalLifecycleStates, DefinedValues_AreDistinct (~193 tok)
- `UserRoleTests.cs` — UserRoleTests: DefinedNames_MatchSupportedAccountRoles, DefinedValues_AreDistinct (~148 tok)
- `UserStatusTests.cs` — UserStatusTests: DefinedNames_MatchSupportedAccountStatuses, DefinedValues_AreDistinct (~151 tok)

## backend/test/Kahoot.Infrastructure.IntegrationTests/

- `Kahoot.Infrastructure.IntegrationTests.csproj` (~140 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/

- `DependencyInjectionTests.cs` — DependencyInjectionTests: AddInfrastructure_WithValidConfiguration_RegistersRootServicesSuccessfully, AddInfrastructure_DisasterRecoveryAbsentOrFal... (~1148 tok)
- `Kahoot.Infrastructure.UnitTests.csproj` (~92 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/Persistence/

- `AuditableEntityInterceptorTests.cs` — AuditableEntityInterceptorTests: SaveChanges_AddedEntityGetsInjectedUtcTimestampsAndActor_Sync, SaveChanges_AddedEntityGetsInjectedUtcTimestampsAnd... (~2599 tok)
- `SuspensionFinalizerChannelTests.cs` — SuspensionFinalizerChannelTests: NotifySuspension_DeliversHintsInOrderFromSingleProducer, NotifySuspension_PreservesRepeatedHints, ReadAsync_WakesA... (~1549 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/Realtime/

- `GameHubFilterTests.cs` — GameHubFilterTests: InvokeMethodAsync_ReturnsDelegateResultWithoutModification, InvokeMethodAsync_ConvertsUnexpectedExceptionToSafeEnvelope, Invoke... (~1456 tok)
- `GameNotificationFailureTests.cs` — GameNotificationFailureTests: PublishAsync_LogsTransportFailureAndContinuesOtherAudience, PublishPersonalEvents_ContinuesAfterOneFailedRecipient, P... (~1337 tok)
- `GameNotificationPersonalEventsTests.cs` — GameNotificationPersonalEventsTests: PublishQuestionEndedWithPersonalResultsAsync_RoutesAggregateThenPersonalEvents, PublishQuestionEndedWithPerson... (~2793 tok)
- `GameNotificationServiceTests.cs` — GameNotificationServiceTests: PublishQuestionStartedAsync_RoutesDistinctAudiencePayloads, PublishAggregateMethods_RouteToBothHostAndPlayerGroups, P... (~1166 tok)
- `RealtimeOptionsTests.cs` — RealtimeOptionsTests: HasValidChannelPrefix_AcceptsAsciiIdentifier, HasValidChannelPrefix_AcceptsMaxLengthSixtyFour, HasValidChannelPrefix_RejectsI... (~516 tok)
- `UnauthenticatedSocketGuardTests.cs` — UnauthenticatedSocketGuardTests: Track_SchedulesOneShotFifteenSecondTimeout, Timeout_AbortsTrackedConnectionAndDisposesTimerOnce, MarkAuthenticated... (~1498 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/Security/

- `JwtTokenGeneratorTests.cs` — JwtTokenGeneratorTests: GenerateAccessToken_IncludesIdentityAndSecurityClaims, GenerateAccessToken_ReturnsMatchingLifetimeMetadata, GenerateAccessT... (~2699 tok)
- `LoginRateLimiterTests.cs` — LoginRateLimiterTests: IsIpRateLimited_AllowsThirtyAttemptsThenRejects, IsIpRateLimited_ExpiresAttemptsAtExactlyOneMinute, IsIpRateLimited_UsesRoll... (~2994 tok)
- `PasswordHasherTests.cs` — PasswordHasherTests: HashPasswordAsync_ProducesConfiguredArgon2idFormat, HashPasswordAsync_UsesDistinctSaltsForRepeatedPassword, VerifyPasswordAsyn... (~1591 tok)
- `PasswordHashingCollection.cs` — Class: PasswordHashingCollection (~51 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/ServiceCollectionExtension/

- `PersistenceInstallerTests.cs` — PersistenceInstallerTests: AddPersistence_RegistersExpectedServiceDescriptors, AddPersistence_ValidConfiguration_ResolvesDatabaseOptionsSuccessfull... (~2140 tok)
- `RealtimeInstallerTests.cs` — RealtimeInstallerTests: AddRealtime_RegistersExpectedLifetimes, AddRealtime_ConfiguresSignalRHubOptionsWithoutInvokingRedisFactory, AddRealtime_Rej... (~1441 tok)
- `SecurityInstallerTests.cs` — SecurityInstallerTests: AddSecurity_RegistersExpectedSingletonDescriptors, AddSecurity_ValidBaselineConfiguration_ResolvesOptionsSuccessfully, AddS... (~3465 tok)
- `StorageInstallerTests.cs` — StorageInstallerTests: AddStorage_RegistersSingletonImageStorageServiceDescriptor, AddStorage_DefaultOptions_ResolvesSuccessfully, AddStorage_Mutat... (~1343 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/Services/

- `CriticalWorkerFailureTrackerTests.cs` — CriticalWorkerFailureTrackerTests: HasDegradedBacklog_NoFailuresReturnsFalseAndNullDetails, HasDegradedBacklog_DegradesAtExactlyFifteenMinutes, Rep... (~2014 tok)
- `PinGeneratorServiceTests.cs` — PinGeneratorServiceTests: GeneratePin_ReturnsEightAsciiDigits, GeneratePin_UsesInvariantDigitsUnderDifferentCulture (~391 tok)

## backend/test/Kahoot.Infrastructure.UnitTests/TestSupport/

- `FilterTestHub.cs` — FilterTestHub: SampleHubMethod (~49 tok)
- `InfrastructureConfiguration.cs` — InfrastructureConfiguration: BuildConfiguration (~407 tok)
- `ManualTimeProvider.cs` — ManualTimeProvider: GetUtcNow, Advance, SetUtcNow (~218 tok)
- `RecordedSend.cs` — Class: RecordedSend (~77 tok)
- `RecordingHubContext.cs` — RecordingHubContext: Group, AllExcept, Client, Clients + 5 more (~793 tok)
- `RecordingLogger.cs` — RecordingLogger: IsEnabled, Dispose (~477 tok)
- `RecordingTimerTimeProvider.cs` — RecordingTimerTimeProvider: CreateTimer, Change, Dispose, DisposeAsync + 1 more (~487 tok)
- `StubCurrentUser.cs` — Class: StubCurrentUser (~114 tok)
- `StubHubCallerContext.cs` — StubHubCallerContext: Abort (~340 tok)
- `SuppressPersistenceInterceptor.cs` — SuppressPersistenceInterceptor: SavingChanges (~202 tok)

## docs/

- `01-roles-and-access.md` — 01. Roles, Ownership, and Access (~5099 tok)
- `02-authentication.md` — 02. Authentication and Credential Lifecycle (~5466 tok)
- `03-account-management.md` — 03. Platform Account Management and Tenant Lifecycle (~3445 tok)
- `04-quiz-and-question-management.md` — 04. Quiz and Question Authoring (~3274 tok)
- `05-image-management.md` — 05. Image Management and Lifecycle (~5025 tok)
- `06-game-lifecycle.md` — 06. Game Lifecycle and Session State Machine (~4473 tok)
- `07-joining-and-lobby.md` — 07. Player Joining, Lobby Management, and Presence (~3956 tok)
- `08-live-gameplay.md` — 08. Live Gameplay and High-Throughput Ingestion (~3836 tok)
- `09-scoring-and-leaderboards.md` — 09. Scoring Engine and Leaderboards (~2713 tok)
- `10-realtime-and-protocol.md` — 10. Realtime Protocol and SignalR Communication (~3748 tok)
- `11-reconnection.md` — 11. Player Reconnection and State Catch-Up (~2948 tok)
- `12-platform-operations-and-health.md` — 12. Platform Operations, Health Probes, and Worker Orchestration (~4149 tok)
- `13-architecture-and-deployment.md` — 13. Architecture and Deployment (~7664 tok)
- `14-verification-and-testing.md` — 14. Verification and Testing (~9419 tok)

## docs/superpowers/plans/

- `2026-10-01-application-integration-tests.md` — Kahoot Application Integration Tests Implementation Plan (~20140 tok)
- `2026-10-01-infrastructure-integration-tests.md` — Kahoot Infrastructure Integration Tests Implementation Plan (~19062 tok)

## frontend/

- `AGENTS.md` — React Engineering Standards (~9193 tok)
- `FOLDER_STRUCTURE.md` — Frontend Folder Structure & Architecture Guide (~3655 tok)
- `libraries.md` — Recommended Frontend Stack (~2036 tok)

## observability/

- `loki.yaml` (~264 tok)
- `otel-collector.yaml` (~767 tok)
- `prometheus.yaml` (~96 tok)

## observability/grafana/provisioning/dashboards/

- `dashboards.yaml` (~60 tok)
- `platform-overview.json` (~2539 tok)

## observability/grafana/provisioning/datasources/

- `datasources.yaml` (~212 tok)

## prompts/

- `learn-phase 1 and 2.md` — Declares validation (~1903 tok)
- `learn-phase 10.md` (~996 tok)
- `learn-phase 11.md` (~788 tok)
- `learn-phase 12.md` (~1573 tok)
- `learn-phase 13.md` (~1515 tok)
- `learn-phase 14.md` (~1448 tok)
- `learn-phase 3.md` (~669 tok)
- `learn-phase 4.md` (~516 tok)
- `learn-phase 5.md` — Declares or (~838 tok)
- `learn-phase 6.md` (~796 tok)
- `learn-phase 7.md` (~822 tok)
- `learn-phase 8.md` (~750 tok)
- `learn-phase 9.md` (~744 tok)
