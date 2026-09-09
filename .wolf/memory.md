---
description: chronological action log per session, consolidated weekly
---
# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.

| 09:53 | Installed axios, mui, zod, emotion, and mui icons | frontend/package.json | success | ~400 |
| 09:54 | Installed react-router-dom v7 | frontend/package.json | success | ~150 |
| 09:55 | Installed @microsoft/signalr and verified all AGENTS.md libraries | frontend/package.json | success | ~200 |
| 09:58 | Installed Prettier, eslint-plugin-simple-import-sort, eslint-config-prettier and formatted codebase | frontend/eslint.config.js, frontend/.prettierrc | success | ~250 |
| 10:00 | Created folder structure in frontend/src from STRUCTURE.md (folders only) | frontend/src/ | success | ~100 |
| 10:01 | Documented complete frontend folder structure and responsibilities in README.md | frontend/README.md | success | ~300 |
| 10:02 | Moved logo.jpeg to frontend/src/assets/logo.jpeg and frontend/public/logo.jpeg | frontend/src/assets/logo.jpeg | success | ~100 |
| 10:03 | Removed .opencode and .cursor folders | .opencode, .cursor | success | ~50 |
| 10:08 | Scaffolded .NET 10 Clean Architecture backend, configured AGENTS.md libraries, and created Dockerfile | backend/Kahoot.sln, backend/Dockerfile | success | ~400 |
| 10:11 | Added backend .gitignore, frontend Dockerfile + nginx.conf, and root docker-compose.yml | docker-compose.yml, frontend/Dockerfile, backend/.gitignore | success | ~250 |
| 10:12 | Removed template files (WeatherForecast*, Kahoot.Api.http, appsettings.Development.json) | backend/src/Kahoot.Api/ | success | ~80 |

## Session: 2026-09-09 10:05

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 10:22 | Created backend/src/Kahoot.Domain/Common/Entity.cs | — | ~49 |
| 10:22 | Created backend/src/Kahoot.Domain/Common/AuditableEntity.cs | — | ~52 |
| 10:22 | Created backend/src/Kahoot.Domain/Common/DomainException.cs | — | ~46 |
| 10:22 | Created backend/src/Kahoot.Domain/Hosts/Host.cs | — | ~464 |
| 10:22 | Created backend/src/Kahoot.Domain/Hosts/RefreshToken.cs | — | ~430 |
| 10:22 | Created backend/src/Kahoot.Domain/Quizzes/Quiz.cs | — | ~443 |
| 10:23 | Created backend/src/Kahoot.Domain/Quizzes/Question.cs | — | ~838 |
| 10:23 | Created backend/src/Kahoot.Domain/Quizzes/Choice.cs | — | ~382 |
| 10:23 | Edited backend/src/Kahoot.Domain/Quizzes/Choice.cs | inline fix | ~15 |
| 10:23 | Created backend/src/Kahoot.Domain/Games/GameStatus.cs | — | ~42 |
| 10:23 | Created backend/src/Kahoot.Domain/Games/GameStatusTransitions.cs | — | ~269 |
| 10:23 | Created backend/src/Kahoot.Domain/Games/InvalidGameStatusTransitionException.cs | — | ~114 |
| 10:23 | Created backend/src/Kahoot.Domain/Games/GameSession.cs | — | ~856 |
| 10:24 | Created backend/src/Kahoot.Domain/Games/Participant.cs | — | ~792 |
| 10:24 | Created backend/src/Kahoot.Domain/Games/Answer.cs | — | ~601 |
| 10:25 | Created backend/projectSchema.dbml | — | ~3665 |
| 10:26 | Edited backend/projectSchema.dbml | inline fix | ~22 |
| 10:26 | Edited backend/projectSchema.dbml | inline fix | ~41 |
| 10:26 | Edited backend/projectSchema.dbml | inline fix | ~33 |
| 10:32 | Created backend/src/Kahoot.Domain/Common/Result.cs | — | ~384 |
| 10:32 | Created backend/src/Kahoot.Domain/Games/GameErrors.cs | — | ~76 |
| 10:32 | Created backend/src/Kahoot.Domain/Common/AuditableEntity.cs | — | ~68 |
| 10:32 | Edited backend/src/Kahoot.Domain/Games/Answer.cs | modified Answer() | ~51 |
| 10:32 | Edited backend/src/Kahoot.Domain/Hosts/RefreshToken.cs | modified RefreshToken() | ~46 |
| 10:32 | Edited backend/src/Kahoot.Domain/Games/GameSession.cs | added 2 condition(s) | ~516 |
| 10:33 | Built Kahoot.Domain layer (8 entities + GameStatus state machine) and projectSchema.dbml; per user feedback dropped the Entity base class and switched game-state transitions to the Result pattern (Result/Result<T>/Error, GameErrors). Full solution builds clean (Release, 0 warn); DBML validated via @dbml/cli. | backend/src/Kahoot.Domain/**, backend/projectSchema.dbml | success | ~9000 |
| 10:36 | Created backend/src/Kahoot.Domain/Common/AuditableEntity.cs | — | ~62 |
| 10:38 | Per user feedback (no DDD): rewrote all 8 Kahoot.Domain entities as anemic data classes (get/set props only), deleted GameStatusTransitions.cs + GameErrors.cs (state machine moves to Application). Kept GameStatus enum + Common/Result.cs. Solution builds clean (Release, 0 warn). | backend/src/Kahoot.Domain/** | success | ~2500 |
| 10:36 | Created backend/src/Kahoot.Domain/Hosts/Host.cs | — | ~123 |
| 10:36 | Created backend/src/Kahoot.Domain/Hosts/RefreshToken.cs | — | ~120 |
| 10:36 | Edited backend/src/Kahoot.Domain/Hosts/Host.cs | 3→3 lines | ~35 |
| 10:36 | Created backend/src/Kahoot.Domain/Quizzes/Quiz.cs | — | ~99 |
| 10:36 | Created backend/src/Kahoot.Domain/Quizzes/Question.cs | — | ~112 |
| 10:36 | Created backend/src/Kahoot.Domain/Quizzes/Choice.cs | — | ~88 |
| 10:36 | Created backend/src/Kahoot.Domain/Games/GameSession.cs | — | ~231 |
| 10:36 | Created backend/src/Kahoot.Domain/Games/Participant.cs | — | ~189 |
| 10:37 | Created backend/src/Kahoot.Domain/Games/Answer.cs | — | ~189 |
| 10:46 | Created docs/functional-requirements.md | — | ~1976 |
| 10:46 | Created docs/non-functional-requirements.md | — | ~2047 |
| 10:46 | Created backend/src/Kahoot.Domain/Hosts/Host.cs | — | ~100 |
| 10:47 | Created backend/projectSchema.dbml | — | ~2791 |
| 10:50 | Created backend/src/Kahoot.Application/Common/Security/IPasswordHasher.cs | — | ~47 |
| 10:50 | Created backend/src/Kahoot.Infrastructure/Security/BcryptPasswordHasher.cs | — | ~133 |
| 10:50 | Created backend/src/Kahoot.Infrastructure/Persistence/KahootDbContext.cs | — | ~334 |
| 10:50 | Created backend/src/Kahoot.Infrastructure/Persistence/KahootDbContextFactory.cs | — | ~302 |
| 10:50 | Created backend/src/Kahoot.Infrastructure/Persistence/KahootDbContextFactory.cs | — | ~223 |
| 10:50 | Created backend/src/Kahoot.Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs | — | ~442 |
| 10:53 | Created backend/src/Kahoot.Domain/Common/AuditableEntity.cs | — | ~60 |
| 10:53 | Created backend/src/Kahoot.Domain/Hosts/RefreshToken.cs | — | ~118 |
| 10:53 | Created backend/src/Kahoot.Domain/Games/GameSession.cs | — | ~228 |
| 10:53 | Created backend/src/Kahoot.Domain/Games/Participant.cs | — | ~188 |
| 10:53 | Created backend/src/Kahoot.Domain/Games/Answer.cs | — | ~188 |
| 10:54 | Edited backend/src/Kahoot.Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs | modified if() | ~102 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/HostConfiguration.cs | — | ~327 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs | — | ~266 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/QuizConfiguration.cs | — | ~274 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/QuestionConfiguration.cs | — | ~508 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/ChoiceConfiguration.cs | — | ~276 |
| 10:54 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/GameSessionConfiguration.cs | — | ~750 |
| 10:55 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/GameSessionConfiguration.cs | 1→5 lines | ~51 |
| 10:55 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs | — | ~631 |
| 10:55 | Created backend/src/Kahoot.Infrastructure/Persistence/Configurations/AnswerConfiguration.cs | — | ~448 |
| 10:56 | Created backend/src/Kahoot.Infrastructure/Startup/HostSeedOptions.cs | — | ~91 |
| 10:57 | Created backend/src/Kahoot.Infrastructure/Startup/DatabaseMigrationHostedService.cs | — | ~465 |
| 10:57 | Created backend/src/Kahoot.Infrastructure/Startup/DatabaseSeederHostedService.cs | — | ~623 |
| 10:57 | Created backend/src/Kahoot.Infrastructure/DependencyInjection.cs | — | ~533 |
| 10:57 | Edited backend/src/Kahoot.Api/appsettings.json | expanded (+6 lines) | ~67 |
| 11:01 | Edited backend/projectSchema.dbml | 4→5 lines | ~74 |
| 11:01 | Edited backend/projectSchema.dbml | 3→4 lines | ~132 |
| 11:05 | Split docs into functional-/non-functional-requirements.md (kept original); email removed system-wide, Host now username+password, DateTime props lost the Utc suffix; built Infrastructure persistence (KahootDbContext + 8 IEntityTypeConfiguration + snake_case + xmin shadow token + AuditableEntityInterceptor + BcryptPasswordHasher/IPasswordHasher), startup IHostedLifecycleService pair (migrate then seed bootstrap host), InitialCreate migration. Verified end-to-end vs Postgres 17: migrate+seed, idempotent re-run, skip-when-unconfigured, fail-fast on bad connection. Release build 0 warn; DBML valid. | backend/src/Kahoot.*/**, backend/projectSchema.dbml, docs/*-requirements.md | success | ~14000 |
| 11:10 | Configured custom Material UI light theme derived from IEEEXtreme Palestine Section logo colors (#00629B IEEE ocean blue, #0284C7 radar cyan, #F4F8FC canvas, #09131F text); created theme/ (palette, typography, components, index), styles/tokens.css, Inter font in index.html, and interactive App.tsx showcase. Verified clean lint, prettier, and build; tested via browser subagent. | frontend/src/theme/**, frontend/src/styles/**, frontend/src/App.tsx, frontend/index.html | success | ~3500 |
| 11:14 | Created backend/src/Kahoot.Domain/Hosts/Host.cs | — | ~103 |
| 11:14 | Created backend/src/Kahoot.Domain/Hosts/RefreshToken.cs | — | ~106 |
| 11:14 | Created backend/src/Kahoot.Domain/Quizzes/Quiz.cs | — | ~114 |
| 11:14 | Created backend/src/Kahoot.Domain/Quizzes/Question.cs | — | ~127 |
| 11:14 | Created backend/src/Kahoot.Domain/Quizzes/Choice.cs | — | ~100 |
| 11:14 | Created backend/src/Kahoot.Domain/Games/GameSession.cs | — | ~232 |
| 11:14 | Created backend/src/Kahoot.Domain/Games/Participant.cs | — | ~191 |
| 11:14 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/HostConfiguration.cs | 6→3 lines | ~48 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs | 3→2 lines | ~23 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/QuizConfiguration.cs | 7→5 lines | ~82 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/QuestionConfiguration.cs | 8→6 lines | ~116 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/ChoiceConfiguration.cs | 7→5 lines | ~80 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/GameSessionConfiguration.cs | 6→3 lines | ~19 |
| 11:15 | Edited backend/src/Kahoot.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs | 7→4 lines | ~65 |
| 11:15 | Created backend/src/Kahoot.Infrastructure/DependencyInjection.cs | — | ~520 |
| 11:17 | Created backend/src/Kahoot.Application/Common/Storage/FileStorageOptions.cs | — | ~213 |
| 11:17 | Created backend/src/Kahoot.Application/Common/Storage/IFileStorage.cs | — | ~49 |
| 11:17 | Created backend/src/Kahoot.Application/Common/Storage/ImageSignature.cs | — | ~242 |
| 11:17 | Created backend/src/Kahoot.Application/Common/Storage/ImageSignature.cs | — | ~255 |
| 11:17 | Created backend/src/Kahoot.Application/Common/Storage/ImageUploadService.cs | — | ~589 |
| 11:17 | Edited backend/src/Kahoot.Application/Common/Storage/ImageUploadService.cs | inline fix | ~13 |
| 11:17 | Created backend/src/Kahoot.Infrastructure/Storage/LocalFileStorage.cs | — | ~318 |
| 11:18 | Created backend/src/Kahoot.Api/Controllers/UploadsController.cs | — | ~409 |
| 11:18 | Created backend/src/Kahoot.Api/Program.cs | — | ~502 |
| 11:18 | Edited backend/src/Kahoot.Api/appsettings.json | expanded (+6 lines) | ~87 |
| 11:19 | Created backend/src/Kahoot.Infrastructure/Persistence/KahootDbContextFactory.cs | — | ~524 |
| 11:19 | Created backend/projectSchema.dbml | — | ~2944 |
| 11:25 | Created backend/src/Kahoot.Application/Common/Storage/IImageUploadService.cs | — | ~75 |
| 11:25 | Edited backend/src/Kahoot.Application/Common/Storage/ImageUploadService.cs | modified ImageUploadService() | ~40 |
| 11:25 | Edited backend/src/Kahoot.Api/Controllers/UploadsController.cs | inline fix | ~26 |
| 11:26 | Built frontend Axios client (src/api/axiosClient.ts), Host auth service & context (src/api/authService.ts, src/context/AuthProvider.tsx, src/hooks/useAuth.ts), Game service (src/api/gameService.ts), centralized router & ProtectedRoute (src/routes/routes.tsx), RootLayout/AuthLayout, Player Game PIN & nickname join HomePage (src/pages/HomePage/HomePage.tsx), Host Login page with username+password & show/hide toggle (src/pages/LoginPage/LoginPage.tsx), and protected HostDashboard. Cleaned App.tsx. Verified with ESLint, Prettier, build, and browser subagent e2e test. | frontend/src/** | success | ~6000 |
| 11:27 | Edited docs/functional-requirements.md | expanded (+14 lines) | ~449 |
| 11:27 | Edited docs/functional-requirements.md | expanded (+9 lines) | ~267 |
| 11:27 | Edited docs/non-functional-requirements.md | expanded (+9 lines) | ~261 |
| 11:35 | Removed auditing entirely (deleted AuditableEntity + interceptor, dropped created_at/updated_at from all tables + DBML); entities are now flat base-less POCOs. Added Quiz.IsPublished, Question/Choice.ImageUrl (nullable), Choice.Text nullable. Built backend file upload: IFileStorage/LocalFileStorage + IImageUploadService/ImageUploadService (magic-byte check) + FileStorageOptions + UploadsController POST /api/uploads/images + static serving in Program.cs. Design-time factory now reads Kahoot.Api/appsettings.json. Migration #2 RemoveAuditColumnsAddMediaAndPublish. Fixed bug-003 (Scrutor: inject abstraction not concrete). Verified vs Postgres 17: both migrations + seed, upload 201 + byte-identical roundtrip, 400 on bad type/magic/empty. Release build 0 warn; DBML valid. | backend/src/Kahoot.*/**, backend/projectSchema.dbml, backend/src/Kahoot.Api/**, docs/*-requirements.md | success | ~20000 |
| 11:36 | Edited backend/src/Kahoot.Domain/Common/Result.cs | modified Error() | ~35 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Messaging/ICommand.cs | — | ~125 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Messaging/IQuery.cs | — | ~79 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/IApplicationDbContext.cs | — | ~188 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/IJwtTokenService.cs | — | ~65 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/ITokenHasher.cs | — | ~31 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/ISecureTokenGenerator.cs | — | ~33 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/IGamePinGenerator.cs | — | ~45 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Abstractions/IDbExceptionInterpreter.cs | — | ~60 |
| 11:36 | Created backend/src/Kahoot.Application/Common/Security/ICurrentUser.cs | — | ~38 |
| 11:37 | Created backend/src/Kahoot.Application/Common/Errors/SharedErrors.cs | — | ~129 |
| 11:37 | Created backend/src/Kahoot.Application/Common/Errors/ValidationError.cs | — | ~63 |
| 11:37 | Created backend/src/Kahoot.Application/Common/Behaviors/ValidationBehavior.cs | — | ~516 |
| 11:37 | Created backend/src/Kahoot.Application/Common/Behaviors/RequestLoggingBehavior.cs | — | ~256 |
| 11:37 | Created backend/src/Kahoot.Application/Games/GameStateMachine.cs | — | ~241 |
| 11:37 | Created backend/src/Kahoot.Application/Games/Scoring/IScoringService.cs | — | ~49 |
| 11:37 | Created backend/src/Kahoot.Application/Games/Scoring/ScoringService.cs | — | ~219 |
| 11:37 | Created backend/src/Kahoot.Application/Games/Leaderboard/ILeaderboardService.cs | — | ~100 |
| 11:37 | Created backend/src/Kahoot.Application/Games/Leaderboard/LeaderboardService.cs | — | ~186 |
| 11:37 | Created backend/src/Kahoot.Application/Authentication/Common/JwtOptions.cs | — | ~146 |
| 11:37 | Created backend/src/Kahoot.Application/Authentication/Common/AuthenticationResponse.cs | — | ~72 |
| 11:38 | Created backend/src/Kahoot.Application/Authentication/Common/AuthenticationErrors.cs | — | ~189 |
| 11:38 | Created backend/src/Kahoot.Application/Authentication/Common/AuthTokenFactory.cs | — | ~321 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Register/RegisterHostCommand.cs | — | ~69 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Register/RegisterHostCommandValidator.cs | — | ~167 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Register/RegisterHostCommandHandler.cs | — | ~496 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Login/LoginCommand.cs | — | ~67 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Login/LoginCommandValidator.cs | — | ~95 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/Login/LoginCommandHandler.cs | — | ~510 |
| 11:39 | Created backend/src/Kahoot.Application/Authentication/RefreshToken/RefreshTokenCommand.cs | — | ~67 |
| 11:40 | Created backend/src/Kahoot.Application/Authentication/RefreshToken/RefreshTokenCommandValidator.cs | — | ~84 |
| 11:40 | Created backend/src/Kahoot.Application/Authentication/RefreshToken/RefreshTokenCommandHandler.cs | — | ~739 |
| 11:40 | Created backend/src/Kahoot.Application/Authentication/Logout/LogoutCommand.cs | — | ~44 |
| 11:40 | Created backend/src/Kahoot.Application/Authentication/Logout/LogoutCommandHandler.cs | — | ~241 |
| 11:40 | Created backend/src/Kahoot.Application/DependencyInjection.cs | — | ~231 |
| 11:41 | Created backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommand.cs | — | ~66 |
| 11:41 | Created backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandValidator.cs | — | ~82 |
| 11:41 | Created backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandHandler.cs | — | ~725 |
| 11:42 | Created backend/src/Kahoot.Application/Quizzes/Common/QuizContracts.cs | — | ~206 |
| 11:42 | Created backend/src/Kahoot.Application/Quizzes/Common/QuizErrors.cs | — | ~311 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/Common/QuestionValidationRules.cs | — | ~401 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/CreateQuiz/CreateQuizCommand.cs | — | ~50 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/CreateQuiz/CreateQuizCommandValidator.cs | — | ~96 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/CreateQuiz/CreateQuizCommandHandler.cs | — | ~283 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/UpdateQuiz/UpdateQuizCommand.cs | — | ~52 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/UpdateQuiz/UpdateQuizCommandValidator.cs | — | ~111 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/UpdateQuiz/UpdateQuizCommandHandler.cs | — | ~428 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/DeleteQuiz/DeleteQuizCommand.cs | — | ~43 |
| 11:43 | Created backend/src/Kahoot.Application/Quizzes/DeleteQuiz/DeleteQuizCommandHandler.cs | — | ~375 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/PublishQuiz/PublishQuizCommand.cs | — | ~43 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/PublishQuiz/PublishQuizCommandHandler.cs | — | ~596 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/Questions/AddQuestion/AddQuestionCommand.cs | — | ~92 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/Questions/AddQuestion/AddQuestionCommandValidator.cs | — | ~251 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/Questions/Common/QuestionMapping.cs | — | ~178 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/Questions/Common/QuizEditGuard.cs | — | ~341 |
| 11:44 | Created backend/src/Kahoot.Application/Quizzes/Questions/AddQuestion/AddQuestionCommandHandler.cs | — | ~564 |
| 11:45 | Created backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/UpdateQuestionCommand.cs | — | ~98 |
| 11:45 | Created backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/UpdateQuestionCommandValidator.cs | — | ~270 |
| 11:45 | Created backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/UpdateQuestionCommandHandler.cs | — | ~486 |
| 11:45 | Edited backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/UpdateQuestionCommandHandler.cs | expanded (+6 lines) | ~204 |
| 11:45 | Edited backend/src/Kahoot.Application/Quizzes/Questions/UpdateQuestion/UpdateQuestionCommandHandler.cs | 2→3 lines | ~30 |
| 11:45 | Created backend/src/Kahoot.Application/Quizzes/Questions/DeleteQuestion/DeleteQuestionCommand.cs | — | ~52 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/Questions/DeleteQuestion/DeleteQuestionCommandHandler.cs | — | ~386 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/Questions/ReorderQuestions/ReorderQuestionsCommand.cs | — | ~59 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/Questions/ReorderQuestions/ReorderQuestionsCommandValidator.cs | — | ~156 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/Questions/ReorderQuestions/ReorderQuestionsCommandHandler.cs | — | ~619 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/GetQuiz/GetQuizQuery.cs | — | ~56 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/GetQuiz/GetQuizQueryHandler.cs | — | ~595 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/ListQuizzes/ListQuizzesQuery.cs | — | ~59 |
| 11:46 | Created backend/src/Kahoot.Application/Quizzes/ListQuizzes/ListQuizzesQueryHandler.cs | — | ~367 |
| 11:49 | Created backend/src/Kahoot.Application/Games/Common/GameContracts.cs | — | ~682 |
| 11:49 | Created backend/src/Kahoot.Application/Games/Common/GameErrors.cs | — | ~630 |
| 11:49 | Created backend/src/Kahoot.Application/Games/Common/HostGameGuard.cs | — | ~260 |
| 11:49 | Created backend/src/Kahoot.Application/Games/Common/GameQuestionMapper.cs | — | ~524 |
| 11:49 | Created backend/src/Kahoot.Application/Games/CreateGame/CreateGameCommand.cs | — | ~58 |
| 11:50 | Created backend/src/Kahoot.Application/Games/CreateGame/CreateGameCommandHandler.cs | — | ~705 |
| 11:50 | Created backend/src/Kahoot.Application/Games/CreateGame/CreateGameCommandHandler.cs | — | ~652 |
| 11:50 | Created backend/src/Kahoot.Application/Games/Common/GameTime.cs | — | ~56 |
| 11:50 | Created backend/src/Kahoot.Application/Games/Common/QuestionActivation.cs | — | ~358 |
| 11:51 | Created backend/src/Kahoot.Application/Games/StartGame/StartGameCommand.cs | — | ~59 |
| 11:51 | Created backend/src/Kahoot.Application/Games/StartGame/StartGameCommandHandler.cs | — | ~612 |
| 11:51 | Created backend/src/Kahoot.Application/Games/StartNextQuestion/StartNextQuestionCommand.cs | — | ~63 |
| 11:51 | Created backend/src/Kahoot.Application/Games/StartNextQuestion/StartNextQuestionCommandHandler.cs | — | ~627 |
| 11:51 | Created backend/src/Kahoot.Application/Games/Common/QuestionResultsBuilder.cs | — | ~552 |
| 11:51 | Created backend/src/Kahoot.Application/Games/EndQuestion/EndQuestionCommand.cs | — | ~60 |
| 11:52 | Created backend/src/Kahoot.Application/Games/EndQuestion/EndQuestionCommandHandler.cs | — | ~647 |
| 11:52 | Created backend/src/Kahoot.Application/Games/Common/LeaderboardBuilder.cs | — | ~584 |
| 11:52 | Created backend/src/Kahoot.Application/Games/ShowLeaderboard/ShowLeaderboardCommand.cs | — | ~61 |
| 11:52 | Created backend/src/Kahoot.Application/Games/ShowLeaderboard/ShowLeaderboardCommandHandler.cs | — | ~556 |
| 11:52 | Created backend/src/Kahoot.Application/Games/EndGame/EndGameCommand.cs | — | ~56 |
| 11:53 | Created backend/src/Kahoot.Application/Games/EndGame/EndGameCommandHandler.cs | — | ~522 |
| 11:53 | Created backend/src/Kahoot.Application/Games/JoinGame/JoinGameCommand.cs | — | ~60 |
| 11:53 | Created backend/src/Kahoot.Application/Games/JoinGame/JoinGameCommandValidator.cs | — | ~174 |
| 11:53 | Created backend/src/Kahoot.Application/Games/JoinGame/JoinGameCommandHandler.cs | — | ~610 |
| 11:53 | Created backend/src/Kahoot.Application/Games/RemoveParticipant/RemoveParticipantCommand.cs | — | ~51 |
| 11:53 | Created backend/src/Kahoot.Application/Games/RemoveParticipant/RemoveParticipantCommandHandler.cs | — | ~446 |
| 11:53 | Created backend/src/Kahoot.Application/Games/SubmitAnswer/SubmitAnswerCommand.cs | — | ~82 |
| 11:54 | Created backend/src/Kahoot.Application/Games/SubmitAnswer/SubmitAnswerCommandValidator.cs | — | ~132 |
| 11:54 | Created backend/src/Kahoot.Application/Games/SubmitAnswer/SubmitAnswerCommandHandler.cs | — | ~1464 |
| 11:54 | Created backend/src/Kahoot.Application/Games/Reconnect/ReconnectParticipantCommand.cs | — | ~64 |
| 11:54 | Created backend/src/Kahoot.Application/Games/Reconnect/ReconnectParticipantCommandHandler.cs | — | ~1052 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetHostGameState/GetHostGameStateQuery.cs | — | ~61 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetHostGameState/GetHostGameStateQueryHandler.cs | — | ~806 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetQuestionResults/GetQuestionResultsQuery.cs | — | ~67 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetQuestionResults/GetQuestionResultsQueryHandler.cs | — | ~537 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetLeaderboard/GetLeaderboardQuery.cs | — | ~59 |
| 11:55 | Created backend/src/Kahoot.Application/Games/GetLeaderboard/GetLeaderboardQueryHandler.cs | — | ~321 |
| 11:56 | Edited backend/src/Kahoot.Infrastructure/Persistence/KahootDbContext.cs | modified KahootDbContext() | ~91 |
| 11:56 | Created backend/src/Kahoot.Infrastructure/Security/TokenHasher.cs | — | ~102 |
| 11:56 | Created backend/src/Kahoot.Infrastructure/Security/SecureTokenGenerator.cs | — | ~138 |
| 11:56 | Created backend/src/Kahoot.Infrastructure/Persistence/DbExceptionInterpreter.cs | — | ~228 |
| 11:57 | Created backend/src/Kahoot.Infrastructure/Games/GamePinGenerator.cs | — | ~290 |
| 11:57 | Created backend/src/Kahoot.Infrastructure/Authentication/JwtTokenService.cs | — | ~438 |
| 11:57 | Created backend/src/Kahoot.Infrastructure/DependencyInjection.cs | — | ~631 |
| 11:57 | Edited backend/src/Kahoot.Api/appsettings.json | expanded (+7 lines) | ~118 |
| 11:59 | Created backend/src/Kahoot.Api/Common/CurrentUser.cs | — | ~170 |
| 11:59 | Created backend/src/Kahoot.Api/Program.cs | — | ~870 |
| 12:01 | Created docs/realtime-protocol.md | — | ~1852 |
| 12:01 | Edited docs/functional-requirements.md | 5→6 lines | ~212 |
| 12:05 | Built the full Application layer (MediatR CQRS: 4 auth + 10 quiz/question + 13 game commands/queries, ValidationBehavior + RequestLoggingBehavior, GameStateMachine/ScoringService/LeaderboardService, ports IApplicationDbContext/IJwtTokenService/ITokenHasher/ISecureTokenGenerator/IGamePinGenerator/IDbExceptionInterpreter, ICurrentUser) and Infrastructure impls (JwtTokenService, TokenHasher, SecureTokenGenerator, GamePinGenerator, DbExceptionInterpreter, KahootDbContext:IApplicationDbContext). Wired Kahoot.Api for readiness: CurrentUser via HttpContext, JWT bearer, AddAuthorization, ValidateOnBuild. Added docs/realtime-protocol.md (SignalR event↔command map for the API layer). Release build 0 warn; API boots vs Postgres 17 with ValidateOnBuild → whole App+Infra DI graph resolves. No schema/migration change. | backend/src/Kahoot.Application/**, backend/src/Kahoot.Infrastructure/**, backend/src/Kahoot.Api/{Program.cs,Common/CurrentUser.cs}, docs/realtime-protocol.md | success | ~55000 |
| 12:19 | Edited backend/src/Kahoot.Application/Authentication/Common/AuthenticationErrors.cs | 7→4 lines | ~51 |
| 12:19 | Created backend/src/Kahoot.Api/Common/HostClaims.cs | — | ~104 |
| 12:19 | Created backend/src/Kahoot.Api/Common/CurrentUser.cs | — | ~98 |
| 12:19 | Created backend/src/Kahoot.Api/Common/ApiErrorMapping.cs | — | ~494 |
| 12:20 | Created backend/src/Kahoot.Api/Common/ApiControllerBase.cs | — | ~336 |
| 12:20 | Edited backend/src/Kahoot.Api/Common/ApiControllerBase.cs | StatusCode() → ObjectResult() | ~96 |
| 12:20 | Created backend/src/Kahoot.Api/Common/GlobalExceptionHandler.cs | — | ~283 |
| 12:20 | Created backend/src/Kahoot.Api/Contracts/AuthRequests.cs | — | ~59 |
| 12:20 | Created backend/src/Kahoot.Api/Contracts/QuizRequests.cs | — | ~128 |
| 12:20 | Created backend/src/Kahoot.Api/Contracts/GameRequests.cs | — | ~42 |
| 12:20 | Created backend/src/Kahoot.Api/Contracts/CreatedIdResponse.cs | — | ~22 |
| 12:20 | Created backend/src/Kahoot.Api/Realtime/RealtimeResponse.cs | — | ~124 |
| 12:20 | Created backend/src/Kahoot.Api/Realtime/GameGroups.cs | — | ~55 |
| 12:20 | Created backend/src/Kahoot.Api/Realtime/IGameClient.cs | — | ~152 |
| 12:21 | Created backend/src/Kahoot.Api/Realtime/GameNotifier.cs | — | ~405 |
| 12:21 | Created backend/src/Kahoot.Api/Realtime/GameHub.cs | — | ~1333 |
| 12:21 | Edited backend/src/Kahoot.Api/Realtime/GameHub.cs | 3→2 lines | ~12 |
| 12:21 | Created backend/src/Kahoot.Api/Controllers/AuthController.cs | — | ~468 |
| 12:21 | Edited backend/src/Kahoot.Api/Controllers/AuthController.cs | 2→2 lines | ~47 |
| 12:21 | Edited backend/src/Kahoot.Api/Controllers/AuthController.cs | 2→2 lines | ~45 |
| 12:22 | Created backend/src/Kahoot.Api/Controllers/QuizzesController.cs | — | ~1666 |
| 12:22 | Created backend/src/Kahoot.Api/Controllers/GamesController.cs | — | ~2019 |
| 12:23 | Edited backend/src/Kahoot.Api/Controllers/UploadsController.cs | modified UploadsController() | ~87 |
| 12:23 | Created backend/src/Kahoot.Api/Program.cs | — | ~1075 |
| 12:26 | Created docs/realtime-protocol.md | — | ~2407 |
| 12:27 | Edited docs/functional-requirements.md | 6→7 lines | ~323 |
| 12:31 | Created backend/src/Kahoot.Api/Endpoints/HomePageEndpoint.cs | — | ~1493 |
| 12:31 | Edited backend/src/Kahoot.Api/Program.cs | 3→4 lines | ~27 |
| 12:31 | Edited backend/src/Kahoot.Api/Program.cs | 5→6 lines | ~32 |
| 12:32 | Edited backend/src/Kahoot.Api/Program.cs | 5→6 lines | ~32 |
| 12:42 | Created backend/src/Kahoot.Application/Games/GameStateMachine.cs | — | ~401 |
| 12:42 | Edited backend/src/Kahoot.Application/Games/GameStateMachine.cs | 2→2 lines | ~25 |
| 12:42 | Created backend/src/Kahoot.Application/Games/Common/QuestionActivation.cs | — | ~314 |
| 12:43 | Created backend/src/Kahoot.Application/Games/StartGame/StartGameCommandHandler.cs | — | ~789 |
| 12:43 | Created backend/src/Kahoot.Application/Games/StartNextQuestion/StartNextQuestionCommandHandler.cs | — | ~848 |
| 12:43 | Edited backend/src/Kahoot.Application/Games/EndQuestion/EndQuestionCommandHandler.cs | not() → CanFire() | ~88 |
| 12:43 | Edited backend/src/Kahoot.Application/Games/ShowLeaderboard/ShowLeaderboardCommandHandler.cs | modified if() | ~113 |
| 12:43 | Edited backend/src/Kahoot.Application/Games/EndGame/EndGameCommandHandler.cs | added 1 condition(s) | ~129 |
| 12:43 | Created backend/src/Kahoot.Application/Games/Presence/AttachParticipantConnectionCommand.cs | — | ~54 |
| 12:43 | Created backend/src/Kahoot.Application/Games/Presence/AttachParticipantConnectionCommandHandler.cs | — | ~269 |
| 12:43 | Created backend/src/Kahoot.Application/Games/Presence/DetachParticipantConnectionCommand.cs | — | ~54 |
| 12:44 | Created backend/src/Kahoot.Application/Games/Presence/DetachParticipantConnectionCommandHandler.cs | — | ~290 |
| 12:44 | Created backend/src/Kahoot.Application/Games/Presence/AuthorizeHostGameQuery.cs | — | ~47 |
| 12:44 | Created backend/src/Kahoot.Application/Games/Presence/AuthorizeHostGameQueryHandler.cs | — | ~196 |
| 12:44 | Edited backend/src/Kahoot.Application/Games/Common/GameErrors.cs | 3→6 lines | ~87 |
| 12:44 | Created backend/src/Kahoot.Api/Realtime/GameHub.cs | — | ~1512 |
| 12:45 | Edited backend/src/Kahoot.Application/Games/Common/GameContracts.cs | 9→11 lines | ~91 |
| 12:45 | Edited backend/src/Kahoot.Application/Games/Reconnect/ReconnectParticipantCommandHandler.cs | added nullish coalescing | ~398 |
| 12:45 | Edited backend/src/Kahoot.Application/Authentication/Common/AuthTokenFactory.cs | modified Issue() | ~324 |
| 12:45 | Edited backend/src/Kahoot.Application/Authentication/Login/LoginCommandHandler.cs | 7→8 lines | ~85 |
| 12:46 | Created backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandHandler.cs | — | ~963 |
| 12:46 | Edited backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandHandler.cs | 8→8 lines | ~59 |
| 12:46 | Edited backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandHandler.cs | 6→6 lines | ~101 |
| 12:46 | Edited backend/src/Kahoot.Application/Games/SubmitAnswer/SubmitAnswerCommandHandler.cs | modified if() | ~441 |
| 12:47 | Edited backend/src/Kahoot.Application/Games/Common/HostGameGuard.cs | added 1 condition(s) | ~351 |
| 12:47 | Created backend/src/Kahoot.Application/Games/GetLeaderboard/GetLeaderboardQueryHandler.cs | — | ~254 |
| 12:48 | Created backend/src/Kahoot.Api/Common/RateLimitingExtensions.cs | — | ~563 |
| 12:49 | Created backend/src/Kahoot.Api/Program.cs | — | ~1366 |
| 12:49 | Edited backend/src/Kahoot.Api/Program.cs | 6→6 lines | ~64 |
| 12:49 | Edited backend/src/Kahoot.Api/Program.cs | 7→7 lines | ~63 |
| 12:50 | Edited backend/src/Kahoot.Api/Controllers/AuthController.cs | modified AuthController() | ~154 |
| 12:50 | Edited backend/src/Kahoot.Api/Controllers/GamesController.cs | 6→7 lines | ~52 |
| 12:50 | Edited backend/src/Kahoot.Api/Controllers/GamesController.cs | 3→4 lines | ~47 |
| 12:53 | Created frontend/src/api/axiosClient.ts | — | ~438 |
| 12:54 | Created frontend/src/api/authService.ts | — | ~504 |
| 12:54 | Edited frontend/src/api/authService.ts | modified logout() | ~78 |
| 12:54 | Created frontend/src/api/gameService.ts | — | ~270 |
| 12:57 | Edited docs/realtime-protocol.md | expanded (+8 lines) | ~347 |
| 12:57 | Edited docs/realtime-protocol.md | expanded (+25 lines) | ~345 |
| 12:57 | Edited docs/functional-requirements.md | 3→3 lines | ~170 |
| 12:57 | Edited docs/non-functional-requirements.md | modified IP() | ~183 |
| 13:02 | Edited backend/src/Kahoot.Application/Authentication/Refresh/RefreshTokenCommandHandler.cs | modified if() | ~338 |

## Session: 2026-09-09 16:57

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 17:06 | Created load-tests/.gitignore | — | ~49 |
| 17:07 | Created load-tests/config/environments.js | — | ~605 |
| 17:07 | Created load-tests/helpers/metrics.js | — | ~893 |
| 17:08 | Created load-tests/helpers/signalr.js | — | ~3428 |
| 17:09 | Created load-tests/helpers/rest.js | — | ~1379 |
| 17:09 | Created load-tests/helpers/testdata.js | — | ~884 |
| 17:10 | Created load-tests/config/thresholds.js | — | ~639 |
| 17:10 | Created load-tests/helpers/summary.js | — | ~1235 |
| 17:10 | Created load-tests/helpers/orchestration.js | — | ~1073 |
| 17:11 | Created load-tests/scenarios/connections.js | — | ~792 |
| 17:11 | Created load-tests/scenarios/join-game.js | — | ~1332 |
| 17:13 | Created load-tests/scenarios/answer-burst.js | — | ~3173 |
| 17:14 | Created load-tests/scenarios/question-broadcast.js | — | ~2076 |
| 17:15 | Created load-tests/scenarios/duplicate-answer.js | — | ~2396 |
| 17:16 | Created load-tests/scenarios/reconnection.js | — | ~2638 |
| 17:16 | Edited load-tests/helpers/testdata.js | modified provisionPerGameQuizzes() | ~608 |
| 17:17 | Created load-tests/scenarios/multiple-games.js | — | ~2460 |
| 17:18 | Created load-tests/scenarios/ramp.js | — | ~1939 |
| 17:18 | Edited load-tests/scenarios/ramp.js | modified player() | ~311 |
| 17:18 | Edited load-tests/scenarios/ramp.js | 2→2 lines | ~26 |
| 17:19 | Created load-tests/scenarios/reconnection-storm.js | — | ~2240 |
| 17:20 | Created load-tests/scenarios/reconnection-storm.js | — | ~2137 |
| 17:20 | Created load-tests/scenarios/endurance.js | — | ~2144 |
| 17:21 | Created load-tests/run-all.js | — | ~1905 |
| 17:21 | Created load-tests/verify/verify.sql | — | ~1151 |
| 17:22 | Edited load-tests/verify/verify.sql | expanded (+6 lines) | ~229 |
| 17:22 | Created load-tests/verify/verify-db.mjs | — | ~464 |
| 17:23 | Created load-tests/README.md | — | ~3698 |
| 17:24 | Edited load-tests/helpers/signalr.js | 2→4 lines | ~64 |
| 17:29 | Edited load-tests/helpers/summary.js | 4→8 lines | ~138 |
| 17:29 | Edited load-tests/run-all.js | 3→3 lines | ~76 |
| 17:29 | Edited load-tests/scenarios/answer-burst.js | 20→23 lines | ~244 |
| 17:34 | Edited load-tests/helpers/rest.js | added 1 import(s) | ~16 |
| 17:34 | Edited load-tests/helpers/rest.js | added 3 condition(s) | ~278 |
| 17:34 | Edited load-tests/run-all.js | 3→3 lines | ~74 |
| 17:35 | Edited load-tests/run-all.js | added error handling | ~823 |
| 17:35 | Edited load-tests/run-all.js | 5→8 lines | ~46 |
| 17:35 | Edited load-tests/helpers/orchestration.js | modified integrity() | ~362 |
| 17:35 | Edited load-tests/helpers/orchestration.js | added 1 condition(s) | ~324 |
| 17:36 | Edited load-tests/scenarios/answer-burst.js | 4→5 lines | ~33 |
| 17:36 | Edited load-tests/scenarios/answer-burst.js | modified if() | ~138 |
| 17:36 | Edited load-tests/scenarios/answer-burst.js | added error handling | ~395 |
| 17:36 | Edited load-tests/scenarios/answer-burst.js | added 1 condition(s) | ~368 |
| 17:36 | Edited load-tests/scenarios/answer-burst.js | 2→3 lines | ~53 |
| 17:37 | Edited load-tests/helpers/metrics.js | added 1 condition(s) | ~206 |
| 17:37 | Edited load-tests/scenarios/answer-burst.js | modified if() | ~71 |
| 17:37 | Edited load-tests/scenarios/answer-burst.js | 4→5 lines | ~26 |
| 17:37 | Edited load-tests/scenarios/answer-burst.js | 7→9 lines | ~137 |
| 17:51 | Edited load-tests/helpers/rest.js | added 1 condition(s) | ~304 |
| 17:52 | Edited load-tests/helpers/signalr.js | modified start() | ~352 |
| 17:53 | Created load-tests/scenarios/connections.js | — | ~1088 |
| 17:53 | Edited load-tests/run-all.js | expanded (+7 lines) | ~180 |
| 17:53 | Edited load-tests/run-all.js | added 1 condition(s) | ~149 |
| 17:53 | Edited load-tests/run-all.js | 3→5 lines | ~52 |
| 17:57 | Edited load-tests/scenarios/ramp.js | 4→7 lines | ~135 |
| 18:01 | Edited load-tests/run-all.js | modified cooldownSeconds() | ~66 |
| 18:02 | Edited load-tests/run-all.js | modified if() | ~54 |
| 18:09 | Created load-tests/docker-compose.railway-sim.yml | — | ~777 |
| 18:13 | Edited load-tests/helpers/rest.js | added error handling | ~240 |
| 18:17 | Edited load-tests/README.md | modified A() | ~1089 |
| 18:17 | Edited load-tests/README.md | 9→12 lines | ~240 |
| 18:17 | Edited load-tests/README.md | expanded (+14 lines) | ~320 |
| 18:17 | Edited load-tests/README.md | 10→11 lines | ~175 |
| 18:18 | Built k6 load-test suite (load-tests/): 10 scenarios + SignalR JSON-protocol client, run-all.js, verify.sql, docker-compose.railway-sim.yml (Railway-sized stack). Smoke runs PASS; per-IP rate limiter (240+120/30s) forces slow connection ramps from a single IP. Full 500-player acceptance run in progress vs Railway-sim (API 2vCPU/1GiB). | load-tests/** | in progress | ~9000 |
| 18:21 | Edited docker-compose.yml | expanded (+22 lines) | ~570 |
| 18:22 | Edited load-tests/README.md | modified A() | ~580 |
| 18:23 | Edited load-tests/README.md | expanded (+12 lines) | ~603 |
| 18:23 | Edited load-tests/verify/verify-db.mjs | 6→9 lines | ~137 |
| 18:41 | Created docker-compose.yml | — | ~509 |
| 18:42 | Edited load-tests/README.md | modified A() | ~399 |
| 18:42 | Edited load-tests/README.md | 8→10 lines | ~150 |
| 18:44 | Edited load-tests/README.md | 4→3 lines | ~43 |
| 18:45 | Created load-tests/run.sh | — | ~1194 |
| 18:46 | Edited load-tests/run.sh | 3→3 lines | ~28 |
| 18:46 | Edited load-tests/run.sh | 3→3 lines | ~20 |
