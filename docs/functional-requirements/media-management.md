# Media Management

## Overview

Hosts can upload validated images and attach their stored URLs to questions or
choices.

## User Stories

### US-007: Add images to quiz content

**As a**
host

**I want**
to attach images to questions and answer choices

**So that**
my quiz can include visual content.

## Acceptance Criteria

- Given an authenticated host and a valid image, when the host uploads it as the `file` field of a `multipart/form-data` request to `POST /api/uploads/images`, then the API returns `{ "url": "<relative path>" }`.
- Given a returned image URL, when a question or choice is created or updated with it, then that URL is stored with the content.
- Given a stored image, when a client requests its relative URL under the configured public base path, then the API serves the static content.

## Business Rules

### FR-3.1: Images

- A host may attach an image to a question, a choice, or both.
- Accepted content types are configured by `FileStorage:AllowedContentTypes`; the defaults are JPEG, PNG, WebP, and GIF.
- The server validates image magic bytes rather than trusting only the declared content type.
- The server enforces `FileStorage:MaxSizeBytes`, which defaults to 5 MB.
- Returned image URLs are paths relative to the API origin.
- Clients compose absolute media URLs using the configured API base URL.
- The API serves stored files under `FileStorage:PublicBasePath`, which defaults to `/uploads`.

## Edge Cases

- An upload is rejected when its declared type is not allowed, its signature is invalid, or its size exceeds the configured maximum.
- A choice without text remains valid only when it has an image.
