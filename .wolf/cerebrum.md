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
- **Backend layout:** .NET 10 Clean Architecture — `Kahoot.Domain` (pure POCO, no package refs), `Kahoot.Application` (MediatR/FluentValidation/Mapster), `Kahoot.Infrastructure` (EF Core 10 + Npgsql + Scrutor), `Kahoot.Api`. Solution file is `backend/Kahoot.slnx`.
- **backend/AGENTS.md hard rules:** no comments in C# code; every model/relationship change must update `backend/projectSchema.dbml` AND add a new EF Core migration in the same task; migrations are immutable.
- **NOT using DDD.** Domain entities are ANEMIC — plain data classes, `public T Prop { get; set; }` only. NO constructors, factory methods, mutation methods, computed properties, validation helpers, guard clauses, or domain constants inside entities. All logic (creation, validation, state transitions, scoring, PIN generation) lives in the `Kahoot.Application` layer services/handlers.
  - NO shared `Entity` base class. `AuditableEntity` is a data-only base: `Id` (UUID v7 field initializer) + `CreatedAt`/`UpdatedAt` (populated by `AuditableEntityInterceptor` via injected `TimeProvider`). Non-auditable entities (`Answer`, `RefreshToken`) declare their own `Id`, no base.
  - DateTime properties have **no `Utc` suffix** (`CreatedAt`, `SubmittedAt`, `CurrentQuestionEndsAt`, …) so C# → snake_case → DBML columns line up (`created_at`, …). UTC is enforced by the interceptor + `timestamptz` mapping.
  - `Kahoot.Domain` holds: the 8 entities, `GameStatus` enum, `Common/Result.cs`. State machine / scoring / PIN gen go in `Kahoot.Application` later.
- **Persistence (`Kahoot.Infrastructure`):** `Persistence/KahootDbContext` + one `IEntityTypeConfiguration<T>` per entity in `Persistence/Configurations/`. `EFCore.NamingConventions` `UseSnakeCaseNamingConvention()`. `xmin` optimistic concurrency on `game_sessions` via a **shadow** `uint "Version"` property mapped to column `xmin` type `xid` (NOT the anemic entity). Password hashing: `IPasswordHasher` (Application `Common/Security`) implemented by `BcryptPasswordHasher` (`BCrypt.Net-Next`, `EnhancedHashPassword`, work factor 12, `ISingletonService`). Design-time: `KahootDbContextFactory`.
- **Startup hosted services** (`Kahoot.Infrastructure/Startup/`): `DatabaseMigrationHostedService` then `DatabaseSeederHostedService`, both `IHostedLifecycleService` doing their work in `StartingAsync` (runs before Kestrel; registration order = migrate then seed). Migration failure propagates → startup fails. Seeder is idempotent and skips with a warning when `Seeding:Host` (`Username`+`Password`) is not fully configured. Verified end-to-end against Postgres 17: migrate → `admin` seeded (bcrypt `$2a$12$`) → idempotent re-run → skip-when-unconfigured.
- Initial migration: `Kahoot.Infrastructure/Persistence/Migrations/20260909075952_InitialCreate`.
- **DBML validation:** `npx -y -p @dbml/cli dbml2sql backend/projectSchema.dbml --postgres`. Gotcha: index-setting `note:` strings must NOT contain escaped `''` apostrophes (table-level `Note: '''...'''` blocks are fine).
- **Local machine gotcha:** a `postgresql-x64-18` Windows service holds `localhost:5432` and shadows docker-compose's `kahoot-db`. For local container testing, publish the test Postgres on a different host port (e.g. `5433`).

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
- **[2026-09-09] Frontend Light-Only Theme & Logo Palette:** Configured Material UI theme with palette extracted from `logo.jpeg` (`#00629B` primary, `#0284C7` secondary, `#F4F8FC` canvas, `#09131F` text). Hardcoded light mode only (`mode: 'light'`) to meet user specification. Added typography scale, CSS tokens, and component overrides.
