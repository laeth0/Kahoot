# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-09-09T08:18:03.447Z
> Files: 47 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `AGENTS.md` — OpenWolf (~75 tok)
- `CLAUDE.md` — OpenWolf (~99 tok)
- `Final Claude Code Prompt — Kahoot-like Platform.md` — 1. Required Technology Stack (~7074 tok)
- `GEMINI.md` — OpenWolf (~75 tok)

## backend/

- `projectSchema.dbml` (~2858 tok)

## backend/src/Kahoot.Api/

- `appsettings.json` (~135 tok)

## backend/src/Kahoot.Api/Controllers/

- `UploadsController.cs` — Controller: UploadsController (~409 tok)

## backend/src/Kahoot.Application/Common/Security/

- `IPasswordHasher.cs` — Class: IPasswordHasher (~47 tok)

## backend/src/Kahoot.Application/Common/Storage/

- `FileStorageOptions.cs` — Class: FileStorageOptions (~213 tok)
- `IFileStorage.cs` — Class: IFileStorage (~49 tok)
- `ImageSignature.cs` — Class: ImageSignature (~255 tok)
- `ImageUploadService.cs` — Class: ImageUploadService (~591 tok)

## backend/src/Kahoot.Domain/Common/

- `AuditableEntity.cs` — Class: AuditableEntity (~60 tok)
- `DomainException.cs` — Class: DomainException (~46 tok)
- `Entity.cs` — Class: Entity (~49 tok)
- `Result.cs` — Class: Result (~384 tok)

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

- `DependencyInjection.cs` — Class: DependencyInjection (~520 tok)

## backend/src/Kahoot.Infrastructure/Persistence/

- `KahootDbContext.cs` — DbContext: Host, RefreshToken, Quiz, Question, Choice, GameSession, Participant, Answer (~334 tok)
- `KahootDbContextFactory.cs` — Class: KahootDbContextFactory (~223 tok)

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

## backend/src/Kahoot.Infrastructure/Startup/

- `DatabaseMigrationHostedService.cs` — Class: DatabaseMigrationHostedService (~465 tok)
- `DatabaseSeederHostedService.cs` — Class: DatabaseSeederHostedService (~623 tok)
- `HostSeedOptions.cs` — Class: HostSeedOptions (~91 tok)

## backend/src/Kahoot.Infrastructure/Storage/

- `LocalFileStorage.cs` — Class: LocalFileStorage (~318 tok)

## docs/

- `functional-requirements.md` — Functional Requirements — Kahoot-like Platform (~1852 tok)
- `non-functional-requirements.md` — Non-Functional Requirements — Kahoot-like Platform (~1919 tok)
