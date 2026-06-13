# Implementation Plan

> **Author:** Solution Architect + Developer (AI-assisted)
> **Date:** 2026-06-13 · **Status:** Draft — awaiting Reviewer + human validation (Step 7 entry)
> **Inputs (all validated):** [`04-domain-model.md`](04-domain-model.md), [`05-api-design.md`](05-api-design.md), [`06-db-design.md`](06-db-design.md), [`07-ui-design.md`](07-ui-design.md), [`adr/ADR-001`](adr/ADR-001-hexagonal-monolith-outbox.md), [`adr/ADR-002`](adr/ADR-002-architect-review.md)
> **Stack (pinned):** .NET 8 (ASP.NET Core Web API + EF Core/Npgsql), PostgreSQL, React + Vite + TypeScript SPA. **This document plans; it writes no code.**

---

## 1. Approach

> **Re-anchored to the timebox (RF-005-A).** Most of the 3–4h (NFR-1) was spent on the
> design/review docs, so this plan is built around a **minimum shippable slice as the
> primary deliverable**, with everything else marked **🔶 stretch**. Infra defaults are
> set *low* and only upgraded if time remains.

- **Primary goal — the vertical slice (must-ship):** one end-to-end path running:
  `ops/poll → ingest → evaluate (match) → enqueue notification+outbox → ops/dispatch →
  mock send → GET /notifications shows sent`. This alone demonstrates AC-1/2/3.
- **Hexagon-first (AD-7):** build the pure core (domain + application + ports) before any
  adapter; adapters depend inward only.
- **Default infra *down* for the slice (upgrade only if time allows):**
  - **DB:** **SQLite** behind the repo/outbox ports for the slice (ADR-001 cut #1);
    PostgreSQL + `SKIP LOCKED` is 🔶 stretch.
  - **Messaging:** **direct in-process use-case calls**; MediatR/`IEventPublisher` is
    🔶 stretch (ADR-002: "recommended, not load-bearing").
  - **Tests:** **domain unit tests are must-have**; Testcontainers/`WebApplicationFactory`
    integration and the SPA test stack are 🔶 stretch (see §8/§F).
  - **Skip for the slice:** NetArchTest, CI — 🔶 stretch.
- **TDD per the `developer` agent:** tests-first on the domain; widen test layers only
  as the budget permits.
- **Cut-line:** the ADR-001 *Timebox contingency* (SQLite → drop SPA → single timer →
  mock-only) is the ordered fallback if the slice is at risk.

---

## 2. Solution structure

```
AlertCenter.sln
 ├─ src/
 │   ├─ AlertCenter.Core           # Stream 1 — domain + application + ports (NO infra deps)
 │   │   ├─ Shared/                #   Channel, ids, IClock, domain-event contracts, IEventPublisher
 │   │   ├─ Ingestion/             #   Article; IFeedSource, IArticleRepository; PollFeeds; ArticleIngested
 │   │   ├─ Alerts/                #   User, Alert, Keyword, KeywordMatcher; IUser/IAlertRepository, IAlertQuery;
 │   │   │                         #   ManageUsers, ManageAlerts, EvaluateAlerts; ArticleMatched
 │   │   ├─ Notifications/         #   Notification, OutboxEntry; INotificationRepository, IOutboxPort;
 │   │   │                         #   EnqueueNotification, DispatchOutbox, ViewNotifications; NotificationEnqueued
 │   │   └─ Channels/              #   INotificationChannel (port), IChannelResolver
 │   ├─ AlertCenter.Infrastructure # Stream 2 — adapters (EF, RSS, senders, scheduler, MediatR, migrations)
 │   └─ AlertCenter.Api            # Stream 3 — inbound HTTP adapter + composition root + SPA hosting
 ├─ tests/
 │   ├─ AlertCenter.Core.Tests           # unit (fast, no I/O)
 │   ├─ AlertCenter.Infrastructure.Tests # integration (Testcontainers Postgres)
 │   └─ AlertCenter.Api.Tests            # integration (WebApplicationFactory + Testcontainers)
 └─ web/                           # Stream 4 — React + Vite + TS SPA
```

> **Module isolation (ADR-002 M-2) — pragmatic choice:** modules live as **folders/
> namespaces inside `AlertCenter.Core`**, with a **NetArchTest** rule enforcing "no
> module references another module's internals; only Shared Kernel + public ports
> cross." Splitting into one project per module is the documented later step; folders
> keep the timebox while preserving the boundary contract. *(Flag for review.)*
> **Core stays MediatR-free:** domain events are plain records; an `IEventPublisher`
> port is implemented by a MediatR adapter in Infrastructure (keeps AD-7 purity).

---

## 3. Stream 1 — Domain (the hexagon core)

**Owns:** entities, value objects, the pure matcher, application use cases, all port
**interfaces**. The foundation every backend stream depends on.

### Files to create
| Area | Files |
|------|-------|
| Shared | `Channel`, `UserId/AlertId/ArticleId/NotificationId`, `IClock`, `DomainEvent` records, `IEventPublisher` |
| Ingestion | `Article` (+ `EvaluatedAt`), `IFeedSource`, `IArticleRepository`, `PollFeedsUseCase`, `ArticleIngested` |
| Alerts | `User`, `Alert`, `Keyword`, **`KeywordMatcher`**, `IUserRepository`, `IAlertRepository`, `IAlertQuery`, `ManageUsersUseCase`, `ManageAlertsUseCase`, `EvaluateAlertsUseCase`, `ArticleMatched` |
| Notifications | `Notification`, `OutboxEntry` (**carries a rendered `payload`**), status enums, `INotificationRepository`, `IOutboxPort`, `EnqueueNotificationUseCase` (**renders the `OutboxMessage` at enqueue** from match-time data), `DispatchOutboxUseCase`, `ViewNotificationsUseCase`, `NotificationEnqueued` |
| Channels | `INotificationChannel.Send(OutboxMessage)`, `IChannelResolver`; **`OutboxMessage`** value (recipient, subject, body, channel) |

### Dependencies
**None** (depends only on the .NET BCL). This is the root of the dependency graph.

### Tests required (unit — `AlertCenter.Core.Tests`)
- **`KeywordMatcher`**: OR semantics (Q-1), whole-word case-insensitive (Q-7),
  title+summary scope (Q-2), single-token guard (D-007 #1), punctuation/empty edge
  cases → satisfies **AC-2** logic.
- **Invariants:** Alert ≥1 keyword + dedup; `Notification` status machine
  (no illegal transitions); `OutboxEntry` lifecycle.
- **Use cases against in-memory fakes:** `EvaluateAlerts` creates notifications
  idempotently (no dupe for same alert+article, FR-7); `DispatchOutbox` transitions
  pending→sent/failed and respects backoff; `EnqueueNotification` writes notification
  + outbox together; `ManageUsers/Alerts` validation.

### Order
Shared → entities/VOs → `KeywordMatcher` → ports → use cases. **Must be first.**

---

## 4. Stream 2 — Infrastructure (adapters)

**Owns:** every outbound adapter + the DB schema/migrations + scheduling + messaging.

### Files to create
| Area | Files |
|------|-------|
| Persistence | `AlertCenterDbContext`, EF `*Configuration` per table, **`Migrations/`** (matches [`06`](06-db-design.md) DDL), `UserRepository`, `AlertRepository`(+`AlertQuery`), `ArticleRepository`, `NotificationRepository`, **`OutboxRepository`** (lease via `FOR UPDATE SKIP LOCKED` + visibility timeout, RF-003-A), transaction/unit-of-work helper |
| Feeds | `RssFeedSource` (`System.ServiceModel.Syndication`/parser) → `IFeedSource`; `FeedOptions` (Reuters/BBC seeds) |
| Channels | `MockEmailChannel`, `MockSlackChannel` → `INotificationChannel`; `ChannelResolver` (by `Channel` enum); real senders are drop-in, creds from config only (NFR-4) |
| Scheduling | **`IngestionHostedService`** — one timer that runs **`PollFeeds` then `EvaluateAlerts`** sequentially each tick (RF-005-C): poll persists new articles, evaluate then drains **un-evaluated** articles (`evaluated_at IS NULL`, RF-003-B) → matches → enqueue. **`DispatchHostedService`** — second timer running `DispatchOutbox`. Two timers total; interval config (NFR-5). 🔶 contingency: collapse both into one tick (ADR-001 cut #3). |
| Messaging | MediatR registration; `MediatrEventPublisher : IEventPublisher`; handlers (`ArticleIngested`→evaluate nudge; `ArticleMatched`→enqueue) — durable triggers remain the watermark/outbox (M-4) |
| Misc | `SystemClock : IClock`; `appsettings.json` + `appsettings.example.json` (no secrets, AC-5) |

> **Message payload — resolved at enqueue, not at dispatch (RF-005-D).** When a match is
> enqueued, `EnqueueNotification` **renders an `OutboxMessage`** (recipient + subject +
> body) from match-time data — Email recipient = the owner's address (via `IAlertQuery`/
> user), Slack target = the **system webhook** from config (Q-3) — and stores it on the
> **outbox row** (new `payload` column, see [`06`](06-db-design.md)). The dispatcher then
> calls `INotificationChannel.Send(payload)` with **no cross-module reads** and the
> Channels adapter stays domain-ignorant. Consistent with the channel snapshot (N3): the
> message reflects match-time state even if the alert/user changes later. (Channel creds
> stay in config, never in the payload — NFR-4.)

### Dependencies
**Stream 1** (implements its ports) · PostgreSQL · NuGet: Npgsql.EFCore, MediatR,
Testcontainers (test-only).

### Tests required (integration — `AlertCenter.Infrastructure.Tests`, Testcontainers PG)
- Repos CRUD + **unique constraints** `(source,guid)` and `(alert_id,article_id)`.
- **Match→enqueue transaction**: notification + outbox written atomically; `ON
  CONFLICT DO NOTHING`; `evaluated_at` watermark set → re-run evaluates once (AC-1, R-6).
- **Outbox lease concurrency**: two simulated dispatchers → **no double-dispatch**
  (visibility timeout, RF-003-A); dead-letter after max attempts.
- **RSS parser**: sample feed fixtures → normalized articles; tolerate a bad feed (R-2).
- **Mock channels** record sends; `ChannelResolver` picks by enum.

### Order
DbContext + configs + **first migration** → repositories → outbox lease → clock/event
publisher → RSS adapter → mock channels → hosted services.

---

## 5. Stream 3 — API (inbound adapter + composition root)

**Owns:** HTTP surface from [`05`](05-api-design.md), DTO mapping, validation, the
RFC-7807 error model, DI wiring, CORS, SPA hosting.

### Files to create
| Area | Files |
|------|-------|
| Host | `Program.cs` (composition root: wire Core + Infrastructure, MediatR, hosted services, CORS, SPA fallback, ProblemDetails) |
| Controllers | `UsersController`, `AlertsController`, `NotificationsController`, `OpsController` |
| Contracts | `UserDto`, `AlertDto` (**incl. `ownerName`**, RF-004-A), `NotificationDto` (+ embedded article), request models, list-envelope, mappers |
| Validation | validators: keyword single-token/≤60 (RF-004-G), email, channel enum, ≥1 keyword |
| Errors | `ProblemDetails` middleware mapping `400/404/409/422/500` (no secret leakage, NFR-4) |
| Config | CORS (SPA origin only), Swagger (dev), options binding |

### Dependencies
**Stream 1** (use cases/ports) at compile time; **Stream 2** at runtime (DI). Can be
built/tested against fakes before Infrastructure is done.

### Tests required (integration — `AlertCenter.Api.Tests`, WebApplicationFactory)
- Endpoint contracts + status codes: `201/400/404/409/422`; RFC-7807 error shape;
  list envelope + pagination; `AlertDto.ownerName` present; `404` unknown vs `422`
  disabled user (RF-003-H).
- **End-to-end vertical slice:** `POST /ops/poll` → notification created (AC-2) →
  `POST /ops/dispatch` → status `sent`, visible via `GET /notifications` (AC-3).
- Ops endpoints return `200` with counts (RF-003-G).

### Order
`Program`/DI skeleton + `/ops/health` → ProblemDetails middleware → Users → Alerts →
Notifications → Ops. DTO mappers alongside each controller.

---

## 6. Stream 4 — Frontend (React + Vite + TS SPA)

**Owns:** the admin SPA from [`07`](07-ui-design.md). Depends only on the **frozen API
contract** — not a running backend — so it parallelizes from day 0.

### Files to create
| Area | Files |
|------|-------|
| Scaffold | `web/` (Vite React-TS), `vite.config.ts` (proxy `/api`→backend + history fallback), `tsconfig`, `index.html` |
| API layer | `api/client.ts`, `usersApi.ts`, `alertsApi.ts`, `notificationsApi.ts`, `opsApi.ts`, `types.ts` (DTOs mirror [`05`](05-api-design.md)), `problemDetails.ts` (error→field map) |
| Shell | `App.tsx`, `routes.tsx` (history mode, RF-004-H), `Layout` (left nav) |
| Pages | `NotificationsPage` (+ `DetailsDrawer`, auto-refresh, by-alert filter), `UsersPage` (+ form), `AlertsPage` (+ `AlertForm`, disabled-owner block), `OpsPage` |
| Components | `DataTable`, `FilterBar`, `ResourceForm`, **`KeywordChipsInput`** (single-token guard), `StatusBadge`, `ConfirmDialog`, `Toast`, `Pagination` |
| Hooks | `useAutoRefresh` (off-by-default, RF-004-E), `useApi` |

### Dependencies
**API contract (`05`)** only. Runtime integration via the Vite proxy once the API runs.

### Tests required (Vitest + React Testing Library; fetch mocked via MSW)
- `KeywordChipsInput` rejects whitespace/over-length (RF-004-G); form error binding
  from RFC-7807; `StatusBadge` mapping; `DataTable`/pagination; `problemDetails` mapper;
  disabled-owner edit is blocked (RF-004-B). Optional: one Playwright happy-path.

### Order
scaffold + `client`/`types` → shell/routing → **Notifications** (read-only, simplest,
the "is it working" view) → Users → **Alerts** (most complex form) → Ops.

---

## 7. Dependency graph & parallelization

> **What "parallel" means here (RF-005-E):** with a single (AI-assisted) implementer the
> streams do **not** run concurrently in wall-clock — the value is **logical
> independence**: stable contracts let work be **reordered, deferred, or cut** without
> blocking other streams. Read the graph as "safe to build in any order consistent with
> the arrows," **not** "4× faster." Estimates in §8 are single-threaded.

```
            ┌─────────────────────────────────────────────┐
            │  Stream 1: DOMAIN (core)  ← foundation       │
            └───────────────┬──────────────┬──────────────┘
                            │              │
        (ports stable) ─────┤              ├───── (ports stable)
                            ▼              ▼
        ┌───────────────────────┐  ┌───────────────────────┐
        │ Stream 2: INFRA       │  │ Stream 3: API          │   ◄── these two run in PARALLEL
        │ (impl ports)          │  │ (uses use cases/ports) │
        └───────────┬───────────┘  └───────────┬───────────┘
                    └──────────────┬───────────┘
                                   ▼
                          Composition (Program.cs) → end-to-end

   Stream 4: FRONTEND  ──────────────────────────────────────►  (parallel throughout;
   depends only on the frozen API contract 05; integrate at the end)
```

**What can run in parallel:**
- **Frontend ∥ all backend** — from the start. The contract is frozen and validated;
  the SPA develops against typed DTOs + MSW mocks, integrating via the Vite proxy last.
- **Infrastructure ∥ API** — as soon as Stream 1's **port interfaces + use case
  signatures** exist (early). API tests against fakes; Infra against Testcontainers.
- **Serial constraint:** Domain (at least its ports) must land **before** Infra/API
  can compile; composition/end-to-end is **after** Infra + API.

**Critical path:** Domain ports → Infra repos/outbox → composition → e2e vertical slice.

---

## 8. Sequencing (waves) — single-threaded estimates

Estimates are rough wall-clock for one AI-assisted implementer (RF-005-B). The
**must-ship line is after Wave 3**; everything below it is **🔶 stretch**, attempted
only if the budget allows.

| Wave | Work | Est. | Exit criterion |
|------|------|------|----------------|
| **0 — Skeleton** | solution + 3 src projects (no CI); Domain Shared Kernel; `/ops/health`→200; **SQLite** DbContext boots | ~20m | app boots, health green |
| **1 — Domain core** | ① entities/VOs/**`KeywordMatcher`** + unit tests; minimal use cases (PollFeeds, EvaluateAlerts, Enqueue, DispatchOutbox) | ~45m | matcher + use-case unit tests green |
| **2 — Minimal infra + API** | ② SQLite repos + outbox (simple `UPDATE…RETURNING` lease), RSS adapter, mock channels, ingestion+dispatch timers, **direct** wiring (no MediatR); ③ `ops/*` + `notifications` GET + ProblemDetails | ~60m | repos + ops endpoints work |
| **3 — VERTICAL SLICE** ✅ | wire `Program.cs`; e2e `ops/poll → match → outbox → mock send → GET /notifications` = sent | ~30m | **AC-1/2/3 pass end-to-end — MUST-SHIP** |
| ════ must-ship line ════ | | **~2.5h** | demoable slice complete |
| **4 — Breadth API** 🔶 | Users + Alerts CRUD (+`ownerName`), enable/disable, validators, filters | ~45m | AC-4 API |
| **5 — SPA** 🔶 | React scaffold + apiClient; Notifications, Users, Alerts, Ops pages | ~60m | AC-4 UI |
| **6 — Harden** 🔶 | PostgreSQL + `SKIP LOCKED`; MediatR/`IEventPublisher`; Testcontainers + `WebApplicationFactory` tests; NetArchTest; dev seeder | ~60m | full design realized |

**Test priority (RF-005-F):** domain unit tests (Wave 1) are **must-have**; integration
+ SPA tests land in Wave 6 (🔶). **Dev seeder (RF-005-G):** a seeded user+alert — pull
it **into Wave 4** if the SPA (Wave 5) is in play, else it stays in Wave 6.

> **Cut-line:** if Wave 3 is at risk, stop adding breadth and protect the slice via the
> ADR-001 contingency order (SQLite already chosen → drop SPA → single timer → mock-only).

---

## 9. Definition of done (acceptance criteria)

| AC | Verified by |
|----|-------------|
| **AC-1** new articles stored once | Infra test: `(source,guid)` unique + watermark re-run |
| **AC-2** match → exactly one notification | Domain `KeywordMatcher` test + Infra `(alert,article)` unique + API e2e |
| **AC-3** delivery status pending→sent/failed, visible | Infra outbox test + API `ops/dispatch`→`GET /notifications` |
| **AC-4** admin lists users/alerts, views history | API endpoint tests + SPA pages |
| **AC-5** no secret in source/config | `appsettings.example` only; config review; `.gitignore` |

---

## 10. Cross-cutting & risks

- **Config/secrets (NFR-4, AC-5):** all channel creds via env/user-secrets;
  `appsettings.example.json` documents shape, real values untracked.
- **Observability (NFR-3):** structured logging on poll/dispatch; `ops/health` exposes
  outbox depth.
- **EF schema fidelity (RF-005-H):** EF migrations won't auto-reproduce `06`'s **partial
  indexes** (`WHERE status='pending'`/`evaluated_at IS NULL`), **CHECK** enums, or the
  raw lease — configure these explicitly (`HasFilter`, `HasCheckConstraint`) and run the
  outbox lease as **raw SQL** (Postgres `SKIP LOCKED`; SQLite single-row `UPDATE…RETURNING`).
- **Risks:** RSS variance (R-2 → tolerant parser, per-feed isolation); timebox (R-1 →
  §1 contingency); Testcontainers needs Docker (fallback: SQLite for Infra tests).

> **Review handoff:** this plan should be challenged by the Reviewer agent (esp. the
> module-folder vs project trade-off and the wave sequencing) and human-validated
> before coding begins. Findings → [`review-findings.md`](review-findings.md).
