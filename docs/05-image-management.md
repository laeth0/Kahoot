# 05. Image Management and Lifecycle

This document defines the normative requirements for question image upload, sanitization, tenant-scoped attachment, public immutable delivery, orphan cleanup, and historical game snapshot protection. Profile images are a possible future feature and are outside the current upload and delivery contract.

---

## 1. Topic Overview & Actors

Question image management handles images attached to quiz questions. Quizzes, answers, and choices have no image fields:
* **Registered User / Host**: Uploads images and attaches them to questions within their own tenant boundary.
* **Player / Participant**: Fetches public image bytes during gameplay or preview via immutable URL.
* **System Administrator**: Cannot browse private image inventories or view unreferenced images.
* **Separation of Bytes vs. Metadata `[NORMATIVE]`**:
  * Uploaded image bytes are intentionally publicly readable by URL once uploaded (`/uploads/{filename}`). The system does not guarantee secrecy of public image URLs.
  * Image ownership metadata, private inventories, and attachment rights are strictly protected tenant resources.

### 1.1 Data Model Decision `[NORMATIVE]`
* **`IMG-MODEL-001` (Question-Scoped Image Records)**: Use a dedicated `QuestionImage` (`question_images`) record for each uploaded question image. Store `Id`, `HostAccountId`, `StoragePath`, validated content type and dimensions, `UnreferencedSince` and `CreatedAt`. `Question.ImageId` is nullable and references the record with a tenant-matched foreign key. A unique index on non-null `Question.ImageId` permits at most one current question owner per image. Give every image record a distinct immutable file path, enforced by a unique `StoragePath` constraint. Index `UnreferencedSince` for bounded cleanup and each snapshot image foreign key for retention checks. Use `imageId` for the opaque image ID in requests and responses.
* **`IMG-MODEL-002` (Historical References)**: A game question snapshot retains its immutable image URL and an optional foreign key to the same `QuestionImage` record. Multiple game snapshots may retain the same question image because they are historical references, not additional authoring owners. Snapshot foreign keys restrict deletion of the image record. Image bytes are never overwritten at an existing URL.
* **Future Profile Images**: If profile images are introduced, specify their visibility, authorization, and lifecycle separately. A nullable `User.ProfileImageId` with a profile-specific record, direct profile image columns, or a dedicated profile image table can be selected then. Reuse the image validation and storage implementation where appropriate; do not make profile images share the question image table or its public delivery policy by default.

| Design | Fit for current requirements | Main cost |
| :--- | :--- | :--- |
| Image metadata columns directly on `questions` | Simple only when an image is uploaded and replaced in the same question operation. | The existing upload-before-attachment flow needs a durable claim token; old image metadata must remain available for game snapshots after replacement. |
| Dedicated `question_images` table, one current question per image | **Recommended.** Keeps upload metadata and historical file retention in one small, purpose-specific model. | Requires a small cleanup process for unclaimed and detached files. |
| Generic asset table | Useful only if unrelated entities share assets and lifecycle policy, which is not required here. | Adds a cross-entity contract and shared lifecycle rules without a current use case. |

This design keeps image processing and filesystem access in Infrastructure; quiz authoring and game snapshot features use a question-image contract through the existing application boundary. The database enforces tenant ownership and single current question ownership. A public image URL grants access to bytes, never the right to attach the image to a question. The two indexed existence checks in cleanup scale with the number of eligible orphan candidates, not with the total image inventory. Storage grows with retained game snapshots; no schema choice can reclaim an image while history still references it. A later profile feature can use a separate data model without inheriting question-image public access.

**Migration note (non-normative):** Before adding the single-owner constraint, audit existing rows for images attached to multiple questions. Give each current question its own image record and file where sharing exists, while preserving old image records and URLs referenced by game snapshots.

---

## 2. Functional Specification & Workflows

### 2.1 Image Upload Processing `[NORMATIVE]`
* **`IMG-UPL-001` (Upload Endpoint)**: `POST /api/uploads/images`
* **`IMG-UPL-002` (Payload & Constraints)**: Multipart form upload with field `file`. Maximum byte size: $5\text{ MiB}$ ($5,242,880\text{ bytes}$).
* **`IMG-UPL-003` (Validation & Sanitization Pipeline)**:
  1. **Header & Signature Sniffing**: Inspects initial magic numbers to verify genuine image format (JPEG, PNG, WebP). Files claiming an image extension but failing magic byte checks are rejected with `400 Image.InvalidImage`.
  2. **Dimension & Decompression Bomb Guards**: Decoder checks declared width/height before full bitmap allocation. Width $\le 4,096\text{px}$, Height $\le 4,096\text{px}$, and total pixel count $\le 16,777,216\text{ pixels}$. Total uncompressed decode memory must not exceed $64\text{ MiB}$.
  3. **Animation / Multi-Frame Handling**: Animated WebP and animated PNG (APNG) are processed by decoding strictly the **first frame** as a static image, or rejected with `415 Image.UnsupportedType`.
  4. **Orientation Correction**: Reads EXIF orientation flags and applies necessary rotation/flip to pixel data *before* metadata stripping, preventing sideways/upside-down rendering.
  5. **Metadata Stripping**: Completely strips all EXIF, IPTC, and XMP metadata (removing GPS coordinates, camera serial numbers, and creator tags).
  6. **Re-Encoding & Polyglot Elimination**: Re-encodes image into canonical JPEG, PNG, or WebP. Eliminates trailing polyglot payloads, embedded HTML/script blocks, and corrupt chunk structures. *(Non-Claim: Re-encoding does NOT guarantee elimination of steganographic data concealed in raw pixel values).*
  7. **Durable File Persistence**: Writes file to persistent volume under `/uploads/{guid}.{extension}` using a cryptographically random UUIDv4.
  8. **Metadata Commit**: After the sanitized file is durable, inserts a `QuestionImage` with `Id`, `HostAccountId`, `StoragePath`, `ContentType`, `ByteSize`, `PixelWidth`, `PixelHeight`, `UnreferencedSince = NOW()`, and `CreatedAt = NOW()`. The file and PostgreSQL commit require failure compensation because they are not one atomic transaction.
* **`IMG-UPL-004` (Upload Response)**: `201 Created`
  ```json
  {
    "imageId": "550e8400-e29b-41d4-a716-446655440001",
    "url": "/uploads/550e8400-e29b-41d4-a716-446655440000.png"
  }
  ```

### 2.2 Image Attachment to Questions `[NORMATIVE]`
* **`IMG-ATT-001` (Attachment Contract)**:
  * Host specifies `imageId` during question creation or edit.
  * Server verifies that the committed `QuestionImage` row exists and belongs to the current Host.
  * The image must not be attached to a different current question. A unique index on non-null `Question.ImageId` enforces this invariant under concurrent requests.
  * Foreign images, images attached to a different question, or images already removed by cleanup return `400 Quiz.InvalidImageReference`.
  * Attachment, replacement, and question deletion update the question's nullable image foreign key in the quiz transaction. Set `UnreferencedSince = NULL` on attachment; on detachment, set it to the current time only if no snapshot retains the old image. If snapshot deletion is ever supported, set it when the last snapshot reference disappears.

### 2.3 Public Image Delivery Boundary `[NORMATIVE]`
* **`IMG-PUB-001` (Public Delivery Route)**:
  * Endpoint: `GET /uploads/{filename}`
  * Serves immutable image bytes directly via reverse proxy or backend file streamer.
* **`IMG-PUB-002` (Delivery Headers)**:
  * `Content-Type`: Matching validated MIME type (`image/jpeg`, `image/png`, `image/webp`).
  * `X-Content-Type-Options: nosniff`
  * `Cache-Control: public, max-age=31536000, immutable`
* **`IMG-PUB-003` (Security Restrictions)**: Directory browsing and script execution are strictly disabled in `/uploads`.

### 2.4 Orphan Cleanup and Historical Retention `[NORMATIVE]`
PostgreSQL and file storage cannot commit or delete atomically. Cleanup is still needed for uploads never attached to a question and images detached after a question edit or deletion. Question and snapshot foreign keys determine whether an image is still in use, allowing database-first deletion without a tombstone state.

1. **`IMG-LIFE-001` (Historical Protection Invariant)**:
   * An image referenced by a current question or any retained game question snapshot is never eligible for physical deletion. Snapshot URLs alone are not used to decide retention; the indexed snapshot image foreign key is authoritative.
   * A detached question image remains available for as long as any snapshot references it. When the last reference disappears, set `UnreferencedSince = NOW()`; clear it when a question acquires an unreferenced image.
2. **`IMG-LIFE-002` (Unclaimed Uploads)**:
   * An uploaded image starts with `UnreferencedSince = CreatedAt`. An image that remains unclaimed for seven days is eligible for cleanup. Replacing or deleting a question does not immediately unlink its old image.
3. **`IMG-LIFE-003` (Database-First Cleanup)**:
   * In bounded batches, lock candidate `QuestionImage` rows with `FOR UPDATE SKIP LOCKED`. Recheck `UnreferencedSince <= NOW() - INTERVAL '7 days'` and absence of both current question and game snapshot references, then delete the image row and commit.
   * The tenant-matched question and snapshot foreign keys use restrictive deletion. Concurrent attachment or snapshot creation must commit before the cleanup row lock or fail after row deletion; no committed reference may point to a deleted image row.
4. **`IMG-LIFE-004` (File Unlink and Recovery)**:
   * After database deletion commits, unlink the image file. A missing file is an idempotent success. If unlink fails or the worker crashes, a bounded filesystem reconciliation pass later removes final files older than seven days with no `QuestionImage` row.
   * File reconciliation also covers a crash after a successful file write but before metadata commit. Do not sweep newly written files that may still be awaiting a database commit.
5. **`IMG-LIFE-005` (Incomplete Upload Quarantine)**:
   * Interrupted upload streams use a staging directory. A periodic sweep removes staging files older than 24 hours.

---

## 3. Boundaries, Edge Cases & Negative Validation

### 3.1 Input & Image Dimension Boundaries Table

| Parameter | Min - 1 | Min | Normal | Max | Max + 1 | Rule / Error | Stable Req ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **File Size (Bytes)** | 0 (400) | 1 byte (201)| 500 KB (201) | 5,242,880 (201)| 5,242,881 (413)| Max 5 MiB payload. | `IMG-BOUND-001` |
| **Pixel Dimensions** | 0 (400) | 1px (201) | 1,920px (201) | 4,096px (201) | 4,097px (400) | Width and Height $\le 4096\text{px}$. | `IMG-BOUND-002` |
| **Total Pixel Area** | 0 (400) | 1px (201) | 2,073,600 (201)| 16,777,216 (201)| 16,777,217 (400)| $W \times H \le 16.7\text{M}$ pixels. | `IMG-BOUND-003` |
| **Decode RAM Budget** | - | - | 12 MiB | 64 MiB | 65 MiB (400) | Decompression bomb protection. | `IMG-BOUND-004` |
| **Orphan Retention** | 6.99d (Keep)| 7.0d (Eligible)| 30d (Eligible)| - | - | 7 days unreferenced retention. | `IMG-BOUND-005` |

### 3.2 Canonical Negative Error Codes

| Status | Code | Meaning | Stable Req ID |
| :--- | :--- | :--- | :--- |
| **400** | `Image.InvalidImage` | Corrupt file, truncated chunks, dimensions $> 4096\text{px}$, or decode memory $> 64\text{ MiB}$. | `IMG-ERR-001` |
| **400** | `Quiz.InvalidImageReference` | Image does not exist, upload is incomplete, belongs to another tenant, or is attached to a different question. | `IMG-ERR-002` |
| **404** | `Image.NotFound` | Image file not found on storage volume during public GET. | `IMG-ERR-003` |
| **413** | `Image.TooLarge` | Raw upload payload exceeds 5 MiB. | `IMG-ERR-004` |
| **415** | `Image.UnsupportedType` | Disallowed MIME type (e.g., SVG, GIF, BMP, TIFF, EXE). | `IMG-ERR-005` |
| **503** | `Image.StorageUnavailable` | Local storage volume write failure or storage disk full. | `IMG-ERR-006` |

---

## 4. Topic-Specific Non-Functional Requirements & Performance SLOs

### 4.1 Workload Capacity & Latency SLO Targets `[NORMATIVE]`
* **`IMG-SLO-001` (Concurrent Upload Capacity)**: Platform supports at least **50 concurrent image uploads** executing simultaneously.
* **`IMG-SLO-002` (Burst Upload Rate)**: Platform sustains **25 upload requests/second** for a 10-second burst.
* **`IMG-SLO-003` (Public Delivery Latency)**: Public image serving via reverse proxy / caching layer sustains at least **2,000 req/s** with:
  * $p95 \le 30\text{ ms}$
* **`IMG-SLO-004` (Image Processing Latency)**:
  * Standard Images ($\le 2\text{ MB}$, $\le 1920\times 1080$): $p50 \le 500\text{ ms}$, $p95 \le 1.2\text{ seconds}$.
  * Large Images ($5\text{ MB}$, $4096\times 4096$): $p95 \le 2.0\text{ seconds}$, $p99 \le 5.0\text{ seconds}$.

---

## 5. Security & Threat Mitigations

### 5.1 Defense Against Malicious Image Formats `[NORMATIVE]`
* **`IMG-SEC-001` (Polyglot Elimination)**: Images are decoded into raw pixel memory buffers and reconstructed. Embedded scripts, trailing HTML, and malformed container structures are dropped.
* **`IMG-SEC-002` (Path Traversal Elimination)**: Client-supplied filenames are ignored. Server assigns UUIDv4 filenames. URLs with traversal tokens (`..`, `/`, `\`, `%2e%2e`) return `404 Image.NotFound`.
* **`IMG-SEC-003` (Storage Exhaustion Defenses)**: Inbound uploads check available storage disk space before accepting payload stream. If storage free space is $< 10\%$, upload returns `503 Image.StorageUnavailable`.

---

## 6. Concurrency, Lost-Response & Failure-Mode Contracts

### 6.1 State-Changing Commit Outcome Contract `[NORMATIVE]`
* **Outcome A (Failure before commit)**: Upload stream aborts or image fails validation: storage file deleted/quarantined; no DB record committed. Caller retries safely.
* **Outcome B (Commit succeeded, response lost)**: Image record and file committed; response dropped. Caller retrying upload receives a new `imageId`. The unacknowledged first image remains unreferenced and is safely reclaimed after 7 days.
* **Outcome C (Outcome unknown to caller)**: Network timeout during upload. Caller retries upload with fresh file; unreferenced orphan is collected automatically.

### 6.2 Per-Topic Failure-Mode & Risk Matrix `[NORMATIVE]`

| Risk ID | Trigger / Failure | Potential Impact | Required System Behavior | Recovery / Mitigation | Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `IMG-RISK-001` | Final image file written, but metadata commit aborts or the process crashes. | Orphan file on disk taking up storage space. | Stage and sanitize first, move to the final path before the metadata commit, and delete it on known failure. Reconcile final files with no metadata row after the retention window. | Staging and orphan-file cleanup. | `IMG-TEST-007` |
| `IMG-RISK-002` | DB metadata committed, but storage disk write fails or disk fills. | Database points to non-existent image bytes. | File write and verification precede metadata transaction commit. DB transaction aborts if file write fails. | Transactional rollback; 503 returned. | `IMG-TEST-008` |
| `IMG-RISK-003` | Host attaches image while cleanup runs. | Question points to a missing image. | Cleanup locks and rechecks the row before deleting it; the restrictive foreign key serializes attachment with deletion. If cleanup commits first, attachment fails with 400. | Database-first deletion and FK enforcement. | `IMG-TEST-009` |
| `IMG-RISK-004` | Worker crashes after deleting the DB row but before unlinking the file. | Orphan file remains in storage. | Filesystem reconciliation finds old final files without an image row and removes them. | Bounded orphan-file sweep. | `IMG-TEST-010` |
| `IMG-RISK-005` | Upload of decompression bomb image (e.g. tiny file declaring $4096\times 4096$). | Out of memory crash in backend application. | Header parser validates declared dimensions; decodes in bounded buffer; aborts if $> 64\text{ MiB}$ required. | Memory ceiling guard; 400 returned. | `IMG-TEST-006` |

---

## 7. Traceable Acceptance Tests & Test Matrix

| Test ID | Mapped Requirement IDs | Category | Description & Preconditions | Expected Outcome |
| :--- | :--- | :--- | :--- | :--- |
| `IMG-TEST-001` | `IMG-UPL-001`, `IMG-UPL-003` | Functional | Host uploads valid PNG (2 MB, $1920\times 1080$). | `201 Created` with `/uploads/{guid}.png`; EXIF stripped. |
| `IMG-TEST-002` | `IMG-ATT-001`, `IMG-MODEL-001` | Functional | Host attaches uploaded image to a question, then attempts to attach it to a second question. | First attachment succeeds; second fails with `400 Quiz.InvalidImageReference`. |
| `IMG-TEST-003` | `IMG-PUB-001`, `IMG-PUB-002` | Functional | Public anonymous client requests image URL. | `200 OK` with `Cache-Control: public, max-age=31536000, immutable` and `nosniff`. |
| `IMG-TEST-004` | `IMG-BOUND-001`, `IMG-ERR-004` | Boundary | Upload file size $5,242,880$ bytes vs $5,242,881$ bytes. | $5,242,880$ succeeds; $5,242,881$ fails with `413 Image.TooLarge`. |
| `IMG-TEST-005` | `IMG-BOUND-002`, `IMG-ERR-001` | Boundary | Upload image with dimensions $4096\times 4096$ vs $4097\times 4096$. | $4096$ succeeds; $4097$ fails with `400 Image.InvalidImage`. |
| `IMG-TEST-006` | `IMG-BOUND-004`, `IMG-RISK-005` | Security / Boundary | Upload malformed decompression bomb requiring $> 64\text{ MiB}$ decode buffer. | Decode aborted; returns `400 Image.InvalidImage`; process RAM stable. |
| `IMG-TEST-007` | `IMG-RISK-001` | Fault Injection | Interrupt upload after the final file is written but before metadata commits. | No DB row remains; the final file is removed by compensation or later orphan-file reconciliation. |
| `IMG-TEST-008` | `IMG-ERR-006`, `IMG-RISK-002` | Fault Injection | Simulate read-only storage volume during image upload. | Upload fails with `503 Image.StorageUnavailable`; zero orphan DB records. |
| `IMG-TEST-009` | `IMG-LIFE-003`, `IMG-RISK-003` | Concurrency | Host attaches a seven-day-old unreferenced image while cleanup runs. | Either attachment commits and cleanup preserves the row and file, or cleanup deletes the row first and attachment receives `400 Quiz.InvalidImageReference`. |
| `IMG-TEST-010` | `IMG-LIFE-004`, `IMG-RISK-004` | Fault Injection | Crash worker after deleting the image row and before unlinking its file. | Reconciliation removes the orphan file; no question or snapshot reference was deleted. |
| `IMG-TEST-011` | `IMG-SLO-003` | Non-Functional | Benchmark public image delivery under 2,000 req/s. | Serves with $p95 \le 30\text{ ms}$; zero server memory growth. |
| `IMG-TEST-012` | `IMG-LIFE-001`, `IMG-MODEL-002` | Functional | Replace a question image after a game snapshot was created, then run orphan cleanup. | The old file and snapshot URL remain available while the snapshot exists. |
| `IMG-TEST-013` | `IMG-LIFE-002` | Functional | Upload an image and never attach it; allow seven days to elapse. | Cleanup removes its file and row after confirming no question or snapshot reference. |
