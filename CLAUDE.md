# OpenWolf & Engineering Guidelines

This project uses OpenWolf for token-conscious context management across all AI coding agents.

- **First Action**: Always read `.wolf/STATUS.md` at session start before reading code or taking action.
- **Standards**: Strictly follow `AGENTS.md` for all architectural, reliability, security, and engineering requirements.
- **Operating Protocol**: The always-on rules live in `.claude/rules/openwolf.md` and `.agents/rules/openwolf.md`. Check `.wolf/cerebrum.md` before generating code.
- **File Navigation**: Never read `.wolf/anatomy.md` whole; use `openwolf find <name>` or grep for single lines.
- **Bug Memory**: Search `.wolf/buglog.json` before debugging; record resolved fixes there.
- **Session End**: Update `.wolf/STATUS.md` and run `openwolf scan` when a task or milestone concludes.
