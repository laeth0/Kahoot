# Platform Operations

## Overview

The application prepares its database predictably at startup and exposes a minimal
liveness endpoint for operational checks.

## User Stories

### US-019: Start against the current schema

**As an**
operator

**I want**
pending schema migrations to run before the application begins serving traffic

**So that**
the deployed application and database stay compatible.

### US-020: Check application liveness

**As an**
operator

**I want**
a safe health endpoint

**So that**
the platform can determine whether the application is alive.

## Acceptance Criteria

- Given pending EF Core migrations, when the application starts, then a hosted service applies them asynchronously before normal operation.
- Given a migration failure, when startup runs, then application startup fails rather than serving against an incompatible schema.
- Given successful migrations, when startup continues, then the separate seeding hosted service runs.
- Given a request to `/health`, when the endpoint responds, then it reports liveness without disclosing sensitive information.

## Business Rules

### FR-9: Startup database operations

- Database migration and bootstrap seeding are separate startup operations with an explicit execution order.

### FR-9.1: Automatic migrations

- Pending EF Core migrations are applied by a hosted service, not directly from `Program.cs`.
- Migration execution uses the startup cancellation token.

### FR-9.2: Seeding

- Seeding runs in a separate hosted service after migrations complete.
- Startup seeding currently creates only the bootstrap host account defined by FR-2.1.
- Development seed data, test data, and production reference data remain separate concerns.

### FR-10: Operational endpoints

- `/health` reports liveness without exposing sensitive information.

## Edge Cases

- A migration failure fails startup immediately.
- Seeding never runs before migrations have completed successfully.
