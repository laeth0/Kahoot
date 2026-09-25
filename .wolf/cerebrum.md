---
description: learned preferences, project conventions, and Do-Not-Repeat rules
budget_tokens: 2000
---
# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-09-24

## User Preferences

<!-- How the user likes things done. Code style, tools, patterns, communication. -->

- Backend instructions prohibit creating automated tests; verify with build, format, migration checks, and reasoned scenarios.

## Key Learnings

- **Project:** kahoot

## Do-Not-Repeat

- [2026-09-25] Do not reintroduce `tenantId` in Auth tokens or responses. The Host account ID is the only ownership identifier; the persistence model uses `HostAccountId`. The prior Auth docs were stale and have been updated.
<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

- [2026-09-25] The schema has a `users` table, not an `accounts` table. `HostAccountId` and `host_account_id` are ownership field names that reference the Host user's `users.id`; explain this mapping in docs instead of assuming a separate account entity.

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->

- [2026-09-25] Auth credential mutations lock the PostgreSQL user row before touching refresh-token rows. This serializes login, refresh, logout, logout-all, and password change across instances so revocation cannot leave a replacement token active.
