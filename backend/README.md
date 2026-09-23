# Backend setup

Configure PostgreSQL through `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. Keep passwords out of committed files. If the database requires password authentication, supply the full connection string through the `ConnectionStrings__DefaultConnection` environment variable for that environment.

The API requires this connection string at startup and applies pending EF Core migrations during startup. Migration failures stop the application.
