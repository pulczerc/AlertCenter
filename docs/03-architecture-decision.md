# Architecture Decision

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-12 · **Revised:** 2026-06-13 (human steering)
> **Status:** ✅ **Accepted** — architecture style and all sub-decisions (Q-8…Q-11) ratified by human, 2026-06-13 (Step 3 complete)
> **Relates to:** [`02-architecture-options.md`](02-architecture-options.md), [`01-requirements-analysis.md`](01-requirements-analysis.md), prompt trail [`002-architecture.md`](prompts/002-architecture.md)

Per the mandatory process, this ADR being *Accepted* with Q-8…Q-11 confirmed
satisfies the Agent Invocation Rule precondition for Step 7 (Implementation).

---

## ADR-001 — Hexagonal modular monolith with a DB Outbox

### Context

AlertCenter polls RSS feeds, matches articles to per-user keyword alerts, and
delivers notifications via Email and Slack. Behaviour is fixed by Q-1…Q-7 (OR
matching, title+summary, whole-word CI, per-user alerts, per-alert channel with
system creds, mocked pluggable delivery, minimal auth). The system is
single-tenant and demo-scale (A-1).

An initial draft recommended a synchronous, timebox-optimized monolith
(Option A). On **2026-06-13 the human reviewer overrode that priority**, stating
the goal is *"a clean, long-term maintainable architecture, not just a
timebox-optimized solution,"* and directed: a **modular monolith with Hexagonal
(Ports & Adapters) architecture + a DB Outbox pattern** — domain/application kept
independent of infrastructure, all external integrations (RSS, Email/Slack,
persistence) behind **ports**, and asynchronous delivery via an **outbox**
(pending → dispatch → sent/failed). Option A was rejected as too coupled; Option C
(distributed services + broker) as overkill. This reprioritization elevated
**maintainability/testability (AD-7)** to a top driver, after which weighted
scoring put **Option B at 41**, ahead of C (35) and A (33).

### Decision

Adopt **Option B — a Hexagonal (Ports & Adapters) modular monolith with a DB
Outbox**, a single deployable, structured as:

1. **Pure domain layer** — entities (`User`, `Alert`, `Article`, `Notification`)
   and the **keyword-matching logic** as a side-effect-free function (OR /
   whole-word CI / title+summary). No infrastructure dependencies. Fully
   unit-testable.
2. **Application layer** — use cases (`PollFeeds`, `EvaluateAlerts`,
   `DispatchOutbox`, plus admin use cases) that depend only on **ports**:
   - *Driven (outbound) ports:* `FeedSourcePort`, `*RepositoryPort`s,
     `OutboxPort`, `NotificationSenderPort`, `ClockPort`.
   - *Driving (inbound):* invoked by scheduler timers and Admin/HTTP controllers.
3. **Adapters** — RSS, persistence (repos + **Outbox table**), Email/Slack
   senders, scheduler, Admin UI. **Mock senders are the default binding** (Q-5);
   real ones are drop-in and read creds from env config only (NFR-4, AC-5).
4. **DB Outbox** — on a match, the `Notification` and its outbox entry are written
   in **one transaction**; a separate `DispatchOutbox` use case (driven by a timer)
   leases pending entries via PostgreSQL `SELECT … FOR UPDATE SKIP LOCKED`, sends
   via the port, and transitions status `pending → sent | failed` with bounded
   retry (NFR-2, FR-10, AC-3).
5. **DB-level dedup** — `unique(source, guid)` (FR-3) and
   `unique(alert_id, article_id)` (FR-7) → restart-idempotent (R-6).
6. **SPA + JSON API** — the .NET app exposes a JSON API (inbound HTTP adapter over
   the application use cases); a separate single-page frontend consumes it for the
   Users / Alerts / Notifications admin surface (FR-11…13).

### Alternatives considered

- **Option A — synchronous layered monolith (domain coupled to infra).**
  Rejected by the human: lowest code but entangles domain with ORM/HTTP/RSS,
  harming testability and evolvability (violates AD-7).
- **Option C — separated services + message broker.** Rejected: its only material
  gain (horizontal scale) is out of scope; broker + multi-process orchestration
  violate AD-1/AD-6. The chosen outbox preserves the *option* to evolve toward a
  broker later without paying for it now.

### Consequences

**Positive**
- Domain and use cases are testable in isolation against fake adapters → directly
  serves the stated maintainability goal (AD-7).
- All integrations are swappable behind ports; new channels/sources are additive.
- Outbox gives an explicit, observable delivery lifecycle with retry; the
  match→enqueue transaction prevents lost notifications.
- Clean, documented evolution path to a distributed system if ever required.

**Negative / accepted trade-offs**
- More upfront structure than the coupled Option A (interfaces, two timed use
  cases, outbox leasing) — a bounded ~30–60 min AD-1 cost, accepted by the human
  in exchange for maintainability.
- A **separate SPA frontend** (Q-10) adds build/tooling and a second deployable
  surface vs. server-rendered — accepted for a cleaner client/server split.
- **PostgreSQL** (Q-11) adds infra setup (a local container/service) vs. an
  embedded store — accepted; in return its `FOR UPDATE SKIP LOCKED` gives
  concurrency-safe outbox leasing with no double-send.
- Single process for the backend is a single point of failure — accepted at demo
  scale (A-1).

### Sub-decisions — ratified by human (Step 3, 2026-06-13)

| # | Question | Decision |
|---|----------|----------|
| **Q-9** | Architecture style & wiring | ✅ Hexagonal modular monolith + DB Outbox |
| **Q-8** | Implementation stack | ✅ **.NET 8** (ASP.NET Core Web API + EF Core / Npgsql) |
| **Q-10** | Admin UI | ✅ **SPA + JSON API** |
| **Q-11** | Datastore | ✅ **PostgreSQL** |

### Gate

> ✅ ADR-001 *Accepted* and Q-8…Q-11 confirmed → the Agent Invocation Rule
> precondition for Implementation (Step 7) is satisfied. **Next mandatory step:
> API Design** ([`04-api-design.md`](04-api-design.md)), whose JSON contracts will
> mirror the application use cases and ports above; then DB Design
> ([`05-db-design.md`](05-db-design.md)) and UI Design
> ([`06-ui-design.md`](06-ui-design.md)) before any code.
