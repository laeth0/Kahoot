---
description: OpenWolf context management and operating protocol for all AI agents
globs: **/*
---

# OpenWolf Operating Protocol

This project uses OpenWolf for token-conscious context management. Every AI agent (Codex, Antigravity, Gemini, Claude, Cursor) must follow these rules:

1. **Session Start / Resume**: Read `.wolf/STATUS.md` first. It contains the current status, recent accomplishments, and active quest goals. Do not waste tokens re-reading old memory or whole code trees.
2. **File Navigation**:
   - To locate a file or symbol, run `openwolf find <name>` in CLI or grep `.wolf/anatomy.md` for the file's path.
   - **NEVER** read `.wolf/anatomy.md` completely; it is an index (~40KB+).
   - Use `openwolf find --file <path>` for file summary and symbol line ranges.
3. **Before Generating Code**:
   - Check `.wolf/cerebrum.md` (grep for `## Do-Not-Repeat` and `## User Preferences`).
   - Respect past mistakes, architectural invariants, and user preferences.
4. **Before Fixing Any Bug**:
   - Run `openwolf bug search "<error>"` or grep `.wolf/buglog.json` to see if a fix is already recorded.
   - After resolving a bug or failed build, log the fix in `.wolf/buglog.json`.
5. **Session Wrap-Up**:
   - Update `.wolf/STATUS.md` before concluding multi-file tasks.
   - Run `openwolf scan` if new files were created or modified.
