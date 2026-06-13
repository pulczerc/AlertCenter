# Context

Reviewer pass over the three design drafts, then the fixes. Continues
[`004-design.md`](004-design.md).

- Agent: **reviewer** (`.claude/agents/reviewer.md`, Principal Engineer — criticism
  over agreement), then **solution-architect** for the fixes.
- Artifacts under review: [`04-domain-model.md`](../04-domain-model.md),
  [`05-api-design.md`](../05-api-design.md), [`06-db-design.md`](../06-db-design.md).
- Findings: [`review-findings.md`](../review-findings.md) **RF-003**.

---

# Iteration 1 — review (RF-003)

## Prompt

> do the review — /review — Use reviewer agent. Review generated artifacts
> (the three new design drafts).

## Output

[`review-findings.md`](../review-findings.md) **RF-003** — verdict *conditionally
accept*; 2 high, 3 medium, 4 low:
- **A (high)** outbox lease not actually concurrency-safe — sets `leased_until` but
  the predicate ignores it and leaves `available_at` unchanged → double-dispatch
  after the lease txn commits.
- **B (high)** *ingest→match* hop is non-durable and the "newly ingested" trigger is
  undefined → lost matches on crash (violates R-6).
- **C** multi-word keyword semantics contradictory; **D** `attempts`/`last_error`
  duplicated on `notifications` and `outbox`; **E** SQLite contingency overstates
  compatibility. **F** no-back-matching unstated; **G** ops `202` vs `200`;
  **H** `POST /alerts` mixes `404`/`422`; **I** `ON DELETE CASCADE` vs history value.

## Human Review

Accepted: "apply fixes and commit".

Rejected: —

Modified: —

---

# Iteration 2 — apply fixes

## Prompt (derived from RF-003)

Apply all 9 findings to `04`/`05`/`06`, log the resolutions, commit.

## Output

- **A** — lease pushes `available_at` (visibility timeout) + checks `leased_until`.
- **B** — `articles.evaluated_at` watermark; evaluation is a restartable query that
  sets the watermark in the same transaction as the enqueue; MediatR `ArticleIngested`
  demoted to a prompt-evaluation nudge.
- **C** — MVP keyword = single token (no whitespace); phrase matching deferred.
- **D** — `attempts` removed from `notifications`; outbox is the retry mechanism-of-record.
- **E** — SQLite type-substitution table (`uuid`/`timestamptz`/`now()`/`varchar`).
- **F** — no-back-matching scope stated. **G** — ops return `200`. **H** — `404`
  unknown / `422` disabled. **I** — `ON DELETE RESTRICT` on history FKs.

All RF-003 statuses flipped to ✅ Resolved with a per-finding resolution summary.

## Human Review

Accepted: fixes applied and committed (`41b0ba1`).

Rejected: —

Modified: —

# Decision

Logged **D-006**. Steps 4–5 marked **Drafted + Reviewed (RF-003 applied)**; human
validation still pending before implementation (Step 7).
