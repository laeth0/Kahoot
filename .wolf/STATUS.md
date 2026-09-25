---
description: session handoff, regenerate with /handoff when a quest finishes
budget_tokens: 1000
---
# STATUS — kahoot

> Last updated: 2026-09-25

## Done

- Reviewed the current four-project backend Auth flow against docs/01, 02, 03, 10–14 and the repository instructions.
- Serialized account credential mutations on a PostgreSQL user-row lock, corrected refresh race/revocation behavior, added cookie CSRF validation and strict origin checks, aligned Auth claims/responses with the Host account ID ownership model, and bounded credential inputs.
- Added a generated initial EF Core migration for the existing model, corrected duplicate enum registration, and synchronized the display username length in projectSchema.dbml.
- Ran `dotnet build Kahoot.slnx`, `dotnet format Kahoot.slnx --verify-no-changes --no-restore`, and `dotnet-ef migrations has-pending-model-changes`; all succeeded.

## Next phase

**Goal:** Address the remaining documented Auth platform requirements after deployment architecture and database access are available.

### Acceptance criteria
1. Implement cross-instance IP and username login limits with bounded state and trusted proxy configuration.
2. Implement administrator bootstrap and bounded refresh-token cleanup, then verify migration and Auth flows against PostgreSQL.
3. Measure the documented Auth and revocation SLOs under representative load.

### Relevant files
- `backend/src/Kahoot.Infrastructure/Security/LoginRateLimiter.cs`
- `backend/src/Kahoot.Infrastructure/Persistence/Migrations/`
- `backend/src/Kahoot.Application/Features/Auth/`
- `docs/02-authentication.md`

### Closed decisions
- Host ownership uses `users.id`, stored in `host_account_id` on owned rows. There is no separate accounts or tenants table.
- Fresh database authority checks remain on each authenticated request to meet rapid revocation requirements.

### Open decisions
- Trusted reverse-proxy addresses and the deployment-wide rate-limit store are not configured in this repository.

## Active architecture

- .NET 10, ASP.NET Core, MediatR, EF Core 10, Npgsql/PostgreSQL; Domain → Application → Infrastructure/Api.
- Auth requests use PostgreSQL as the durable credential authority. Backend tests are prohibited by `backend/AGENTS.md`.

## External blockers

- No accessible PostgreSQL credentials or Docker integration were available for live migration and concurrency scenarios.
