# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-09-09T07:50:53.190Z
> Files: 28 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `AGENTS.md` — OpenWolf (~75 tok)
- `CLAUDE.md` — OpenWolf (~99 tok)
- `Final Claude Code Prompt — Kahoot-like Platform.md` — 1. Required Technology Stack (~7074 tok)
- `GEMINI.md` — OpenWolf (~75 tok)

## backend/

- `projectSchema.dbml` (~2791 tok)

## backend/src/Kahoot.Application/Common/Security/

- `IPasswordHasher.cs` — Class: IPasswordHasher (~47 tok)

## backend/src/Kahoot.Domain/Common/

- `AuditableEntity.cs` — Class: AuditableEntity (~62 tok)
- `DomainException.cs` — Class: DomainException (~46 tok)
- `Entity.cs` — Class: Entity (~49 tok)
- `Result.cs` — Class: Result (~384 tok)

## backend/src/Kahoot.Domain/Games/

- `Answer.cs` — Class: Answer (~189 tok)
- `GameErrors.cs` — Class: GameErrors (~76 tok)
- `GameSession.cs` — Class: GameSession (~231 tok)
- `GameStatus.cs` — Class: GameStatus (~42 tok)
- `GameStatusTransitions.cs` — Class: GameStatusTransitions (~269 tok)
- `InvalidGameStatusTransitionException.cs` — Class: InvalidGameStatusTransitionException (~114 tok)
- `Participant.cs` — Class: Participant (~189 tok)

## backend/src/Kahoot.Domain/Hosts/

- `Host.cs` — Class: Host (~100 tok)
- `RefreshToken.cs` — Class: RefreshToken (~120 tok)

## backend/src/Kahoot.Domain/Quizzes/

- `Choice.cs` — Class: Choice (~88 tok)
- `Question.cs` — Class: Question (~112 tok)
- `Quiz.cs` — Class: Quiz (~99 tok)

## backend/src/Kahoot.Infrastructure/Persistence/

- `KahootDbContext.cs` — DbContext: Host, RefreshToken, Quiz, Question, Choice, GameSession, Participant, Answer (~334 tok)
- `KahootDbContextFactory.cs` — Class: KahootDbContextFactory (~223 tok)

## backend/src/Kahoot.Infrastructure/Persistence/Interceptors/

- `AuditableEntityInterceptor.cs` — Class: AuditableEntityInterceptor (~442 tok)

## backend/src/Kahoot.Infrastructure/Security/

- `BcryptPasswordHasher.cs` — Class: BcryptPasswordHasher (~133 tok)

## docs/

- `functional-requirements.md` — Functional Requirements — Kahoot-like Platform (~1852 tok)
- `non-functional-requirements.md` — Non-Functional Requirements — Kahoot-like Platform (~1919 tok)
