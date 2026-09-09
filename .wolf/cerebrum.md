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

## Key Learnings

- **Project:** kahoot
- **Backend layout:** .NET 10 Clean Architecture — `Kahoot.Domain` (pure POCO, no package refs), `Kahoot.Application` (MediatR/FluentValidation/Mapster), `Kahoot.Infrastructure` (EF Core 10 + Npgsql + Scrutor), `Kahoot.Api`. Solution file is `backend/Kahoot.slnx`.
- **backend/AGENTS.md hard rules:** no comments in C# code; every model/relationship change must update `backend/projectSchema.dbml` AND add a new EF Core migration in the same task; migrations are immutable.
- **NOT using DDD.** Domain entities are ANEMIC — plain data classes, `public T Prop { get; set; }` only. NO constructors, factory methods, mutation methods, computed properties, validation helpers, guard clauses, or domain constants inside entities. All logic (creation, validation, state transitions, scoring, PIN generation) lives in the `Kahoot.Application` layer services/handlers.
  - NO shared `Entity` base class. `AuditableEntity` is a data-only base: `Id` (UUID v7 field initializer) + `CreatedAtUtc`/`UpdatedAtUtc` (populated later by an Infrastructure SaveChanges interceptor). Non-auditable entities (`Answer`, `RefreshToken`) declare their own `Id`, no base.
  - `Kahoot.Domain` currently holds: the 8 entities, `GameStatus` enum, and `Common/Result.cs` (`Result`/`Result<T>`/`Error` — shared primitive for Application handlers, per the user's "Result pattern not exceptions" preference). The game state-machine rules table will be built in `Kahoot.Application` (spec's `GameStateMachine`), not the domain.
- **DBML validation:** `npx -y -p @dbml/cli dbml2sql backend/projectSchema.dbml --postgres`. Gotcha: index-setting `note:` strings must NOT contain escaped `''` apostrophes — reword to avoid them (table-level `Note: '''...'''` blocks are fine).

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
