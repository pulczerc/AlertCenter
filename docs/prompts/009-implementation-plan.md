# Context

Entry into the implementation phase (Step 7): an implementation plan, then a Reviewer
pass and fixes. Plan splits the build into four independent streams (Domain,
Infrastructure, API, Frontend).

- Agents: **solution-architect** (plan) + **reviewer** (`.claude/agents/reviewer.md`).
- Inputs: the validated design set ([`04`](../04-domain-model.md)–[`07`](../07-ui-design.md)),
  [`adr/ADR-001`](../adr/ADR-001-hexagonal-monolith-outbox.md), [`adr/ADR-002`](../adr/ADR-002-architect-review.md).
- Findings: [`review-findings.md`](../review-findings.md) **RF-005**.

---

# Iteration 1 — create the plan

## Prompt

> We are entering implementation phase. Read documents. Create an implementation plan.
> Split the work into independent streams: 1 Domain, 2 Infrastructure, 3 API, 4 Frontend.
> For each: files to create, dependencies, tests required, estimated implementation order.
> Identify streams that can be implemented in parallel. Do not write code yet.
> Create: docs/08-implementation-plan.md

## Output

[`08-implementation-plan.md`](../08-implementation-plan.md) — solution structure (Core /
Infrastructure / Api + 3 test projects + `web/`); the four streams each with files,
dependencies, tests, and order; a dependency graph + parallelization analysis; wave
sequencing; DoD mapped to AC-1…AC-5.

---

# Iteration 2 — review (RF-005)

## Prompt

> /review — the implementation plan — Use reviewer agent.

## Output

**RF-005** — *conditionally accept*; 1 high, 4 medium, 3 low. **A** over-scoped vs the
3–4h timebox (contingency should be the spine); **B** no time estimates; **C**
`EvaluateAlerts` not wired to a scheduler; **D** dispatch payload assembly undefined
(outbox has no payload; cross-module reads at send); **E** "parallel" overstated for a
solo builder; **F** heavy test stack; **G** no dev seed; **H** EF won't auto-match `06`
partial indexes/checks/raw lease.

## Human Review

Accepted: "apply fixes and record everything and do the commit in separate format as usual."

---

# Iteration 3 — apply fixes

## Prompt (derived from RF-005)

Apply all 8 findings; for **D**, decide the approach and apply.

## Output / Decision

- **A** re-anchored to a minimum-shippable vertical slice; infra defaulted down
  (SQLite, direct calls, no MediatR/NetArchTest/CI); rest marked 🔶 stretch.
- **B** per-wave estimates + must-ship line (~2.5h) + cut-line.
- **C** single `IngestionHostedService` runs poll→evaluate per tick (watermark backlog).
- **D** **decided: render `OutboxMessage` at enqueue, store in a new outbox `payload`
  column** (rippled to [`06`](../06-db-design.md) + [`04`](../04-domain-model.md)); dispatcher
  does no cross-module reads — consistent with the channel snapshot (N3).
- **E** parallelization reframed as logical independence. **F** test layers prioritized.
  **G** dev seeder scheduled. **H** manual EF config noted.

## Human Review

Accepted: fixes applied & recorded (this commit set).

Rejected: —

Modified: `D` resolved as payload-at-enqueue (not dispatch-time resolution).

# Decision

Logged **D-009**. Plan is **reviewed (RF-005 applied)**; awaiting human go-ahead to
begin Wave 0. The full design+plan set is consistent.
