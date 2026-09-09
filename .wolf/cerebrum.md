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
- **Domain conventions established:** encapsulated entities (private setters + private ctor for EF + static factory methods). Time-dependent domain logic takes `nowUtc` params (no `DateTime.UtcNow` in domain).
  - NO shared `Entity` base class. `AuditableEntity` is self-contained: `Id` (UUID v7 field initializer) + `CreatedAtUtc`/`UpdatedAtUtc` (populated later by an Infrastructure SaveChanges interceptor). Entities that are not auditable (`Answer`, `RefreshToken`) declare their own `Id` and have no base class.
  - Result pattern, NOT exceptions, for expected domain failure outcomes: `Kahoot.Domain.Common.Result` / `Result<T>` / `Error(Code, Description)`. Per-aggregate error factories, e.g. `GameErrors.InvalidStatusTransition`. `GameSession` transition methods return `Result`. BCL `ArgumentException`/`ArgumentOutOfRangeException` are still used for genuine precondition/contract violations (FluentValidation guards these upstream).
- **DBML validation:** `npx -y -p @dbml/cli dbml2sql backend/projectSchema.dbml --postgres`. Gotcha: index-setting `note:` strings must NOT contain escaped `''` apostrophes — reword to avoid them (table-level `Note: '''...'''` blocks are fine).

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
