# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-09-09T07:26:49.571Z
> Files: 19 tracked | Anatomy hits: 0 | Misses: 0

> Project structure index. Auto-maintained by OpenWolf hooks and daemon.
> Run `openwolf scan` to generate, or wait for the first Claude Code session.
> Status: Pending initial scan

## ./

- `AGENTS.md` — OpenWolf (~75 tok)
- `CLAUDE.md` — OpenWolf (~99 tok)
- `Final Claude Code Prompt — Kahoot-like Platform.md` — 1. Required Technology Stack (~7074 tok)
- `GEMINI.md` — OpenWolf (~75 tok)

## backend/

- `projectSchema.dbml` — ============================================================================= (~3671 tok)

## backend/src/Kahoot.Domain/Common/

- `AuditableEntity.cs` — Class: AuditableEntity (~52 tok)
- `DomainException.cs` — Class: DomainException (~46 tok)
- `Entity.cs` — Class: Entity (~49 tok)

## backend/src/Kahoot.Domain/Games/

- `Answer.cs` — Class: Answer (~601 tok)
- `GameSession.cs` — Class: GameSession (~856 tok)
- `GameStatus.cs` — Class: GameStatus (~42 tok)
- `GameStatusTransitions.cs` — Class: GameStatusTransitions (~269 tok)
- `InvalidGameStatusTransitionException.cs` — Class: InvalidGameStatusTransitionException (~114 tok)
- `Participant.cs` — Class: Participant (~792 tok)

## backend/src/Kahoot.Domain/Hosts/

- `Host.cs` — Class: Host (~464 tok)
- `RefreshToken.cs` — Class: RefreshToken (~430 tok)

## backend/src/Kahoot.Domain/Quizzes/

- `Choice.cs` — Class: Choice (~378 tok)
- `Question.cs` — Class: Question (~838 tok)
- `Quiz.cs` — Class: Quiz (~443 tok)
