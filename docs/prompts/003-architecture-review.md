# Context

Architecture-phase reviews and the amendment that followed Step 3. Covers three
linked activities, all after ADR-001 was Accepted:

1. Reviewer pass on **ADR-001** (`/review`) → findings **RF-001** → fixes.
2. Evaluation of an **independent architect review** (module communication) →
   **ADR-002** → human ratification.
3. Extraction of a dedicated **ADR-001** file + stale-score cleanup.

- Agents: **reviewer** (`.claude/agents/reviewer.md`, Principal Engineer) and
  **solution-architect** (`.claude/agents/solution-architect.md`).
- Inputs: [`03-architecture-decision.md`](../03-architecture-decision.md),
  [`02-architecture-options.md`](../02-architecture-options.md).
- Findings logged in [`review-findings.md`](../review-findings.md) (RF-001, RF-002).

---

# Iteration 1 — review ADR-001 (RF-001)

## Prompt

> /review last step and @docs/03-architecture-decision.md — Use reviewer agent.
> Review generated artifacts.

## Output

[`review-findings.md`](../review-findings.md) **RF-001** — verdict *conditionally
accept*; 3 high, 3 medium, 2 low:
- **H-1** timebox cost understated, no contingency; **H-2** "no double-send" overclaim
  (really at-least-once); **H-3** scoring matrix not reproducible from its weights.
- **M-1** stale SQLite/server-rendered line; **M-2** AD-6 score vs SPA second
  deployable; **M-3** AD-ID numbering retrofit. **L-1** stub docs; **L-2** empty findings file.

## Human Review

Accepted: "log all findings every time, it's baseline." Then: "fix the findings and
log it then commit the changes."

Rejected: —

Modified: M-3 resolved as a **deviation** (AD-IDs kept stable, note added, not
renumbered — architect's call over the reviewer's literal suggestion).

## Decision

Applied all RF-001 fixes to `02`/`03`/`current-status.md`; corrected matrix totals
**A=38 / B=46 / C=41**; added a *Timebox contingency* cut-line. Logged **D-004**.
Commit `5dd5f39`.

---

# Iteration 2 — evaluate independent architect review → ADR-002

## Prompt

> Use the solution-architect agent. The architecture decision has already been made.
> An independent architect review has been received and must be evaluated before
> implementation. Review: *modules communicate exclusively through MediatR internal
> domain events; no module references another's internals (Shared Kernel only); flow
> FeedProcessor→AlertCreated→AlertRouter (ISubscriptionQuery)→AlertDispatched→
> NotificationDispatcher (INotificationChannel); API calls ports directly; MediatR
> reserved for module-to-module; migration path MediatR→RabbitMQ/NServiceBus.*
> Tasks: analyze, compare with current architecture, identify accepted/rejected/
> partial, update architecture if appropriate, generate an ADR amendment, update the
> decision doc if required. Create: docs/review-findings.md, docs/adr/ADR-002-architect-review.md.
> Wait for human approval before modifying any implementation artifacts.

## Output

[`adr/ADR-002-architect-review.md`](../adr/ADR-002-architect-review.md) (**Proposed**)
and [`review-findings.md`](../review-findings.md) **RF-002**. Disposition: 4 accepted
(module isolation, API-calls-ports, public-port queries, named modules), 2 partial
(MediatR as in-process bus — fan-out only; broker migration — Outbox is the primary
seam), 2 rejected:
- **"Exclusive MediatR"** — collides with the ADR-001 **DB Outbox**; MediatR is
  non-durable, so routing the delivery handoff through it voids NFR-2/AD-4/R-6.
  Durability boundary stays on the Outbox (M-4).
- **`AlertCreated`/`AlertDispatched`** naming — corrupts the ubiquitous language
  (an Alert is a *rule*; a match yields a *Notification*). Renamed to
  `ArticleIngested`/`ArticleMatched`/`NotificationEnqueued` (M-6).

Left as **Proposed** (not applied) pending the mandatory Human Decision gate.

## Human Review

Accepted: "Approved @docs/adr/ADR-002-architect-review.md".

Rejected: —

Modified: —

## Decision

Marked ADR-002 **Accepted**; applied the inter-module section/pointers to `03`/`02`;
logged **D-005** and **V-003**. Binding baseline = ADR-001 + ADR-002. Commit `2bcfecc`.

---

# Iteration 3 — dedicated ADR-001 file + score cleanup

## Prompt

> create a dedicated ADR-001 file based on @docs/03-architecture-decision.md
> *(then)* accepted your suggestions *(fix stale "B at 41" prose; rename the misnamed
> ADR-001-template.md; commit).*

## Output

[`adr/ADR-001-hexagonal-monolith-outbox.md`](../adr/ADR-001-hexagonal-monolith-outbox.md)
— ADR-001 extracted parallel to ADR-002 with corrected totals; stale "B at 41" prose
fixed in `03`/`current-status`; generic `ADR-001-template.md` renamed to
[`ADR-template.md`](../adr/ADR-template.md).

## Human Review

Accepted: all three suggestions.

Rejected: —

Modified: prompt trail `002-architecture.md` left untouched as a faithful historical
record of the original (wrong) scores.

## Decision

Committed `22d407e`.
