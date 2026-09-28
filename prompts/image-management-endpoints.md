# Image Management and Lifecycle Endpoints

This document specifies the REST API endpoints and background processing contracts for image upload processing, sanitization, public delivery, and lifecycle management based on [docs/05-image-management.md](../docs/05-image-management.md).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             IMAGE ENDPOINTS                                 │
├───────────────────────┬─────────────────────────────────────────────────────┤
│ POST /api/uploads/images │ Upload, sanitize, and persist image (Host role)  │
│ GET  /uploads/{filename} │ Public immutable static image delivery           │
├───────────────────────┴─────────────────────────────────────────────────────┤
│                         LIFECYCLE & CLEANUP WORKER                          │
├───────────────────────┬─────────────────────────────────────────────────────┤
│ Background Worker     │ Database-first orphan cleanup & physical unlink sweep  │
└───────────────────────┴─────────────────────────────────────────────────────┘
```

---

### Group A: Image Upload & Ingestion

#### 1. `POST /api/uploads/images` (Upload and Sanitize Image)
- **Requirement IDs**: `IMG-UPL-001`, `IMG-UPL-002`, `IMG-UPL-003`, `IMG-UPL-004`, `IMG-BOUND-001`, `IMG-BOUND-002`, `IMG-BOUND-003`, `IMG-BOUND-004`, `IMG-SEC-001`, `IMG-SEC-002`, `IMG-SEC-003`, `IMG-SLO-001`, `IMG-SLO-002`, `IMG-SLO-004`
- **Authentication**: Requires authenticated user in `Host` role (`UserRole.Host`).
- **Content-Type**: `multipart/form-data`
- **Form Fields**:
  - `file`: Binary file stream (`IFormFile`).
- **Validation & Technical Safety Limits**:
  1. **Storage Free Space Check (`IMG-SEC-003`, `IMG-ERR-006`)**:
     - System checks available disk storage before processing. If storage free space is $< 10\%$, immediately rejects request with `503 Image.StorageUnavailable`.
  2. **Payload Size Guard (`IMG-BOUND-001`, `IMG-ERR-004`)**:
     - File byte length must be between $1\text{ byte}$ and $5\text{ MiB}$ ($5,242,880\text{ bytes}$).
     - Payloads $> 5,242,880\text{ bytes}$ reject with `413 Image.TooLarge`.
     - Empty or missing `file` field rejects with `400 Validation.Failed`.
  3. **Magic Byte Signature Sniffing (`IMG-UPL-003.1`, `IMG-ERR-001`, `IMG-ERR-005`)**:
     - Reads initial magic bytes to verify genuine file signature:
       - **JPEG**: `FF D8 FF`
       - **PNG**: `89 50 4E 47 0D 0A 1A 0A`
       - **WebP**: `RIFF` [4 bytes size] `WEBP`
     - Files claiming an image extension but failing magic byte checks reject with `400 Image.InvalidImage`.
     - Disallowed types (e.g. SVG, GIF, BMP, TIFF, executable formats) reject with `415 Image.UnsupportedType`.
  4. **Dimension & Decompression Bomb Guards (`IMG-BOUND-002`, `IMG-BOUND-003`, `IMG-BOUND-004`, `IMG-RISK-005`, `IMG-ERR-001`)**:
     - Image header parser validates declared width and height *before* full bitmap buffer allocation:
       - Width $\le 4,096\text{px}$
       - Height $\le 4,096\text{px}$
       - Total pixel area ($W \times H$) $\le 16,777,216\text{ pixels}$
       - Uncompressed 32-bit RGBA decode RAM budget: $W \times H \times 4\text{ bytes} \le 64\text{ MiB}$ ($67,108,864\text{ bytes}$).
     - Any image violating width, height, pixel count, or memory budget rejects with `400 Image.InvalidImage`.
  5. **Animation / Multi-Frame Handling (`IMG-UPL-003.3`)**:
     - Animated WebP and animated PNG (APNG) are processed by strictly decoding the **first frame** as a static image.
  6. **Orientation Correction (`IMG-UPL-003.4`)**:
     - Inspects EXIF orientation flags and applies rotation/flip directly to pixel data prior to stripping metadata.
  7. **Metadata Stripping (`IMG-UPL-003.5`)**:
     - Completely strips all EXIF, IPTC, and XMP metadata chunks (removing geolocation GPS coordinates, camera serial numbers, and creator tags).
  8. **Re-Encoding & Polyglot Elimination (`IMG-UPL-003.6`, `IMG-SEC-001`)**:
     - Decodes into raw pixel memory buffer and re-encodes into canonical JPEG, PNG, or WebP format.
     - Eliminates trailing polyglot payloads, embedded HTML/script blocks, and corrupt chunk structures.
  9. **Durable File Persistence (`IMG-UPL-003.7`, `IMG-RISK-001`, `IMG-SEC-002`)**:
     - Client-provided filenames are strictly discarded.
     - Generates a cryptographically random UUIDv4 (`Guid.NewGuid()`).
     - Writes to temporary staging quarantine first; moves to destination `/uploads/{guid}.{extension}` under backend static web root (`wwwroot/uploads`).
  10. **Metadata Persistence and File Compensation (`IMG-UPL-003.8`, `IMG-RISK-002`)**:
      - After the sanitized file is durable, inserts a `QuestionImage` row in a database transaction. Compensate for file writes when the database commit fails:
        - `Id`: Generated Guid (UUIDv4)
        - `HostAccountId`: Authenticated Host user ID
        - `StoragePath`: `/uploads/{guid}.{extension}`
        - `ContentType`: Sanitized MIME type (`image/jpeg`, `image/png`, or `image/webp`)
        - `ByteSize`: Actual written file size on disk in bytes
        - `PixelWidth`: Final image pixel width
        - `PixelHeight`: Final image pixel height
        - `UnreferencedSince`: Current UTC timestamp
        - `CreatedAt`: Current UTC timestamp
- **Response**: `201 Created`
  - **Headers**:
    - `Location: /uploads/550e8400-e29b-41d4-a716-446655440000.png`
  - **Body**:
    ```json
    {
      "imageId": "01923485-aaaa-7abc-9f5a-222222222222",
      "url": "/uploads/550e8400-e29b-41d4-a716-446655440000.png"
    }
    ```
- **Error Codes**:
  - `400 Validation.Failed`: Missing or empty file parameter.
  - `400 Image.InvalidImage`: Corrupt file, invalid magic signature, dimensions $> 4,096\text{px}$, pixel area $> 16.7\text{M}$, or decode buffer $> 64\text{ MiB}$.
  - `401 Auth.Unauthorized`: Missing or invalid JWT.
  - `403 Auth.Forbidden`: Authenticated user is not in `Host` role (e.g. `SystemAdmin`).
  - `413 Image.TooLarge`: Uploaded file exceeds $5\text{ MiB}$ ($5,242,880\text{ bytes}$).
  - `415 Image.UnsupportedType`: Disallowed file type or extension (e.g. SVG, GIF, BMP, TIFF, EXE).
  - `503 Image.StorageUnavailable`: Volume free space $< 10\%$ or disk write failure.

---

### Group B: Public Image Delivery

#### 2. `GET /uploads/{filename}` (Public Immutable Image Delivery)
- **Requirement IDs**: `IMG-PUB-001`, `IMG-PUB-002`, `IMG-PUB-003`, `IMG-SEC-002`, `IMG-SLO-003`
- **Authentication**: None (Anonymous / Public access).
- **Route Parameters**:
  - `filename` (`string`): Target image filename (e.g. `550e8400-e29b-41d4-a716-446655440000.png`).
- **Security & Path Traversal Mitigations (`IMG-SEC-002`, `IMG-PUB-003`)**:
  - Filename is validated against strict regex: `^[0-9a-fA-F-]{36}\.(jpg|jpeg|png|webp)$`.
  - Traversal characters (`..`, `/`, `\`, `%2e%2e`, `%2f`, `%5c`), path separators, or non-matching filenames return `404 Image.NotFound` immediately without filesystem lookup.
  - Directory browsing and server-side script execution in `/uploads` are strictly disabled.
- **Delivery Headers (`IMG-PUB-002`)**:
  - `Content-Type`: Matching canonical MIME type (`image/jpeg`, `image/png`, `image/webp`).
  - `X-Content-Type-Options: nosniff`
  - `Cache-Control: public, max-age=31536000, immutable`
- **Response**: `200 OK`
  - Raw binary image stream.
- **Error Codes**:
  - `404 Image.NotFound`: File does not exist on disk or path traversal attempt detected.

---

### Group C: Lifecycle, Attachment & Background Cleanup Protocols

#### 3. Image Attachment Contract (`IMG-ATT-001`, `IMG-LIFE-001`, `IMG-LIFE-002`)
- When a Host attaches an `imageId` to a question, verify the committed `QuestionImage` belongs to the same Host and is not attached to another current question. Missing, foreign, or already attached images return `400 Quiz.InvalidImageReference`.
- The nullable `Question.ImageId` uses a tenant-matched foreign key. A unique index enforces one current question owner, including under concurrent requests.
- Attachment clears `UnreferencedSince`. Detachment or question deletion sets it to the current time only when no game question snapshot still references the image.
- Immutable game question snapshots retain their own `ImageId` and `ImageUrl`. Restrictive foreign keys protect their referenced image records and files.

---

#### 4. Background Cleanup Worker (`IMG-LIFE-003`, `IMG-LIFE-004`, `IMG-LIFE-005`, `IMG-RISK-001`, `IMG-RISK-003`, `IMG-RISK-004`)
- Use a bounded periodic worker across replicas. Lock eligible `question_images` rows with `FOR UPDATE SKIP LOCKED` and recheck `UnreferencedSince` is at least seven days old and neither a current question nor a game question snapshot references the image.
- Delete the database row and commit before unlinking its file. A concurrent attachment either commits before the row is deleted or fails after deletion; restrictive foreign keys prevent a dangling committed reference.
- If unlinking fails after commit, a bounded filesystem reconciliation pass removes old files with no database row. An upload that wrote a file but failed to commit its record is handled by the same pass.
- Remove interrupted staging uploads older than 24 hours. Do not sweep newly written files awaiting database commit.

---

### Non-Functional Latency & Capacity Targets

| Metric | Target | Stable Req ID |
| :--- | :--- | :--- |
| **Concurrent Upload Capacity** | $\ge 50$ simultaneous active uploads | `IMG-SLO-001` |
| **Burst Upload Rate** | $25\text{ req/s}$ sustained for 10-second burst | `IMG-SLO-002` |
| **Public Delivery Throughput** | $\ge 2,000\text{ req/s}$ with $p95 \le 30\text{ ms}$ | `IMG-SLO-003` |
| **Processing Latency (Standard Image $\le 2\text{ MB}$)** | $p50 \le 500\text{ ms}$, $p95 \le 1.2\text{ s}$ | `IMG-SLO-004` |
| **Processing Latency (Large Image $5\text{ MB}$, $4096\times 4096$)** | $p95 \le 2.0\text{ s}$, $p99 \le 5.0\text{ s}$ | `IMG-SLO-004` |
