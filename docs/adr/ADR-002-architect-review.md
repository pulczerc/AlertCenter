# ADR-002 — Inter-module communication (amends ADR-001)

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-13
> **Status:** ✅ **Accepted** — ratified by human (csaba.pulczer), 2026-06-13 (D-005, V-003). Amends and is binding alongside ADR-001.
> **Amends:** [`ADR-001`](../03-architecture-decision.md) (Hexagonal modular monolith + DB Outbox)
> **Trigger:** an independent architect review of module communication (MediatR-based internal domain events). Full evaluation logged as **RF-002** in [`review-findings.md`](../review-findings.md).

---

## Context

ADR-001 fixed the *vertical* shape of the system — a Hexagonal (Ports & Adapters)
modular monolith with a **DB Outbox** for durable, at-least-once delivery — but it
left the *horizontal* boundaries **between modules** largely implicit. It named use
cases (`PollFeeds`, `EvaluateAlerts`, `DispatchOutbox`) and outbound ports, but did
not state how one module is allowed to talk to another.

An independent architect review proposes filling exactly that gap, with a concrete
scheme:

1. Modules communicate **exclusively** through **MediatR** in-process domain events.
2. No module references another module's internal classes — only **Shared Kernel**
   types cross boundaries.
3. Flow: `FeedProcessor` → publishes `AlertCreated` → `AlertRouter` handles, queries
   `ISubscriptionQuery` → publishes `AlertDispatched` → `NotificationDispatcher`
   sends via `INotificationChannel`.
4. Rules: modules expose only public ports; cross-module calls go via MediatR
   `Publish` (fire-and-forget) **or** a public port (synchronous query); the API
   layer calls ports directly (MediatR is module↔module only).
5. Migration path: thin MediatR `Publish` calls, later swappable for RabbitMQ /
   NServiceBus.

The review is largely sound and addresses a real gap, but **one of its claims
collides head-on with ADR-001** and its event vocabulary conflicts with our
domain language. This ADR adopts the good parts and bounds the rest.

---

## Decision

Adopt **explicit module boundaries and a two-channel inter-module communication
model**, layered onto ADR-001 *without* displacing the DB Outbox:

### M-1 · Module decomposition (made explicit)
Four horizontal modules inside the single deployable, each a hexagon with its own
domain/application/adapters:

| Module | Owns | Maps to review's |
|--------|------|------------------|
| **Ingestion** | feed polling, article normalization + dedup | `FeedProcessor` |
| **Alerts** | `User`, `Alert` (keyword rules), matching logic, `IAlertQuery` port | `AlertRouter` + `Subscriptions` |
| **Notifications** | `Notification` lifecycle, **the Outbox**, dispatch | `NotificationDispatcher` |
| **Channels** (adapters) | Email / Slack senders behind `INotificationChannel` | `INotificationChannel` |

### M-2 · Module isolation rule (**accepted from the review**)
A module MUST NOT reference another module's internal types. Cross-module access is
only via (a) **Shared Kernel** value types, or (b) another module's **public port**.
This operationalizes ADR-001's port discipline at module granularity.

### M-3 · Two sanctioned communication channels
- **Synchronous query** through a public port (e.g. `IAlertQuery`) for
  request/response reads — direct, in-transaction, no bus. (Review's `ISubscriptionQuery`.)
- **In-process event** via MediatR `Publish` for *fan-out notifications between
  modules* — **but only for steps that are safe to lose on a crash** (see M-4).

### M-4 · The durability boundary stays on the Outbox (**bounds the review**)
The **match → notification → delivery** handoff MUST use the **ADR-001 DB Outbox**,
not a MediatR event. On a match, the `Notification` and its outbox row are written in
**one transaction**; the timer-driven dispatcher leases and sends. MediatR `Publish`
is in-memory and **non-durable** — using it for the delivery handoff would silently
discard notifications on a crash between publish and handler, voiding NFR-2
(at-least-once), AD-4 (reliability) and R-6 (restart-idempotency). Therefore MediatR
**coexists with**, and does not replace, the Outbox.

### M-5 · API calls ports directly (**accepted from the review**)
Inbound HTTP/Admin adapters invoke application use cases / module ports directly.
MediatR is reserved for module↔module fan-out. (Pure hexagonal; already implied by
ADR-001, now stated.)

### M-6 · Domain-faithful event names (**corrects the review**)
Reject `AlertCreated` / `AlertDispatched` — in our ubiquitous language an **Alert is
a user-defined keyword rule** (FR-4), not an event, and a match yields a
**Notification**. Use instead: `ArticleIngested`, `ArticleMatched`,
`NotificationEnqueued`. (`ArticleMatched` leads into the M-4 transaction; it is not
itself the delivery trigger.)

---

## Recommendations: accepted / partial / rejected

| # | Review recommendation | Disposition | Rationale |
|---|-----------------------|-------------|-----------|
| 1 | No cross-module internal refs; Shared Kernel + public ports only | ✅ **Accepted** (M-2) | Strengthens ADR-001 port discipline; the review's best contribution |
| 2 | API layer calls module ports directly; MediatR is module↔module only | ✅ **Accepted** (M-5) | Idiomatic hexagonal |
| 3 | Synchronous cross-module **query** via public port (`ISubscriptionQuery`) | ✅ **Accepted** (M-3) | Renamed `IAlertQuery` to fit domain |
| 4 | Explicit module decomposition + named modules | ✅ **Accepted** (M-1) | Fills a real ADR-001 gap; names reconciled |
| 5 | MediatR as the **in-process bus** for module events | 🟡 **Partial** (M-3/M-4) | OK for crash-tolerant fan-out; **not** for the durable delivery handoff |
| 6 | Broker migration path (MediatR → RabbitMQ/NServiceBus) | 🟡 **Partial** | Accepted in spirit, but the **Outbox is the primary broker seam** (it already persists messages); MediatR points are secondary |
| 7 | Modules communicate **exclusively** via MediatR fire-and-forget | ❌ **Rejected** (M-4) | Collides with the DB Outbox; non-durable for delivery; also self-contradicts rec #3 (direct port queries) |
| 8 | Event names `AlertCreated` / `AlertDispatched` | ❌ **Rejected** (M-6) | Corrupts ubiquitous language (Alert = rule, not event; match → Notification) |

---

## Alternatives considered

- **Adopt the review verbatim (MediatR-exclusive, including delivery).** Rejected:
  trades away the Outbox's durability — the single most deliberate reliability
  decision in ADR-001 — for no offsetting gain at demo scale.
- **Reject the review wholesale, keep ADR-001 as-is.** Rejected: discards genuinely
  useful, low-cost contributions (explicit module boundaries, isolation rule, the
  API-vs-bus split) that ADR-001 left under-specified.
- **MediatR for *all* in-process steps but keep Outbox for delivery** (this ADR).
  Chosen: captures the review's separation-of-concerns value while preserving every
  ADR-001 guarantee.

---

## Consequences

**Positive**
- Module boundaries are now explicit and enforceable; new modules/channels stay additive.
- A clear, teachable rule: *queries → ports; crash-tolerant fan-out → MediatR;
  must-not-lose handoffs → Outbox.*
- Both broker-migration seams (Outbox consumer **and** MediatR publish points) are documented.

**Negative / accepted trade-offs**
- Two in-process mechanisms (MediatR + Outbox) is marginally more concept-surface than
  one — accepted, because they serve different guarantees (best-effort vs durable).
- MediatR is a new dependency vs. plain port calls; for a demo-scale system its main
  payoff is the documented migration story, which the Outbox already partly provides —
  so MediatR adoption is **recommended but not load-bearing**, and is itself an
  AD-1 timebox candidate (see ADR-001 *Timebox contingency*: collapse to direct
  in-process calls if time is short).

---

## Required changes to other documents

1. [`03-architecture-decision.md`](../03-architecture-decision.md) — ✅ applied:
   "Amended by ADR-002" pointer under *Status* + an *Inter-module communication*
   section referencing M-1…M-6.
2. [`02-architecture-options.md`](../02-architecture-options.md) — ✅ applied: module
   decomposition note under §2.

No implementation artifacts are touched by this ADR.

---

## Gate

> ✅ **Accepted (2026-06-13).** Ratified via the mandatory **Human Decision** gate
> (D-005, V-003); §"Required changes" edits applied. ADR-001 + ADR-002 together are
> now the binding architecture baseline for Step 4 (API Design) onward.
