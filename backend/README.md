# ASP.NET Core Backend

Production-ready modular monolith backend built on the latest **ASP.NET Core (.NET 10)** following Clean Architecture principles, CQRS, and real-time communication capabilities.

---

## Technology Stack

- **Target Framework:** .NET 10 (`net10.0`)
- **Web API & Hosting:** ASP.NET Core Web API with Controllers
- **Real-Time Communication:** ASP.NET Core SignalR
- **Database & ORM:** PostgreSQL with Entity Framework Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **CQRS & Mediator:** MediatR 12
- **Validation:** FluentValidation with automatic assembly scanning
- **Object Mapping:** Mapster
- **Dependency Injection Scanning:** Scrutor convention-based scanning (`ITransientService`, `IScopedService`, `ISingletonService`)
- **API Documentation:** Microsoft.AspNetCore.OpenApi + Scalar (`Scalar.AspNetCore`)
- **Health Checks:** Native `/health` endpoint

---

## Solution Structure

```text
backend/
├── Kahoot.sln
├── Dockerfile                  # Multi-stage production container configuration
├── .dockerignore
├── AGENTS.md                   # Engineering rules and architectural guidelines
└── src/
    ├── Kahoot.Domain/          # Core business entities, value objects, domain exceptions
    ├── Kahoot.Application/     # CQRS Commands, Queries, Behaviors, DTOs, Mapping, Validators
    ├── Kahoot.Infrastructure/  # EF Core DbContext, PostgreSQL configs, Scrutor conventions
    └── Kahoot.Api/             # Controllers, SignalR Hubs, Middleware, Scalar OpenAPI setup
```

---

## Commands

### Local Development

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API locally
dotnet run --project src/Kahoot.Api
```

Interactive API documentation with Scalar will be accessible at:
- `http://localhost:5000/scalar/v1` (or HTTPS equivalent)
- Health check: `http://localhost:5000/health`

### Docker Build & Run

```bash
# Build Docker image
docker build -t kahoot-backend -f Dockerfile .

# Run Docker container on port 8080
docker run -d -p 8080:8080 --name kahoot-api kahoot-backend
```
