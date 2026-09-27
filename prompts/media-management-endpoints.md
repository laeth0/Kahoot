# Media Management and Lifecycle Endpoints

This document specifies the REST API endpoints and background processing contracts for image upload processing, sanitization, public delivery, and lifecycle management based on [docs/05-media-management.md](file:///c:/Users/laeth/Desktop/kahoot/docs/05-media-management.md).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             MEDIA ENDPOINTS                                 │
├───────────────────────┬─────────────────────────────────────────────────────┤
│ POST /api/uploads/images │ Upload, sanitize, and persist image (Host role)  │
│ GET  /uploads/{filename} │ Public immutable static image delivery           │
├───────────────────────┴─────────────────────────────────────────────────────┤
│                         LIFECYCLE & CLEANUP WORKER                          │
├───────────────────────┬─────────────────────────────────────────────────────┤
│ Background Worker     │ Two-phase orphan tombstone & physical unlink sweep  │
└───────────────────────┴─────────────────────────────────────────────────────┘
```

---

### Group A: Image Upload & Ingestion

#### 1. `POST /api/uploads/images` (Upload and Sanitize Image)
- **Requirement IDs**: `MED-UPL-001`, `MED-UPL-002`, `MED-UPL-003`, `MED-UPL-004`, `MED-BOUND-001`, `MED-BOUND-002`, `MED-BOUND-003`, `MED-BOUND-004`, `MED-SEC-001`, `MED-SEC-002`, `MED-SEC-003`, `MED-SLO-001`, `MED-SLO-002`, `MED-SLO-004`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Content-Type**: `multipart/form-data`
- **Form Fields**:
  - `file`: Binary file stream (`IFormFile`).
- **Validation & Technical Safety Limits**:
  1. **Storage Free Space Check (`MED-SEC-003`, `MED-ERR-006`)**:
     - System checks available disk storage before processing. If storage free space is $< 10\%$, immediately rejects request with `503 Media.StorageUnavailable`.
  2. **Payload Size Guard (`MED-BOUND-001`, `MED-ERR-004`)**:
     - File byte length must be between $1\text{ byte}$ and $5\text{ MiB}$ ($5,242,880\text{ bytes}$).
     - Payloads $> 5,242,880\text{ bytes}$ reject with `413 Media.TooLarge`.
     - Empty or missing `file` field rejects with `400 Validation.Failed`.
  3. **Magic Byte Signature Sniffing (`MED-UPL-003.1`, `MED-ERR-001`, `MED-ERR-005`)**:
     - Reads initial magic bytes to verify genuine file signature:
       - **JPEG**: `FF D8 FF`
       - **PNG**: `89 50 4E 47 0D 0A 1A 0A`
       - **WebP**: `RIFF` [4 bytes size] `WEBP`
     - Files claiming an image extension but failing magic byte checks reject with `400 Media.InvalidImage`.
     - Disallowed types (e.g. SVG, GIF, BMP, TIFF, executable formats) reject with `415 Media.UnsupportedType`.
  4. **Dimension & Decompression Bomb Guards (`MED-BOUND-002`, `MED-BOUND-003`, `MED-BOUND-004`, `MED-RISK-005`, `MED-ERR-001`)**:
     - Image header parser validates declared width and height *before* full bitmap buffer allocation:
       - Width $\le 4,096\text{px}$
       - Height $\le 4,096\text{px}$
       - Total pixel area ($W \times H$) $\le 16,777,216\text{ pixels}$
       - Uncompressed 32-bit RGBA decode RAM budget: $W \times H \times 4\text{ bytes} \le 64\text{ MiB}$ ($67,108,864\text{ bytes}$).
     - Any image violating width, height, pixel count, or memory budget rejects with `400 Media.InvalidImage`.
  5. **Animation / Multi-Frame Handling (`MED-UPL-003.3`)**:
     - Animated WebP and animated PNG (APNG) are processed by strictly decoding the **first frame** as a static image.
  6. **Orientation Correction (`MED-UPL-003.4`)**:
     - Inspects EXIF orientation flags and applies rotation/flip directly to pixel data prior to stripping metadata.
  7. **Metadata Stripping (`MED-UPL-003.5`)**:
     - Completely strips all EXIF, IPTC, and XMP metadata chunks (removing geolocation GPS coordinates, camera serial numbers, and creator tags).
  8. **Re-Encoding & Polyglot Elimination (`MED-UPL-003.6`, `MED-SEC-001`)**:
     - Decodes into raw pixel memory buffer and re-encodes into canonical JPEG, PNG, or WebP format.
     - Eliminates trailing polyglot payloads, embedded HTML/script blocks, and corrupt chunk structures.
  9. **Durable File Persistence (`MED-UPL-003.7`, `MED-RISK-001`, `MED-SEC-002`)**:
     - Client-provided filenames are strictly discarded.
     - Generates a cryptographically random UUIDv4 (`Guid.NewGuid()`).
     - Writes to temporary staging quarantine first; moves to destination `/uploads/{guid}.{extension}` under backend static web root (`wwwroot/uploads`).
  10. **Atomic Metadata Persistence (`MED-UPL-003.8`, `MED-RISK-002`)**:
      - Inserts record into `MediaItem` PostgreSQL table within database transaction:
        - `Id`: Generated Guid (UUIDv4)
        - `HostAccountId`: Authenticated Host user ID
        - `StoragePath`: `/uploads/{guid}.{extension}`
        - `ContentType`: Sanitized MIME type (`image/jpeg`, `image/png`, or `image/webp`)
        - `ByteSize`: Actual written file size on disk in bytes
        - `PixelWidth`: Final image pixel width
        - `PixelHeight`: Final image pixel height
        - `Status`: `MediaStatus.Active`
        - `ReferenceCount`: `0`
        - `UnreferencedSince`: `null`
        - `CreatedAt`: Current UTC timestamp
- **Response**: `201 Created`
  - **Headers**:
    - `Location: /uploads/550e8400-e29b-41d4-a716-446655440000.png`
  - **Body**:
    ```json
    {
      "mediaId": "01923485-aaaa-7abc-9f5a-222222222222",
      "url": "/uploads/550e8400-e29b-41d4-a716-446655440000.png"
    }
    ```
- **Error Codes**:
  - `400 Validation.Failed`: Missing or empty file parameter.
  - `400 Media.InvalidImage`: Corrupt file, invalid magic signature, dimensions $> 4,096\text{px}$, pixel area $> 16.7\text{M}$, or decode buffer $> 64\text{ MiB}$.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role (e.g. `SystemAdmin`).
  - `413 Media.TooLarge`: Uploaded file exceeds $5\text{ MiB}$ ($5,242,880\text{ bytes}$).
  - `415 Media.UnsupportedType`: Disallowed file type or extension (e.g. SVG, GIF, BMP, TIFF, EXE).
  - `503 Media.StorageUnavailable`: Volume free space $< 10\%$ or disk write failure.

---

### Group B: Public Image Delivery

#### 2. `GET /uploads/{filename}` (Public Immutable Image Delivery)
- **Requirement IDs**: `MED-PUB-001`, `MED-PUB-002`, `MED-PUB-003`, `MED-SEC-002`, `MED-SLO-003`
- **Authentication**: None (Anonymous / Public access).
- **Route Parameters**:
  - `filename` (`string`): Target image filename (e.g. `550e8400-e29b-41d4-a716-446655440000.png`).
- **Security & Path Traversal Mitigations (`MED-SEC-002`, `MED-PUB-003`)**:
  - Filename is validated against strict regex: `^[0-9a-fA-F-]{36}\.(jpg|jpeg|png|webp)$`.
  - Traversal characters (`..`, `/`, `\`, `%2e%2e`, `%2f`, `%5c`), path separators, or non-matching filenames return `404 Media.NotFound` immediately without filesystem lookup.
  - Directory browsing and server-side script execution in `/uploads` are strictly disabled.
- **Delivery Headers (`MED-PUB-002`)**:
  - `Content-Type`: Matching canonical MIME type (`image/jpeg`, `image/png`, `image/webp`).
  - `X-Content-Type-Options: nosniff`
  - `Cache-Control: public, max-age=31536000, immutable`
- **Response**: `200 OK`
  - Raw binary image stream.
- **Error Codes**:
  - `404 Media.NotFound`: File does not exist on disk or path traversal attempt detected.

---

### Group C: Lifecycle, Attachment & Background Cleanup Protocols

#### 3. Media Attachment Contract (`MED-ATT-001`, `MED-LIFE-001`, `MED-LIFE-002`)
- **Integration with Question Authoring**:
  - When a Host attaches a `mediaId` to a question during question creation or update (`POST/PUT /api/quizzes/{quizId}/questions`):
    - Validates that `MediaItem` exists in database.
    - Validates that `MediaItem.HostAccountId == CurrentHostAccountId` (`RA-ISOL-001`, `QUIZ-SEC-001`). If cross-tenant, returns `400 Quiz.InvalidMediaReference`.
    - Validates that `MediaItem.Status == MediaStatus.Active`. If `MediaStatus.DeletionPending`, returns `400 Quiz.InvalidMediaReference`.
    - Increments `ReferenceCount` by 1.
    - Clears `UnreferencedSince = null`.
  - When an image is detached from a question or replaced:
    - Decrements `ReferenceCount = Math.Max(0, ReferenceCount - 1)`.
    - If `ReferenceCount == 0`, sets `UnreferencedSince = UtcNow`.
  - When a quiz is deleted (never-played invariant):
    - Decrements `ReferenceCount` for all media referenced by questions in the quiz.
    - Any media with resulting `ReferenceCount == 0` has `UnreferencedSince = UtcNow`.
  - Historical Protection Invariant (`MED-LIFE-001`): Any media item with `ReferenceCount > 0` is strictly protected from deletion while attached to active questions or immutable game snapshots.

---

#### 4. Background Cleanup Worker (`MED-LIFE-003`, `MED-LIFE-004`, `MED-LIFE-005`, `MED-RISK-001`, `MED-RISK-003`, `MED-RISK-004`)
- **Component**: `IHostedService` / `BackgroundService` executing on periodic schedule.
- **Phase 1: Database Tombstone (`MED-LIFE-003`)**:
  - Queries unreferenced media eligible for cleanup:
    ```sql
    SELECT id, storage_path 
    FROM media_items 
    WHERE status = 'ACTIVE' 
      AND reference_count = 0 
      AND unreferenced_since <= NOW() - INTERVAL '7 days'
    LIMIT 100;
    ```
  - Transitions eligible rows to `Status = 'DELETION_PENDING'`.
  - Concurrency Lock (`MED-RISK-003`): Once marked `DELETION_PENDING`, any concurrent attempt to attach this media to a question fails with `400 Quiz.InvalidMediaReference`.
- **Phase 2: Physical Unlink & Row Removal (`MED-LIFE-004`)**:
  - Iterates tombstoned records:
    1. Physically deletes file from disk (`wwwroot/uploads/{guid}.{ext}`).
    2. Upon verified file deletion (or if file is already absent), permanently deletes the database row from `media_items`.
  - Crash Recovery (`MED-RISK-004`):
    - If the worker process restarts or crashes between tombstoning and physical deletion, orphaned `DELETION_PENDING` records older than 1 hour are reclaimed and deleted on subsequent runs.
- **Quarantine Staging Sweeper (`MED-LIFE-005`, `MED-RISK-001`)**:
  - Periodically scans temporary staging upload directory.
  - Deletes interrupted, abandoned, or uncommitted files older than 24 hours without database interaction.

---

### Non-Functional Latency & Capacity Targets

| Metric | Target | Stable Req ID |
| :--- | :--- | :--- |
| **Concurrent Upload Capacity** | $\ge 50$ simultaneous active uploads | `MED-SLO-001` |
| **Burst Upload Rate** | $25\text{ req/s}$ sustained for 10-second burst | `MED-SLO-002` |
| **Public Delivery Throughput** | $\ge 2,000\text{ req/s}$ with $p95 \le 30\text{ ms}$ | `MED-SLO-003` |
| **Processing Latency (Standard Image $\le 2\text{ MB}$)** | $p50 \le 500\text{ ms}$, $p95 \le 1.2\text{ s}$ | `MED-SLO-004` |
| **Processing Latency (Large Image $5\text{ MB}$, $4096\times 4096$)** | $p95 \le 2.0\text{ s}$, $p99 \le 5.0\text{ s}$ | `MED-SLO-004` |
