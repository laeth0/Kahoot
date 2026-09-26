Act as a senior .NET / ASP.NET Core engineer with strong experience in OpenTelemetry, structured logging, distributed tracing, metrics, Prometheus, Jaeger, Loki, Grafana, PostgreSQL/Npgsql, Docker, Clean Architecture, Vertical Slice Architecture, and production observability.

I want you to **inspect the existing Kahoot repository and implement a production-quality observability architecture** using:

```text
OpenTelemetry
OpenTelemetry Collector
Prometheus
Jaeger
Loki
Grafana
```

The main signals are:

```text
Metrics -> Prometheus
Traces  -> Jaeger
Logs    -> Loki
```

Grafana should provide the unified operational UI where practical.

The ASP.NET Core application itself must remain vendor-neutral and communicate with the OpenTelemetry Collector using OTLP.

Do not treat this as a greenfield example. Read and respect the actual repository, requirements, architecture, Docker topology, configuration conventions, and existing code before modifying anything.

---

# 1. Important project decision

I have explicitly changed the previous project decision that prohibited centralized observability.

The following technologies are now APPROVED:

```text
OpenTelemetry
OpenTelemetry Collector
Prometheus
Jaeger
Loki
Grafana
```

Some existing documentation currently says things such as:

```text
"No Centralized Observability Stack"
"OpenTelemetry is excluded"
"Prometheus is excluded"
"Grafana is excluded"
"Jaeger is excluded"
"Loki is excluded"
```

Those requirements are now outdated.

Before modifying application code:

1. Search the entire repository documentation for observability requirements that conflict with this decision.
2. Update the relevant requirements/documentation.
3. Preserve all unrelated requirements and SLOs.
4. Do not silently leave contradictory documentation behind.
5. Clearly document the new observability architecture.

The new direction is:

```text
Application
     |
     | OTLP
     v
OpenTelemetry Collector
     |
     +---- Metrics ----> Prometheus
     |
     +---- Traces -----> Jaeger
     |
     +---- Logs -------> Loki

Prometheus ----+
Loki ----------+----> Grafana
Jaeger --------+
```

The Collector is the telemetry gateway.

The application must NOT directly depend on Prometheus, Jaeger, Loki, or Grafana SDKs.

---

# 2. Understand the repository before changing it

Before implementing anything, inspect at minimum:

```text
AGENTS.md
backend/AGENTS.md

docs/

docker-compose.yml

.env.example
.env.development
.env.production

backend/Kahoot.slnx

src/Kahoot.Api/Kahoot.Api.csproj
src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj

Program.cs

appsettings.json
appsettings.Development.json
appsettings.Production.json

GlobalExceptionHandler

DatabaseMigrationService
RefreshTokenCleanupWorker
DatabaseSeeder
SystemAdminSeeder

PersistenceInstaller
SecurityInstaller
DependencyInjection

authentication implementation
health checks
existing ILogger usage
```

Search the entire repository for:

```text
ILogger
LogInformation
LogWarning
LogError
LogCritical
LogDebug
LoggerMessage
BeginScope
EventName
EventId
TraceIdentifier
Activity
ActivitySource
Meter
OpenTelemetry
Prometheus
Grafana
Jaeger
Loki
observability
telemetry
```

Understand existing conventions before designing the implementation.

Do not perform unrelated architecture changes or refactoring.

---

# 3. Preserve Clean Architecture

The backend currently consists of:

```text
Kahoot.Domain
Kahoot.Application
Kahoot.Infrastructure
Kahoot.Api
```

using Clean Architecture + Vertical Slice Architecture.

Preserve the current dependency direction.

Observability SDK configuration belongs primarily in the **API composition root**, because it configures the running process.

Do not put OpenTelemetry SDK dependencies in Domain.

Do not make business rules depend on observability infrastructure.

Do NOT introduce abstractions such as:

```text
ILoggingService
ILoggerService
ITelemetryService
IObservabilityService
IMetricsService
ITracingService
```

unless a concrete requirement genuinely requires them.

Prefer the existing platform abstractions:

```csharp
ILogger<T>
ActivitySource
Meter
```

Application and Infrastructure code should continue using `ILogger<T>`.

Do not inject:

```text
TracerProvider
MeterProvider
LoggerProvider
OTLP exporters
Collector clients
Prometheus clients
Jaeger clients
Loki clients
Grafana clients
```

into normal handlers or domain services.

---

# 4. Observability architecture

Use this architecture unless repository inspection reveals a concrete incompatibility:

```text
                         +------------------+
                         | ASP.NET Core API |
                         +--------+---------+
                                  |
                        OTLP logs/traces/metrics
                                  |
                                  v
                    +---------------------------+
                    | OpenTelemetry Collector   |
                    +------------+--------------+
                                 |
             +-------------------+-------------------+
             |                   |                   |
             v                   v                   v
        Prometheus            Jaeger               Loki
         Metrics              Traces               Logs
             |                   |                   |
             +-------------------+-------------------+
                                 |
                                 v
                              Grafana
```

The application must send telemetry only to the Collector.

This gives us a vendor-neutral application boundary.

Do not configure application code like:

```text
API -> Loki
API -> Jaeger
API -> Prometheus
```

unless there is an exceptional technical reason.

---

# 5. Package selection

Use only stable packages compatible with the repository's actual `.NET 10` target.

Before installing packages:

1. inspect existing package versions,
2. check current official documentation,
3. verify latest stable compatible versions,
4. avoid Preview / RC packages,
5. avoid redundant instrumentation packages.

Likely application packages include the appropriate stable versions of:

```text
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Exporter.OpenTelemetryProtocol

OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.Runtime

Npgsql.OpenTelemetry
```

Add other packages only when they solve a concrete requirement.

Do NOT add:

```text
Serilog
NLog
Application Insights
Sentry
Aspire
prometheus-net
Jaeger-specific .NET exporter
Loki-specific .NET logging provider
Grafana-specific .NET SDK
```

unless inspection demonstrates a real need.

OpenTelemetry OTLP should be the integration boundary.

In particular:

- do not use an old/deprecated Jaeger exporter from the application;
- export traces using OTLP;
- do not use an old Loki-specific Collector exporter when Loki's native OTLP endpoint is available.

---

# 6. Focused observability registration

Follow the repository convention that service-registration extensions have a focused responsibility.

Create something such as:

```csharp
AddObservability(...)
```

in the appropriate API registration location if that fits the current structure.

`Program.cs` should remain understandable.

Prefer composition such as:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, builder.Environment);
```

Do not put hundreds of lines of telemetry configuration directly into `Program.cs`.

Do not introduce reflection-based service registration.

---

# 7. OpenTelemetry Resource configuration

Configure consistent resource identity for logs, metrics, and traces.

At minimum consider:

```text
service.name
service.version
service.instance.id
service.namespace
deployment.environment.name
```

Use actual application/environment information such as:

```text
application name
assembly version
environment name
container/process instance
```

Do NOT use request-specific values as Resource attributes.

Never use:

```text
UserId
AccountId
GameId
ParticipantId
username
nickname
IP address
TraceId
```

as resource identity.

Support standard OpenTelemetry environment variables where appropriate:

```text
OTEL_SERVICE_NAME
OTEL_RESOURCE_ATTRIBUTES

OTEL_EXPORTER_OTLP_ENDPOINT
OTEL_EXPORTER_OTLP_PROTOCOL

OTEL_TRACES_SAMPLER
OTEL_TRACES_SAMPLER_ARG
```

Avoid custom configuration when a standard OpenTelemetry configuration mechanism solves the requirement.

---

# 8. Tracing

Configure distributed tracing using OpenTelemetry.

At minimum investigate and instrument:

```text
ASP.NET Core inbound requests
HttpClient outbound requests
Npgsql/PostgreSQL database operations
```

Use official/current instrumentation compatible with the installed packages.

Do not create spans manually for functionality already represented adequately by automatic instrumentation.

Use custom `ActivitySource` only for important application workflows that cannot be understood from automatic spans.

Examples that might eventually justify custom spans:

```text
game creation snapshot transaction
game state transition
account suspension finalization
large cleanup/finalizer pass
```

But do not add custom spans merely because tracing exists.

---

# 9. Jaeger architecture

Use **Jaeger as the trace backend**.

The application must NOT send directly to Jaeger.

Use:

```text
ASP.NET Core
    -> OTLP
OpenTelemetry Collector
    -> OTLP
Jaeger
```

Use Jaeger's native OTLP support.

Do not install a Jaeger-specific exporter in the .NET application.

Use current supported Jaeger architecture and image versions.

Do NOT use `latest`.

Pin an explicit stable image version.

For local Docker development, an all-in-one/single-node Jaeger deployment is acceptable.

However:

- clearly document if local trace storage is ephemeral;
- do not describe in-memory Jaeger storage as production durable storage;
- document what durable storage would be required for a real production environment;
- do not add Elasticsearch/OpenSearch/Cassandra merely for local development unless the project genuinely needs persistent production-like trace storage now.

Expose the Jaeger UI for development using the normal Jaeger query/UI port.

Only expose ports to the host that developers actually need.

Collector-to-Jaeger communication should remain inside the Docker network.

---

# 10. Trace sampling

Do not hardcode an inflexible production sampling strategy.

Development may use:

```text
AlwaysOn
```

Production should support configurable parent-based trace ID ratio sampling, using standard OpenTelemetry configuration where possible:

```text
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=<ratio>
```

Do not independently sample child spans in a way that breaks distributed trace consistency.

Errors and unusual operations are operationally important, but do not implement complicated tail-based sampling unless there is a concrete need.

If tail sampling is considered, explain:

- the problem it solves,
- why head sampling is insufficient,
- Collector memory implications,
- deployment implications.

Do not introduce it automatically.

---

# 11. Health-check tracing

Prevent health probes from polluting normal traces.

Consider filtering:

```text
/health
/health/live
/health/ready
```

from ASP.NET Core tracing.

Do so only at the tracing instrumentation layer.

Do not disable the actual health endpoints.

Do not filter real user/API traffic.

---

# 12. Metrics architecture

Use **Prometheus as the metrics backend**.

Prefer the conventional Prometheus pull model.

The preferred topology is:

```text
Application
     |
     | OTLP metrics
     v
OpenTelemetry Collector
     |
     | exposes Prometheus-compatible metrics endpoint
     v
Prometheus scrapes Collector
```

This keeps:

```text
application -> Collector
```

as the single application telemetry boundary while preserving normal Prometheus scraping semantics.

Do not add a separate Prometheus endpoint directly to the API unless there is a concrete technical reason.

Do not enable Prometheus remote-write receiving merely because it is convenient.

Do not push metrics directly from the application to Prometheus.

If current official OpenTelemetry/Prometheus guidance or the pinned Collector version makes a different topology materially better, document the evidence and reason before changing this design.

---

# 13. Prometheus instrumentation

Collect useful metrics including, where supported:

```text
ASP.NET Core request duration
ASP.NET Core request counts
HTTP client metrics
.NET runtime metrics
GC metrics
thread-pool metrics
process/runtime health
Npgsql connection-pool metrics
database client metrics
```

Inspect the current package versions and actual meter names.

Do not guess Npgsql meter names.

Do not duplicate metrics already emitted by modern .NET/runtime instrumentation.

Do not create custom counters for data already available from framework instrumentation.

---

# 14. Custom metrics

Add custom metrics only when they answer a meaningful operational question.

Potential candidates include:

```text
authentication rate-limit activations
refresh-token malicious-reuse detections
refresh-token race conflicts
refresh-token cleanup rows deleted
cleanup pass failures
game lifecycle transition counts
```

Do NOT implement all of these automatically.

Start with framework/runtime/database metrics.

Then introduce only the custom metrics with a concrete operational use case.

Every new metric must have:

```text
purpose
instrument type
unit
description
allowed labels
cardinality analysis
```

---

# 15. Metric cardinality

Treat metric cardinality as a critical production concern.

NEVER use these as metric labels:

```text
UserId
AccountId
GameId
ParticipantId
username
nickname
IP address
JWT ID
refresh-token ID
refresh-token family ID
TraceId
SpanId
exception message
raw URL
raw route ID
question ID
choice ID
```

Use bounded dimensions such as:

```text
operation=login|refresh|logout
outcome=success|failure|rate_limited
role=host|system_admin
worker=refresh_token_cleanup
```

only if those dimensions are genuinely useful.

Use route templates rather than concrete URLs.

---

# 16. Prometheus storage

Configure Prometheus with an explicit persistent Docker volume for local/single-host deployments.

Set a sensible retention policy.

Do not allow unlimited metric storage growth.

Use current stable Prometheus.

Pin the Docker image version.

Do not use `latest`.

Prometheus's web UI may be exposed locally for debugging.

Do not expose it publicly as part of the application's normal API surface.

---

# 17. Logging architecture

Keep:

```csharp
ILogger<T>
```

throughout the .NET application.

OpenTelemetry should integrate with the existing `Microsoft.Extensions.Logging` pipeline.

Do NOT replace application logging with an OpenTelemetry-specific abstraction.

Do NOT create a second `LoggerFactory`.

Do NOT call:

```csharp
builder.Logging.ClearProviders();
```

solely to install OpenTelemetry.

The desired architecture is:

```text
                         +--> JSON Console -> stdout/stderr
                         |
ILogger<T> --------------+
                         |
                         +--> OpenTelemetry Logs -> OTLP Collector -> Loki
```

Console logs remain available even if the Collector or Loki is unavailable.

---

# 18. Structured console logging

Keep structured JSON logs on stdout/stderr for container diagnostics.

Use the built-in .NET JSON console formatter unless repository inspection identifies a concrete deficiency.

Production logs should contain useful structured fields such as:

```text
timestamp
level
category
event id
event name
message
exception
structured state
scopes
trace id
span id
request id where appropriate
```

Do not duplicate console providers.

Development may use a more readable console format if the repository deliberately prefers it.

Production should use machine-readable structured JSON.

---

# 19. Loki architecture

Use **Loki as the log backend**.

Use this topology:

```text
ILogger<T>
     |
OpenTelemetry Logging Provider
     |
     | OTLP
     v
OpenTelemetry Collector
     |
     | OTLP/HTTP
     v
Loki native OTLP endpoint
```

Do NOT add a Loki-specific .NET logging provider.

Do NOT have the application call Loki APIs.

Do NOT use a deprecated Loki Collector exporter if native OTLP ingestion is supported by the selected Loki version.

Use the Collector's standard OTLP HTTP exporter to Loki's native OTLP endpoint.

---

# 20. Loki labels and cardinality

Be extremely careful with Loki indexed labels.

Do NOT turn every OpenTelemetry attribute into a Loki index label.

Use only a small bounded set of useful labels.

Reasonable candidates might include:

```text
service.name
service.namespace
deployment.environment.name
severity
```

depending on the selected Loki version and current OTLP mapping guidance.

Avoid indexing high-cardinality values such as:

```text
service.instance.id
TraceId
SpanId
RequestId
AccountId
GameId
ParticipantId
username
IP address
exception message
HTTP URL
```

Keep those values as structured metadata/log attributes where useful rather than indexed labels.

Inspect the current Loki OTLP defaults carefully.

Override defaults where needed to prevent excessive cardinality.

---

# 21. Loki storage

For the Docker Compose development/single-host environment:

- use Loki single-binary mode unless there is a concrete reason for a distributed topology;
- use a persistent volume;
- configure sensible retention;
- use filesystem/local storage appropriate for development or single-host operation.

Do not introduce S3/MinIO/object storage merely to make the local stack complicated.

However, clearly document that a large multi-node production Loki deployment would normally require production-grade shared/object storage.

Pin the Loki image version.

Never use `latest`.

---

# 22. Grafana

Add **Grafana as the unified observability UI**.

Provision data sources automatically where practical:

```text
Prometheus
Loki
Jaeger
```

A developer should not need to manually configure all three after every `docker compose up`.

Use provisioning files committed to the repository if that matches the project structure.

Pin a stable Grafana image version.

Do not use `latest`.

Persist Grafana state only where it provides value.

Do not hardcode real production admin credentials.

Development credentials must be clearly development-only and supplied through configuration/environment variables.

---

# 23. Cross-signal correlation

One major goal of the stack is to correlate:

```text
logs
metrics
traces
```

OpenTelemetry should automatically correlate `ILogger` logs with active Activities through:

```text
TraceId
SpanId
TraceFlags
```

Do not manually copy TraceId and SpanId into every log call.

Preserve the existing:

```text
HttpContext.TraceIdentifier
```

as `requestId` in ProblemDetails.

Also add an OpenTelemetry `traceId` to ProblemDetails when an active trace exists, if this can be done additively without breaking the existing response contract.

Conceptually:

```json
{
  "requestId": "...",
  "traceId": "..."
}
```

---

# 24. Grafana logs-to-traces correlation

Where supported cleanly by the pinned Grafana/Loki/Jaeger versions, configure Grafana so a developer can navigate:

```text
Loki log
   |
   | trace_id
   v
Jaeger trace
```

Use OpenTelemetry trace IDs already present in log records.

Do not add custom logging code solely to create this relationship if OpenTelemetry already supplies the field.

Prefer Grafana data-source provisioning/derived-field configuration.

---

# 25. Metrics-to-traces exemplars

Evaluate OpenTelemetry exemplars.

If the selected OpenTelemetry .NET + Prometheus + Grafana versions support it cleanly, configure **trace-based exemplars** so developers can navigate:

```text
Prometheus metric sample
        |
        | exemplar TraceId
        v
Jaeger trace
```

Do not force this if doing so requires unstable APIs or excessive complexity.

If implemented:

- ensure exemplar data does not become high-cardinality metric labels;
- confirm Prometheus actually stores the exemplars;
- configure Grafana's Prometheus datasource with the Jaeger datasource for trace navigation;
- verify the complete metric-to-trace flow.

If not implemented, document it as an optional later improvement and explain why.

---

# 26. Structured logging rules

Review existing logging calls.

Keep structured message templates.

GOOD:

```csharp
_logger.LogInformation(
    "Database migrations completed in {ElapsedMilliseconds} ms",
    elapsedMilliseconds);
```

BAD:

```csharp
_logger.LogInformation(
    $"Database migrations completed in {elapsedMilliseconds} ms");
```

Do not use string interpolation for structured logs.

Prefer stable property names such as:

```text
RequestId
AccountId
GameId
Operation
Outcome
ElapsedMilliseconds
DeletedCount
Attempt
RetryDelaySeconds
StatusCode
ErrorCode
```

Only attach IDs where they are safe and operationally useful.

---

# 27. LoggerMessage source generation

Use compile-time `LoggerMessage` source generation selectively for:

- important recurring events;
- performance-sensitive paths;
- operational components with stable event definitions.

Potential candidates include:

```text
DatabaseMigrationService
RefreshTokenCleanupWorker
SystemAdminSeeder
GlobalExceptionHandler
future high-throughput realtime components
```

Do NOT mechanically convert every log statement in the repository.

Use stable EventId/EventName values if doing so remains simple.

If you establish numeric EventIds, document a small range convention.

Do not build a custom event-catalog framework.

---

# 28. Logging levels

Use consistent semantics.

## Trace / Debug

Detailed diagnostic information.

Examples:

```text
migration advisory lock acquired
cleanup found zero rows
internal retry state
low-level connection diagnostics
```

## Information

Meaningful successful lifecycle or operational events.

Examples:

```text
database migration completed
bootstrap admin created
cleanup deleted records
game created
game finished
account suspended
password changed
logout-all completed
```

Do not log every normal high-frequency gameplay action at Information.

## Warning

Unexpected but recoverable conditions.

Examples:

```text
transient database failure being retried
rate limit activated
refresh token malicious reuse detected
worker retry
temporary external dependency failure
```

## Error

An operation failed after normal recovery was exhausted, or an unexpected operation-level failure occurred.

## Critical

Only genuine process/system integrity failures where safe operation cannot reasonably continue.

Do not treat normal 4xx outcomes as errors.

---

# 29. Fix current retry logging

Review `RefreshTokenCleanupWorker`.

The current design may log a retryable failure as Error before a later retry succeeds.

Improve this.

Desired semantics:

```text
intermediate failure -> Warning
retry                -> Warning
successful recovery  -> Information/Debug as appropriate
final exhausted fail -> Error
```

Likewise:

```text
DeletedCount == 0 -> Debug
DeletedCount > 0  -> Information
```

where appropriate.

Use the existing migration service's approach—Warning for transient recovery and Error for terminal failure—as the baseline convention.

Review other workers for the same issue.

---

# 30. Global exception logging

Review `GlobalExceptionHandler`.

Unexpected HTTP exceptions should normally be logged exactly once at the outer exception boundary.

Conceptually:

```text
Expected business error
    -> Result/Error
    -> ProblemDetails
    -> no Error log

Unexpected exception
    -> GlobalExceptionHandler
    -> one Error log
    -> ProblemDetails
```

Do not repeatedly log the same exception in:

```text
repository
handler
controller
exception handler
```

and rethrow it through every layer.

Background workers and startup services are separate operational boundaries and may log their own terminal failures.

Do not treat:

```text
Auth.InvalidCredentials
Auth.RefreshRace
Quiz.NotFound
Game.Full
validation errors
expected 404
expected 409
```

as unexpected application errors.

---

# 31. Security and telemetry redaction

Telemetry is a security boundary.

Under no circumstances may logs, traces, metrics, Collector output, Loki, Jaeger, or Prometheus contain sensitive values such as:

```text
password
password hash
bootstrap password
raw JWT
raw refresh token
Authorization header
Cookie header
raw authentication cookies
CSRF token
database password
connection string password
JWT signing key
SignalR access_token
question text
player answers
secret environment variables
```

Do not log complete request/response bodies for authentication or gameplay.

Do not capture sensitive HTTP headers.

Do not enable EF Core sensitive-data logging in Production.

Do not enable Npgsql parameter values in telemetry.

Keep URL query-string redaction enabled.

This is especially important because SignalR authentication may use:

```text
?access_token=...
```

Never disable ASP.NET Core/OpenTelemetry query redaction merely to make traces more detailed.

Do not attach raw query strings to logs or spans.

Prevent secrets from entering telemetry at the source.

Collector-side redaction may be used only as defense in depth, not as permission to log secrets.

---

# 32. Database telemetry

The project uses PostgreSQL with Npgsql/EF Core.

Use official Npgsql OpenTelemetry instrumentation compatible with the actual installed Npgsql version.

Collect useful tracing and metrics without leaking query parameters.

Do not create a custom EF Core interceptor solely for telemetry if official instrumentation provides what is needed.

Be cautious with SQL statement collection.

Evaluate whether SQL text could contain sensitive application information.

Do not configure raw SQL text as span names.

Span names must remain stable and low-cardinality.

Prefer semantic database operation attributes over user-specific SQL.

---

# 33. OpenTelemetry Collector

Add an OpenTelemetry Collector to Docker Compose.

Prefer the Collector contrib distribution when required components are needed.

Pin an exact stable image version.

Do not use `latest`.

Create a dedicated configuration file such as:

```text
observability/otel-collector.yaml
```

or another path consistent with repository conventions.

Configure OTLP receivers:

```text
4317 gRPC
4318 HTTP
```

Use processors including at minimum:

```text
memory_limiter
batch
```

in appropriate order.

Consider only justified additional processors such as:

```text
resource
attributes
filter
transform
```

Do not create a complicated Collector pipeline unnecessarily.

---

# 34. Collector pipelines

Use separate pipelines for:

```text
traces
metrics
logs
```

Conceptually:

```yaml
service:
  pipelines:

    traces:
      receivers:
        - otlp
      processors:
        - memory_limiter
        - batch
      exporters:
        - otlp/jaeger

    metrics:
      receivers:
        - otlp
      processors:
        - memory_limiter
        - batch
      exporters:
        - prometheus

    logs:
      receivers:
        - otlp
      processors:
        - memory_limiter
        - batch
      exporters:
        - otlphttp/loki
```

Do not copy this blindly.

Verify exact Collector component names and configuration against the pinned Collector version.

---

# 35. Collector -> Jaeger

Export traces from the Collector to Jaeger via OTLP.

Use either OTLP gRPC or OTLP HTTP based on current best practice and Docker networking.

Do not use deprecated Jaeger-specific Collector exporters.

Keep transport internal to Docker networking.

---

# 36. Collector -> Loki

Export logs to Loki using Loki's native OTLP ingestion endpoint.

Use the Collector's standard:

```text
otlphttp
```

exporter.

Do not use the historical/deprecated Loki exporter if native OTLP is supported.

Configure the correct Loki OTLP base endpoint for the pinned version.

Keep transport internal to Docker.

---

# 37. Collector -> Prometheus

Prefer exposing Collector metrics through its Prometheus exporter and let Prometheus scrape that endpoint.

Configure:

```text
Prometheus -> scrape -> OpenTelemetry Collector
```

rather than pushing application metrics directly to Prometheus.

Verify that this remains appropriate for the selected current Collector and Prometheus versions.

Document the decision.

---

# 38. Collector backpressure and failures

Observability must never become an application availability dependency.

The API must continue operating when:

```text
Collector is unavailable
Prometheus is unavailable
Jaeger is unavailable
Loki is unavailable
Grafana is unavailable
```

Do NOT make the API's startup/readiness depend on these services.

Do not configure:

```text
api depends_on collector health
```

as a hard application dependency.

Use bounded exporter queues and retry policies in the Collector where appropriate.

Avoid unbounded in-memory telemetry buffering.

If a telemetry backend remains unavailable, bounded telemetry loss is preferable to bringing down the Kahoot API.

Document this explicitly.

---

# 39. Docker Compose topology

Extend the existing Docker topology with:

```text
otel-collector
prometheus
jaeger
loki
grafana
```

Keep PostgreSQL isolated appropriately.

Add an observability network if it meaningfully improves separation:

```text
observability
```

Possible network relationships:

```text
api              -> observability
otel-collector   -> observability
prometheus       -> observability
jaeger           -> observability
loki             -> observability
grafana          -> observability
```

Do not attach PostgreSQL to the observability network merely for convenience.

Only expose host ports that developers need.

For example, developers may need:

```text
Grafana
Jaeger UI
Prometheus UI
```

Collector ingestion, Loki HTTP, and internal telemetry ports normally do not need public host exposure.

---

# 40. Persistence and retention

Avoid unbounded telemetry storage.

Configure persistent Docker volumes where appropriate:

```text
Prometheus
Loki
Grafana
```

Configure sensible development/single-host retention limits.

Do not silently allow logs or metrics to fill the host disk forever.

Clearly distinguish:

```text
local/single-host reference deployment
```

from:

```text
large production distributed observability deployment
```

Do not introduce a distributed Loki cluster, Prometheus HA cluster, or production Jaeger storage cluster unless this project currently requires it.

Keep the local architecture educational and maintainable.

---

# 41. Environment configuration

Follow repository rules requiring environment files to remain synchronized.

If observability environment variables are added, update:

```text
.env.example
.env.development
.env.production
```

Preserve environment-specific values.

Never commit:

```text
production passwords
Grafana production passwords
OTLP authentication tokens
cloud observability credentials
API keys
```

Use placeholders or environment injection.

Do not modify the developer's local `.env` secrets unnecessarily.

Support standard OTEL configuration where appropriate.

Potential variables include:

```text
OTEL_SERVICE_NAME
OTEL_EXPORTER_OTLP_ENDPOINT
OTEL_EXPORTER_OTLP_PROTOCOL
OTEL_TRACES_SAMPLER
OTEL_TRACES_SAMPLER_ARG
```

Only add project-specific variables when standard OTEL configuration is insufficient.

---

# 42. Application log-level configuration

Review:

```text
appsettings.json
appsettings.Development.json
appsettings.Production.json
```

Keep the current environment configuration strategy.

A possible production baseline is:

```text
Kahoot                                  Information
Microsoft.AspNetCore                    Warning
Microsoft.EntityFrameworkCore           Warning
Npgsql                                  Warning
OpenTelemetry                           Warning
```

Do not copy this blindly.

Inspect actual category names and adjust appropriately.

Avoid excessive framework noise.

Never suppress Kahoot Error/Critical logs.

---

# 43. Do not manually log every request

ASP.NET Core/OpenTelemetry instrumentation already provides HTTP telemetry.

Do not introduce middleware that writes an Information log for every successful request unless there is a real requirement.

At Kahoot scale, avoid Information logs for every:

```text
answer submission
SignalR frame
heartbeat
presence ping
leaderboard broadcast
reconnect
database query
```

Use the correct signal:

```text
Logs    -> important discrete events
Metrics -> aggregated system behavior
Traces  -> causality and latency
```

Do not use logging as a substitute for metrics.

Do not use metrics as a substitute for tracing.

---

# 44. High-cardinality observability rules

Review all telemetry attributes/tags.

Metrics require especially strict cardinality.

Never create metric dimensions from:

```text
UserId
AccountId
GameId
ParticipantId
question ID
username
nickname
IP
URL query
TraceId
SpanId
exception message
```

Logs and traces may contain safe resource IDs when they materially help diagnose a specific operation, but do not add them indiscriminately.

Loki labels must also remain low-cardinality.

---

# 45. Grafana provisioning

Provision Grafana data sources automatically where possible.

Expected data sources:

```text
Prometheus
Loki
Jaeger
```

The Docker environment should become usable after:

```bash
docker compose up --build -d
```

without developers manually configuring each datasource through the UI.

Do not create dozens of dashboards.

A small initial operational dashboard is acceptable if useful.

If creating one, keep it focused on:

```text
HTTP request rate
HTTP errors
request duration
runtime memory/GC
database connection pool
worker failures
```

Do not create speculative business dashboards.

---

# 46. Collector telemetry

The Collector itself should expose enough diagnostics to troubleshoot telemetry flow.

Use its supported internal health/metrics mechanisms without making the stack overly complex.

Do not send Collector diagnostic telemetry recursively into itself without understanding the consequences.

Keep Collector logs useful and bounded.

---

# 47. Observability component health checks

Add Docker health checks when supported and reliable for:

```text
OpenTelemetry Collector
Prometheus
Jaeger
Loki
Grafana
```

Use these for Docker operational visibility.

Do not make the Kahoot API's own `/health/ready` depend on the observability stack.

Application readiness should continue representing whether the application can serve its business functionality.

Observability is not a correctness dependency.

---

# 48. Existing logging review

Review current logging in:

```text
DatabaseMigrationService
RefreshTokenCleanupWorker
SystemAdminSeeder
GlobalExceptionHandler
authentication flows
```

Look for:

```text
incorrect levels
duplicate errors
secret exposure
high-frequency noise
string interpolation
unstable message templates
missing EventName/EventId
missing useful context
```

Improve only what is justified.

Do not refactor unrelated code.

---

# 49. Documentation updates

Update relevant repository documentation.

Document:

```text
overall observability architecture
OpenTelemetry's responsibility
Collector responsibility
Prometheus responsibility
Jaeger responsibility
Loki responsibility
Grafana responsibility

OTLP flow
log levels
structured logging
trace sampling
metric cardinality
Loki label cardinality
retention
security/redaction
cross-signal correlation
Collector outage behavior
local URLs
environment variables
Docker commands
```

Remove or update old requirements that prohibit:

```text
OpenTelemetry
Prometheus
Jaeger
Loki
Grafana
```

Search the whole repository after editing to make sure contradictory statements do not remain unintentionally.

---

# 50. Tests constraint

The repository currently explicitly requires:

```text
backend/test/
```

to remain empty except for its existing placeholder.

Respect that rule.

Do NOT add:

```text
test projects
unit tests
integration tests
end-to-end tests
```

for this task unless the repository instructions themselves have changed.

Use runtime/config/build verification.

---

# 51. Verification

Run the strongest verification available.

From the correct backend directory run at minimum:

```bash
dotnet restore Kahoot.slnx
dotnet build Kahoot.slnx
dotnet format Kahoot.slnx --verify-no-changes
```

Validate Docker:

```bash
docker compose config
docker compose up --build -d
docker compose ps
```

Inspect container health and logs.

Verify all of the following:

1. API starts successfully.
2. PostgreSQL starts successfully.
3. OpenTelemetry Collector starts successfully.
4. Prometheus starts successfully.
5. Jaeger starts successfully.
6. Loki starts successfully.
7. Grafana starts successfully.
8. API emits OTLP logs to the Collector.
9. API emits OTLP traces to the Collector.
10. API emits OTLP metrics to the Collector.
11. traces appear in Jaeger.
12. metrics appear in Prometheus.
13. logs appear in Loki.
14. Grafana can query Prometheus.
15. Grafana can query Loki.
16. Grafana can access Jaeger traces.
17. request logs are correlated with TraceId/SpanId.
18. ProblemDetails still contains `requestId`.
19. ProblemDetails includes `traceId` if that change was implemented.
20. ASP.NET Core requests create traces.
21. HttpClient calls create spans when present.
22. PostgreSQL operations create safe spans.
23. Npgsql telemetry does not expose parameter secrets.
24. runtime metrics are present.
25. health-check traces are filtered if configured.
26. high-cardinality IDs are not metric labels.
27. high-cardinality IDs are not Loki index labels.
28. passwords/tokens/credentials are absent from telemetry.
29. SignalR `access_token` query values are redacted.
30. production EF sensitive-data logging remains disabled.
31. stopping Grafana does not affect API operation.
32. stopping Loki does not affect API operation.
33. stopping Jaeger does not affect API operation.
34. stopping Prometheus does not affect API operation.
35. stopping the Collector does not affect API business functionality.
36. JSON console logging continues if Collector is down.
37. restoring the Collector resumes telemetry flow.
38. Prometheus/Loki data persists across normal container restart if persistence is configured.
39. no database migration/schema change was introduced.
40. `backend/test/` remains untouched.
41. final diff contains no unrelated changes.

If Grafana log-to-trace or exemplar metric-to-trace navigation was implemented, verify those flows as well.

Do not claim verification that was not actually executed.

---

# 52. Security verification

Search generated configuration and application code for accidental sensitive capture.

Specifically verify telemetry cannot expose:

```text
Authorization
access_token
kahoot_refresh_token
kahoot_csrf_token
Password
PasswordHash
SigningKey
POSTGRES_PASSWORD
JWT_SIGNING_KEY
connection-string credentials
question text
answer contents
```

Check:

```text
application logs
OpenTelemetry Collector output
Loki
Jaeger span attributes
Prometheus labels
Grafana views
```

Do not assume automatic instrumentation is safe without verifying its configuration.

---

# 53. Final architecture review

Before finishing, inspect the complete diff.

Ask:

```text
Did we add unnecessary packages?

Is OpenTelemetry still vendor-neutral?

Does the application talk only to the Collector?

Did we accidentally create duplicate telemetry?

Did we duplicate framework metrics?

Did we create high-cardinality metrics?

Did we create high-cardinality Loki labels?

Can telemetry failure affect API availability?

Are secrets protected?

Did we over-instrument business code?

Did we violate Clean Architecture?

Did we unnecessarily couple Infrastructure/Application to OpenTelemetry?

Are Docker images pinned?

Are storage/retention limits bounded?

Can the design be simpler without losing required observability?
```

Simplify where possible.

---

# 54. Final report

When complete, provide a technically detailed report covering:

1. Files changed.
2. Documentation requirements changed.
3. NuGet packages added and why.
4. Docker images/services added and their pinned versions.
5. Final observability architecture.
6. OpenTelemetry configuration.
7. Collector pipelines.
8. Prometheus configuration.
9. Jaeger configuration.
10. Loki configuration.
11. Grafana configuration.
12. Logs collected.
13. Traces collected.
14. Metrics collected.
15. Cross-signal correlation.
16. Trace sampling.
17. Metric and Loki cardinality protections.
18. Security/redaction protections.
19. Storage and retention behavior.
20. Failure-isolation behavior.
21. Existing logging improvements.
22. Commands actually executed.
23. Verification actually completed.
24. Anything that could not be verified.
25. Remaining optional improvements.

Also explain important engineering decisions and trade-offs so I can learn from the implementation.

---

# Core objective

Build the **smallest maintainable production-quality observability architecture** that fits the current Kahoot backend.

The primary stack should be:

```text
ASP.NET Core
      |
      | ILogger<T>
      | Activity / OpenTelemetry tracing
      | System.Diagnostics.Metrics / OpenTelemetry metrics
      |
      v
OpenTelemetry SDK
      |
      | OTLP
      v
OpenTelemetry Collector
      |
      +----------> Prometheus  -> metrics
      |
      +----------> Jaeger      -> traces
      |
      +----------> Loki        -> logs
                       |
Prometheus -------------|
Jaeger -----------------|----> Grafana
Loki -------------------|
```

Keep business code independent of the observability backends.

Use:

```text
ILogger<T>
OpenTelemetry
OTLP
OpenTelemetry Collector
Prometheus
Jaeger
Loki
Grafana
ASP.NET Core instrumentation
HttpClient instrumentation
Npgsql instrumentation
runtime metrics
```

Do not create an observability framework inside the application.

The observability system should make Kahoot significantly easier to operate and diagnose while remaining:

```text
secure
bounded
vendor-neutral
low-cardinality
failure-isolated
maintainable
production-oriented
simple enough to understand
```