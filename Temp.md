
## ASP.NET Core Application Structure

- Always follow established ASP.NET Core best practices and clean-code principles. Write
  production-ready code that is maintainable, readable, well-structured, and consistent
  with the project's .NET 10 ASP.NET Core backend.
- **Code Comments & Documentation Policy:**
  - **No inline comments:** Do not write step-by-step, redundant comments or dead code; code must be self-explanatory.
  - **XML documentation on key functions:** Write standard XML doc comments (`/// <summary>`, `<param>`, `<returns>`) only for critical functions: public API endpoints, complex domain algorithms, and core interfaces.
  - **Focus on intent:** Document the *why*, parameter constraints, and expected return/failure outcomes rather than restating method signatures.
- Use the modern `WebApplication.CreateBuilder(args)` and `WebApplication` hosting model
  unless the project intentionally targets an older ASP.NET Core version.
- Keep `Program.cs` focused on service registration and HTTP pipeline composition.
  Move cohesive registrations into clearly named `IServiceCollection` extension methods
  when `Program.cs` would otherwise become difficult to scan.
- Preserve the solution's existing project boundaries and feature organization. Keep
  ASP.NET Core transport types in the API project and MediatR requests and handlers in
  the established application project or feature slice.
- Prefer Clean Architecture and preserve clear separation of concerns and dependency
  direction between the project's established layers.
- Use design patterns only when they solve a concrete architectural or maintainability
  problem. Apply Strategy, Decorator, Factory, Singleton, or another pattern only when
  it fits the specific problem, existing architecture, dependency lifetimes, and project
  conventions. Prefer the simplest maintainable design over unnecessary abstraction or
  over-engineering.
- Keep controllers limited to ASP.NET Core concerns: model binding, authorization
  metadata, status codes, headers, and dispatching the relevant MediatR request.
- Use explicit request and response contracts at HTTP boundaries. Do not expose EF Core
  entities, `IdentityUser`, MediatR request types, or internal exception types as API
  responses.
- Use ASP.NET Core options binding and validation for configuration. Do not read
  configuration values ad hoc throughout handlers or create static configuration
  accessors.
- Avoid magic numbers and strings. Extract meaningful reusable values into clearly
  named C# constants, enums, or typed configuration. Keep environment-specific values,
  credentials, secrets, URLs, and ports out of source code and load them through the
  project's ASP.NET Core configuration patterns.

### EF Core Migrations

> **CRITICAL INSTRUCTION:** Whenever a database model or EF Core relationship changes,
> create a new migration for that change. Never modify, rename, or delete an existing
> migration; always represent later schema changes with an additional migration.

- **CRITICAL RULE**: Every time you add, modify, or remove database models, you MUST
  also update `projectSchema.dbml` to reflect the current schema.
- Every model or relationship change must include both the corresponding
  `projectSchema.dbml` update and a newly generated EF Core migration in the same task.
- Keep schema migrations, development seed data, test data, and production reference
  data as separate responsibilities. Do not combine them in the same initialization
  workflow or service without a clear architectural reason.

> **Migration immutability:** Treat every existing migration and its generated designer
> file as immutable. Do not modify a previous migration to include a later change. Update
> the models and `projectSchema.dbml`, then generate a new migration containing only the
> new schema change.

## OpenAPI and Scalar

- Generate the OpenAPI document with the ASP.NET Core OpenAPI integration used by the
  pinned target framework and expose its interactive UI with Scalar.
- Register Scalar with the package-version-compatible `Scalar.AspNetCore` APIs. Keep the
  OpenAPI document route and Scalar endpoint configuration consistent.
- Add endpoint names, summaries, descriptions, response metadata, authorization
  requirements, and documented status codes so Scalar accurately describes the API.
- Configure authentication support in Scalar without embedding tokens, credentials, or
  environment-specific secrets in source code.
- Expose Scalar according to the project's environment policy. Do not unintentionally
  publish internal or privileged API documentation in production.

## Testability and Testing Files

- Do not create new ASP.NET Core unit, integration, functional, or end-to-end test files
  or new .NET test projects unless the user explicitly requests tests to be added.
- Design all new and modified code for future testability even when automated tests are
  not part of the current task.
- Keep business logic separate from infrastructure, HTTP or UI presentation, persistence,
  and external services. Avoid unnecessary coupling and side effects.
- Use dependency injection or clear abstractions where appropriate so infrastructure and
  external dependencies can be replaced without changing the core implementation.
- Keep classes, methods, handlers, and modules focused on one responsibility. Make
  important logic callable and verifiable independently without requiring the ASP.NET
  Core host, a database, the network, or other unrelated infrastructure.
- Do not choose designs that would require major refactoring merely to add tests later.
- Continue to run and maintain any existing tests affected by a change. Do not delete,
  disable, skip, or weaken existing tests merely to satisfy this rule or make the
  solution build pass.

## ASP.NET Core Verification

- Build the affected solution with the repository-pinned .NET SDK and run its existing
  formatting, analyzer, and test commands.
- Verify ASP.NET Core service registration at startup, including Scrutor-scanned
  services, MediatR handlers and pipeline ordering, FluentValidation validators,
  Mapster configuration, EF Core provider setup, OpenAPI generation, and Scalar mapping.
- When HTTP behavior changes, exercise the affected endpoint and confirm binding,
  validation, authorization, status codes, `ProblemDetails`, and cancellation behavior.
- When EF Core behavior changes, inspect generated SQL where relevant and confirm that
  the migration, model snapshot, and database update contain only the intended changes.
- Before completion, inspect the final diff and confirm every ASP.NET Core-specific rule
  and documentation standard is maintained.
