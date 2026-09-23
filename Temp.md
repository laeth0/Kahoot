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

## Testability and Testing Files

- Do not create new ASP.NET Core unit, integration, functional, or end-to-end test files or new .NET test projects unless the user explicitly requests tests to be added.
