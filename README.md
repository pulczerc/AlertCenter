# AlertCenter

Polls RSS feeds, matches articles against per-user keyword alerts, and delivers
notifications via Email/Slack (mock-first), with an admin SPA for Users, Alerts, and
Notification history.

Built as an AI-assisted engineering exercise — the full analysis → architecture →
design → review → implementation trail lives in [`docs/`](docs/) (start with
[`docs/current-status.md`](docs/current-status.md)).

## Architecture (at a glance)

- **.NET 8 hexagonal modular monolith + DB Outbox** ([ADR-001](docs/adr/ADR-001-hexagonal-monolith-outbox.md), [ADR-002](docs/adr/ADR-002-architect-review.md))
  - Pure domain + application core (no infra deps); all integrations behind **ports**
  - **DB Outbox** for durable, at-least-once delivery (visibility-timeout lease)
  - Restartable matching via an article `evaluated_at` watermark
- **React + Vite + TypeScript** admin SPA
- **Datastore:** SQLite by default (the design targets PostgreSQL; see [`docs/06-db-design.md`](docs/06-db-design.md))

```
src/AlertCenter.Core            domain + application + ports (pure)
src/AlertCenter.Infrastructure  EF/SQLite, RSS, mock channels, timers, DI
src/AlertCenter.Api             ASP.NET Core minimal API (composition root)
web/                            React + Vite + TS admin SPA
tests/                          Core (unit) · Infrastructure (SQLite) · Api (e2e)
```

## Prerequisites

- **.NET SDK 8** (pinned via `global.json`; this repo uses 8.0.x)
- **Node.js 18+** and npm (for the SPA)

## Run

**1 — Backend API** (creates the SQLite DB and, in Development, seeds a demo user + alerts):

```bash
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/AlertCenter.Api
```

Swagger is served at `http://localhost:5080/swagger` in Development.

**2 — Admin SPA** (proxies `/api` → `http://localhost:5080`):

```bash
cd web
npm install      # first time only
npm run dev      # http://localhost:5173
```

**Try it:** open the SPA → **Ops → “Poll feeds now”** (pulls BBC headlines and matches
the seeded alerts) → **“Dispatch outbox now”** → open **Notifications** to see them as
`sent`. The background timers also do this automatically (poll every 5 min, dispatch
every 15 s — see `appsettings.json`).

### Key API endpoints (`/api/v1`)

| Method · Path | Purpose |
|---------------|---------|
| `POST /ops/poll` · `POST /ops/dispatch` · `GET /ops/health` | manual triggers + health (demo) |
| `GET /notifications` (`?status=`) · `GET /notifications/{id}` | delivery history (read-only) |
| `GET/POST /users` · `PATCH /users/{id}` | manage users |
| `GET/POST /alerts` · `PATCH /alerts/{id}` | manage alerts |

## Test

**Backend** (unit + SQLite integration + WebApplicationFactory e2e):

```bash
dotnet test
```

**Frontend** (Vitest + React Testing Library):

```bash
cd web && npm test
```

All tests run without Docker or network (the e2e suite uses in-memory SQLite, a fake
feed, and disabled timers).

## Configuration & secrets

- Feeds, scheduling intervals, and the connection string live in
  `src/AlertCenter.Api/appsettings.json`.
- **No secrets are committed** — `appsettings.example.json` documents the shape; real
  channel credentials belong in environment variables / user-secrets.

## MVP scope

In: RSS ingestion (dedup), keyword alerts (OR, whole-word, case-insensitive,
title+summary), Email/Slack delivery via a pluggable mock sender, admin for
Users/Alerts/Notifications. Out: regex/NLP matching, multi-tenancy/RBAC, real-time
push. See [`docs/01-requirements-analysis.md`](docs/01-requirements-analysis.md).
