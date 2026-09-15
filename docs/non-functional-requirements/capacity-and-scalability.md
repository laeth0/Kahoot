# Capacity and Scalability

## Purpose

Define the number of users the platform must support and the conditions under
which the deployment may scale beyond one backend instance.

## NFR-1: Capacity and Performance

### Required Capacity

- A single game must support all **500 reserved participant seats** allowed by
  the backend.
- A live game must support up to **500 concurrently connected players** without
  losing accepted answers, duplicating scores, corrupting state, or violating
  game-session isolation.
- The platform must accept a burst of **500 answer submissions in approximately
  one second** for one open question.
- The platform must also support at least **10 simultaneous games with 50
  connected players each** so that aggregate capacity is not limited to one
  large room.
- Capacity limits must be enforced by the server. Clients must not be relied on
  to prevent excess joins or connections.

### Capacity Acceptance

- The 500-player single-game profile is the release capacity target. A smaller
  development or smoke-test profile does not prove this target.
- Tests must run against the same application topology and materially equivalent
  compute, database, proxy, and network configuration as the release environment.
- Capacity is acceptable only when the latency, error-rate, real-time delivery,
  and data-integrity targets in the other non-functional requirement documents
  pass during the same test run.
- Stress tests may continue from 50 through 750 users to identify the failure
  boundary, but overload results above the supported 500-player limit do not
  redefine the release target.

## NFR-10: Scaling Strategy

- Begin with one backend replica and measure the actual bottleneck before adding
  distributed infrastructure.
- PostgreSQL remains the durable source of truth at every scale.
- If a single replica cannot satisfy the measured requirements, optimize the
  proven bottleneck before adding replicas.
- Multiple backend replicas require a supported SignalR scale-out mechanism,
  such as Redis backplane or Azure SignalR Service, so groups and broadcasts work
  across instances.
- Any process-local guards or presence data must either remain non-authoritative
  or move to shared infrastructure before horizontal scaling.
- The modular-monolith design must avoid coupling that prevents future horizontal
  scaling, but Redis, a backplane, or extra replicas must not be introduced
  without load-test evidence that they are needed.

## Verification

- Run the single-game connection, answer-burst, multiple-game, reconnection, and
  endurance scenarios in `load-tests/`.
- Record the deployment version, environment sizing, player count, test duration,
  latency percentiles, error rate, resource saturation, and database integrity
  results with every capacity result.
- Treat a run as failed when any required correctness invariant is violated,
  regardless of its latency result.
