# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-09-09T18:43:27.034Z
> Files: 255 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `AGENTS.md` — OpenWolf (~75 tok)
- `CLAUDE.md` — OpenWolf (~99 tok)
- `docker-compose.yml` — Docker Compose services (~509 tok)
- `Final Claude Code Prompt — Kahoot-like Platform.md` — 1. Required Technology Stack (~7074 tok)
- `GEMINI.md` — OpenWolf (~75 tok)

## backend/

- `projectSchema.dbml` — Declares xid (~2944 tok)

## backend/src/Kahoot.Api/

- `appsettings.json` (~254 tok)
- `Program.cs` — Class: Program (~1363 tok)

## backend/src/Kahoot.Api/Common/

- `ApiControllerBase.cs` — Controller: ApiControllerBase (~365 tok)
- `ApiErrorMapping.cs` — Class: ApiErrorMapping (~494 tok)
- `CurrentUser.cs` — Class: CurrentUser (~98 tok)
- `GlobalExceptionHandler.cs` — Class: GlobalExceptionHandler (~283 tok)
- `HostClaims.cs` — Class: HostClaims (~104 tok)
- `RateLimitingExtensions.cs` — Class: RateLimitingExtensions (~563 tok)

## backend/src/Kahoot.Api/Contracts/

- `AuthRequests.cs` — Class: AuthRequests (~59 tok)
- `CreatedIdResponse.cs` — Class: CreatedIdResponse (~22 tok)
- `GameRequests.cs` — Class: GameRequests (~42 tok)
- `QuizRequests.cs` — Class: QuizRequests (~128 tok)

## backend/src/Kahoot.Api/Controllers/

- `AuthController.cs` — Class: AuthController (~503 tok)
- `GamesController.cs` — Class: GamesController (~2046 tok)
- `QuizzesController.cs` — Class: QuizzesController (~1666 tok)
- `UploadsController.cs` — Controller: UploadsController (~423 tok)

## backend/src/Kahoot.Api/Endpoints/

- `HomePageEndpoint.cs` — Class: HomePageEndpoint (~1493 tok)

## backend/src/Kahoot.Api/Realtime/

- `GameGroups.cs` — Class: GameGroups (~55 tok)
- `GameHub.cs` — Class: GameHub (~1512 tok)
- `GameNotifier.cs` — Class: GameNotifier (~405 tok)
- `IGameClient.cs` — Class: IGameClient (~152 tok)
- `RealtimeResponse.cs` — Class: RealtimeResponse (~124 tok)

## backend/src/Kahoot.Application/

- `DependencyInjection.cs` — Class: DependencyInjection (~231 tok)

## backend/src/Kahoot.Application/Authentication/Common/

- `AuthenticationErrors.cs` — Class: AuthenticationErrors (~156 tok)
- `AuthenticationResponse.cs` — Class: AuthenticationResponse (~72 tok)
- `AuthTokenFactory.cs` — Class: AuthTokenFactory (~325 tok)
- `JwtOptions.cs` — Class: JwtOptions (~146 tok)

## backend/src/Kahoot.Application/Authentication/Login/

- `LoginCommand.cs` — Class: LoginCommand (~67 tok)
- `LoginCommandHandler.cs` — Class: LoginCommandHandler (~518 tok)
- `LoginCommandValidator.cs` — Class: LoginCommandValidator (~95 tok)

## backend/src/Kahoot.Application/Authentication/Logout/

- `LogoutCommand.cs` — Class: LogoutCommand (~44 tok)
- `LogoutCommandHandler.cs` — Class: LogoutCommandHandler (~241 tok)

## backend/src/Kahoot.Application/Authentication/Refresh/

- `RefreshTokenCommand.cs` — Class: RefreshTokenCommand (~66 tok)
- `RefreshTokenCommandHandler.cs` — Class: RefreshTokenCommandHandler (~1029 tok)
- `RefreshTokenCommandValidator.cs` — Class: RefreshTokenCommandValidator (~82 tok)

## backend/src/Kahoot.Application/Authentication/RefreshToken/

- `RefreshTokenCommand.cs` — Class: RefreshTokenCommand (~67 tok)
- `RefreshTokenCommandHandler.cs` — Class: RefreshTokenCommandHandler (~739 tok)
- `RefreshTokenCommandValidator.cs` — Class: RefreshTokenCommandValidator (~84 tok)

## backend/src/Kahoot.Application/Authentication/Register/

- `RegisterHostCommand.cs` — Class: RegisterHostCommand (~69 tok)
- `RegisterHostCommandHandler.cs` — Class: RegisterHostCommandHandler (~496 tok)
- `RegisterHostCommandValidator.cs` — Class: RegisterHostCommandValidator (~167 tok)

## backend/src/Kahoot.Application/Common/Abstractions/

- `IApplicationDbContext.cs` — DbContext: Host, RefreshToken, Quiz, Question, Choice, GameSession, Participant, Answer (~188 tok)
- `IDbExceptionInterpreter.cs` — Class: IDbExceptionInterpreter (~60 tok)
- `IGamePinGenerator.cs` — Class: IGamePinGenerator (~45 tok)
- `IJwtTokenService.cs` — Class: IJwtTokenService (~65 tok)
- `ISecureTokenGenerator.cs` — Class: ISecureTokenGenerator (~33 tok)
- `ITokenHasher.cs` — Class: ITokenHasher (~31 tok)

## backend/src/Kahoot.Application/Common/Behaviors/

- `RequestLoggingBehavior.cs` — Class: RequestLoggingBehavior (~256 tok)
- `ValidationBehavior.cs` — Class: ValidationBehavior (~516 tok)

## backend/src/Kahoot.Application/Common/Errors/

- `SharedErrors.cs` — Class: SharedErrors (~129 tok)
- `ValidationError.cs` — Class: ValidationError (~63 tok)

## backend/src/Kahoot.Application/Common/Messaging/

- `ICommand.cs` — Class: ICommand (~125 tok)
- `IQuery.cs` — Class: IQuery (~79 tok)

## backend/src/Kahoot.Application/Common/Security/

- `ICurrentUser.cs` — Class: ICurrentUser (~38 tok)
- `IPasswordHasher.cs` — Class: IPasswordHasher (~47 tok)

## backend/src/Kahoot.Application/Common/Storage/

- `FileStorageOptions.cs` — Class: FileStorageOptions (~213 tok)
- `IFileStorage.cs` — Class: IFileStorage (~49 tok)
- `IImageUploadService.cs` — Class: IImageUploadService (~75 tok)
- `ImageSignature.cs` — Class: ImageSignature (~255 tok)
- `ImageUploadService.cs` — Class: ImageUploadService (~598 tok)

## backend/src/Kahoot.Application/Games/

- `GameStateMachine.cs` — Class: GameStateMachine (~387 tok)

## backend/src/Kahoot.Application/Games/Common/

- `GameContracts.cs` — Class: GameContracts (~705 tok)
- `GameErrors.cs` — Class: GameErrors (~674 tok)
- `GameQuestionMapper.cs` — Class: GameQuestionMapper (~524 tok)
- `GameTime.cs` — Class: GameTime (~56 tok)
- `HostGameGuard.cs` — Class: HostGameGuard (~422 tok)
- `LeaderboardBuilder.cs` — Class: LeaderboardBuilder (~584 tok)
- `QuestionActivation.cs` — Class: QuestionActivation (~314 tok)
- `QuestionResultsBuilder.cs` — Class: QuestionResultsBuilder (~552 tok)

## backend/src/Kahoot.Application/Games/CreateGame/

- `CreateGameCommand.cs` — Class: CreateGameCommand (~58 tok)
- `CreateGameCommandHandler.cs` — Class: CreateGameCommandHandler (~652 tok)

## backend/src/Kahoot.Application/Games/EndGame/

- `EndGameCommand.cs` — Class: EndGameCommand (~56 tok)
- `EndGameCommandHandler.cs` — Class: EndGameCommandHandler (~572 tok)

## backend/src/Kahoot.Application/Games/EndQuestion/

- `EndQuestionCommand.cs` — Class: EndQuestionCommand (~60 tok)
- `EndQuestionCommandHandler.cs` — Class: EndQuestionCommandHandler (~668 tok)

## backend/src/Kahoot.Application/Games/GetHostGameState/

- `GetHostGameStateQuery.cs` — Class: GetHostGameStateQuery (~61 tok)
- `GetHostGameStateQueryHandler.cs` — Class: GetHostGameStateQueryHandler (~806 tok)

## backend/src/Kahoot.Application/Games/GetLeaderboard/

- `GetLeaderboardQuery.cs` — Class: GetLeaderboardQuery (~59 tok)
- `GetLeaderboardQueryHandler.cs` — Class: GetLeaderboardQueryHandler (~254 tok)

## backend/src/Kahoot.Application/Games/GetQuestionResults/

- `GetQuestionResultsQuery.cs` — Class: GetQuestionResultsQuery (~67 tok)
- `GetQuestionResultsQueryHandler.cs` — Class: GetQuestionResultsQueryHandler (~537 tok)

## backend/src/Kahoot.Application/Games/JoinGame/

- `JoinGameCommand.cs` — Class: JoinGameCommand (~60 tok)
- `JoinGameCommandHandler.cs` — Class: JoinGameCommandHandler (~610 tok)
- `JoinGameCommandValidator.cs` — Class: JoinGameCommandValidator (~174 tok)

## backend/src/Kahoot.Application/Games/Leaderboard/

- `ILeaderboardService.cs` — Class: ILeaderboardService (~100 tok)
- `LeaderboardService.cs` — Class: LeaderboardService (~186 tok)

## backend/src/Kahoot.Application/Games/Presence/

- `AttachParticipantConnectionCommand.cs` — Class: AttachParticipantConnectionCommand (~54 tok)
- `AttachParticipantConnectionCommandHandler.cs` — Class: AttachParticipantConnectionCommandHandler (~269 tok)
- `AuthorizeHostGameQuery.cs` — Class: AuthorizeHostGameQuery (~47 tok)
- `AuthorizeHostGameQueryHandler.cs` — Class: AuthorizeHostGameQueryHandler (~196 tok)
- `DetachParticipantConnectionCommand.cs` — Class: DetachParticipantConnectionCommand (~54 tok)
- `DetachParticipantConnectionCommandHandler.cs` — Class: DetachParticipantConnectionCommandHandler (~290 tok)

## backend/src/Kahoot.Application/Games/Reconnect/

- `ReconnectParticipantCommand.cs` — Class: ReconnectParticipantCommand (~64 tok)
- `ReconnectParticipantCommandHandler.cs` — Class: ReconnectParticipantCommandHandler (~1281 tok)

## backend/src/Kahoot.Application/Games/RemoveParticipant/

- `RemoveParticipantCommand.cs` — Class: RemoveParticipantCommand (~51 tok)
- `RemoveParticipantCommandHandler.cs` — Class: RemoveParticipantCommandHandler (~446 tok)

## backend/src/Kahoot.Application/Games/Scoring/

- `IScoringService.cs` — Class: IScoringService (~49 tok)
- `ScoringService.cs` — Class: ScoringService (~219 tok)

## backend/src/Kahoot.Application/Games/ShowLeaderboard/

- `ShowLeaderboardCommand.cs` — Class: ShowLeaderboardCommand (~61 tok)
- `ShowLeaderboardCommandHandler.cs` — Class: ShowLeaderboardCommandHandler (~564 tok)

## backend/src/Kahoot.Application/Games/StartGame/

- `StartGameCommand.cs` — Class: StartGameCommand (~59 tok)
- `StartGameCommandHandler.cs` — Class: StartGameCommandHandler (~789 tok)

## backend/src/Kahoot.Application/Games/StartNextQuestion/

- `StartNextQuestionCommand.cs` — Class: StartNextQuestionCommand (~63 tok)
- `StartNextQuestionCommandHandler.cs` — Class: StartNextQuestionCommandHandler (~848 tok)

## backend/src/Kahoot.Application/Games/SubmitAnswer/

- `SubmitAnswerCommand.cs` — Class: SubmitAnswerCommand (~82 tok)
- `SubmitAnswerCommandHandler.cs` — Class: SubmitAnswerCommandHandler (~1590 tok)
- `SubmitAnswerCommandValidator.cs` — Class: SubmitAnswerCommandValidator (~132 tok)

## backend/src/Kahoot.Application/Quizzes/Common/

- `QuestionValidationRules.cs` — Class: QuestionValidationRules (~401 tok)
- `QuizContracts.cs` — Class: QuizContracts (~206 tok)
- `QuizErrors.cs` — Class: QuizErrors (~311 tok)

## backend/src/Kahoot.Application/Quizzes/CreateQuiz/

- `CreateQuizCommand.cs` — Class: CreateQuizCommand (~50 tok)
- `CreateQuizCommandHandler.cs` — Class: CreateQuizCommandHandler (~283 tok)
- `CreateQuizCommandValidator.cs` — Class: CreateQuizCommandValidator (~96 tok)

## backend/src/Kahoot.Application/Quizzes/DeleteQuiz/

- `DeleteQuizCommand.cs` — Class: DeleteQuizCommand (~43 tok)
- `DeleteQuizCommandHandler.cs` — Class: DeleteQuizCommandHandler (~375 tok)

## backend/src/Kahoot.Application/Quizzes/GetQuiz/

- `GetQuizQuery.cs` — Class: GetQuizQuery (~56 tok)
- `GetQuizQueryHandler.cs` — Class: GetQuizQueryHandler (~595 tok)

## backend/src/Kahoot.Application/Quizzes/ListQuizzes/

- `ListQuizzesQuery.cs` — Class: ListQuizzesQuery (~59 tok)
- `ListQuizzesQueryHandler.cs` — Class: ListQuizzesQueryHandler (~367 tok)

## backend/src/Kahoot.Application/Quizzes/PublishQuiz/

- `PublishQuizCommand.cs` — Class: PublishQuizCommand (~43 tok)
- `PublishQuizCommandHandler.cs` — Class: PublishQuizCommandHandler (~596 tok)

## backend/src/Kahoot.Application/Quizzes/Questions/AddQuestion/

- `AddQuestionCommand.cs` — Class: AddQuestionCommand (~92 tok)
- `AddQuestionCommandHandler.cs` — Class: AddQuestionCommandHandler (~564 tok)
- `AddQuestionCommandValidator.cs` — Class: AddQuestionCommandValidator (~251 tok)

## backend/src/Kahoot.Application/Quizzes/Questions/Common/

- `QuestionMapping.cs` — Class: QuestionMapping (~178 tok)
- `QuizEditGuard.cs` — Class: QuizEditGuard (~341 tok)

## backend/src/Kahoot.Application/Quizzes/Questions/DeleteQuestion/

- `DeleteQuestionCommand.cs` — Class: DeleteQuestionCommand (~52 tok)
- `DeleteQuestionCommandHandler.cs` — Class: DeleteQuestionCommandHandler (~386 tok)

## backend/src/Kahoot.Application/Quizzes/Questions/ReorderQuestions/

- `ReorderQuestionsCommand.cs` — Class: ReorderQuestionsCommand (~59 tok)
- `ReorderQuestionsCommandHandler.cs` — Class: ReorderQuestionsCommandHandler (~619 tok)
- `ReorderQuestionsCommandValidator.cs` — Class: ReorderQuestionsCommandValidator (~156 tok)

## backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/

- `UpdateQuestionCommand.cs` — Class: UpdateQuestionCommand (~98 tok)
- `UpdateQuestionCommandHandler.cs` — Class: UpdateQuestionCommandHandler (~566 tok)
- `UpdateQuestionCommandValidator.cs` — Class: UpdateQuestionCommandValidator (~270 tok)

## backend/src/Kahoot.Application/Quizzes/UpdateQuiz/

- `UpdateQuizCommand.cs` — Class: UpdateQuizCommand (~52 tok)
- `UpdateQuizCommandHandler.cs` — Class: UpdateQuizCommandHandler (~428 tok)
- `UpdateQuizCommandValidator.cs` — Class: UpdateQuizCommandValidator (~111 tok)

## backend/src/Kahoot.Domain/Common/

- `AuditableEntity.cs` — Class: AuditableEntity (~60 tok)
- `DomainException.cs` — Class: DomainException (~46 tok)
- `Entity.cs` — Class: Entity (~49 tok)
- `Result.cs` — Class: Result (~382 tok)

## backend/src/Kahoot.Domain/Games/

- `Answer.cs` — Class: Answer (~188 tok)
- `GameErrors.cs` — Class: GameErrors (~76 tok)
- `GameSession.cs` — Class: GameSession (~232 tok)
- `GameStatus.cs` — Class: GameStatus (~42 tok)
- `GameStatusTransitions.cs` — Class: GameStatusTransitions (~269 tok)
- `InvalidGameStatusTransitionException.cs` — Class: InvalidGameStatusTransitionException (~114 tok)
- `Participant.cs` — Class: Participant (~191 tok)

## backend/src/Kahoot.Domain/Hosts/

- `Host.cs` — Class: Host (~103 tok)
- `RefreshToken.cs` — Class: RefreshToken (~106 tok)

## backend/src/Kahoot.Domain/Quizzes/

- `Choice.cs` — Class: Choice (~100 tok)
- `Question.cs` — Class: Question (~127 tok)
- `Quiz.cs` — Class: Quiz (~114 tok)

## backend/src/Kahoot.Infrastructure/

- `DependencyInjection.cs` — Class: DependencyInjection (~631 tok)

## backend/src/Kahoot.Infrastructure/Authentication/

- `JwtTokenService.cs` — Class: JwtTokenService (~438 tok)

## backend/src/Kahoot.Infrastructure/Games/

- `GamePinGenerator.cs` — Class: GamePinGenerator (~290 tok)

## backend/src/Kahoot.Infrastructure/Persistence/

- `DbExceptionInterpreter.cs` — Class: DbExceptionInterpreter (~228 tok)
- `KahootDbContext.cs` — DbContext: Host, RefreshToken, Quiz, Question, Choice, GameSession, Participant, Answer (~353 tok)
- `KahootDbContextFactory.cs` — Class: KahootDbContextFactory (~524 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Configurations/

- `AnswerConfiguration.cs` — Class: AnswerConfiguration (~448 tok)
- `ChoiceConfiguration.cs` — Class: ChoiceConfiguration (~247 tok)
- `GameSessionConfiguration.cs` — Class: GameSessionConfiguration (~743 tok)
- `HostConfiguration.cs` — Class: HostConfiguration (~285 tok)
- `ParticipantConfiguration.cs` — Class: ParticipantConfiguration (~582 tok)
- `QuestionConfiguration.cs` — Class: QuestionConfiguration (~482 tok)
- `QuizConfiguration.cs` — Class: QuizConfiguration (~252 tok)
- `RefreshTokenConfiguration.cs` — Class: RefreshTokenConfiguration (~245 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Interceptors/

- `AuditableEntityInterceptor.cs` — Class: AuditableEntityInterceptor (~439 tok)

## backend/src/Kahoot.Infrastructure/Security/

- `BcryptPasswordHasher.cs` — Class: BcryptPasswordHasher (~133 tok)
- `SecureTokenGenerator.cs` — Class: SecureTokenGenerator (~138 tok)
- `TokenHasher.cs` — Class: TokenHasher (~102 tok)

## backend/src/Kahoot.Infrastructure/Startup/

- `DatabaseMigrationHostedService.cs` — Class: DatabaseMigrationHostedService (~465 tok)
- `DatabaseSeederHostedService.cs` — Class: DatabaseSeederHostedService (~623 tok)
- `HostSeedOptions.cs` — Class: HostSeedOptions (~91 tok)

## backend/src/Kahoot.Infrastructure/Storage/

- `LocalFileStorage.cs` — Class: LocalFileStorage (~318 tok)

## docs/

- `frontend-pages-plan.md` — Frontend Pages Plan (~13623 tok)
- `functional-requirements.md` — Functional Requirements — Kahoot-like Platform (~2560 tok)
- `non-functional-requirements.md` — Non-Functional Requirements — Kahoot-like Platform (~2249 tok)
- `realtime-protocol.md` — Real-Time Protocol (SignalR) — Kahoot-like Platform (~2669 tok)

## frontend/src/api/

- `authService.ts` — Raw contract returned by `POST /api/auth/login` and `/api/auth/refresh`. (~505 tok)
- `axiosClient.ts` — Exports TOKEN_STORAGE_KEY, USER_STORAGE_KEY, REFRESH_TOKEN_STORAGE_KEY, ACCESS_TOKEN_EXPIRES_KEY + 3 (~781 tok)
- `gameService.ts` — Server game-state values (must match `Kahoot.Domain.Games.GameStatus`). (~270 tok)
- `hostGameService.ts` — API routes: DELETE (1 endpoints) (~806 tok)
- `media.ts` — Exports resolveMediaUrl (~120 tok)

## frontend/src/components/AnswerFeedbackScreen/

- `AnswerFeedbackScreen.tsx` — CONTENT (~1053 tok)
- `index.ts` (~28 tok)

## frontend/src/components/AnsweredCounter/

- `AnsweredCounter.tsx` — AnsweredCounter (~698 tok)
- `index.ts` (~26 tok)

## frontend/src/components/ChoiceGrid/

- `ChoiceButton.tsx` — ChoiceButton (~1109 tok)
- `ChoiceGrid.tsx` — ChoiceGrid (~566 tok)
- `ChoiceShape.tsx` — ChoiceShape (~249 tok)
- `choiceVisuals.ts` — Exports CHOICE_LETTERS, CHOICE_COLORS, choiceLetter, choiceColor (~119 tok)
- `choiceVisuals.tsx` — CHOICE_LETTERS (~307 tok)
- `index.ts` (~33 tok)

## frontend/src/components/HostGameControls/

- `HostGameControls.tsx` — primaryButtonSx (~1768 tok)

## frontend/src/components/KickedNotice/

- `index.ts` (~24 tok)
- `KickedNotice.tsx` — KickedNotice (~527 tok)

## frontend/src/components/NicknameEntryForm/

- `index.ts` (~27 tok)
- `NicknameEntryForm.tsx` — NicknameEntryForm (~1728 tok)

## frontend/src/components/QuestionMedia/

- `index.ts` (~24 tok)
- `QuestionMedia.tsx` — QuestionMedia (~261 tok)

## frontend/src/components/QuestionResultsChart/

- `index.ts` (~28 tok)
- `QuestionResultsChart.tsx` — QuestionResultsChart (~982 tok)

## frontend/src/components/ServerCountdown/

- `index.ts` (~26 tok)
- `ServerCountdown.tsx` — barColor (~652 tok)

## frontend/src/components/WaitingScreen/

- `index.ts` (~24 tok)
- `WaitingScreen.tsx` — pulseKeyframes (~1433 tok)

## frontend/src/constants/

- `errorCodes.ts` — Exports ERROR_MESSAGES, getFriendlyErrorMessage (~506 tok)
- `gameStatus.ts` — Exports NormalizedGameStatus, GameStatus, GAME_PHASES, normalizeGameStatus + 6 more (~617 tok)

## frontend/src/hooks/

- `useHostGame.ts` — Exports useHostGame (~3490 tok)
- `usePlayerGame.ts` — Exports AnswerState, PlayerState, usePlayerGame (~2861 tok)
- `useServerCountdown.ts` — Exports CountdownState, ServerCountdownOptions, useServerCountdown (~819 tok)
- `useSessionToken.ts` — Exports PlayerSession, getSession, saveSession, clearSession + 4 more (~844 tok)

## frontend/src/pages/HostGamePage/

- `HostGamePage.tsx` — HostGamePage (~2455 tok)
- `HostLeaderboardView.tsx` — HostLeaderboardView (~621 tok)
- `HostQuestionView.tsx` — HostQuestionView (~830 tok)

## frontend/src/pages/JoinPage/

- `JoinPage.tsx` — SIX_DIGIT_PIN (~1883 tok)

## frontend/src/pages/PlayerGamePage/

- `index.ts` (~30 tok)
- `PlayerGamePage.tsx` — ordinal (~2054 tok)
- `PlayerQuestionView.tsx` — PlayerQuestionView (~853 tok)
- `PlayerResultsView.tsx` — PlayerResultsView (~854 tok)

## frontend/src/realtime/

- `events.ts` — Exports GameStatus, RealtimeError, RealtimeResponse, GameParticipantResponse + 11 more (~806 tok)
- `gameHub.ts` — Exports createGameHubConnection, invokeJoinAsHost, invokeReconnect, invokeSubmitAnswer (~564 tok)

## frontend/src/routes/

- `routes.tsx` — AppRoutes (~546 tok)

## load-tests/

- `.gitignore` — Git ignore rules (~49 tok)
- `docker-compose.railway-sim.yml` — Kahoot backend + Postgres, tuned to approximate ONE small Railway instance: (~777 tok)
- `README.md` — Project documentation (~4711 tok)
- `run-all.js` — fs: cooldownSeconds, loadDotenv, parseArgs + 9 more (~2760 tok)
- `run.sh` — run.sh - one command to load-test the Kahoot backend. (~1207 tok)

## load-tests/config/

- `environments.js` — Resolves the target from BASE_URL / SIGNALR_URL. Defaults to the local dev API (~605 tok)
- `thresholds.js` — Threshold sets derived from non-functional-requirements.md (NFR-1 / NFR-2). (~639 tok)

## load-tests/helpers/

- `metrics.js` — --- SignalR connection lifecycle ---------------------------------------------- (~1006 tok)
- `orchestration.js` — Director-side helpers: wait for players to be present, run a question cycle, (~1310 tok)
- `rest.js` — REST helpers for the host-facing API (auth + quiz authoring + game control). (~2013 tok)
- `signalr.js` — Minimal SignalR client for k6, speaking the real protocol the app uses: (~3599 tok)
- `summary.js` — Compact, dependency-free end-of-test report. Emits: (~1330 tok)
- `testdata.js` — Repeatable test-data provisioning, entirely through the public host API (~1475 tok)

## load-tests/scenarios/

- `answer-burst.js` — Scenario 4 — 500-player answer burst (the primary performance test). (~3660 tok)
- `connections.js` — Scenario 1 — 500 concurrent SignalR connections. (~1088 tok)
- `duplicate-answer.js` — Scenario 5 — Duplicate-answer concurrency / idempotency. (~2396 tok)
- `endurance.js` — Scenario 10 — Endurance / soak. (~2144 tok)
- `join-game.js` — Scenario 2 — 500 unique players join ONE game. (~1332 tok)
- `multiple-games.js` — Scenario 7 — GAMES x PLAYERS_PER_GAME concurrent games (default 10 x 50 = 500). (~2460 tok)
- `question-broadcast.js` — Scenario 3 — Question broadcast with 500 connected players + group isolation. (~2076 tok)
- `ramp.js` — Scenario 8 — Ramp / stress: 50 -> 100 -> 250 -> 500 -> 750 concurrent players. (~2006 tok)
- `reconnection-storm.js` — Scenario 9 — Reconnection storm. (~2137 tok)
- `reconnection.js` — Scenario 6 — Reconnection preserves identity and state. (~2638 tok)

## load-tests/verify/

- `verify-db.mjs` — Declares here (~522 tok)
- `verify.sql` — Out-of-band correctness verification, straight against PostgreSQL. (~1267 tok)
