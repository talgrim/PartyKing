---
paths:
  - "CLAUDE.md"
  - "docs/**"
  - ".claude/rules/**"
---

`CLAUDE.md` is an index: commands, conventions, the layering rules and a map to `docs/`. Put details in `docs/` (`architecture/`, `features/`, `games/`, or a top-level file for UI, e2e and deployment), one topic per file, with a heading per subsystem and short paragraphs or bullets, not one long line.

- A new topic gets a file in `docs/`, a row in the table of `CLAUDE.md` and, when it maps to code paths, a rule here with a `paths:` list that points to the file.
- A rule file here only says when to read which doc; it does not repeat the doc.
- Keep docs current: change the doc in the same change as the code. Do not record what git history or the code already says.
