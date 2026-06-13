# Context

Human validation of the design phase (the **Validation** requirement of the
mandatory process applied to Steps 4–6). Follows the Reviewer pass in
[`005-design-review.md`](005-design-review.md); the docs were reviewed (RF-003
applied) but not yet **human-validated**.

- Artifacts validated: [`04-domain-model.md`](../04-domain-model.md),
  [`05-api-design.md`](../05-api-design.md), [`06-db-design.md`](../06-db-design.md).
- No agent — a direct human review, conducted one decision at a time.

---

# Prompt

> first I want to make a human review — go through step by step

Driven as six targeted decisions (the judgment calls not dictated outright by the
requirements), each presented with its trade-off for an accept/change verdict.

# Output

Six decisions reviewed; **all confirmed as drafted** (no edits required):

| # | Decision | Verdict |
|---|----------|---------|
| 1 | Keyword granularity — **single token** (no spaces); phrase matching deferred | ✅ as drafted |
| 2 | **Future-only matching** — new alerts do not back-match already-ingested articles | ✅ as drafted |
| 3 | **Keep `/ops/poll` + `/ops/dispatch` + `/ops/health`** demo triggers | ✅ as drafted |
| 4 | **Two-record delivery model** — notifications (status) + separate outbox (mechanism); `attempts` on outbox only | ✅ as drafted |
| 5 | **Channel snapshot** on each notification (history not rewritten by later alert edits) | ✅ as drafted |
| 6 | **`ON DELETE RESTRICT`** on history FKs (disable, never delete) | ✅ as drafted |

# Human Review

Accepted: all six decisions, as designed.

Rejected: —

Modified: —

# Decision

Logged **V-004** ([`validation-log.md`](../validation-log.md)) and **D-007**
([`decision-log.md`](../decision-log.md)). Steps 4–5 marked **Complete &
validated**; UI Design (Step 6) is next. Commit `c1c32b3`.
