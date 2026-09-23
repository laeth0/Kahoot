# Backend Development Rules

## Architecture
- Use Clean Architecture for project boundaries and Vertical Slice Architecture for application features.
- Keep domain entities in Domain, use cases in Application, persistence in Infrastructure, and HTTP concerns in Api.

## Database Configuration and Migrations
- Keep PostgreSQL connection settings in `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. Do not use .NET user secrets or commit passwords.
- Supply `Jwt__SigningKey` at runtime. Never commit JWT signing keys.
- Apply pending EF Core migrations through an `IHostedService` outside `Program.cs`. Use the startup `CancellationToken` and let migration failures stop application startup.

## HTTP Errors and Middleware
- Return ASP.NET Core `ProblemDetails` for errors. Route unexpected request exceptions through the global exception handler, log useful context, and keep stack traces and sensitive details out of HTTP responses.
- Do not swallow exceptions or use empty `catch` blocks.
- Keep middleware in the required order when configured: exception handling, forwarded headers, HTTPS, CORS, authentication, authorization, rate limiting, then endpoint mapping.

## Testing Policy
- **No Testing Code**: Do not create or add any test code, test projects, unit tests, or integration tests to the backend.
- **Empty Test Directory**: The `backend/test/` directory must remain completely empty, containing only the `.gitkeep` file.
