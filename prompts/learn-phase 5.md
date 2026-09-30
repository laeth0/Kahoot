| Concept | Brief Meaning |
|---|---|
| **Public Bytes vs. Protected Metadata Separation** | Making uploaded image bytes publicly readable by URL while strictly protecting image ownership metadata, private inventories, and attachment rights as tenant resources. |
| **Magic Byte / Header Signature Sniffing** | Verifying a file's genuine format by inspecting its binary magic numbers before trusting the declared MIME type or file extension. |
| **Decompression Bomb Guard** | Validating declared pixel dimensions and capping the uncompressed decode memory budget ($\le 64\text{ MiB}$) before full bitmap allocation to prevent OOM crashes. |
| **Polyglot File Elimination via Re-encoding** | Decoding images into raw pixel buffers and reconstructing them in a canonical format to drop embedded scripts, trailing HTML, and corrupt container structures. |
| **EXIF / Metadata Stripping** | Completely removing all EXIF, IPTC, and XMP metadata (including GPS, camera serial, and creator tags) from uploaded images before storage. |
| **Orientation Correction Before Stripping** | Reading EXIF orientation flags and applying rotation/flip to pixel data *before* metadata stripping, preventing sideways or upside-down image rendering. |
| **UUIDv4 Server-Assigned Filenames (Path Traversal Prevention)** | Ignoring client-supplied filenames and assigning cryptographically random UUIDv4 filenames to eliminate path traversal risks. |
| **Immutable Public Image URLs** | Writing each image to a unique, never-overwritten path (`/uploads/{guid}.ext`) with `Cache-Control: public, max-age=31536000, immutable` response headers. |
| **Single Current Question Owner Constraint** | Enforcing a unique index on `(ImageId, HostAccountId)` so each image may be attached to at most one current question at a time. |
| **Historical Snapshot Retention Protection** | Preserving image files and records indefinitely while any game question snapshot holds a foreign key reference, regardless of whether the current question still uses the image. |
| **Database-First Orphan Cleanup** | Deleting the `QuestionImage` database row first (under a `FOR UPDATE SKIP LOCKED` lock with re-verification) before unlinking the file, so foreign key constraints prevent concurrent attachment to a deleted record. |
| **Unreferenced Image Retention Window** | Retaining unreferenced image records and files for at least 7 days (`UnreferencedSince`) before cleanup eligibility, providing a safe window for upload-retry scenarios. |
| **Staging Directory for Incomplete Uploads** | Writing upload streams to a quarantine staging directory and sweeping staging files older than 24 hours to handle interrupted upload sessions without polluting permanent storage. |
| **File-DB Failure Compensation** | Handling the non-atomic relationship between filesystem writes and database commits by deleting staged files on known failure and running periodic filesystem reconciliation to remove orphan files with no matching database record. |
| **Storage Exhaustion Pre-check** | Verifying available disk space before accepting an upload stream and returning `503 Image.StorageUnavailable` when free space falls below 10% to prevent disk-full corruption. |
| **Animation Frame Extraction (First-Frame Policy)** | Processing animated WebP and APNG by extracting strictly the first frame as a static image, discarding animation data. |
