# Phase 1 - Align requirements and deployment docs

## Objective

Make the repository's normative documentation agree with the approved OpenTelemetry/Collector/Prometheus/Jaeger/Loki/Grafana direction **before application code changes**. Preserve existing product, health, security, capacity, and SLO requirements. The prompt explicitly supersedes older observability exclusions; this phase records the documentation change that resolves that conflict.

## Files to create or modify

- Modify `docs/12-platform-operations-and-health.md`: extend observability and local structured logging requirements to all three OTel signals, Collector gateway, redaction, noncritical outage behavior, and health/readiness independence.
- Modify `docs/13-architecture-and-deployment.md`: add the reference observability topology, internal network/ports, local storage/retention and ephemeral Jaeger caveat, deployment overrides, and production scale distinction; do not rewrite unrelated architecture.
- Modify `docs/14-verification-and-testing.md` only if its conditional `VERIF-OBS-001` remains inconsistent after the new requirement wording; retain its test/SLO framework.
- Modify any other `docs/*.md` **only** where Phase 0 confirms a specific contradictory statement. Do not edit backend code, Compose, env files, or `backend/test/` here.
- Record exact selected version matrix and rationale in Phase 0 if not already done during Phase 0 execution.

## Required packages/dependencies

No package or image installation. Consume the Phase 0 version/component decision record. The docs may refer to selected image versions as an operator detail without claiming that services already run.

## Step-by-step implementation tasks

- [ ] Re-read current `docs/12`, `docs/13`, `docs/14` and search `docs/` for explicit exclusions and other logging/health/telemetry statements. Cite exact lines being amended in the phase review note; do not manufacture a conflict if the text is already compatible.
- [ ] Add a concise normative statement that application logs, traces, and metrics leave through OTLP to the Collector only. Define Prometheus scrape from Collector, Collector-to-Jaeger OTLP, Collector-to-Loki native OTLP, and Grafana as the query UI.
- [ ] State that structured JSON console logging continues independently; telemetry backend/Collector failure cannot fail API startup or readiness. Keep current `/health` behavior and existing requirements for future `/health/live` and `/health/ready` distinct.
- [ ] Document operational constraints: standard `OTEL_*` settings and environment override precedence, parent-based production sampling, low-cardinality metric/Loki labels, no query/header/body secrets, persistent Prometheus/Loki single-host volumes, ephemeral local Jaeger, and bounded data retention.
- [ ] Keep normative existing SLO IDs, auth contracts, multi-replica rules, and PostgreSQL startup/locking behavior untouched. Run a final `rg` search for old exclusion phrases and review the focused diff.

## Configuration/environment variables

Document planned `OTEL_SERVICE_NAME`, `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_SAMPLER`, `OTEL_TRACES_SAMPLER_ARG`, and a Grafana admin password supplied at deployment. Do not put credentials in docs or change `.env.*` yet. Explain that Compose passes variables into the API and host-run API needs a reachable Collector endpoint.

## Instrumentation covered

Requirements documentation for OTel logs/traces/metrics; no runtime instrumentation in this phase. Document auto-instrumented HTTP/runtime/Npgsql first and optional custom spans/metrics only after evidence of an operational question.

## Testing and validation

- `rg -n -i 'centralized observability|opentelemetry|prometheus|jaeger|loki|grafana|telemetry' docs -g '*.md'` to find any remaining contradiction.
- Focused `git diff --check -- docs` and `git diff -- docs/12-platform-operations-and-health.md docs/13-architecture-and-deployment.md docs/14-verification-and-testing.md`.
- Manual comparison against the Phase 0 requirement map. No build/test required for docs-only edits.

## Acceptance criteria

- Docs approve and accurately describe the future three-signal topology, including failure isolation, security, storage, and correlation; no current hard ban remains.
- No unrelated SLO, feature contract, or health endpoint claim is changed. Wording distinguishes already implemented behavior from the target architecture.
- Phase 0 records the exact versions that subsequent phases must use.

## Dependencies on previous phases

Phase 0 completed and version/component decisions recorded.
