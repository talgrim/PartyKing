---
paths:
  - "src/api/PartyKing.Persistence/**"
  - "src/api/PartyKing.Infrastructure/**"
  - "src/api/PartyKing.API/Metrics/**"
  - "src/api/PartyKing.API/ErrorHandling.cs"
  - "src/api/PartyKing.API/Controllers/CoreController.cs"
---

You are touching repositories, DbContexts, migrations, metrics or error mapping. Read `docs/architecture/persistence-and-concurrency.md` first: it holds the details that are deliberately not in `CLAUDE.md`. If you change behaviour it describes, update it in the same change.
