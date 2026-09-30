| Concept | Brief Meaning |
|---|---|
| **Account-Free Ephemeral Join** | Allowing players to enter a game using only a PIN and nickname with no registration, email, or prior credentials required. |
| **Lobby-Only Join Gate** | Permanently blocking all new player joins once the game transitions out of `LOBBY`, with no late-join mechanism. |
| **Nickname NFKC Normalization & Case-Folding** | Applying NFKC normalization and culture-independent case folding to compute a `NormalizedNickname` used for uniqueness enforcement within a game session. |
| **JoinOperationId as Recovery Credential** | Using a client-generated UUIDv4 `JoinOperationId` (stored as SHA-256) as a temporary idempotency key, allowing safe join retries after dropped network responses. |
| **JoinOperationId Recovery Lifetime & Scope** | Scoping the recovery key strictly to one `(GameId, NormalizedNickname)` pair, valid only while the game is in `LOBBY` or within 15 minutes of creation. |
| **Active Socket Fencing on Late Retry** | Returning existing session metadata on a late join retry without generating a replacement token or severing the player's active WebSocket connection. |
| **JoinOperationId Mismatched Input Rejection** | Rejecting any retry that presents an existing `JoinOperationId` with a different nickname or PIN (`400 Validation.Failed`). |
| **256-bit PlayerSessionToken (Hash-Only Storage)** | Generating a cryptographically secure 256-bit random session token, storing only `SHA256(RawToken)`, and returning the raw value exactly once in the join response. |
| **24-Hour Strict Post-Game Token Boundary** | Enforcing a strict `serverTime < FinishedAt + 24h` comparison for player token validity in read-only recovery mode, with immediate rejection at or after the boundary. |
| **Token Expiry Without Data Deletion** | Expiring or cleaning up session token hashes after 24 hours while never deleting historical business data (participant names, answers, scores, or ranks). |
| **Permanent Nickname Tombstone** | Permanently reserving a removed player's nickname for the game session so no new player can claim it, preventing disruptive users from rejoining under the same identity. |
| **Kick Phase Semantics (Lobby vs. Active)** | Freeing a reserved seat when kicked from `LOBBY`, but preserving the kicked player's already-submitted answers, points, and denominator inclusion when kicked from an active question. |
| **500-Seat Hard Capacity with Transactional Lock** | Enforcing a maximum of 500 non-removed participants using a `SELECT FOR UPDATE` seat-counter lock to prevent over-allocation under concurrent join races. |
| **Decoupled Transient Presence Tracking** | Maintaining durable relational records for reserved seats and participant identities while allowing transient WebSocket connect/disconnect events to be tracked via memory leases or generation fencing without database row thrashing. |
| **Monotonic PresenceVersion** | Incrementing a monotonic version counter on every authoritative presence change (join, kick, seat release) so stale disconnect events cannot overwrite newer connection generation state. |
| **NAT-Friendly Token-Bucket Rate Limiting** | Applying per-IP rate limits with sufficient burst capacity (e.g., 1,200 tokens, refill 600/10s) to allow an entire classroom (500 players sharing one NAT) to join without false throttling. |
| **500-to-0 Seat Fill Under 5 Seconds** | Supporting filling a 500-seat lobby in under 5 seconds without database deadlock or seat contention errors through transactional seat reservation design. |
