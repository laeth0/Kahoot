# Architecture and Deployment

## Purpose

Constrain the application structure and define production deployment qualities
without prescribing unnecessary distributed components.

## NFR-8: Architecture

- The system is a modular monolith; it must not be split into microservices
  without an independently approved requirement.
- Clean Architecture boundaries are:
  - `Kahoot.Domain` for entities and enums;
  - `Kahoot.Application` for use cases, state transitions, validation, and scoring;
  - `Kahoot.Infrastructure` for EF Core, persistence, startup services, storage,
    and security implementations; and
  - `Kahoot.Api` for controllers, SignalR hubs, health endpoints, and the HTTP
    pipeline.
- Controllers and SignalR hubs must remain thin. Business rules belong in the
  application layer.
- Global mutable process memory must not be the sole source of truth for state
  that must survive restart or scaling.
- I/O must be asynchronous and non-blocking, cancellation and timeouts must be
  propagated, and retries must be bounded and safe for the operation.

## NFR-9: Deployment

- Production runs on the documented Azure Linux VM using Docker Compose and an
  Nginx reverse proxy.
- The current production sizing is a Standard D2s v3 VM with 2 vCPUs and 8 GB of
  RAM. Container resource settings must be treated as limits competing within
  that host, not as additional physical capacity.
- PostgreSQL and uploaded media must use durable named volumes in the single-VM
  topology and must be backed up according to the operational guide.
- Environment-specific settings and secrets must be supplied through the
  uncommitted `.env` file and synchronized committed templates; no production
  secret may be embedded in an image or source file.
- Frontend REST and SignalR addresses must be environment-configured and must not
  use hard-coded development URLs in production.
- The reverse proxy must support WebSocket upgrades, preserve the required
  forwarded headers, enforce request limits, and expose only intended public
  routes.
- The deployment must provide `/health/live`, `/health/ready`, and the compatible
  `/health` endpoint. Orchestration should use readiness when deciding whether to
  send traffic.
- Startup must fail when required configuration, migration, PostgreSQL, or enabled
  host-seeding requirements cannot be satisfied.
- Public production traffic must migrate to HTTPS when a domain and certificate
  are available. The current documented IP-only HTTP deployment does not satisfy
  the final transport-security target.
- All required environment variables and operating procedures must remain
  documented in `docs/azure-vm-deployment.md`.

## Operational Requirements

- Containers must run with the repository's security restrictions, bounded
  resources, restart policies, health checks, and rotated logs.
- Deployment and rollback procedures must preserve the PostgreSQL and uploads
  volumes.
- Scaling beyond one application replica requires shared SignalR delivery and
  shared media storage before traffic is distributed across replicas.
