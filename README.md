# Kahoot Clone

A real-time multiplayer quiz platform built with ASP.NET Core, PostgreSQL, and SignalR WebSockets.

---

## Quick Start

```sh
# Deploy locally (uses .env):
docker compose up -d

# Or specify an environment file:
docker compose --env-file .env.development up -d
docker compose --env-file .env.production up -d
```

---

## Service & Telemetry URLs

| Service | Local URL | Notes |
| :--- | :--- | :--- |
| **Backend API** | [http://localhost:8080](http://localhost:8080) | REST API root |
| **Health Check** | [http://localhost:8080/health](http://localhost:8080/health) | Database-aware readiness probe |
| **Grafana** | [http://localhost:3000](http://localhost:3000) | User: `admin` \| Password: `GRAFANA_ADMIN_PASSWORD` in `.env` |
| **Jaeger UI** | [http://localhost:16686](http://localhost:16686) | Distributed tracing & query graphs |
| **Prometheus** | [http://localhost:9090](http://localhost:9090) | Metrics exploration & PromQL console |
| **OTel Collector** | `http://localhost:4318` (HTTP) / `:4317` (gRPC) | OTLP ingestion gateway |
| **PostgreSQL** | `localhost:5433` | Database port on host (container: `5432`) |
