# Media Handling and Business Rules Architecture

## 1. Business Rule: Question Images Only

The quiz media model enforces that images are supported **only for questions**, never for answer choices:

- **Questions**: Support an optional image via `imageUrl` (`/uploads/<guid>.<extension>`). Existing images are preserved during question edits unless explicitly replaced or cleared.
- **Answer Choices**: Strictly text-only. Choice objects contain only `text`, `isCorrect`, and required metadata (`id`, `orderIndex`).
- **Choice Validation**: Every choice requires non-empty text between 1 and 300 characters.

Any image upload capabilities or references in answer choices have been completely removed across:
- Domain entities (`Choice.cs`, `GameChoiceSnapshot.cs`)
- Application DTOs and contracts (`QuizContracts.cs`, `GameContracts.cs`)
- API endpoints and handlers (`AddQuestion`, `UpdateQuestion`, `PublishQuiz`, `GetQuiz`, `CreateGame`, SignalR mappers)
- Database schema (`choices` and `game_choice_snapshots` tables drop `image_url`)
- Frontend UI (`QuestionFormDialog`, `ChoiceEditorRow`)

---

## 2. Media Validation and Canonical Path Scheme

To prevent SSRF, content injection, directory traversal, and request exfiltration, the platform strictly enforces canonical same-origin media references:

- **Canonical Format**: `/uploads/<guid>.<extension>`
- **Allowed Extensions**: `.jpg`, `.jpeg`, `.png`, `.webp` (case-insensitive)
- **Disallowed Formats**: `.gif` is completely disallowed.
- **GUID Validation**: The filename stem must parse as a valid GUID (supporting both 32-hex `N` format and standard 36-character hyphenated `D` format).
- **Rejected Patterns**:
  - External URLs (`http://`, `https://`)
  - Protocol-relative URLs (`//`)
  - Data URIs (`data:image/...`)
  - Blob URIs (`blob:...`)
  - Directory traversal (`..`, `\`, `/`)
  - Non-GUID filenames (e.g., `/uploads/photo.jpg`)

Validation is strictly consistent across backend FluentValidation rules (`QuestionValidationRules.cs`) and frontend URL resolution (`frontend/src/api/media.ts`). The frontend resolver returns `null` for any non-canonical or untrusted URL, ensuring the browser never triggers third-party requests.

---

## 3. Media Ownership and Storage Verification

Clients cannot fabricate arbitrary `/uploads/<guid>.<extension>` paths:
- Question image paths must originate from the server-authoritative upload flow (`POST /api/uploads/images`).
- When adding or modifying questions with an `imageUrl`, the backend verifies that the file physically exists in server storage (`IFileStorage.Exists`).
- If a client supplies a non-existent or fabricated media path, the request fails with `QuizErrors.InvalidMediaReference`.

---

## 4. Image Decoding, Normalization, and Safe Storage

All uploaded images are processed and normalized server-side using `SixLabors.ImageSharp`:
1. **Length Check**: Files must not be empty and must not exceed the configured maximum size (`FileStorage:MaxSizeBytes`, 5 MB default).
2. **Signature Verification**: Header magic bytes are checked against known signatures (`ImageSignature.Matches`) for JPEG, PNG, and WebP before full parsing.
3. **Dimension Verification**: `Image.IdentifyAsync` checks that image dimensions do not exceed 4096px (width or height) and that total pixel count does not exceed 16 MP (16,777,216 pixels), mitigating decompression bomb attacks.
4. **Metadata Stripping**: `Image.LoadAsync` decodes the image and strips all metadata profiles (`ExifProfile`, `IptcProfile`, `XmpProfile`), eliminating location/device data leakage and polyglot metadata exploits.
5. **Re-encoding**: Images are re-encoded into clean streams and saved under server-generated version-7 GUID filenames (`{guid:n}.{ext}`).

---

## 5. Caching and Delivery Security Headers

Media files served under `/uploads/` are immutable content-addressed assets:
- `Cache-Control: public, max-age=31536000, immutable`
- `X-Content-Type-Options: nosniff`
- Exact `Content-Type` matching the validated extension

These headers are enforced both at the ASP.NET Core application level (`app.UseStaticFiles` `OnPrepareResponse`) and at the Nginx reverse proxy level (`nginx/default.conf` and `nginx/default.prod.conf`).

---

## 6. Storage Lifecycle & Deployment Notes

- **No Runtime Orphan Cleanup**: To keep the runtime simple and avoid unintended race conditions with active games and quiz editing, automated background deletion jobs are not run.
- **Pre-Production / Deployment Reset**: During deployment, storage volumes and database containers are reset clean, discarding old files and unneeded historical data.
