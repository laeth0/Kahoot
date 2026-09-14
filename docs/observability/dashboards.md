# Grafana Dashboards Reference

This document maps all panels across the six provisioned Grafana dashboards (`Kahoot Observability` folder) to their data source, underlying metrics or recording rules, units, expected baselines, and drill-down links.

---

## 1. Application Health Dashboard (`kahoot-application`)

**Primary Purpose:** Operator visibility into backend availability, HTTP traffic volume, error ratios, latency percentiles, and runtime health.

| Panel Title | Data Source | Metric / Recording Rule | Units | Expected Baseline | Drill-down / Action |
|---|---|---|---|---|---|
| Active Alerts | Prometheus | Unified alert list for `Kahoot Observability` | State | 0 firing alerts | View alert details |
| Backend Availability | Prometheus | `kahoot:blackbox_success{instance=~".*backend.*"}` | Boolean | 1 (UP) | Check container logs / pause state |
| Frontend Availability | Prometheus | `kahoot:blackbox_success{instance=~".*frontend.*"}` | Boolean | 1 (UP) | Check frontend container |
| HTTP Request Rate | Prometheus | `kahoot:http_requests:rate5m` | req/s | 5–50 req/s under load | N/A |
| HTTP 5xx Error Ratio | Prometheus | `kahoot:http_5xx:ratio5m` | Percent (`%`) | < 0.5% (NFR threshold: 2%) | Drill-down to Logs dashboard |
| HTTP Latency Percentiles | Prometheus | `kahoot:http_latency:p50_5m`, `p95_5m`, `p99_5m` | Seconds (`s`) | p95 < 0.3s (300ms) | Exemplar links to Jaeger traces |
| HTTP Status Breakdown | Prometheus | `http_server_request_duration_seconds_count` by status | req/s | Predominantly 200 OK | Filter status codes in Loki |
| Failed Requests by Route | Prometheus | `traces_span_metrics_calls_total` (error status) | req/s | 0 req/s | Link to Loki error logs by route |
| Slow Endpoints Top 10 | Prometheus | `traces_span_metrics_latency_bucket` p95 by route | Seconds | < 0.5s | Link to Jaeger slow traces |
| MediatR Operations Duration | Prometheus | `kahoot_application_operation_duration_seconds_bucket` | Seconds | < 0.1s | Drill-down to handler traces |
| GC & Memory Pressure | Prometheus | `dotnet_gc_pause_ratio`, `process_working_set` | Bytes / % | < 500 MB RAM | Check memory leak in runtime |
| Recent Backend Error Logs | Loki | `{service_name="kahoot-backend"} \|~ "(?i)error\|exception"` | Logs | 0 entries | Trace ID link to Jaeger |

---

## 2. Realtime & SignalR Dashboard (`kahoot-realtime`)

**Primary Purpose:** High-velocity monitoring for live Kahoot multiplayer games, player connections, and SignalR hub throughput.

| Panel Title | Data Source | Metric / Recording Rule | Units | Expected Baseline | Drill-down / Action |
|---|---|---|---|---|---|
| Active Alerts | Prometheus | Unified alert list for `Kahoot Observability` | State | 0 firing alerts | View alert details |
| Active Games by State | Prometheus | `kahoot_game_sessions_active` by state | Count | 0–10 sessions | Breakdown by Lobby, Active, etc. |
| Connected Players | Prometheus | `kahoot_players_connected` | Count | 0–500 players | Correlate with SignalR count |
| Active SignalR Connections | Prometheus | `kahoot:signalr_connections` | Count | Matches connected players | Alert if players > 0 and connections == 0 |
| Game Creation / End Rate | Prometheus | `kahoot_game_sessions_created_total`, `ended_total` | ops/s | 0.01–0.1 ops/s | Correlate game lifecycles |
| Player Join Rate | Prometheus | `rate(kahoot_players_joined_total[5m])` | joins/s | Spikes during game lobby | Monitor lobby batching |
| Questions Served Rate | Prometheus | `rate(kahoot_questions_served_total[5m])` | ops/s | Matches game cadence | N/A |
| Answers Submitted by Outcome | Prometheus | `rate(kahoot_answers_submitted_total[5m])` by outcome | answers/s | Predominantly `accepted` | Check `late` or `duplicate` ratios |
| Answer Processing Latency | Prometheus | `kahoot:answer_latency:p95_5m` | Seconds | p95 < 0.5s (500ms) | Exemplar links to Jaeger traces |
| SignalR Reconnects & Disconnects | Prometheus | `kahoot_signalr_reconnections_total`, `disconnects_total` | events/s | Disconnects < 1/min | Network instability indicator |
| Realtime Broadcast Failures | Prometheus | `rate(kahoot_signalr_events_sent_total{outcome="failure"}[5m])` | failures/s | 0 failures | Check SignalR hub errors |
| Broadcast Duration p95 | Prometheus | `histogram_quantile(0.95, ... kahoot_signalr_broadcast_duration_seconds_bucket)` | Seconds | < 0.05s (50ms) | Check network fanout delay |
| State Transition Failures | Prometheus | `rate(kahoot_game_transition_failures_total[5m])` | failures/s | 0 failures | Investigate concurrency conflicts |

---

## 3. Infrastructure & Host Dashboard (`kahoot-infrastructure`)

**Primary Purpose:** Host VM resource utilization, container-level CPU/memory consumption, Docker health, and Prometheus TSDB retention.

| Panel Title | Data Source | Metric / Recording Rule | Units | Expected Baseline | Drill-down / Action |
|---|---|---|---|---|---|
| Active Alerts | Prometheus | Unified alert list for `Kahoot Observability` | State | 0 firing alerts | View alert details |
| VM CPU Usage | Prometheus | `kahoot:vm_cpu:ratio5m` | Percent (`%`) | < 60% (Alert: 80%) | Check runaway processes |
| VM Memory Usage & Available | Prometheus | `kahoot:vm_memory:ratio`, `node_memory_MemAvailable_bytes` | Bytes / % | > 1 GB available (Alert: 90%) | Mitigate OOM risk |
| Root Disk Usage & Free Bytes | Prometheus | `kahoot:vm_disk:ratio`, `node_filesystem_avail_bytes` | Bytes / % | < 70% used (Alert: 90%) | Clean Docker build cache |
| Disk I/O & IOPS | Prometheus | `rate(node_disk_read_bytes_total)`, `write_bytes_total` | B/s / IOPS | < 10 MB/s sustained | Disk bottleneck diagnosis |
| Network Traffic & Drops | Prometheus | `rate(node_network_receive_bytes_total)`, `errs_total` | B/s / errs | 0 packet drops | Azure NSG / bandwidth check |
| Container CPU by Service | Prometheus | `kahoot:container_cpu:ratio5m` by name | vCPU | Backend < 0.8, DB < 0.5 | Identify heavy containers |
| Container Memory by Service | Prometheus | `kahoot:container_memory:bytes` by name | Bytes | All containers below caps | Compare against limits |
| Container Uptime & Presence | Prometheus | `container_last_seen` by service name | Table | All 14 services present | Trigger for container missing alert |
| OTel Collector Health & Queue | Prometheus | `otelcol_processor_queue_latency`, `batch_size` | Seconds / Items | Queue latency < 1s | Check collector dropping alert |
| Prometheus Storage & Head Chunks | Prometheus | `prometheus_tsdb_storage_blocks_bytes`, `head_chunks` | Bytes | Total storage < 4 GB cap | Retention compliance |

---

## 4. Database & Queries Dashboard (`kahoot-database`)

**Primary Purpose:** PostgreSQL connection pool saturation, query latency percentiles, slow query detection via `pg_stat_statements`, and database error rates.

| Panel Title | Data Source | Metric / Recording Rule | Units | Expected Baseline | Drill-down / Action |
|---|---|---|---|---|---|
| Active Alerts | Prometheus | Unified alert list for `Kahoot Observability` | State | 0 firing alerts | View alert details |
| PostgreSQL Availability | Prometheus | `up{job="postgres"}` | Boolean | 1 (UP) | Immediate recovery action |
| Connection Pool Saturation | Prometheus | `pg_stat_activity_count`, `npgsql_active_connections` | Count | Active < 50 (Max: 100) | Check connection leaks |
| Query Latency Percentiles | Prometheus | `kahoot:db_latency:p95_5m` | Seconds | p95 < 0.1s (Warning: 1.0s) | Exemplar links to DB spans |
| Top 20 Slow Queries | Prometheus | `pg_stat_statements_total_exec_time_ms` by queryid | Seconds | Top query < 500ms avg | Inspect EF Core LINQ query |
| Query Throughput | Prometheus | `rate(pg_stat_database_xact_commit[5m])` | tps | 10–100 tps | Transaction volume |
| Database Errors & Rollbacks | Prometheus | `kahoot:db_errors:rate5m`, `pg_stat_database_xact_rollback` | errs/s | 0 errors | Investigate failed queries |
| Deadlocks & Longest Transaction | Prometheus | `pg_stat_database_deadlocks`, lock wait time | Count / s | 0 deadlocks, tx < 5s | Concurrency conflict check |
| Buffer Cache Hit Ratio | Prometheus | `pg_stat_database_blks_hit / (blks_hit + blks_read)` | Percent | > 95% | Memory cache efficiency |
| Database Growth Rate | Prometheus | `pg_database_size_bytes` | Bytes / MB/day | < 100 MB / week | Storage capacity planning |

---

## 5. Logs & Exceptions Dashboard (`kahoot-logs`)

**Primary Purpose:** Correlated log exploration with structured fields, request ID tracking, runtime exceptions, and Nginx reverse-proxy error logs.

| Panel Title | Data Source | Query / Metric | Filtering Variables | Drill-down / Action |
|---|---|---|---|---|
| Active Alerts | Prometheus | Unified alert list for `Kahoot Observability` | All | View alert details |
| Error Volume in Loki | Loki | `sum(count_over_time({service_name=~".+"} \|~ "(?i)error"[1m]))` | service, level | Identifies error spikes |
| Runtime Exception Frequency | Prometheus | `rate(dotnet_exceptions_total[5m])` | None | Unhandled exception rate |
| Nginx 5xx Rate & Upstream Latency | Prometheus | `rate(nginx_access_observability_5xx)` | None | Nginx gateway errors |
| Errors by Route and Method | Loki | Structured table: timestamp, route, method, status | service, level | Link to detailed log stream |
| Game Transition Failures | Prometheus | `rate(kahoot_game_transition_failures_total[5m])` by transition | None | Specific failed state change |
| Live Log Stream | Loki | `{service_name=~"$service"} \| json \| level=~"$level"` | service, level | Direct link from `trace_id` to Jaeger |

---

## 6. Distributed Tracing Dashboard (`kahoot-tracing`)

**Primary Purpose:** End-to-end request latency profiling, trace drill-downs, slow trace identification, span error distribution, and service dependency mapping.

| Panel Title | Data Source | Query / Metric | Thresholds / Behavior | Drill-down / Action |
|---|---|---|---|---|
| Sampling Notification Banner | Markdown | Text note explaining sampling policy | Fixed | "Normal traces sampled at 10%; errors & traces > 1s retained 100%" |
| Slow Traces (> 1s) | Jaeger | Query for traces with `duration > 1s` | Latency > 1000ms | Open trace timeline in Jaeger; link to Loki |
| Failed & Error Traces | Jaeger | Query for traces with `status: error` | Errors retained 100% | Drill-down to span exceptions and stack traces |
| Service Operation Latency | Prometheus | `traces_span_metrics_latency_bucket` p95 | NFR marker: 1.0s | Inspect slow span operation |
| Span Error Rate % | Prometheus | `traces_span_metrics_calls_total` error ratio | < 1% | Pinpoint failing micro-operations |
| Service Dependency Graph | Prometheus | `servicegraph_request_total` | Node graph | Application → Database / Nginx flow |
| Database Spans by Operation | Prometheus | `traces_span_metrics_calls_total{db_system_name="postgresql"}` | Latency & count | Link to Database dashboard |
| Recent SignalR Spans | Jaeger | Traces matching `operation: kahoot.signalr.*` | Duration & outcome | Validate broadcast delivery |
