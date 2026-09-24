# Backend agent instructions

These instructions apply to `backend/`. Read the affected code and nearby dependencies before changing it. Keep changes focused, preserve existing contracts, and follow the conventions already present in the relevant project. Make this project production-ready: write clean code and apply industry best practices.

## Architecture and Core Principles

- Use Clean Architecture and Vertical Slice Architecture.
- Keep dependencies flowing in the existing direction. Put HTTP concerns in Api, application behavior, vertical feature slices, and contracts in Application, database and external-service implementations in Infrastructure, and core entities in Domain.
- Make this project production-ready: write clean code, handle edge cases gracefully, follow idiomatic C#/.NET design patterns, and ensure strict separation of concerns.

## Project layout

- `Kahoot.slnx` contains four .NET 10 projects under `src/`.
- `Kahoot.Domain` holds entities and domain contracts. It has no project references.
- `Kahoot.Application` holds application contracts, vertical slice feature folders, MediatR command/query interfaces, results, and service registration. It references Domain.
- `Kahoot.Infrastructure` implements persistence. It references Application; its registration is in `DependencyInjection.cs`.
- `Kahoot.Api` is the HTTP entry point. It references Application and Infrastructure; `Program.cs` configures middleware, CORS, health checks, and endpoints.
- `test/` directory must remain completely empty, containing only the `.gitkeep` file.

## Existing patterns

- Use the existing `ICommand`/`IQuery` and `Result`/`Error` types for application operations where they fit. MediatR handlers and FluentValidation validators are registered by assembly scanning in `Kahoot.Application/DependencyInjection.cs`.
- Preserve the existing ASP.NET Core Problem Details responses and centralized exception handling in `GlobalExceptionHandler`.
- EF Core uses PostgreSQL, snake_case names, `AppDbContext`, and entity configurations in `Persistence/Configurations`. The app applies pending migrations at startup.
- Pass `CancellationToken` through async request, database, and service calls. Avoid logging credentials or sensitive data.
- **Constructor and Dependency Injection Style:** Always use explicit constructor injection with `private readonly` backing fields (prefixed with `_`) and assignments inside the constructor body. Do not use C# primary constructors on classes for dependency injection.

## Database and EF Core Migrations

- **Migration Immutability:** Existing migrations and generated designer files are immutable. Never modify, rename, or delete an existing migration; always add a new migration for subsequent schema changes.
- **`projectSchema.dbml` Sync:** Whenever database models or relationships change, you MUST update `projectSchema.dbml` and generate the new EF Core migration together in the same task.
- **Separation of Concerns:** Keep schema migrations, development seed data, and production reference data strictly separated. Do not combine them in the same initialization workflow or service.

## Configuration and local verification

- Keep PostgreSQL connection settings in `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. Do not use .NET user secrets or commit passwords.
- See `README.md` for local setup and required environment variables. Keep passwords out of committed configuration. `ConnectionStrings__DefaultConnection` supplies a password-bearing connection string.
- Use the .NET Options Pattern for grouped runtime configuration when it improves type safety, validation, and maintainability. Do not use it for ordinary constants or values that are not meant to vary by environment. Prefer strongly typed options over scattered configuration-string lookups, and validate critical options at startup.
- Development CORS origins are in `src/Kahoot.Api/appsettings.Development.json`. OpenAPI and Scalar are exposed only in Development. The database-aware health endpoint is `/health`.
- From `backend/`, run `dotnet build Kahoot.slnx` after code changes. Run `dotnet format Kahoot.slnx --verify-no-changes` when formatting is relevant. Report any verification that could not run.

## Testing constraints

- Do not create or add any test code, test projects, unit tests, integration tests, or end-to-end tests to the backend.
- The `backend/test/` directory must remain completely empty, containing only the `.gitkeep` file.

Before finishing, review the changed files for unintended edits, unused code, contract changes, security effects, and consistency with the existing project structure.

