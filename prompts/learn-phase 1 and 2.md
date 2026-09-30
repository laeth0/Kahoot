| Concept | Brief Meaning |
|---|---|
| **Authentication** | Verifying who the user is. |
| **Authorization** | Determining whether the user is permitted to perform a specific action. |
| **JWT Authentication** | Signed access token carrying identity and claims. |
| **JWT Validation** | Validating signature, issuer, audience, and expiration. |
| **Token Revocation** | Invalidating a JWT before its expiration. |
| **Refresh Tokens** | Long-lived session management paired with short-lived access tokens. |
| **Refresh Token Rotation** | Issuing a replacement refresh token on every usage. |
| **Refresh Token Replay Detection** | Detecting and handling reuse attempts of previously consumed tokens. |
| **Token Hashing** | Storing SHA-256 hashes of refresh tokens instead of raw secrets in the database. |
| **Secure Cookies** | Using `HttpOnly`, `Secure`, `SameSite`, and scoped `Path` flags. |
| **CSRF Protection** | Preventing third-party sites from exploiting ambient cookies to forge user requests. |
| **Origin / Referer Validation** | Verifying that cookie-backed requests originate from an allowed client origin. |
| **CORS** | Specifying which frontend origins are permitted to read API responses. |
| **Password Hashing** | Storing computationally slow hashes (e.g. Argon2id, BCrypt) instead of plaintext passwords. |
| **Timing Attack Defense** | Preventing attackers from enumerating usernames or data via execution response time differences. |
| **User Enumeration Defense** | Hiding whether a username exists or is suspended through generic responses. |
| **Rate Limiting** | Throttling requests to prevent brute-force attacks and abuse. |
| **Load Shedding** | Rejecting excess requests (e.g. 429/503) under saturation to prevent server crashes. |
| **Least Privilege** | Granting only the minimum necessary permissions to each actor or service. |
| **IDOR Prevention** | Preventing access to other users' resources merely by modifying an ID parameter. |
| **Foreign Resource Concealment** | Treating foreign tenant resources as non-existent (returning 404 instead of 403). |
| **Multi-Tenant Isolation** | Ensuring Tenant A can never view, modify, or receive events for Tenant B's data. |
| **Server-derived Identity** | Deriving identity and tenant boundaries strictly from authenticated tokens, ignoring client-supplied IDs. |
| **Optimistic Concurrency** | Detecting concurrent modifications (e.g., version/timestamp check) rather than blind overwrites. |
| **Atomic Transactions** | Grouping operations so they all commit together or roll back completely on failure. |
| **Input Validation** | Rejecting malformed or out-of-bound inputs before reaching business logic. |
| **Unicode Normalization / NFKC** | Normalizing text to a canonical representation to prevent visual and character discrepancies. |
| **XSS / Output Encoding** | Preventing untrusted user content from executing as script in browsers. |
| **Sensitive Data Redaction** | Ensuring tokens, passwords, and secrets are never leaked into application logs. |
| **Secret Management** | Keeping production keys, connection strings, and credentials out of source control. |
| **RFC 7807 ProblemDetails** | Providing structured, standard error responses without exposing internal implementation details. |
| **Keyset (Cursor-based) Pagination** | Paginating with deterministic composite cursors like `(CreatedAt, Id)` instead of `OFFSET` to prevent slow queries and missed/duplicate rows. |
| **Dummy Password Hashing** | Executing a dummy hash calculation of equivalent cost when a user is not found to prevent timing-based user enumeration. |
| **Token Family Tracking** | Tracking refresh token lineage under a `TokenFamilyId` with an absolute cap (30 days) and revoking the family upon detected reuse. |
| **Rotation Race Grace Window** | Providing a brief grace period (10 seconds) to prevent logging out users during concurrent multi-tab token refreshes. |
| **Progressive Backoff (No Lockout)** | Delaying login responses progressively after repeated failures instead of locking accounts to prevent denial-of-service (DoS) on legitimate users. |
| **Ephemeral Session Tokens** | Lightweight session tokens for account-free players, stored as SHA-256 hashes and isolated from host/admin privileges. |
| **Zero Impersonation Policy** | Barring administrators from impersonating hosts or accessing private tenant quizzes, questions, and gameplay. |
| **Fail-Closed Architecture** | Halting startup or rejecting requests immediately when encountering unexpected states or conflicts rather than operating in a degraded security posture. |
| **Commit Outcome Taxonomy (A, B, C)** | Categorizing transaction and network failure states to determine when retries are safe and idempotent. |
| **Evidence Retention & Batch Cleanup** | Retaining revoked tokens for a forensic retention period (7 days) before deleting them in bounded background batches to prevent table locking. |
| **Key Identifier (`kid`) & Key Grace Transition** | Seamlessly rotating JWT signing keys by accepting the previous key during an overlap grace window equal to access token lifetime. |
| **Control / Zero-Width Characters Rejection** | Rejecting control characters and zero-width formatting codes (`Cc`, `Cf`) to prevent name spoofing and UI layout corruption. |
| **Hierarchical Ownership Inheritance** | Automatically inheriting tenant boundaries for child resources (questions, choices, participants) from parent quizzes and games. |
| **Composite Indexing for Tenant Isolation** | Placing `HostAccountId` as the leading column in composite indexes to guarantee fast query filtering and data segregation per tenant. |
| **Token Security Version (Versioned Claims)** | Including a security version claim (`TokenSecurityVersion`) in JWTs and incrementing it on password change or global logout to instantly invalidate all prior tokens without a blacklist. |
| **Password Hashing Concurrency & Memory Cap** | Restricting concurrent Argon2id hashing operations (e.g. max 16 threads, 64 MB per hash) via semaphores and bounded queues to avoid out-of-memory (OOM) crashes. |
| **Dual Representation Model (Display vs. Normalized)** | Storing usernames in two forms: `DisplayUsername` (preserving user casing) and `NormalizedUsername` (NFKC + invariant case-folded) for unique constraints and lookups. |
| **Zero Normalization of Passwords** | Never trimming, lowercasing, or Unicode-normalizing passwords; validating and hashing them byte-for-byte as entered. |
| **System Administrator Bootstrap Lifecycle** | Provisioning an initial admin account from environment variables at startup, failing closed on username conflict, and disabling bootstrap in production. |
| **Separation of Registration from Session Issuance** | Returning `201 Created` without issuing access tokens/cookies upon registration, requiring an explicit login request to initiate a session. |
| **Cross-Instance Revocation Broadcast & Socket Severing** | Broadcasting revocation events (suspension, logout-all) across all nodes within $\le 100\text{ ms}$ and immediately severing active SignalR connections. |
| **Tamper-Proof Keyset Cursors** | Protecting pagination cursors with cryptographic signatures (HMAC) or strict type validation to prevent parameter tampering and cross-tenant hopping. |
| **Homoglyph & Confusable Non-Claims** | Acknowledging that while NFKC resolves compatibility equivalence (e.g. full-width characters), it does not defend against cross-script visual homoglyphs (e.g. Latin `a` vs Cyrillic `а`). |