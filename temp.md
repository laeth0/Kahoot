
The observability architecture and resource design have been reviewed and approved.

Your task now is to create a detailed implementation plan that will be given to Gemini for implementation.

Do not implement the feature yourself.
Create a clear, execution-ready implementation plan.

The plan must include:

# 1. Implementation Goals

Implement a complete production observability platform for the existing Azure deployment:

Environment:
- Azure Linux VM
- Docker Compose deployment
- Nginx reverse proxy
- ASP.NET Core backend
- Frontend application
- PostgreSQL database
- ~200 concurrent users

Target access:

http://20.19.48.78/grafana/

After implementation:
- Grafana should open with username/password authentication.
- Dashboards should already exist.
- Datasources should already be configured.
- Alerts should already be created.
- No manual Grafana configuration should be required.

---

# 2. Final Architecture

Implement this architecture:

```

ASP.NET Core Application
|
| OTLP
v
OpenTelemetry Collector
|
+---- Metrics ----> Prometheus
|
+---- Logs -------> Loki
|
+---- Traces -----> Jaeger

Node Exporter ---------> Prometheus
cAdvisor -------------> Prometheus
PostgreSQL Exporter --> Prometheus
Blackbox Exporter ---> Prometheus

Internet
|
| HTTP/HTTPS
v
Nginx
|
| /grafana/
v
Grafana

```

Requirements:

- Two Docker networks:
  1. Application network
  2. Internal observability network

- Nginx is the only service allowed to expose public ports.

Do not expose publicly:
- Prometheus
- Loki
- Jaeger
- OpenTelemetry Collector
- Exporters

---

# 3. Resource Constraints

The implementation must respect the Azure VM limitations:

VM:

- Standard D2s v3
- 2 vCPU
- 8 GB RAM

Required resource limits:

Grafana:
- CPU: 0.25
- Memory: 384 MB

Prometheus:
- CPU: 0.50
- Memory: 1 GB
- Retention:
  - 7 days
  - Maximum 4GB storage

Loki:
- CPU: 0.25
- Memory: 512 MB
- Retention:
  - 7 days
- Configure ingestion limits.

Jaeger:
- CPU: 0.25
- Memory: 512 MB
- Retention:
  - 3 days
- Protect storage from uncontrolled growth.

OpenTelemetry Collector:
- CPU: 0.25
- Memory: 384 MB
- Memory limiter:
  - 256 MB

cAdvisor:
- CPU: 0.15
- Memory: 256 MB

Node Exporter:
- CPU: 0.10
- Memory: 128 MB

PostgreSQL Exporter:
- CPU: 0.10
- Memory: 128 MB

Blackbox Exporter:
- CPU: 0.10
- Memory: 128 MB


Also add explicit resource limits/reservations for application containers so observability cannot impact the main application.

Application priority:

1. Application
2. Database
3. Observability services

---

# 4. OpenTelemetry Implementation Plan

Explain:

## Backend instrumentation

Implement:

- HTTP request tracing
- Exception tracing
- Database tracing
- PostgreSQL/Npgsql telemetry
- Custom business spans

Important business flows:

- Create game
- Join game
- Start game
- Submit answer
- Advance question
- End game

---

## Logging

Implement structured OTLP logs.

Required fields:

- timestamp
- service name
- environment
- severity
- message
- trace_id
- span_id

Never log:

- JWT tokens
- passwords
- secrets
- SQL parameters
- request bodies
- user sensitive data

---

## Metrics

Create:

Application metrics:

- HTTP requests
- Error count
- Latency
- Active requests

Business metrics:

- Games created
- Active games
- Connected players
- Questions served
- Answers submitted
- Answer latency

Realtime metrics:

- WebSocket/SignalR connections
- Disconnect rate
- Reconnect count
- Event throughput

Database metrics:

- Connection count
- Query latency
- Errors

Infrastructure metrics:

- CPU
- Memory
- Disk
- Network
- Container health

---

# 5. Sampling Strategy

Implement tail-based sampling:

Keep:

100%:
- Errors
- Failed requests
- Slow requests > 1 second

10%:
- Normal successful requests

Explain where this configuration is implemented.

---

# 6. Grafana Automatic Provisioning

The implementation must NOT require manual dashboard creation.

Create provisioning files:

```

grafana/
├── provisioning/
│    ├── datasources/
│    ├── dashboards/
│    └── alerting/
│
└── dashboards/
├── application.json
├── infrastructure.json
├── database.json
├── logs.json
├── tracing.json
└── realtime.json

```

Automatically configure:

Datasources:

- Prometheus
- Loki
- Jaeger

---

# 7. Required Dashboards

Create dashboards:

## Application Dashboard

Include:

- Request rate
- Error rate
- P50/P95/P99 latency
- HTTP status codes
- Failed endpoints
- Slow endpoints


## Infrastructure Dashboard

Include:

- CPU
- Memory
- Disk
- Network
- Docker containers


## Database Dashboard

Include:

- Connections
- Query latency
- Slow queries
- Errors


## Logs Dashboard

Include:

- Error logs
- Exceptions
- Trace correlation


## Tracing Dashboard

Include:

- Slow traces
- Failed traces
- Service dependencies


## Realtime/Game Dashboard

Include:

- Active games
- Active players
- WebSocket connections
- Connection failures
- Reconnect rate
- Events per second
- Answer latency

---

# 8. Alert Definitions

Create Grafana-managed alerts.

Critical:

- Application unavailable
- Backend container down
- Database unavailable
- Disk usage > 90%
- Memory > 90%
- Realtime connections drop unexpectedly

Warning:

- HTTP 5xx > threshold
- P95 latency too high
- CPU > threshold
- Slow database queries
- High error rate

Business alerts:

- No game events received for a period
- WebSocket failures
- Player connection degradation

---

# 9. Deployment Changes

The plan must specify changes to:

- docker-compose.yml
- nginx configuration
- environment files
- Grafana configuration
- Prometheus configuration
- Loki configuration
- Jaeger configuration
- OpenTelemetry Collector configuration

Include:

- New files
- Modified files
- Required environment variables

---

# 10. Validation Plan

After implementation Gemini must verify:

Containers:

```

docker compose up -d
docker ps

```

Health:

- All containers running
- No restart loops

Grafana:

Open:

http://20.19.48.78/grafana/

Verify:

- Login works
- Dashboards exist
- Datasources connected
- Alerts loaded


Generate test failures:

- Backend exception
- Database failure
- Slow request
- Container restart

Verify:

- Logs appear in Loki
- Metrics appear in Prometheus
- Traces appear in Jaeger
- Alerts trigger correctly

---

# 11. Final Output Format

Return only the implementation plan.

Structure:

1. Overview
2. Architecture
3. File changes
4. Implementation phases
5. Configuration changes
6. Dashboard implementation
7. Alert implementation
8. Deployment steps
9. Validation checklist
10. Risks and rollback plan

The plan must be detailed enough that Gemini can execute it directly without additional architectural decisions.
```
