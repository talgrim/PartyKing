---
paths:
  - "Dockerfile"
  - "fly.toml"
  - "docker/**"
  - ".github/workflows/deploy.yml"
  - "src/api/PartyKing.API/appsettings*.json"
---

You are touching the Docker image, nginx, Fly.io or the upload hardening. Read `docs/deployment.md` first: it holds the details that are deliberately not in `CLAUDE.md`. If you change behaviour it describes, update it in the same change.
