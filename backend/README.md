# Backend setup

Configure PostgreSQL through `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. Keep passwords out of committed files. If the database requires password authentication, supply the full connection string through the `ConnectionStrings__DefaultConnection` environment variable for that environment.

The API requires this connection string at startup and applies pending EF Core migrations during startup. Migration failures stop the application.

JWT issuer, audience, and token lifetimes are configured in `src/Kahoot.Api/appsettings.json`. Set `Jwt__SigningKey` to a 64-character hexadecimal key in the process environment before starting the API. The signing key is intentionally absent from committed configuration. The JWT service issues access tokens and one-time refresh tokens; refresh tokens are stored as hashes and can be rotated or revoked. Authentication endpoints are not included yet.

Development CORS allows `http://localhost:5173` and `http://127.0.0.1:5173`. Configure `Cors:AllowedOrigins` for other environments; the default production list is empty.
