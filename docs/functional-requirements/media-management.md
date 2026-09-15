# Media Management

## Overview

Hosts can upload validated images and attach their stored URLs to questions.

## User Stories

### US-007: Add images to quiz content

**As a**
host

**I want**
to attach images to questions

**So that**
my quiz can include visual content.

## Acceptance Criteria

- Given an authenticated host and a valid image, when the host uploads it as the `file` field of a `multipart/form-data` request to `POST /api/uploads/images`, then the API returns `{ "url": "<relative path>" }`.
- Given a returned image URL, when a question is created or updated with it, then that URL is stored with the question.
- Given a stored image, when a client requests its relative URL under the configured public base path, then the API serves the static content.

## Business Rules

### FR-3.1: Images

- A host may attach one optional image to a question. Choices are text-only.
- Accepted content types are configured by `FileStorage:AllowedContentTypes`; the defaults are JPEG, PNG, and WebP.
- The server validates image magic bytes rather than trusting only the declared content type.
- The server enforces `FileStorage:MaxSizeBytes`, which defaults to 5 MB.
- The server enforces a maximum width or height of 4,096 pixels and a maximum total area of 16,777,216 pixels unless configured otherwise.
- Accepted images are decoded and re-encoded in their validated format, with EXIF, IPTC, and XMP metadata removed before storage.
- Returned image URLs are paths relative to the API origin.
- Clients compose absolute media URLs using the configured API base URL.
- The API serves stored files under `FileStorage:PublicBasePath`, which defaults to `/uploads`.
- Question create and update accept only canonical server-generated paths shaped as `/uploads/<guid>.<extension>`, and the referenced file must exist.

## Edge Cases

- An upload is rejected when no file is provided, the file is empty, its declared type is not allowed, its signature is invalid, it is corrupt, its byte size exceeds the configured maximum, or its dimensions exceed either configured limit.
- A question image reference is rejected when its path is non-canonical, contains path traversal or nested path segments, has an unsupported extension, or does not exist in storage.
