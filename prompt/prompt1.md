# Optimize Docker Resource Configuration for Development and Production

Review the current Docker Compose configuration and improve the resource management strategy for both development and production environments.

Context:

- The application is deployed on Azure VM:
  - VM size: Standard_D2s_v3
  - CPU: 2 vCPU
  - RAM: 8 GB
- Expected production load:
  - ~200 concurrent users
  - Real-time gameplay using SignalR/WebSockets
  - PostgreSQL database
  - Frontend + Backend + Nginx
  - Future observability stack (Grafana, Prometheus, Loki, Jaeger)

Current concern:

Some containers have very restrictive limits such as:

```yaml
cpus: 0.50
mem_limit: 512m
mem_reservation: 256m
cpu_shares: 768
````

These limits may unnecessarily throttle the backend under real production traffic.

## Tasks

### 1. Review current resource allocation

Analyze:

* docker-compose.yml
* docker-compose.prod.yml
* Dockerfiles
* application architecture
* expected workload

Identify:

* services that need more resources
* services that should have limits
* services that can safely remain lightweight

Do not add limits just for the sake of adding limits.

---

# Development Environment Requirements

Development should prioritize:

* developer experience
* easy debugging
* fast reload
* no unnecessary resource restrictions

Requirements:

* Avoid aggressive CPU/memory limits.
* Allow containers to use available machine resources.
* Keep configuration simple.
* Prevent only extreme runaway resource usage if necessary.

Prefer:

* no hard CPU limits for development
* reasonable memory limits only if they prevent system instability

---

# Production Environment Requirements

Production runs on Azure VM with limited resources.

Design balanced resource allocation.

Goals:

1. Backend must handle:

   * 200 concurrent users
   * SignalR connections
   * realtime events
   * API requests
   * database operations

2. PostgreSQL must have enough memory for:

   * query cache
   * indexes
   * active connections

3. Nginx/frontend should remain lightweight.

4. Observability services should not starve the application.

---

# Recommended Direction

Review and apply appropriate limits similar to:

Example only (adjust after analysis):

```yaml
backend:
  cpus: "1.5"
  mem_reservation: 512m
  mem_limit: 2g

db:
  mem_reservation: 1g
  mem_limit: 2g

frontend:
  mem_reservation: 64m
  mem_limit: 256m

nginx:
  mem_reservation: 64m
  mem_limit: 256m
```

For monitoring services:

Use stricter limits because they are secondary workloads.

---

# Additional Requirements

## Backend

Review:

* ASP.NET Core memory usage
* SignalR connection requirements
* thread pool behavior
* garbage collection configuration
* database connection pool settings

Ensure resource configuration does not create artificial bottlenecks.

---

## PostgreSQL

Review:

* shared_buffers
* max_connections
* work_mem
* connection pooling

Ensure database settings match the available VM resources.

---

## Docker Compose Quality

Ensure:

* Development and production compose files have different goals.
* Production configuration protects the application.
* Development configuration remains flexible.
* No service can accidentally consume all VM resources.

---

# Verification

After changes:

Run:

```bash
docker compose config
docker compose -f docker-compose.prod.yml config
```

Verify:

* containers start successfully
* backend is not CPU/memory throttled
* database has enough resources
* production can support expected 200 concurrent users

Document:

1. Why each resource value was chosen.
2. What happens when limits are reached.
3. Trade-offs between protection and performance.

Do not make unrelated code changes.
Only modify Docker/resource/performance-related configuration.

```
