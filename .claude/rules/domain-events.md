---
paths:
  - "src/api/PartyKing.Domain/Events/**"
  - "src/api/PartyKing.Domain/Entities/Base/**"
  - "src/api/PartyKing.Application/Events/**"
  - "src/api/PartyKing.Application/Realtime/**"
  - "src/api/PartyKing.Application/**/Handlers/**"
  - "src/api/PartyKing.API/Hubs/**"
  - "src/ui/PartyKing.UI/src/**/useSessionMusicHub*"
---

You are touching domain events, handlers, notifiers or the SignalR hub. Read `docs/architecture/domain-events-and-realtime.md` first: it holds the details that are deliberately not in `CLAUDE.md`. If you change behaviour it describes, update it in the same change.
