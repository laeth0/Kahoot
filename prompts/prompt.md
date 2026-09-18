You are working directly inside my existing Kahoot-like project repository using Claude Code in VS Code.

Your task is to **carefully analyze, redesign, and update the project's functional and non-functional requirements** so that the existing application evolves from a single-tenant Kahoot-like system into a **production-ready, multi-tenant SaaS platform**.

This task is about **requirements and architecture documentation only**.

**Do NOT implement or modify application source code.**

The final requirements must be suitable for a large-scale SaaS platform that may eventually serve a **large number of tenants, registered users, games, participants, concurrent realtime connections, and requests**.

---

# 1. Primary Objective

Transform the existing Kahoot-like system into a documented **multi-tenant SaaS platform** while preserving the valid existing quiz/game behavior wherever multi-tenancy does not require a change.

The new requirements must explicitly define:

* SaaS platform boundaries
* tenants
* tenant lifecycle
* tenant users
* platform/system administrators
* roles and responsibilities
* authentication
* authorization
* tenant ownership
* tenant data isolation
* cross-tenant security
* quiz ownership
* game ownership
* participant behavior
* realtime isolation
* media ownership
* database integrity
* concurrency
* scalability
* security
* deployment
* platform administration

Do not make superficial changes such as merely adding `TenantId` to some requirements.

Think through how multi-tenancy affects **every functional and non-functional requirement**.

---

# 2. Read the Repository Before Changing Anything

Before modifying any documentation, thoroughly inspect the repository.

The known requirements are primarily under:

```text
docs/
├── functional-requirements/
│   ├── authentication.md
│   ├── game-lifecycle.md
│   ├── joining-and-lobby.md
│   ├── live-gameplay.md
│   ├── media-management.md
│   ├── platform-operations.md
│   ├── quiz-and-question-management.md
│   ├── realtime-features.md
│   ├── reconnection.md
│   ├── roles-and-access.md
│   └── scoring-and-leaderboards.md
│
├── non-functional-requirements/
│   ├── api-and-database-performance.md
│   ├── architecture-and-deployment.md
│   ├── capacity-and-scalability.md
│   ├── concurrency-reliability-and-data-integrity.md
│   ├── observability-and-verification.md
│   ├── realtime-performance.md
│   └── security-and-privacy.md
│
├── observability/
│
├── azure-vm-deployment.md
├── host-bootstrap-and-auth.md
├── media-handling.md
└── realtime-protocol.md
```

Do **not** assume this list is exhaustive.

Search the repository for other documents related to:

* authentication
* authorization
* roles
* users
* host
* admin
* system administrator
* account
* tenant
* organization
* quiz ownership
* game ownership
* participants
* WebSocket
* SignalR
* realtime communication
* reconnection
* media
* database
* PostgreSQL
* indexes
* deployment
* scaling
* rate limiting
* security
* Grafana
* Loki
* Prometheus
* observability
* monitoring

Read the relevant documents carefully before proposing or making changes.

---

# 3. Think Carefully About Every Requirement

Do not mechanically edit documents.

For every functional and non-functional requirement, ask:

1. Does this requirement assume a single tenant?
2. Who owns the affected resource?
3. Which role is allowed to perform the operation?
4. How is the tenant determined?
5. Can a client manipulate an ID to cross a tenant boundary?
6. Does the backend enforce the tenant boundary?
7. Does this requirement introduce a cross-tenant security risk?
8. Does it work when many tenants are using the platform concurrently?
9. Does it affect database constraints or indexes?
10. Does it affect realtime communication?
11. Does it affect authentication or authorization?
12. Does it affect scalability or capacity?
13. Does it conflict with another requirement?
14. Is it implementation-specific when it should instead describe a required system guarantee?

The result must be internally consistent and testable.

---

# 4. SaaS Role Model

The existing role model must be reconsidered.

The new system must clearly distinguish **platform-level roles**, **tenant-level roles**, and **players/participants**.

At minimum, analyze the following concepts.

## System Administrator

A **System Administrator** operates the SaaS platform itself.

Responsibilities may include:

* creating tenants
* viewing tenant accounts
* activating tenants
* suspending tenants
* disabling/deactivating tenants
* managing platform-level accounts
* performing platform-level administrative operations
* viewing platform-level tenant status and metadata necessary to operate the SaaS service

System Administrators are **not ordinary tenant users**.

Their permissions must be explicitly separated from tenant permissions.

Do **not** automatically give System Administrators unrestricted access to tenant quizzes, games, participants, media, answers, or other tenant content.

If such access could be required, ask me before defining it.

---

## Tenant

A **Tenant** represents one customer/account/organization using the SaaS platform.

Every tenant-owned resource must belong to exactly the appropriate tenant.

Examples may include:

* tenant users
* quizzes
* questions
* choices
* game sessions
* participants
* answers
* scores
* leaderboards
* uploaded media
* tenant configuration
* other tenant-specific data

The requirements must explicitly state which resources are tenant-owned.

---

## Tenant Users

Registered users working inside a tenant must operate only within their authorized tenant boundary.

Do not invent a complicated tenant role hierarchy without first checking the repository and asking me when necessary.

Determine whether the existing **Host** concept should become:

* a tenant user role,
* a capability of tenant users,
* or another tenant-scoped role.

If this is not clear from the current requirements, ask me.

---

## Host

The existing Host functionality should remain conceptually available unless the SaaS model requires changes.

A Host generally:

* creates and manages quizzes
* starts games
* controls game sessions
* views results
* manages players in games

However, a Host must now operate inside an appropriate tenant boundary.

Define this relationship clearly.

---

## Player / Participant

Players should remain separate from registered SaaS administration users unless I decide otherwise.

Review whether players continue to:

* join without accounts
* use a game PIN or join link
* use a nickname
* receive a secure session token
* reconnect without creating a SaaS account

If the existing requirements already make this clear, preserve that behavior unless multi-tenancy requires a change.

---

# 5. Explicit Role and Responsibility Matrix

Create or update the appropriate requirement documentation so that responsibilities are unambiguous.

The documentation should make it possible to determine whether an operation is:

* System Administrator only
* tenant-level administration
* normal tenant user / Host
* Player / Participant
* anonymous/public

The requirements must make authorization responsibilities explicit.

Do not rely on frontend visibility or UI controls for security.

The backend must enforce all authorization boundaries.

---

# 6. Critical Tenant Isolation Requirements

Tenant isolation is a **critical security invariant**.

The documentation must explicitly require that a user associated with Tenant A can never access Tenant B's protected data by manipulating client-controlled information.

This includes changing:

* resource IDs
* tenant IDs
* URLs
* query parameters
* route parameters
* request bodies
* quiz IDs
* question IDs
* game IDs
* participant IDs
* game PINs
* media paths
* reconnect tokens
* WebSocket/SignalR payloads
* hub methods
* connection parameters
* API parameters
* pagination/filter parameters
* cached keys
* or any other identifier supplied by a client

Tenant ownership and authorization must always be verified **server-side**.

Do not treat a client-supplied tenant identifier as proof of authorization.

---

# 7. Tenant Identification

Define how tenant context must affect application requests.

At the requirements level, consider:

* authenticated tenant identity
* tenant membership
* resolving the current tenant
* APIs
* SignalR/WebSocket connections
* background operations
* database queries
* caching
* media
* authorization
* logs where necessary for debugging/security

Do not prematurely dictate implementation details unless required.

The fundamental invariant should be that the server determines and validates the effective tenant context before accessing protected tenant resources.

---

# 8. Authentication and Authorization

Carefully review at least:

```text
docs/functional-requirements/authentication.md
docs/functional-requirements/roles-and-access.md
docs/host-bootstrap-and-auth.md
```

Update them for the SaaS model.

The new requirements must distinguish between:

* System Administrator authentication
* tenant-user authentication
* Host permissions
* Player/Participant identity
* tenant membership
* platform-level authorization
* tenant-level authorization
* ownership authorization

Consider whether the existing bootstrap-host model remains appropriate after introducing System Administrators and tenant accounts.

Do not assume that the existing bootstrap Host should automatically become the System Administrator.

If the correct bootstrap/account-creation flow is unclear, ask me.

---

# 9. Tenant Lifecycle

Define the tenant lifecycle requirements.

Consider states and operations such as:

* tenant creation
* provisioning
* activation
* suspension
* reactivation
* deactivation
* deletion, if supported

Do not invent lifecycle states unnecessarily.

The requirements must explain what each supported state means.

For example, if suspension exists, determine what happens to:

* user login
* existing authenticated sessions
* quiz editing
* new game creation
* already-running games
* player reconnection
* media access
* administrative access
* tenant data

If these behaviors are product decisions rather than obvious technical consequences, ask me.

---

# 10. SaaS Platform Administration

Review `platform-operations.md` and related documentation.

Add or restructure requirements for platform administration where appropriate.

Potential System Administrator capabilities include:

* create tenant
* retrieve tenant
* list tenants
* activate tenant
* suspend tenant
* reactivate tenant
* deactivate tenant
* manage platform-level administrative users, if needed
* inspect tenant status
* perform required platform operations

Do not invent:

* billing
* subscriptions
* pricing
* payment providers
* trial periods
* plan tiers
* invoicing
* commercial quotas

unless they already exist or I explicitly approve them.

---

# 11. Database and Data Integrity

Analyze the requirements from a multi-tenant database perspective.

Where applicable, define requirements for:

* tenant ownership of records
* tenant identifiers
* tenant-aware foreign keys
* tenant-aware unique constraints
* tenant-aware indexes
* tenant-aware queries
* cross-tenant relationship prevention
* referential integrity
* transactional integrity
* concurrency control
* tenant-aware deletion/deactivation behavior

Examples of questions that must be considered:

* Should quiz-title uniqueness, if any, be scoped by tenant?
* Should user usernames/emails be globally unique or tenant-scoped?
* Should game identifiers be globally unique or tenant-scoped?
* Should tenant ID participate in important database constraints?
* How do foreign keys prevent linking a resource from Tenant A to one from Tenant B?

Do not automatically answer product-level questions that are unresolved.

---

# 12. Do Not Prematurely Choose a Database Multi-Tenancy Strategy

Do not arbitrarily decide between:

* shared database/shared schema
* schema per tenant
* database per tenant

unless the current repository clearly establishes the decision or I explicitly approve one.

Requirements should first describe the guarantees that are necessary regardless of the physical implementation.

For example:

> All tenant-owned persistence operations shall prevent data belonging to one tenant from being returned, modified, linked, or deleted through the context of another tenant.

If the physical tenant-storage model is important enough that a decision is required now, ask me.

---

# 13. Quiz and Question Management

Review all quiz requirements for tenant ownership.

At minimum ensure that:

* quizzes belong to a tenant
* questions inherit the quiz's tenant boundary
* choices cannot cross tenant boundaries
* tenant users cannot retrieve another tenant's quizzes
* tenant users cannot modify another tenant's quizzes
* tenant users cannot start games from another tenant's quizzes
* publication remains tenant-scoped
* historical game snapshots preserve correct ownership
* query filtering cannot leak quizzes from another tenant

Preserve existing quiz functionality that remains valid.

---

# 14. Game Sessions

Every game session must be associated with the appropriate tenant.

Review:

* game creation
* game lifecycle
* quiz snapshotting
* state transitions
* participant isolation
* answer isolation
* scoring
* leaderboards
* reconnection
* host controls
* game history

A tenant user must not control or retrieve another tenant's game by guessing or modifying an identifier.

The system must preserve isolation even when many tenants run games concurrently.

---

# 15. Game PINs and Public Player Joining

Pay special attention to game PINs.

Players may not know or authenticate to a tenant before joining.

Determine whether PINs are:

* globally unique among joinable games,
* tenant-scoped,
* or resolved another way.

The existing system requires short active-game PINs.

Do not change this behavior without reason.

If multi-tenancy creates an unresolved product decision regarding PIN uniqueness or tenant discovery, ask me.

Public player joining must never expose protected tenant information.

---

# 16. Realtime / SignalR Isolation

This application relies heavily on realtime communication.

Review:

```text
realtime-features.md
reconnection.md
live-gameplay.md
scoring-and-leaderboards.md
realtime-protocol.md
```

Every realtime workflow must preserve tenant isolation.

Consider:

* SignalR connection authentication
* host subscriptions
* player sessions
* groups
* game identifiers
* reconnect operations
* presence events
* participant removal
* question broadcasts
* answer submissions
* leaderboard broadcasts
* final-game events

A connection associated with Tenant A must never receive protected realtime data belonging to Tenant B.

Do not rely solely on SignalR group names as an authorization boundary.

Authorization must happen before group membership or protected operations are granted.

Game groups must remain isolated even if two tenants somehow use similar human-visible identifiers.

---

# 17. Player Session Tokens and Reconnection

Review secure participant session behavior.

Ensure that:

* participant session tokens identify the correct player/game context
* a token cannot be reused to access another game
* a token cannot cross a tenant boundary
* reconnect cannot attach to another tenant's participant
* removed participants remain unable to reconnect
* session tokens remain server-validated
* connection IDs remain non-authoritative

Preserve the valid existing reconnection behavior.

---

# 18. Media

Review media handling from a tenant-isolation perspective.

Ensure that tenant users cannot:

* attach another tenant's media to their questions
* access protected media they are not authorized to use, if media is protected
* fabricate references to another tenant's files
* overwrite or delete another tenant's media

Determine whether stored media needs explicit tenant ownership.

If public game images remain publicly accessible during gameplay, document the security boundary carefully rather than assuming that public URL access means tenant ownership does not matter.

---

# 19. Caching

If the project uses or later introduces caching, tenant isolation must also apply to cache keys and cached results.

Requirements should prevent a cached response produced for one tenant from being served to another tenant.

Do not introduce Redis or another cache simply because the application is becoming SaaS.

Only define the isolation requirement unless an existing architecture decision requires a specific caching technology.

---

# 20. Concurrency and Data Integrity

Update concurrency and integrity requirements for a multi-tenant environment.

Consider:

* concurrent operations from different tenants
* duplicate answer prevention
* tenant-aware uniqueness constraints
* tenant-aware optimistic concurrency
* game-session isolation
* participant isolation
* score isolation
* state transitions
* database transactions
* cross-tenant foreign-key protection

Concurrency in one tenant must not incorrectly mutate state belonging to another tenant.

---

# 21. Security

Treat tenant isolation as a first-class security boundary.

Review the security requirements for:

* authentication
* authorization
* tenant impersonation
* insecure direct-object references
* cross-tenant access
* privilege escalation
* media access
* WebSocket authorization
* session-token handling
* rate limiting
* secret handling
* database access
* API access
* administrative operations

Platform-level System Administrator endpoints must not be accessible to ordinary tenant users.

Tenant users must not be able to grant themselves System Administrator privileges.

---

# 22. Large-Scale SaaS Requirements

This platform is intended to support **a large number of users**, not only a small single classroom deployment.

Review all non-functional requirements with this in mind.

Think beyond:

> "Can one game support 500 players?"

Also consider the SaaS platform as a whole:

* many tenants
* many tenant users
* many simultaneous games
* large numbers of concurrent players
* aggregate SignalR connections
* high concurrent API traffic
* simultaneous answer bursts across many games
* database connection pressure
* database contention
* media traffic
* tenant administration traffic
* mass reconnection
* startup/restart behavior
* application scaling
* horizontal scaling
* failure isolation
* noisy-neighbor effects between tenants
* rate-limit behavior for legitimate shared networks
* bounded queries and pagination
* resource exhaustion
* concurrency across tenants

Do not invent arbitrary capacity numbers without evidence or my approval.

Existing validated capacity targets, such as the current 500-player per-game target, should be preserved unless there is a documented reason to change them.

Where platform-wide SaaS capacity targets are missing, identify that gap and ask me if a concrete business target is needed.

---

# 23. Scalability

The new SaaS requirements must not accidentally lock the platform into assumptions that only work for one tenant or one small deployment.

Review:

```text
capacity-and-scalability.md
architecture-and-deployment.md
api-and-database-performance.md
realtime-performance.md
concurrency-reliability-and-data-integrity.md
```

Consider requirements related to:

* horizontal application scaling
* shared realtime delivery
* database scaling
* media storage
* stateless application instances where practical
* avoiding process-local authoritative tenant/game state
* connection distribution
* bounded database queries
* pagination
* background processing where justified
* failure isolation between tenants

Do not automatically introduce additional infrastructure without demonstrated requirements.

Do not introduce microservices merely because the system is becoming SaaS.

Preserve the modular-monolith architecture unless there is a strong documented reason to change it.

---

# 24. Rate Limiting and Noisy-Neighbour Protection

Review rate limiting for a SaaS deployment.

Rate limiting must:

* protect authentication endpoints
* protect public joining endpoints
* protect realtime answer operations
* protect administrative APIs
* tolerate legitimate classroom or organization traffic behind shared NAT
* avoid allowing one tenant to consume unreasonable platform resources
* remain compatible with large concurrent player bursts

Do not invent commercial quotas or subscription limits.

Technical protection against abuse/resource exhaustion is different from commercial plan quotas.

---

# 25. Pagination and Bounded Operations

A SaaS system may accumulate significantly more data than the original application.

Review list/query requirements.

Avoid requirements that implicitly return unbounded collections such as:

* all tenants
* all tenant users
* all quizzes ever created
* all games
* all historical participants
* all results

Where necessary, define bounded query behavior and pagination requirements.

Do not unnecessarily paginate small bounded game payloads when existing gameplay requirements need complete information.

---

# 26. Observability — Remove It

This is a critical instruction.

**I do NOT want an observability platform in this project.**

Do not introduce or preserve dependencies on:

* Grafana
* Loki
* Prometheus
* OpenTelemetry dashboards/stacks
* metrics scraping infrastructure
* observability dashboards
* observability agents
* dedicated monitoring stacks
* log aggregation stacks
* observability-specific deployment infrastructure

Review the **entire repository**, especially `docs/`, for these references.

The existing:

```text
docs/observability/
```

is not part of the intended architecture.

Remove obsolete observability documentation when safe to do so.

---

# 27. `observability-and-verification.md`

Carefully review:

```text
docs/non-functional-requirements/observability-and-verification.md
```

Do not blindly delete valuable requirements.

Separate:

### Remove

Requirements specifically dependent on:

* metrics dashboards
* Grafana
* Loki
* Prometheus
* metrics scraping
* centralized observability platforms
* observability dashboards/stacks

### Preserve or relocate where useful

Requirements related to:

* correctness verification
* load testing
* performance testing
* integrity testing
* automated verification
* health checks
* security validation
* release acceptance criteria
* deterministic testability
* structured application logging needed for debugging/security
* safe error handling

If the file becomes unnecessary after moving useful verification requirements elsewhere, delete it and fix all references.

---

# 28. Normal Logging Is Still Allowed

Removing observability does **not** mean removing ordinary application logging.

Normal logs may still be required for:

* debugging
* security events
* startup failures
* database failures
* authentication failures
* realtime delivery failures
* operational troubleshooting

Keep logging requirements where they are appropriate.

However, do not require Loki or another central logging/observability platform.

---

# 29. Health Checks Are Still Required

Do not remove application health checks merely because observability is being removed.

Existing endpoints such as:

```text
/health
/health/live
/health/ready
```

may remain useful for:

* container health
* reverse-proxy routing
* deployment readiness
* application startup verification

These are operational application features, not an observability stack.

---

# 30. Deployment

Review the deployment model carefully.

The current documentation references an Azure Linux VM, Docker Compose, Nginx, PostgreSQL, and durable media volumes.

Determine whether the existing deployment requirements are compatible with the expected SaaS scale.

Do not automatically redesign the infrastructure.

Instead:

1. preserve valid requirements,
2. identify assumptions that will not scale,
3. define scalability requirements,
4. identify architecture decisions that require clarification.

Do not introduce Kubernetes, microservices, Redis, Kafka, or cloud-managed services merely because they are common SaaS technologies.

Architecture decisions must be justified by requirements.

---

# 31. Requirement Style

Requirements should primarily describe **what the system must guarantee**, not unnecessarily prescribe implementation details.

Prefer:

> The system shall ensure that every tenant-owned query is constrained to the effective tenant associated with the authenticated request.

instead of:

> Add `Where(x => x.TenantId == tenantId)` to every EF query.

Implementation details may be included only when they represent an existing architectural constraint or are necessary to enforce a requirement.

Requirements must be:

* explicit
* testable
* internally consistent
* security-conscious
* implementation-aware
* suitable for later implementation
* precise about actors and authorization
* precise about failure behavior

Where useful, include:

* actor
* preconditions
* action
* expected behavior
* authorization
* invariants
* failure conditions
* edge cases

---

# 32. Terminology

Establish a clear terminology section or otherwise make terminology consistent throughout the documents.

Use consistent meanings for:

### Platform

The complete SaaS application operated by the provider.

### System Administrator

A platform-level administrative user.

### Tenant

A customer/account boundary within the SaaS platform.

### Tenant User

An authenticated registered user belonging to a tenant.

### Tenant ID

The durable identifier representing the tenant boundary.

### Tenant-Owned Resource

A resource whose access and lifecycle are constrained to one tenant.

### Host

The tenant-scoped user/capability responsible for authoring quizzes and controlling games, subject to the final role model.

### Player / Participant

A person participating in a live game, normally without a registered tenant account unless future requirements say otherwise.

### Game Session

An isolated run of a quiz.

Do not casually mix terms such as:

* tenant
* organization
* customer
* account
* workspace

unless the documentation explicitly defines them as equivalent.

---

# 33. Do Not Invent Product Decisions

Do not silently make decisions about:

* tenant hierarchy
* tenant administrators
* user invitations
* self-registration
* user membership in multiple tenants
* public quizzes
* quiz sharing across tenants
* tenant deletion
* tenant data retention
* tenant quotas
* subscriptions
* plans
* billing
* pricing
* trials
* System Administrator access to tenant content
* database-per-tenant vs shared database
* tenant-specific domains
* SSO
* social login

unless those decisions already exist in the repository.

When such a decision materially affects the requirements, ask me first.

---

# 34. Important Clarification Questions

After reading the repository, identify unresolved product and architecture decisions.

Do **not** ask questions whose answers are already clearly documented.

Potential areas include:

1. Can one tenant contain multiple registered users?
2. Does each tenant need its own Tenant Administrator/Owner role?
3. Can one user belong to multiple tenants?
4. Who creates tenant users?
5. Can tenants self-register?
6. Are tenants created only by the System Administrator?
7. How is the first user of a tenant provisioned?
8. What exactly happens when a tenant is suspended?
9. What happens to active games during suspension?
10. Is permanent tenant deletion supported?
11. If deletion is supported, is data deleted immediately, soft-deleted, or retained?
12. Can System Administrators inspect tenant content?
13. Are tenant quotas required?
14. Is billing in scope?
15. Can quizzes be shared between tenants?
16. Are any quizzes globally/publicly available?
17. Can players continue to join without accounts?
18. Must game PINs remain globally unique among active games?
19. Can tenant users use the same username/email in different tenants?
20. Which database multi-tenancy model should be used, if that decision must be made now?

Ask only questions that materially affect the design.

---

# 35. Required Workflow

Follow these phases in order.

## Phase 1 — Repository Analysis

Read all relevant requirements and architecture documentation.

Search the repository for terminology and architecture related to SaaS conversion and observability removal.

Understand the current system before changing files.

---

## Phase 2 — Requirement Inventory

Build an internal understanding of:

* current actors
* current resources
* ownership relationships
* authentication
* authorization
* game lifecycle
* realtime architecture
* database constraints
* scalability requirements
* security requirements
* deployment assumptions

Do not edit yet if major decisions remain unresolved.

---

## Phase 3 — Gap and Conflict Analysis

Determine:

* which requirements assume a single tenant
* which requirements require tenant scoping
* which new SaaS requirements are missing
* which role definitions need to change
* which permissions are ambiguous
* which database rules need tenant awareness
* which realtime rules need tenant isolation
* which performance requirements need SaaS-scale reconsideration
* which documents contain stale observability assumptions
* which requirements conflict with each other
* which terminology is inconsistent
* which architectural decisions are unresolved

---

## Phase 4 — Clarification

If material product or architectural decisions remain unresolved, **STOP before making assumption-dependent edits**.

Ask me a concise numbered set of questions.

For each question:

* explain briefly why it matters
* give reasonable alternatives when useful
* do not choose the answer for me
* do not ask questions already answered by the repository

Wait for my answers before making changes that depend on those decisions.

You may identify obvious documentation errors during this phase, but do not lock the requirements into an unapproved product decision.

---

## Phase 5 — Update the Documentation

Once the necessary decisions are known, modify the Markdown files **directly in the repository**.

Do not merely show me suggested replacements in chat.

Preserve requirements that remain correct.

Modify only what is necessary to:

* support SaaS
* support multi-tenancy
* clarify roles
* enforce tenant isolation
* improve large-scale readiness
* remove obsolete observability requirements
* eliminate contradictions
* improve testability

Create new documentation only when it improves organization.

For example, something such as:

```text
docs/functional-requirements/tenant-management.md
```

may make sense if tenant lifecycle requirements would otherwise make another document confusing.

Do not create unnecessary files.

---

## Phase 6 — Cross-Document Consistency Review

After editing, review all requirements as one system.

Check for:

* contradictory role definitions
* missing tenant ownership
* missing tenant authorization
* unscoped database operations
* unscoped realtime operations
* inconsistent terminology
* stale Host assumptions
* System Administrator privilege leaks
* cross-tenant access vulnerabilities
* stale single-tenant assumptions
* incorrect links
* duplicate requirements
* missing edge cases
* unbounded SaaS operations
* incorrect scalability assumptions

---

## Phase 7 — Observability Cleanup Verification

Search the repository again for:

```text
Grafana
Loki
Prometheus
observability
metrics scraping
dashboard
```

Review every remaining match.

Remove remaining unwanted dependencies and stale references.

Do not remove the word `observability` blindly when it appears in history, comments, or contexts that should instead be intentionally rewritten.

The resulting architecture must **not depend on an observability stack**.

---

## Phase 8 — Final Validation

Before finishing, confirm that:

* System Administrator responsibilities are explicit.
* Tenant responsibilities are explicit.
* Tenant-user/Host responsibilities are explicit.
* Player responsibilities are explicit.
* Platform operations are separated from tenant operations.
* Tenant ownership is explicit.
* Cross-tenant access is prohibited.
* API authorization is tenant-aware.
* Realtime authorization is tenant-aware.
* Database integrity is tenant-aware.
* Media ownership is tenant-aware.
* Reconnection is tenant-safe.
* Game PIN behavior is clearly defined or flagged for clarification.
* Large-scale SaaS concerns have been addressed.
* Existing valid gameplay behavior has been preserved.
* Observability-platform dependencies have been removed.
* Grafana has been removed.
* Loki has been removed.
* Prometheus has been removed.
* Health checks and useful verification requirements remain.
* Documentation links are valid.
* Requirements are internally consistent.

---

# 36. Final Response

After all approved modifications are complete, provide a concise summary containing:

1. **Files modified**
2. **Files created**
3. **Files deleted**
4. **Role model changes**
5. **System Administrator responsibilities**
6. **Tenant responsibilities**
7. **Tenant User / Host responsibilities**
8. **Player responsibilities**
9. **Major multi-tenancy requirements added**
10. **Major security/isolation guarantees added**
11. **Database/data-integrity changes**
12. **Realtime multi-tenant changes**
13. **Large-scale/scalability changes**
14. **Existing requirements preserved**
15. **Observability/Grafana/Loki/Prometheus content removed**
16. **Verification requirements preserved or relocated**
17. **Architectural/product decisions made from my clarification answers**
18. **Remaining open questions or risks**

Do not paste every modified Markdown document unless I explicitly request it.

---

# 37. Non-Negotiable Constraints

You must follow these rules:

* Do not implement application code.
* Modify requirements/documentation only.
* Read the repository before editing.
* Think carefully about every functional requirement.
* Think carefully about every non-functional requirement.
* Do not perform a superficial `TenantId` conversion.
* Explicitly define roles and responsibilities.
* Treat tenant isolation as a critical security boundary.
* Enforce authorization server-side in the requirements.
* Do not trust client-supplied tenant/resource identifiers.
* Preserve valid Kahoot-style gameplay behavior.
* Design requirements for a large-scale SaaS platform.
* Do not arbitrarily invent capacity targets.
* Do not invent billing or subscriptions.
* Do not invent product rules.
* Do not choose a database multi-tenancy strategy without sufficient context or my approval.
* Do not introduce microservices without a requirement.
* Do not introduce Redis, Kafka, Kubernetes, or similar infrastructure merely because this is SaaS.
* Do not introduce an observability replacement.
* Do not use Grafana.
* Do not use Loki.
* Do not use Prometheus.
* Remove obsolete observability-stack requirements and references.
* Preserve normal application logging where useful.
* Preserve health/readiness checks where useful.
* Preserve testing, correctness, performance, security, and integrity verification.
* Ask me before making material assumptions.
* Do not ask me questions that the repository already answers.
* Modify the actual repository files after the requirements are sufficiently clear.

---

# Start Now

Begin with **Phase 1: Repository Analysis**.

Read the existing functional requirements, non-functional requirements, architecture documents, authentication documentation, realtime protocol, deployment documentation, and any related files you discover.

Then perform the gap/conflict analysis.

**Do not immediately rewrite the requirements.**

First determine whether any important SaaS, role, tenant-lifecycle, security, database, realtime, or scalability decisions require my clarification.

If clarification is required, stop and ask me a concise numbered list of only the important questions before making assumption-dependent changes.
