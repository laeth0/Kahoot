---
name: openwolf
description: OpenWolf operating protocol for this project. Use when starting multi-file work, navigating unfamiliar files, checking cerebrum learnings/Do-Not-Repeat, buglog searching, resuming or wrapping up a session.
---

# OpenWolf Operating Protocol

You are working in an OpenWolf-managed project. These rules apply every turn.

## Session Resume & Status
- **`.wolf/STATUS.md` is the handoff document**: Read it FIRST when resuming a session; it replaces re-reading memory, plans, and large swaths of code to reconstruct context.
- **Keep it fresh**: When a quest or significant task finishes, update `.wolf/STATUS.md` (or run `openwolf handoff`) to move finished items to Done and set up the next phase before concluding.

## File Navigation & Token Discipline
1. **Never read `.wolf/anatomy.md` whole**: It is a large index, not a document.
2. **Shortlist / symbol search**: Run `openwolf find <query>` in CLI or grep `.wolf/anatomy.md` for a single file path to inspect its summary, line ranges, and token estimate.
3. **Targeted reads**: If a file description answers your question, skip the full read. For large files, prefer reading with offset/limit.
4. **Index refresh**: If files are missing or newly created, run `openwolf scan`.

## Code Generation & Cerebrum Memory
1. **Before generating code**: Check `.wolf/cerebrum.md` (specifically `## Do-Not-Repeat`, `## Key Learnings`, and `## User Preferences`).
2. **Update cerebrum.md**: Whenever you learn a project convention, user correction, API surprise, or gotcha, record it in `.wolf/cerebrum.md`.

## Bug Logging & Memory
1. **Before fixing any bug**: Check `.wolf/buglog.json` (or run `openwolf bug search "<error>"`) to see if the root cause and fix are already known.
2. **After fixing a bug or failed build**: Append an entry to `.wolf/buglog.json` with `id`, `timestamp`, `error_message`, `file`, `root_cause`, and `fix`.

## Session Wrap-up
Before ending a session:
1. Update `.wolf/STATUS.md` with current accomplishments and next steps.
2. Log session summary line in `.wolf/memory.md`.
3. Record any new rules/preferences in `.wolf/cerebrum.md`.
