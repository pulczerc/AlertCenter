# Architecture Options

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-12 · **Revised:** 2026-06-13 (human steering — see §0 & prompt trail [`002`](prompts/002-architecture.md))
> **Status:** Proposed — awaiting human ratification of remaining sub-decisions (Step 3)
> **Inputs:** [`01-requirements-analysis.md`](01-requirements-analysis.md) (FR-1…FR-13, NFR-1…NFR-5), binding decisions Q-1…Q-7 ([`decision-log.md`](decision-log.md) D-002), and human architectural steering of 2026-06-13.

---

## 0. Human steering (2026-06-13) — supersedes the original recommendation

The first draft of this document recommended a **synchronous monolith (Option A)**
optimized primarily for the timebox. The human reviewer **rejected that framing**:

> "The goal is a clean, long-term maintainable architecture, not just a
> timebox-optimized solution… modular monolith with a Hexagonal (Ports &
> Adapters) architecture + DB Outbox pattern. The domain/application layer must
> remain clean and independent from infrastructure; external integrations (news
> polling, email/Slack, persistence) accessed only through ports; asynchronous
> delivery via a DB Outbox (pending → dispatch → sent/failed). Option A is too
> tightly coupled; Option C is overkill; Option B is the right direction **only
> if** strengthened with explicit ports/adapters and a proper outbox model."

Accordingly this revision:
- **elevates maintainability/testability/separation-of-concerns (AD-7) to a top driver**, on par with timebox;
- recasts **Option B** as a **Hexagonal modular monolith + DB Outbox** and makes it the recommendation;
- keeps A and C only as the rejected bookends of the design space.

---

## 1. Architectural drivers

| # | Driver | Source | Weight |
|---|--------|--------|--------|
| AD-1 | **Timebox fit** — buildable in the remaining slice of 3–4h | NFR-1, R-1 | ★★★ |
| AD-7 | **Maintainability & testability** — clean domain isolated from infrastructure; integrations behind ports | **Human steer 2026-06-13** | ★★★ |
| AD-2 | **Extensibility of channels & sources** — Email/Slack/RSS pluggable, mock-first | Q-5, FR-8/9, A-2 | ★★★ |
| AD-3 | **Observability** — notification status + ingestion runs inspectable | NFR-3, FR-10, AC-3 | ★★ |
| AD-4 | **Reliability** — at-least-once, retry, no duplicate notifications | NFR-2, FR-7, R-3/R-6 | ★★ |
| AD-5 | **Secret hygiene** — channel creds are config, never committed | NFR-4, R-4, AC-5 | ★★ |
| AD-6 | **Single deployable** — demo scale, single tenant, no distributed infra | A-1, NFR-1 | ★★ |

> *Note on IDs:* AD-numbers are **stable identifiers assigned in the order
> drivers were introduced** (AD-7 was added by the human steer of 2026-06-13),
> not priority ranks — the table is **sorted by weight**. IDs are kept stable on
> purpose because they are referenced across this doc and the ADR.

> The tension is **AD-1 vs AD-7**. The human has resolved it in favor of clean
> boundaries; the recommended option (§3.B) is chosen to satisfy AD-7 while keeping
> the AD-1 cost bounded (hexagonal structure is mostly *discipline*, not extra
> moving parts).

---

## 2. Component decomposition — Hexagonal (Ports & Adapters)

The system is a **single deployable** organized in concentric layers. Dependencies
point **inward only**: adapters depend on the application, the application depends
on the domain, the domain depends on nothing.

```
        ┌──────────────────────── ADAPTERS (infrastructure) ─────────────────────────┐
        │                                                                             │
        │  INBOUND (driving)                              OUTBOUND (driven)           │
        │  ┌───────────────────┐                          ┌────────────────────────┐ │
        │  │ Scheduler adapter  │ ── drives ──►            │ RSS feed adapter        │ │
        │  │  (poll / dispatch  │                          │  (feedparser/SyndFeed)  │ │
        │  │   timers)          │                          ├────────────────────────┤ │
        │  ├───────────────────┤        ┌──────────────┐   │ Persistence adapter     │ │
        │  │ Admin UI / HTTP    │ ──►    │              │◄──│  (EF Core/ORM · repos · │ │
        │  │  controllers       │        │ APPLICATION  │   │   Outbox table)         │ │
        │  └───────────────────┘        │  (use cases  │   ├────────────────────────┤ │
        │                               │   + PORTS)   │──►│ Email sender adapter     │ │
        │                               │              │   │  (MOCK default)         │ │
        │                               │  ┌────────┐  │   ├────────────────────────┤ │
        │                               │  │ DOMAIN │  │──►│ Slack sender adapter     │ │
        │                               │  │ (pure) │  │   │  (MOCK default)         │ │
        │                               │  └────────┘  │   └────────────────────────┘ │
        │                               └──────────────┘                              │
        └─────────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Domain (pure, no infrastructure dependencies) — serves AD-7
- Entities/value objects: `User`, `Alert` (keywords, target channel, owner), `Article` (title, summary, link, source, published-at, guid), `Notification` (status: pending/sent/failed), `Keyword`.
- **Matching logic lives here** as a pure function: OR across keywords (Q-1), whole-word case-insensitive tokenization (Q-7) over title+summary (Q-2). Fully unit-testable with no I/O.

### 2.2 Application (use cases + port definitions)
Driving (inbound) ports / use cases:
- `PollFeedsUseCase` — fetch → normalize → persist new articles.
- `EvaluateAlertsUseCase` — match new articles → create notifications + **outbox entries**.
- `DispatchOutboxUseCase` — read pending outbox → send → transition status.
- Admin use cases: `ManageUsers`, `ManageAlerts`, `ViewNotifications` (FR-11…13).

Driven (outbound) ports — the **only** way the core touches the outside world (AD-7, AD-2):
- `FeedSourcePort` — fetch raw feed items (FR-1/2).
- `ArticleRepositoryPort`, `AlertRepositoryPort`, `NotificationRepositoryPort`.
- `OutboxPort` — enqueue/lease/complete outbox entries.
- `NotificationSenderPort` — `send(channel, notification)`; resolved per channel.
- `ClockPort` — for testable scheduling/timestamps.

### 2.3 Adapters (infrastructure — swappable, mock-first)
- **Inbound:** Scheduler adapter (timers driving Poll & Dispatch), Admin UI / HTTP controllers.
- **Outbound:** RSS adapter, Persistence adapter (repositories + Outbox table) , Email sender (**mock default**), Slack sender (**mock default**). Real senders are drop-in and read creds from env config only (NFR-4, AC-5).

> **Module decomposition (ADR-002):** within this hexagon the horizontal modules are
> **Ingestion**, **Alerts**, **Notifications** (owns the Outbox), and **Channels**
> (adapters). Inter-module rules — public ports / Shared Kernel only, MediatR for
> crash-tolerant fan-out, Outbox for the durable delivery handoff — are specified in
> [`ADR-002`](adr/ADR-002-architect-review.md).

---

## 3. Options

### Option A — Synchronous layered monolith (domain coupled to infrastructure) — *rejected*

Single deployable; one scheduled job runs poll→match→send inline, with domain
logic interleaved with ORM/HTTP/RSS calls (transaction-script style).

- **Pros:** least code; fastest to throw together (AD-1).
- **Cons:** domain logic is **entangled with infrastructure** → poor testability, hard to evolve, channels not cleanly pluggable. **Directly violates AD-7.**
- **Verdict:** ✗ Rejected by human — "too tightly coupled between domain and infrastructure."

### Option B — Hexagonal modular monolith + DB Outbox — **RECOMMENDED**

Single deployable structured as §2: pure domain, application with explicit
**ports**, swappable **adapters**, and asynchronous delivery via a **DB Outbox**.

```
[Poll timer]    → PollFeedsUseCase   → FeedSourcePort / ArticleRepositoryPort
                → EvaluateAlertsUseCase → (match in domain) → create Notification(pending)
                                                            → OutboxPort.enqueue   ─┐  (same
                                                                                     │   txn)
[Dispatch timer]→ DispatchOutboxUseCase → OutboxPort.lease(pending)                ◄┘
                → NotificationSenderPort.send()  → transition sent | failed (+retry)
```

- **Pros:**
  - Clean domain isolation + ports → **strongest AD-7** (unit-test the domain and use cases with fake adapters; swap mock↔real senders trivially).
  - DB Outbox gives an honest async lifecycle (pending → dispatch → sent/failed) with **retry** → strong AD-3/AD-4; the match-and-enqueue happen in one transaction (no lost notifications).
  - Natural, non-disruptive evolution path to a real broker (the outbox dispatcher becomes a queue consumer) — without committing to distribution now.
  - Still a **single deployable** (AD-6); mock-first delivery (Q-5, A-2).
- **Cons:**
  - More upfront structure (interfaces, two timed use cases, outbox lease to avoid concurrent double-dispatch, PostgreSQL, separate SPA) → a real AD-1 cost that revives R-1; mitigated by the timebox contingency in [`03`](03-architecture-decision.md).
- **Verdict:** ✓ Recommended — the human-directed target.

### Option C — Separated services + message broker — *rejected*

Distinct Ingestion/Matching/Delivery services over RabbitMQ/Redis.

- **Pros:** horizontal scale, fault isolation.
- **Cons:** broker + multi-process orchestration → violates AD-1 & AD-6; scale is out of scope (A-1).
- **Verdict:** ✗ Rejected by human — "overkill for the scope." *(Option B's outbox keeps this as a future option without paying for it now.)*

---

## 4. Cross-cutting design (recommended option)

- **Scheduling** — in-process timers (two driving adapters: poll, dispatch); configurable interval, default 5m (NFR-5). No external cron.
- **Dedup** — DB-level: `unique(source, guid)` on articles (FR-3), `unique(alert_id, article_id)` on notifications (FR-7) → restart-idempotent (R-6).
- **Outbox** — one row per pending delivery with `status`, `attempts`, `last_error`, `available_at`; leased by the dispatcher via PostgreSQL `SELECT … FOR UPDATE SKIP LOCKED` (clean concurrency-safe leasing — no concurrent double-lease; delivery is at-least-once per NFR-2, dedup of notification rows via FR-7 not of sends); simple capped retry/backoff (NFR-2, AD-4).
- **Matching** — pure domain function; lower-cased word-boundary tokens (Q-7), OR (Q-1), title+summary (Q-2).
- **Sender seam** — `NotificationSenderPort` resolved by channel enum; **mock binding default** (Q-5); real senders read creds from env only (NFR-4).
- **Secrets** — env / untracked local settings; committed `.example` documents shape without values (AC-5, R-4).

---

## 5. Stack candidates (sub-decision — Q-8)

All support hexagonal layering + scheduled workers + PostgreSQL (the ratified store, Q-11) + a JSON API for the SPA admin (Q-10).

| Stack | Fit notes | Timebox |
|-------|-----------|---------|
| **.NET 8 (ASP.NET Core Web API + EF Core + Npgsql)** | Windows-native; DI container makes ports/adapters idiomatic; `BackgroundService` for timers; EF for repos + outbox + unique constraints; strong typing for ports; exposes a JSON API for the SPA. | ★★★ |
| Node/TypeScript (Fastify + Prisma + PostgreSQL) | Fast scaffolding; interfaces for ports; rich RSS ecosystem. | ★★ |
| Python (FastAPI + SQLModel + PostgreSQL + APScheduler) | Concise; `feedparser`; Protocols for ports. | ★★ |

**Decided (human, 2026-06-13):** **.NET 8** — ASP.NET Core **Web API** + **EF Core (Npgsql/PostgreSQL)**, with a separate **SPA** frontend (Q-10) consuming the API. DI + interface model expresses Ports & Adapters most naturally and is native to this Windows box.

---

## 6. Comparison matrix

**This matrix corroborates a decision the human already directed (Option B); it
does not make it.** Cell scores are 1 (weak) / 2 (moderate) / 3 (strong).
**Weights: ★★★ = 3, ★★ = 2.** Each total is `Σ (cell × weight)` — shown below so
the arithmetic is reproducible, not asserted.

| Driver (weight) | A (coupled) | **B (hexagonal + outbox)** | C (services) |
|---|:--:|:--:|:--:|
| AD-1 Timebox ×3 | 3 | **2** | 1 |
| AD-7 Maintainability/testability ×3 | 1 | **3** | 3 |
| AD-2 Extensibility ×3 | 2 | **3** | 3 |
| AD-3 Observability ×2 | 2 | **3** | 3 |
| AD-4 Reliability/retry ×2 | 2 | **3** | 3 |
| AD-5 Secret hygiene ×2 | 3 | 3 | 3 |
| AD-6 Single deployable ×2 | 3 | **2** ¹ | 1 |
| **Weighted total** | **38** | **46** | **41** |

> ¹ Option B scores **2**, not 3, on AD-6: the ratified separate SPA (Q-10) is a
> second deployable surface, so B is not a *pure* single deployable. The penalty
> is reflected here to keep the score honest against the ADR consequences.

Arithmetic: **A** = 3·3+1·3+2·3+2·2+2·2+3·2+3·2 = **38**; **B** =
2·3+3·3+3·3+3·2+3·2+3·2+2·2 = **46**; **C** = 1·3+3·3+3·3+3·2+3·2+3·2+1·2 = **41**.

> With maintainability weighted as a top driver (human steer), **Option B wins
> clearly (46)**. A (38) is penalized for coupling; C (41) scores well on quality
> but is dominated by its infra cost against AD-1/AD-6.

---

## 7. Recommendation

Adopt **Option B — a Hexagonal (Ports & Adapters) modular monolith with a DB
Outbox** for asynchronous delivery. This keeps the domain/application core clean
and independent of infrastructure, accesses all external integrations (RSS,
Email, Slack, persistence) only through ports, and models delivery as an explicit
outbox lifecycle (pending → dispatch → sent/failed) with retry — while remaining a
single deployable and preserving a low-friction path to a broker if ever needed.

ADR candidate, consequences, and remaining sub-decisions in
[`03-architecture-decision.md`](03-architecture-decision.md).

---

## 8. Architecture decisions (Step 3 — all ratified by human, 2026-06-13)

| # | Question | Decision |
|---|----------|----------|
| **Q-9** | Architecture style & wiring | ✅ **Hexagonal modular monolith + DB Outbox** |
| **Q-8** | Implementation stack | ✅ **.NET 8** (ASP.NET Core Web API + EF Core / Npgsql) |
| **Q-10** | Admin UI | ✅ **SPA + JSON API** (separate frontend over the .NET API) |
| **Q-11** | Datastore | ✅ **PostgreSQL** (enables `FOR UPDATE SKIP LOCKED` outbox leasing) |

> Step 2 (Architecture) and Step 3 (Human Decision) are now **complete**. The
> Agent Invocation Rule precondition for implementation is satisfied once ADR-001
> is marked *Accepted* — see [`03-architecture-decision.md`](03-architecture-decision.md).
