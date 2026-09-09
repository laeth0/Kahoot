# Project-Wide Engineering Standards

These rules apply to every part of the repository. More specific `AGENTS.md` files may
exist for individual areas, stacks, or subdirectories; when one applies to the code
you are changing, follow it in addition to this file. When rules appear to conflict,
follow the more specific rule without weakening the requirements in this file.

## Agent Conduct and Requirements

- Read the request, the relevant instructions, and the affected code before making
  changes. Understand the current behavior, architecture, conventions, and nearby
  dependencies first.

> If you encounter anything that is missing, unclear, ambiguous, contradictory, or
> requires a product/technical decision that is not explicitly defined in the
> requirements or existing codebase, stop and ask me before making assumptions or
> implementing a solution. Ask any questions you need in order to proceed correctly.

- Make reasonable implementation decisions only when they follow clearly from the
  established code and do not change product behavior or materially expand scope.
- Keep changes focused on the requested outcome. Do not perform unrelated refactors,
  dependency upgrades, migrations, formatting sweeps, or cleanup.
- Preserve user changes and unrelated work in a dirty working tree. Never discard or
  overwrite work that is outside the task.
- Before completion, inspect the final diff and verify that every changed line is
  intentional, necessary, and consistent with these standards.

## Repository Awareness and Change Discipline

- Follow the project's existing architecture, conventions, patterns, naming,
  formatting, and coding standards for both C# and React code.
- Before adding, modifying, or removing functionality, inspect the relevant code and
  understand how the change may affect controllers, EF Core queries and relationships,
  DTO contracts, authentication and authorization, API consumers, React state and
  hooks, routing, shared components, and user workflows.
- Preserve existing behavior and backward compatibility unless a change is explicitly
  required. Identify and communicate unavoidable breaking changes.
- Reuse or extend suitable existing code before adding parallel implementations.
- When replacing or removing behavior, also remove obsolete references, imports,
  configuration, documentation, and dead code within the affected scope.
- Preserve clear dependency direction and avoid unnecessary coupling between data
  access, domain behavior, API contracts, frontend state, and presentation. Carefully
  improve structural problems that directly affect the requested change, but do not
  perform unrelated large-scale refactors.
- Do not introduce new layers, wrappers, helpers, services, patterns, or infrastructure
  without a concrete benefit in the current scope.

## Code Quality and Design

- Write production-ready code that is correct, readable, maintainable, testable, and
  appropriately efficient.
- Apply separation of concerns, DRY, SOLID, KISS, and YAGNI as judgment tools, not as
  reasons to add ceremony. Choose the simplest design that fully meets the requirement.
- Give every module, class, method, function, hook, and component one clear
  responsibility. Keep methods and functions focused, use guard clauses and clear
  control flow, and avoid deeply nested logic.
- Prefer clear, explicit implementations over clever, overly generic, or speculative
  abstractions.
- Preserve strong typing where the language supports it. Do not bypass type safety
  without a narrow, documented reason.
- Remove dead code, unused imports, unused variables, debugging statements, duplicated
  logic, obsolete or unnecessary comments, temporary workarounds, and abstractions that
  do not provide clear value from every file you modify.
- Avoid shared mutable global state. Make ownership, lifecycle, and dependency direction
  clear.

## Remove Unused Code

- Remove any function or class that is not used anywhere in the project.
- Before removing anything, verify across the entire project that it has no references,
  imports, calls, inheritance usage, registrations, dynamic usage, or other dependencies.
- Do not remove code based only on a simple text search if it could be used indirectly
  or dynamically.
- Only delete a function or class when you are confident it is truly unused and removing
  it will not affect project behavior.
- After removal, check for broken imports, references, tests, builds, or type/lint errors
  caused by the deletion.

## Naming and Readability

- Use consistent, intention-revealing, domain-specific names. Avoid vague abbreviations
  and generic placeholders when a precise name is available.
- Name operations for what they do and booleans for the condition they represent.
- Reserve short names for conventional, very small scopes where their meaning is
  immediately clear.
- Prefer self-explanatory code. Add comments only for non-obvious intent, business
  rules, constraints, tradeoffs, or workarounds that clearer code cannot express.
- Do not add comments that merely restate the code. Keep necessary comments accurate
  when behavior changes.
- Replace repeated magic values with named constants, types, or configuration when the
  name adds meaning or the value has a shared policy role.

## Contracts and Boundaries

- Keep public interfaces and data contracts explicit, predictable, and as small as
  practical.
- Validate assumptions where data crosses a trust, process, module, or persistence
  boundary. Treat all external input as untrusted.
- Keep responsibilities separated across presentation, orchestration, business rules,
  integration, and persistence boundaries as appropriate to the repository.
- Do not expose internal implementation details through public contracts unless they
  are intentionally part of the contract.
- Coordinate contract changes across all affected producers, consumers, validation,
  documentation, and tests in the same task.

## Error Handling and Reliability

- Handle expected failure modes deliberately and consistently with the repository's
  error model. Never silently swallow failures or use empty catch blocks.
- Preserve useful diagnostic context while presenting safe, actionable errors at the
  appropriate boundary.
- Let unexpected failures reach the established centralized handling mechanism unless
  local recovery is both safe and meaningful.
- Account for relevant edge cases, cancellation, cleanup, partial failure, recovery,
  and resource lifetime.
- Make retries, fallbacks, and degraded behavior explicit. Do not retry operations that
  are unsafe to repeat or likely to amplify an incident.
- Avoid fragile workarounds. If a temporary limitation is unavoidable, constrain its
  scope and document the reason and removal condition.

## Security and Privacy

- Use secure defaults and established platform protections. Do not invent custom
  security mechanisms when a proven repository or platform capability exists.
- Follow least privilege for identities, permissions, data, files, services, and
  configuration.
- Validate, normalize, constrain, and when appropriate sanitize untrusted data at the
  correct boundary. Apply output encoding for the destination context.
- Prevent relevant injection, authorization bypass, unsafe redirects, path traversal,
  malicious file handling, request forgery, and sensitive-data exposure.
- Never hardcode or commit secrets, credentials, private keys, access tokens, or
  production connection details. Load sensitive and environment-specific values from
  the repository's approved configuration or secret-management mechanism.
- Do not expose credentials, tokens, personal data, internal paths, or sensitive
  implementation details through logs, errors, responses, URLs, artifacts, or source.
- Never weaken authentication, authorization, transport security, validation,
  sanitization, or other protections merely to make a feature work.
- Review the security and privacy impact of new dependencies, integrations, permissions,
  data collection, storage, file handling, and external communication.

## Configuration, Dependencies, and Compatibility

- Keep environment-specific values outside source code and use the existing typed or
  validated configuration approach where available.
- Prefer language, platform, and repository capabilities before adding a dependency.
- Add a dependency only when its current-scope benefit justifies its security,
  maintenance, compatibility, size, licensing, and operational costs.
- Inspect pinned versions before using framework or library APIs. Do not silently
  upgrade dependencies, adopt preview features, or require newer runtimes.
- Use authoritative documentation for the versions in the repository when behavior is
  version-sensitive or uncertain. Preserve local architectural decisions unless a
  change is authorized.
- Keep generated files, caches, build output, local settings, secrets, and machine-
  specific artifacts out of version control unless the repository explicitly tracks
  them.

## Version-Control Hygiene

- Keep each change set cohesive and limited to the requested work. Do not mix unrelated
  formatting, generated output, or cleanup into the same diff.
- Inspect status and diffs before delivery. Distinguish pre-existing user changes from
  task changes and do not revert, stage, amend, or commit unrelated work.
- Use the repository's ignore rules and contribution conventions. Do not commit local
  credentials, environment files, editor state, temporary files, or build artifacts.
- When commits are requested, use concise messages that describe the behavioral intent
  of the change. Do not rewrite shared history without explicit authorization.
- Resolve merge conflicts by understanding both sides and preserving intended behavior;
  never choose a side mechanically or discard work to make the conflict disappear.

## Git Hook Conventions

- Prefer repository-managed Git hooks in a root-level `.githooks/` directory, and
  document or configure their activation with `git config core.hooksPath .githooks`.
- Before creating or modifying `pre-commit` or `pre-push`, inspect the repository
  structure, frameworks, package managers, solution files, and existing scripts. Do
  not assume a fixed folder structure or embed project-specific paths or commands
  before inspection; the repository may contain ASP.NET Core, React, or both and may
  use a monorepo structure.
- Use `pre-commit` to detect relevant staged files and run the appropriate formatter
  for each affected part of the repository without formatting unrelated files. Reuse
  existing formatting scripts, tooling, and project conventions where available.
- Use `pre-push` to determine the current branch and its upstream safely, and prevent
  pushing when the local branch is behind its remote tracking branch and the user must
  pull first. Handle repositories, branches, and environments without an upstream
  gracefully.
- Keep hooks portable, maintainable, and as simple as possible.

## Performance and Scalability

- Design for the expected workload and realistic growth without over-engineering for
  hypothetical scale.
- Avoid obvious waste: repeated work, redundant I/O, unnecessary allocations, excessive
  payloads, unbounded operations, avoidable sequential waits, and needless round trips.
- Choose data structures, algorithms, batching, caching, pagination, concurrency, and
  loading strategies based on demonstrated or clearly foreseeable needs.
- Do not add caching, memoization, parallelism, or other optimization machinery by
  default. Measure or profile when the correct optimization is not evident.
- Define ownership, limits, cleanup, backpressure, and failure behavior for resources
  whose use can grow with traffic or data volume.
- Avoid unnecessary bottlenecks and single points of failure within the requested scope.

## Logging and Observability

- Follow the repository's established logging, metrics, tracing, and audit conventions.
- Record information that helps operators understand failures and important state
  transitions without creating noise or exposing sensitive data.
- Use appropriate severity levels and structured, stable fields when supported.
- Preserve correlation or request context across boundaries where the existing system
  supports it.
- Do not use logs as a substitute for correct error handling or return raw diagnostic
  details to end users.

## Testing and Verification

- Verify changes in proportion to their risk. Use the repository's documented build,
  formatting, linting, type-checking, static-analysis, and test commands.
- Run the smallest relevant checks during development and the complete affected checks
  before completion. Run existing tests that cover modified behavior.
- Test externally observable behavior and contracts rather than incidental implementation
  details. Keep tests deterministic, isolated, readable, and representative.
- Do not delete, disable, skip, or weaken tests merely to make verification pass. Fix
  the implementation or update expectations only when the required behavior changed.
- Do not create new test files or test projects unless the user explicitly requests
  tests. Still design code for testability and maintain all existing affected tests.
- When automated verification is unavailable or impractical, perform the best safe
  alternative and clearly report what was and was not verified.
- Do not declare completion while known failures or regressions remain in the affected
  scope.

## Documentation and Delivery

- Update documentation when behavior, contracts, setup, configuration, operations, or
  non-obvious constraints change.
- Keep documentation concise, accurate, and consistent with the implementation. Prefer
  examples that can be verified and do not contain secrets or machine-specific values.
- Summarize the completed outcome, important design decisions, and verification results.
  Call out remaining risks, limitations, or required follow-up plainly.


<!-- openwolf:begin -->
# OpenWolf

This project uses OpenWolf for context management. Read and follow .wolf/OPENWOLF.md at session start. Check .wolf/cerebrum.md before generating code. Grep .wolf/anatomy.md for a file's path before reading it (never read the whole index).
<!-- openwolf:end -->
