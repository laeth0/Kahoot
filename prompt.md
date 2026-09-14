Use this prompt with Claude Code. I structured it as a production implementation task, including Azure deployment, Docker, Grafana dashboards, alerting, and failure detection.

```md
# Implement Production-Grade OpenTelemetry Observability Platform

You are a senior Site Reliability Engineer (SRE) and Backend Architect.

I have a production application deployed on Azure using:

- Azure Linux Virtual Machine
- Docker Compose
- Nginx reverse proxy
- Backend API
- Frontend application
- PostgreSQL database

Production environment:
- Application URL: http://20.19.48.78/
- Deployment model: Docker containers running on Azure VM

I want you to implement a complete OpenTelemetry-based observability platform.

The main goal is:

> If anything fails in the system, becomes slow, produces errors, or has abnormal behavior, I should be able to detect it quickly through dashboards and alerts.

The observability stack must use:

- OpenTelemetry
- Grafana
- Prometheus
- Loki
- Jaeger

---

# Objectives

Implement:

1. Distributed tracing
2. Metrics collection
3. Centralized logging
4. Correlation between logs, metrics, and traces
5. Production dashboards
6. Failure detection and alerting

The final system should allow me to answer:

- Why is the application slow?
- Which API endpoint is failing?
- Which service caused the failure?
- Are database queries causing latency?
- How many errors happened?
- When did the failure start?
- What is the exact trace causing the issue?

---

# Step 1 — Analyze Existing Project

Before modifying anything:

Analyze:

- Backend architecture
- Frontend architecture
- Docker Compose setup
- Nginx configuration
- Database configuration
- Existing logging approach
- Existing error handling
- API communication flow

Identify:

- Where telemetry should be added.
- Important business flows to trace.
- Critical failure points.

Do not blindly install packages.

---

# Step 2 — Implement OpenTelemetry SDK

## Backend

Add OpenTelemetry instrumentation for:

### HTTP

Track:

- Incoming requests
- Response status codes
- Request duration
- Endpoint latency
- Exceptions

Include:

- Trace ID
- Span ID
- Request correlation


### Database

Instrument PostgreSQL:

Track:

- Query duration
- Failed queries
- Connection failures
- Database latency


### Application

Create custom spans for important business operations.

Example:

Game system:

```

Create Game
|
Generate PIN
|
Save Game
|
Notify Players
|
Start Session

```

Each important step should have trace visibility.

---

# Step 3 — Logging Implementation

Implement structured logging.

Requirements:

Logs must include:

```

timestamp
service_name
environment
trace_id
span_id
level
message
exception

````

Example:

```json
{
 "level":"ERROR",
 "service":"backend",
 "trace_id":"abc123",
 "span_id":"xyz456",
 "message":"Failed creating game",
 "exception":"Database timeout"
}
````

Send logs to:

Loki

---

# Step 4 — Metrics Implementation

Expose Prometheus metrics.

Required metrics:

## Application Metrics

* HTTP request count
* HTTP error count
* Request duration
* Active requests
* Exceptions count

## Business Metrics

Create application-specific metrics.

Examples:

* Games created
* Active games
* Players joined
* Questions answered
* Failed game sessions

## Database Metrics

Monitor:

* Connection count
* Query duration
* Errors
* Pool usage

## System Metrics

Monitor:

* CPU
* Memory
* Disk
* Network

---

# Step 5 — Observability Architecture

Build this architecture:

```
Application Containers
        |
        |
 OpenTelemetry SDK
        |
        |
 OpenTelemetry Collector
        |
        |
 +-------------+-------------+
 |             |             |
Prometheus    Loki        Jaeger
Metrics       Logs        Traces
 |
 |
Grafana
Dashboards
Alerts
```

Create the required Docker Compose services:

* otel-collector
* prometheus
* loki
* jaeger
* grafana

Integrate them with the existing deployment.

---

# Step 6 — Azure Deployment Considerations

The application is running on Azure VM.

Make sure:

* Grafana is accessible from Azure.
* Ports are configured correctly.
* Security is considered.

Required access:

Application:

```
80
443
```

Grafana:

```
3000
```

Jaeger UI:

```
16686
```

Prometheus:

```
9090
```

If exposing dashboards publicly:

Explain the security risks.

Prefer:

* Restricting access by IP
* Authentication
* Reverse proxy through Nginx if needed

---

# Step 7 — Grafana Dashboards

Create production dashboards.

## Dashboard 1: Application Overview

Panels:

* Request rate
* Error rate
* Average latency
* P95 latency
* P99 latency
* Active users
* Failed requests

## Dashboard 2: API Performance

Panels:

* Slowest endpoints
* Error endpoints
* Response time distribution
* HTTP status codes

## Dashboard 3: Infrastructure

Panels:

* CPU usage
* Memory usage
* Disk usage
* Network traffic
* Container health

## Dashboard 4: Database

Panels:

* Query latency
* Failed queries
* Connections
* PostgreSQL health

## Dashboard 5: Business Metrics

Panels:

* Games created
* Players joined
* Active sessions
* Failed operations

---

# Step 8 — Alerting

Create Grafana/Prometheus alerts.

Critical alerts:

## Application Down

Trigger:

```
Service unavailable for > 1 minute
```

## High Error Rate

Example:

```
HTTP 5xx > 5% for 5 minutes
```

## High Latency

Example:

```
P95 latency > 2 seconds
```

## Database Problems

Examples:

* Connection failures
* Slow queries
* Database unavailable

## Infrastructure Problems

Examples:

* CPU > 85%
* Memory > 90%
* Disk > 85%

---

# Step 9 — Trace Correlation

Ensure:

From Grafana:

Metric
↓
Related logs
↓
Trace

Example:

A latency spike should allow:

Grafana metric
→ Loki logs
→ Jaeger trace

---

# Step 10 — Production Validation

After implementation:

Run:

```
docker compose up -d
```

Verify:

All containers healthy:

```
docker ps
```

Verify:

Grafana:

```
http://<azure-ip>:3000
```

Prometheus:

```
http://<azure-ip>:9090
```

Jaeger:

```
http://<azure-ip>:16686
```

Test failures:

Create controlled failures:

* API exception
* Database failure
* Slow endpoint

Confirm:

* Logs appear in Loki
* Metrics increase in Prometheus
* Trace appears in Jaeger
* Grafana dashboard shows the issue

---

# Deliverables

Provide:

1. Modified source code.
2. Docker Compose changes.
3. OpenTelemetry configuration.
4. Grafana dashboards as JSON files.
5. Prometheus configuration.
6. Loki configuration.
7. Jaeger configuration.
8. Alert rules.
9. Documentation:

Create:

```
docs/observability/
    architecture.md
    setup.md
    dashboards.md
    alerts.md
    troubleshooting.md
```

Include Mermaid diagrams for:

* Observability architecture
* Telemetry data flow
* Request tracing flow

---

# Important Constraints

* Do not break existing application functionality.
* Do not add unnecessary dependencies.
* Follow production best practices.
* Keep secrets in environment variables.
* Do not expose sensitive logs.
* Make the solution maintainable for future scaling.
* Explain every architectural decision and tradeoff.

```

This prompt should push Claude Code to build a real observability stack instead of just adding a few logging libraries. It also matches your current Azure + Docker deployment model.
```
