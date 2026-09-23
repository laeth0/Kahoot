# Backend setup

## Run locally with Docker Compose

From the repository root, copy `.env.example` to `.env` and set `POSTGRES_PASSWORD`. Use a unique PostgreSQL password without connection-string delimiters (`;` or `=`). For example, in PowerShell:

```powershell
Copy-Item .env.example .env
```

Set `POSTGRES_PASSWORD` in `.env`, then run:

```sh
docker compose up --build -d
docker compose ps
```

The API is available at `http://localhost:8080` and its database-aware health check at `http://localhost:8080/health`. PostgreSQL is available to local tools on `localhost:5432`. Set `API_PORT` or `POSTGRES_PORT` in `.env` if those host ports are occupied. Compose waits for PostgreSQL to become healthy before starting the API; the API applies pending migrations during startup. The named `postgres_data` volume persists across `docker compose down`. Do not use `docker compose down -v` unless you intend to delete local database data.

The `docker-compose.yml` file is at the repository root so a future frontend service can join the `web` network. PostgreSQL remains on the separate `data` network. Compose currently runs only the API and PostgreSQL. The `.env` file is ignored by Git and stays on your machine.

To run the API on the host while keeping PostgreSQL in Compose, start `docker compose up -d db`, provide a password-bearing `ConnectionStrings__DefaultConnection` through your process environment, then run the API with `dotnet run`. The values in committed appsettings remain defaults for local development.

## Configuration

Configure PostgreSQL through `src/Kahoot.Api/appsettings.json` and `src/Kahoot.Api/appsettings.Development.json`. Keep passwords out of committed files. If the database requires password authentication, supply the full connection string through the `ConnectionStrings__DefaultConnection` environment variable for that environment.

The API requires this connection string at startup and applies pending EF Core migrations during startup. Migration failures stop the application.

Development CORS allows `http://localhost:5173` and `http://127.0.0.1:5173`. Configure `Cors:AllowedOrigins` for other environments; the default production list is empty.

## Commit formatting

Enable the shared pre-commit hook once per clone from the repository root:

```sh
git config --local core.hooksPath .githooks
```

The hook runs `dotnet format` on `backend/Kahoot.slnx`. If formatting changes files, the commit stops so you can review and stage the formatted files before retrying.
