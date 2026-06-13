# Context

Steps 4–6 of the mandatory process (the design phase), produced in a single
**solution-architect** run. The human renumbered the design docs to put a **Domain
Model** ahead of API and DB design.

- Agent: **solution-architect** (`.claude/agents/solution-architect.md`) — design
  only, never implementation code.
- Inputs: [`01-requirements-analysis.md`](../01-requirements-analysis.md) (FR/NFR/AC,
  Q-1…Q-7), [`adr/ADR-001`](../adr/ADR-001-hexagonal-monolith-outbox.md),
  [`adr/ADR-002`](../adr/ADR-002-architect-review.md).
- Review trail for these artifacts: [`005-design-review.md`](005-design-review.md).

---

# Prompt

> Use the solution-architect agent. Based on: requirements-analysis,
> architecture-decision. Produce: 1. Domain Model, 2. API Design, 3. Database Design.
> Do not implement code. Create: docs/04-domain-model.md, docs/05-api-design.md,
> docs/06-db-design.md. Wait for human approval before modifying any implementation
> artifacts.

# Output

- [`04-domain-model.md`](../04-domain-model.md) — ubiquitous language; the 4 modules
  + aggregates (ADR-002); entities/VOs with invariants; `Notification` & `Outbox`
  state machines; the pure `KeywordMatcher` (OR / whole-word CI / title+summary);
  domain events `ArticleIngested`/`ArticleMatched`/`NotificationEnqueued`; FR traceability.
- [`05-api-design.md`](../05-api-design.md) — `/api/v1` JSON contract (Users, Alerts,
  Notifications read-only, Ops triggers); DTOs; RFC-7807 errors; endpoint → use case
  → port mapping (controllers call ports directly, M-5).
- [`06-db-design.md`](../06-db-design.md) — PostgreSQL schema (DDL); one-transaction
  match→enqueue; `FOR UPDATE SKIP LOCKED` lease; SQLite contingency; repository/port mapping.

Design choices surfaced: separate `notifications.status` vs `outbox` table; channel
snapshotted onto the notification; demo-only `ops/*` triggers of the internal use
cases; auth deliberately none (Q-6) but header-ready.

Cleanup: removed the three orphaned empty stubs (`04-api-design`, `05-db-design`,
`06-ui-design`) left over from the renumber; UI Design (Step 6) deferred to a future
`07-ui-design.md`.

# Human Review

Accepted: drafts accepted into the repo; "(2) first" → commit before reviewing.
Commit `e39e284`. Marked **Drafts — awaiting human validation**.

Rejected: —

Modified: design docs renumbered (Domain Model inserted ahead of API/DB); UI Design
dropped from this set.

# Decision

Three design drafts committed; then challenged by the Reviewer agent — see
[`005-design-review.md`](005-design-review.md). Full human validation (Step:
Validation) still pending.
