Act as a Senior .NET / ASP.NET Core Software Engineer and perform a deep production-quality review of this repository, with special focus on the Authentication and Account Security features that are currently implemented.

This is both a real application and a learning project. Do not merely make the code compile. I want you to understand the architecture, requirements, security model, concurrency requirements, and existing conventions before changing anything.

Your task is to inspect the repository thoroughly, identify real problems, and fix them where justified.

# 1. Read the repository before editing anything

Before making any code changes, inspect the repository carefully.

Start by reading and following all repository instructions, especially:

- root `AGENTS.md`
- `backend/AGENTS.md`
- `CLAUDE.md`
- OpenWolf instructions if present and required by repository instructions
- relevant README/configuration files
- solution/project files
- existing architecture and folder structure

Then inspect the requirements under `docs/`.

For authentication and account security, pay especially close attention to:

- `01-roles-and-access.md`
- `02-authentication.md`
- `03-account-management.md`
- `10-realtime-and-protocol.md` where authentication/revocation affects realtime connections
- `11-reconnection.md` where credentials/session authority matters
- `12-platform-operations-and-health.md`
- `13-architecture-and-deployment.md`
- `14-verification-and-testing.md`

Do not assume generic best practices override these requirements.

The requirements are the authoritative product contract.

If existing code conflicts with the requirements, fix the code when the intended behavior is clear.

However, if my requested change itself conflicts with a normative requirement or repository instruction, follow the repository's conflict-handling rules instead of silently changing the specification.

# 2. Understand the entire architecture

Inspect all four Clean Architecture projects:

- `Kahoot.Domain`
- `Kahoot.Application`
- `Kahoot.Infrastructure`
- `Kahoot.Api`

Understand:

- project references
- dependency direction
- domain entities
- EF Core model/configurations
- Application abstractions
- CQRS/MediatR conventions
- Vertical Slice organization
- API controllers
- middleware
- dependency injection
- configuration/options
- migrations
- authentication/authorization
- error/result handling
- health checks
- Docker/deployment configuration

Do not review Auth files in isolation.

Trace their dependencies, callers, persistence behavior, configuration, database constraints, and HTTP behavior.

# 3. Deeply audit the complete Auth implementation

Find every file involved directly or indirectly in authentication, including but not limited to:

- Register
- Login
- Refresh
- Logout
- Logout All
- Change Password
- JWT generation
- JWT validation
- refresh-token generation
- refresh-token hashing
- refresh-token rotation
- refresh-token family lifetime
- refresh-token reuse detection
- refresh race handling
- password hashing
- dummy password verification
- password hashing concurrency protection
- login rate limiting
- username normalization
- current-user resolution
- account status validation
- token security version validation
- authorization
- CSRF protection
- Origin / Referer validation
- refresh-token cookies
- account suspension effects
- configuration/options validation
- database persistence
- concurrency behavior
- transactions
- exception/error handling

Trace each flow from:

HTTP request
→ Controller
→ Command / Query
→ Validator
→ Handler
→ Application abstraction
→ Infrastructure implementation
→ EF Core / PostgreSQL
→ Result
→ HTTP response

Verify the entire flow rather than reviewing individual classes separately.

# 4. Requirements compatibility

Compare the implementation carefully against the documented requirements.

Pay particular attention to requirements such as:

## Registration

Verify:

- username validation
- username NFKC normalization
- culture-independent case folding
- global normalized username uniqueness
- password rules
- password zero-normalization requirement
- Host account creation
- correct initial status
- correct initial TokenSecurityVersion
- correct HTTP response
- registration does NOT automatically log the user in
- transaction/concurrency safety

## Login

Verify:

- normalized username lookup
- generic invalid-credential behavior
- account enumeration resistance
- dummy password verification for nonexistent usernames
- suspended-account concealment
- IP rate limiting
- username backoff
- password hashing concurrency protection
- correct JWT claims
- correct access-token lifetime
- refresh-token generation
- cryptographically secure randomness
- SHA-256 refresh-token persistence
- refresh-token family creation
- cookie security
- correct response contract

## JWT

Verify:

- issuer validation
- audience validation
- signing key validation
- lifetime validation
- zero/appropriate clock skew
- role claims
- account ID claims
- tenant claims if required by current schema/requirements
- TokenSecurityVersion
- account status freshness
- revocation behavior
- failure responses
- no credential leakage

Pay special attention to whether the current JWT validation strategy causes unnecessary database work on every authenticated request.

Do not optimize this blindly.

Analyze it against the requirement for fast authority freshness and revocation propagation, and choose the simplest architecture that still satisfies the documented guarantees.

## Refresh token rotation

Verify:

- cookie token input
- permitted body fallback
- CSRF requirements
- Origin/Referer validation
- hashed lookup
- expiry
- revocation
- absolute family lifetime
- atomic rotation
- token family preservation
- 10-second race grace behavior
- `409 Auth.RefreshRace`
- no replacement secret leakage during a race
- malicious reuse after the grace period
- entire family revocation
- TokenSecurityVersion increment
- concurrency correctness when two refreshes happen simultaneously
- transaction isolation / unique constraints where necessary

This flow is highly security-sensitive. Analyze race conditions carefully.

## Logout

Verify:

- correct CSRF behavior when cookie authentication is used
- token-family revocation
- idempotency
- cookie deletion
- no token enumeration
- correct `204 No Content`

## Logout All

Verify:

- authenticated Host/SystemAdmin only
- all refresh families revoked
- TokenSecurityVersion incremented
- old JWTs become invalid
- cookie cleared
- correct transaction boundary

## Change Password

Verify:

- authenticated caller derived server-side
- current password verification
- new password validation
- new hash generation outside unnecessarily long DB locks
- all refresh token families revoked
- TokenSecurityVersion incremented
- optimistic concurrency behavior
- account status
- correct race handling
- correct response contract

# 5. Security audit

Perform a focused security review.

Check for:

- broken authentication
- broken authorization
- IDOR
- tenant-boundary bypasses
- account enumeration
- timing attacks
- refresh-token replay
- token theft amplification
- race conditions
- improper token storage
- plaintext token persistence
- weak randomness
- JWT misconfiguration
- insecure cookies
- CSRF weaknesses
- improper Origin validation
- CORS mistakes
- header spoofing
- password normalization bugs
- sensitive information in logs
- secrets committed to source-controlled configuration
- unsafe exception details
- mass assignment
- SQL injection
- denial-of-service opportunities
- unbounded expensive password hashing
- rate-limit bypasses
- concurrency bypasses
- stale authorization
- privilege escalation

Do not add security mechanisms merely because they are popular.

Only add mechanisms that are required by the documented threat model or that solve an actual weakness in this implementation.

# 6. Performance and scalability audit

Review Auth behavior under the documented production workload.

Look for:

- unnecessary database round trips
- unnecessary tracking queries
- full-table scans
- missing indexes
- repeated normalization
- repeated hashing
- inefficient LINQ
- N+1 queries
- overly long transactions
- excessive locks
- database operations while password hashing is executing
- unnecessary allocations
- blocking synchronous I/O
- incorrect async usage
- cancellation token propagation
- connection pool pressure
- expensive work performed on every JWT-authenticated request
- race conditions under multiple application instances
- process-local state that incorrectly assumes a single server instance

Do not optimize hypothetical micro-performance issues.

Focus on meaningful bottlenecks given the documented scale and SLOs.

# 7. PostgreSQL and EF Core review

This project uses PostgreSQL.

Review all relevant persistence code for correct PostgreSQL behavior.

Check:

- EF Core queries
- indexes
- unique constraints
- transactions
- isolation behavior
- optimistic concurrency
- `ExecuteUpdateAsync` usage
- locking where appropriate
- race-safe uniqueness enforcement
- normalized username lookup performance
- refresh-token hash lookup performance
- token-family queries
- appropriate AsNoTracking usage
- query projection
- cancellation token propagation

Do not introduce SQL Server-specific packages, assumptions, or APIs.

Use Npgsql/PostgreSQL-compatible solutions only.

If a database/schema change is truly required:

1. Do NOT modify an existing migration.
2. Add a new migration.
3. Update `projectSchema.dbml` in the same task.
4. Keep migration safety and deployment compatibility in mind.

Do not create a migration merely because one could theoretically improve something.

# 8. Clean Architecture review

Ensure responsibilities remain in the correct layers.

## Domain

Should contain core domain concepts and invariants that genuinely belong to the business model.

Do not put:

- HTTP concerns
- EF implementation concerns
- ASP.NET Core concerns
- Infrastructure-specific code

into Domain.

## Application

Should contain:

- use cases
- vertical feature slices
- commands/queries
- handlers
- validators
- result/error contracts
- abstractions representing real architectural boundaries
- application orchestration

Business/use-case behavior should not leak into controllers.

## Infrastructure

Should contain:

- EF Core persistence
- PostgreSQL-specific implementation
- password hashing implementation
- JWT/token implementation where appropriate
- technical external concerns

## API

Should contain:

- HTTP concerns
- controllers/endpoints
- authentication/authorization registration
- middleware
- cookies
- request/response transport behavior
- API-specific configuration

Keep controllers thin.

Do not redesign the architecture unless there is a concrete problem.

# 9. Vertical Slice Architecture review

Review the Auth feature structure.

Each feature should be easy to locate and understand.

For example:

`Features/Auth/Register/...`

`Features/Auth/Login/...`

`Features/Auth/Refresh/...`

`Features/Auth/Logout/...`

`Features/Auth/LogoutAll/...`

`Features/Auth/ChangePassword/...`

Keep feature-specific request, response, command, handler, validator, and errors close to the feature when appropriate.

Keep truly shared Auth behavior shared rather than duplicating it between slices.

Do not force everything into shared folders.

Do not create generic abstractions merely to reduce one or two lines of duplication.

# 10. Clean Code review

Review every affected class for:

- naming
- cohesion
- method size
- responsibility
- readability
- duplicated logic
- unnecessary comments
- magic values
- unnecessary state
- hidden dependencies
- excessive null-forcing
- poor exception handling
- inconsistent async patterns
- unnecessary abstractions
- unnecessary interfaces
- dead code
- unused imports
- duplicated validation
- duplicated constants
- unnecessary wrappers
- inconsistent naming

Preserve the repository convention of explicit constructor injection with private readonly fields.

Do NOT convert DI classes to primary constructors.

Comments should explain important reasoning, security constraints, concurrency rules, or non-obvious behavior.

Remove comments that merely repeat the code.

# 11. Dependency Injection review

Inspect all registrations and lifetimes.

Verify Scoped / Singleton / Transient choices intentionally.

Consider:

- DbContext lifetime
- thread safety
- mutable state
- rate limiter lifetime
- password hashing concurrency gate
- TimeProvider
- current user
- token generators
- options

Do not choose Singleton just for performance.

Do not create interfaces solely to make mocking easier.

Also respect the repository's `ServiceCollectionExtension` convention:

- each installer must have one focused responsibility
- do not create one giant installer
- core framework registrations belong in `Program.cs`
- feature-specific registration should remain focused and obvious

# 12. Configuration and Options Pattern

Review:

- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- Docker Compose environment variables
- relevant `.env.example`
- options classes and registrations

Ensure:

- no production secrets are committed
- production fails fast when critical configuration is absent
- development configuration remains convenient but safe
- values are not unnecessarily duplicated
- one source of truth exists for token lifetimes and related settings
- options are validated at startup where appropriate
- environment-variable overrides use ASP.NET Core conventions correctly
- JWT signing keys have appropriate entropy
- invalid Base64 configuration fails clearly
- cookie configuration matches environment expectations

Do not introduce .NET User Secrets because this repository explicitly does not use them.

Do not expose actual secret values in your final report.

# 13. Error handling and API contracts

Review the complete error path:

Application Error
→ Result
→ ApiController
→ ProblemDetails
→ GlobalExceptionHandler

Ensure responses match the documented canonical errors.

Review:

- status code
- error code
- title
- detail
- RFC 7807 structure
- validation errors
- authentication challenge responses
- authorization failures
- rate limiting
- database outage behavior

Avoid duplicated ProblemDetails construction when a simple existing mechanism can be reused.

But do not introduce a large abstraction merely to remove small duplication.

Ensure unexpected infrastructure/internal exceptions do not leak implementation details.

# 14. Concurrency review

This is particularly important.

Analyze real concurrent operations such as:

- duplicate registration with same normalized username
- two simultaneous logins
- refresh vs refresh
- refresh vs logout
- refresh vs logout-all
- refresh vs password change
- refresh vs account suspension
- JWT request vs suspension
- two simultaneous password changes
- password change vs login
- refresh-token cleanup vs refresh
- multiple application instances processing the same account

Use database constraints and transaction boundaries rather than process-local locks when correctness must hold across multiple backend instances.

Do not add locks or distributed coordination unless actually required.

# 15. Code organization

After understanding the existing code, clean up Auth-related file/folder organization when doing so materially improves consistency with:

- Clean Architecture
- Vertical Slice Architecture
- existing repository conventions

Do not reorganize unrelated features.

Do not rename or move files just for aesthetics.

Every move must improve discoverability, responsibility, dependency direction, or consistency.

Check namespaces after file moves.

# 16. Remove redundancy carefully

Search for duplicated:

- password validation
- username normalization
- token hashing
- token lifetime calculation
- cookie constants
- error definitions
- role checks
- refresh revocation logic
- options values
- authorization checks

Consolidate only when the duplication represents the same concept.

Do not create a generic `Helper`, `Utility`, `Manager`, `BaseService`, or abstraction without a concrete reason.

Prefer small explicit code over clever reusable frameworks.

# 17. Do not over-engineer

Before adding any:

- interface
- service
- repository
- factory
- strategy
- decorator
- wrapper
- helper
- generic abstraction
- middleware
- dependency
- NuGet package

ask:

1. What concrete problem does this solve?
2. Does that problem currently exist?
3. Is there a simpler solution?
4. Does the project already have a solution?
5. Is the added complexity justified?

If not, do not add it.

Use existing .NET/runtime/repository capabilities before adding dependencies.

# 18. Testing constraint

Follow the repository instructions concerning tests.

The current backend repository explicitly requires the `test/` directory to remain empty except for `.gitkeep`.

Therefore:

- do NOT create test projects
- do NOT create unit tests
- do NOT create integration tests
- do NOT add testing packages

You should still reason against the acceptance tests in the requirements and use them as verification scenarios while reviewing the implementation.

# 19. Verification

After making changes, run the strongest verification allowed by this repository.

At minimum, from `backend/` run:

`dotnet build Kahoot.slnx`

Also run when applicable:

`dotnet format Kahoot.slnx --verify-no-changes`

Inspect the final diff.

If migrations were added, verify they are consistent with the model and `projectSchema.dbml`.

Do not claim anything was verified unless you actually ran the verification successfully.

Do not hide build, formatting, migration, or tooling failures.

# 20. Scope discipline

Although I want you to understand the entire repository, the implementation focus is the currently built Authentication functionality and the code directly supporting it.

You may change shared infrastructure or configuration when Auth genuinely requires it.

Do not refactor unrelated quiz/game/media functionality just because you notice stylistic differences.

If you find serious unrelated security/data-integrity problems, report them separately rather than expanding this task into a repository-wide rewrite.

# 21. Important behavior while working

Do not immediately start rewriting code.

First:

1. inspect repository instructions
2. inspect requirements
3. inspect architecture
4. locate every Auth-related file
5. trace complete request flows
6. inspect persistence/configuration/dependencies
7. compare implementation to normative requirements
8. identify concrete problems
9. choose the smallest correct fixes
10. implement them

Prefer repository evidence over assumptions.

Do not ask me questions that can be answered by inspecting the repository.

# 22. Final review

Before finishing, inspect the complete diff and verify:

- every changed file was necessary
- no unrelated behavior changed
- no secrets were introduced
- no security requirement was weakened
- dependency direction remains correct
- Auth requirements remain satisfied
- concurrency semantics remain correct
- PostgreSQL behavior is correct
- configuration is consistent
- code remains easy to understand
- no dead code remains
- no unused imports remain
- no unnecessary abstractions were introduced
- no duplicate implementation remains where consolidation is clearly appropriate
- file organization remains consistent
- existing migrations were not modified
- `test/` remains untouched except for its existing `.gitkeep`

Ask yourself one final question:

"Can this implementation be simpler without sacrificing correctness, security, clarity, maintainability, performance, scalability, or required behavior?"

If yes, simplify it.

# 23. Final response format

After the work is complete, give me a concise but technically useful report containing:

## Findings

Describe the important problems you found.

For each significant problem explain:

- what was wrong
- why it mattered
- whether it was a correctness, security, performance, architecture, concurrency, maintainability, or configuration issue

## Changes Made

List the meaningful changes and explain briefly why each was necessary.

## Architecture

Explain any file moves, dependency changes, abstractions added/removed, or important placement decisions.

If you intentionally kept something where it already was, mention that when the decision is non-obvious.

## Security

Summarize the Auth/security issues reviewed and any fixes made.

## Performance / Scalability

Summarize meaningful database/query/concurrency/performance improvements.

Do not list trivial micro-optimizations.

## Requirements Compatibility

Identify the important requirement IDs or requirement sections that were verified or fixed.

## Verification

Report exactly which commands were run and their results.

## Remaining Issues

List anything that could not safely be fixed within this task, requires a product decision, conflicts with requirements, or could not be verified.

Do not say the implementation is "perfect", "fully secure", "fully tested", or "production-ready" unless the evidence actually proves that.

The objective is not to maximize the number of changes.

The objective is to leave the Authentication implementation as the smallest correct, secure, clean, maintainable, performant, scalable, requirements-compatible solution that fits the existing Clean Architecture and Vertical Slice Architecture.