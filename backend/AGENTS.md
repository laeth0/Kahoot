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
- **Service Registration and `ServiceCollectionExtension`:** Every file/installer inside `src/Kahoot.Api/ServiceCollectionExtension` must strictly focus on one single responsibility. Do not create large installers or extension methods that bundle multiple unrelated concerns. Every installer/extension method must have one focused purpose (for example, a dedicated installer for JWT authentication, a dedicated installer for CORS). The class name must be obvious and accurately describe the contents of the class (e.g., `JwtAuthenticationInstaller`, `CorsInstaller`). Core framework and hosting registrations (`AddControllers`, `AddOpenApi`, `AddExceptionHandler`, `AddProblemDetails`, `AddHealthChecks`, `AddHttpContextAccessor`, `AddAuthorization`, `AddApplication`, `AddInfrastructure`) belong directly in `Program.cs`.

## Database and EF Core Migrations

- **Migration Immutability:** Existing migrations and generated designer files are immutable. Never modify, rename, or delete an existing migration; always add a new migration for subsequent schema changes.
- **`projectSchema.dbml` Sync:** Whenever database models or relationships change, you MUST update `projectSchema.dbml` and generate the new EF Core migration together in the same task.
- **Separation of Concerns:** Keep schema migrations, development seed data, and production reference data strictly separated. Do not combine them in the same initialization workflow or service.

## Configuration and local verification

- Review and keep all three configuration files consistent with their intended environments whenever adding, modifying, or removing configuration values:
  - `src/Kahoot.Api/appsettings.json`: Keep only safe shared defaults that make sense across environments. Do not store secrets here. Avoid production-specific values unless they are truly shared defaults.
  - `src/Kahoot.Api/appsettings.Development.json`: Use development-friendly values. It may contain local non-sensitive defaults that simplify development (e.g., local CORS origins, local JWT signing key). Do not place real production secrets here. Keep behavior close enough to production that configuration mistakes are detectable.
  - `src/Kahoot.Api/appsettings.Production.json`: Use production-safe settings. Do not hardcode secrets such as JWT signing keys, database passwords, API keys, or credentials. Sensitive values must come from environment variables, secret stores, or the deployment platform. Prefer fail-fast validation for required production configuration instead of silent fallback values. Do not weaken security just to make startup succeed.
- **No .NET User Secrets (`secrets.json`):** Never use or initialize .NET User Secrets (`dotnet user-secrets`). Put all configuration exclusively across the three appsettings files (`appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json`). Local development configuration belongs in `appsettings.Development.json`, shared defaults belong in `appsettings.json`, and production settings belong in `appsettings.Production.json` (with production secrets supplied via environment variables / deployment platform).
- Keep PostgreSQL connection settings in `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. In `appsettings.Production.json`, keep `DefaultConnection` empty so missing production connection strings fail fast. Do not commit production passwords. In production or custom environments, `ConnectionStrings__DefaultConnection` supplies a password-bearing connection string via environment variables.
- Use the .NET Options Pattern for grouped runtime configuration when it improves type safety, validation, and maintainability:
  - Bind from the correct configuration section.
  - Validate important values with `ValidateOnStart()` where appropriate.
  - Keep validation rules consistent with the real application requirements.
  - Do not duplicate the same configuration value in multiple places.
  - Keep one source of truth for values such as token lifetimes, limits, and security settings.
  - Do not move normal implementation constants into configuration unless they genuinely need to vary by environment.
  - Verify that environment-variable overrides work correctly with ASP.NET Core configuration conventions (e.g., `Section__Key`).
- Development CORS origins are in `src/Kahoot.Api/appsettings.Development.json`. OpenAPI and Scalar are exposed only in Development. The database-aware health endpoint is `/health`.
- From `backend/`, run `dotnet build Kahoot.slnx` after code changes. Run `dotnet format Kahoot.slnx --verify-no-changes` when formatting is relevant. Report any verification that could not run.

## Testing constraints

- Do not create or add any test code, test projects, unit tests, integration tests, or end-to-end tests to the backend.
- The `backend/test/` directory must remain completely empty, containing only the `.gitkeep` file.

Before finishing, review the changed files for unintended edits, unused code, contract changes, security effects, and consistency with the existing project structure.

