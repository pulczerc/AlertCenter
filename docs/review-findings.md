# Review Findings

> Baseline rule: **every** Reviewer pass is logged here in full (all findings,
> every time), regardless of severity or outcome. Newest review on top.

---

## RF-003 — Step 4–6 design drafts (domain model, API, DB)

> **Date:** 2026-06-13 · **Reviewer:** Principal Engineer (AI-assisted, `reviewer` agent)
> **Artifacts:** [`04-domain-model.md`](04-domain-model.md), [`05-api-design.md`](05-api-design.md), [`06-db-design.md`](06-db-design.md)
> **Verdict:** ⚠️ **Conditionally accept** — solid and well-traced, but two correctness/reliability defects must be fixed before implementation. 2 high, 3 medium, 4 low.
> **Resolution (2026-06-13, solution-architect):** all 9 findings actioned across `04`/`05`/`06`. Summary:
> - **A** — lease now pushes `available_at` (visibility timeout) + checks `leased_until`; double-dispatch closed.
> - **B** — added `articles.evaluated_at` watermark; evaluation is a restartable query + same-txn watermark set, not an event.
> - **C** — MVP keyword = single token (no whitespace); phrase matching explicitly deferred; validation + invariant K1 updated.
> - **D** — `attempts` removed from `notifications`; outbox is the retry mechanism-of-record; `last_error` only on terminal failure.
> - **E** — SQLite contingency now lists the `uuid`/`timestamptz`/`now()`/`varchar` substitutions.
> - **F** — no-back-matching scope stated in domain §5.2.
> - **G** — ops endpoints return `200` (synchronous), not `202`.
> - **H** — `POST /alerts`: unknown user → `404`, disabled user → `422`.
> - **I** — history FKs (`alerts.user_id`, `notifications.*`) changed to `ON DELETE RESTRICT`.

### 🔴 High

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **A** | **Outbox lease is not actually concurrency-safe as written.** The lease statement sets `leased_until` but the selection predicate filters only `status='pending' AND available_at<=now()` — it **never reads `leased_until`**, and `available_at` is left unchanged. Once the lease txn commits, `SKIP LOCKED` no longer protects the row, so a second dispatcher re-selects a leased-but-unsent entry → **double-dispatch**. This silently breaks the "no concurrent double-lease" guarantee the design claims (cf. RF-001 H-2). **Fix:** on lease, also push `available_at = now() + lease_window` (visibility timeout) **and/or** add `AND (leased_until IS NULL OR leased_until < now())` to the predicate. Alternatively keep send inside the `FOR UPDATE` txn. | [`06`](06-db-design.md) §5 | ✅ Resolved |
| **B** | **Ingest→match hop is non-durable and the match trigger is undefined → lost matches on crash.** Matching runs off `ArticleIngested` (in-process MediatR, non-durable) against "newly ingested articles," but "newly ingested" is never defined. If it means "articles from this poll cycle," a crash between the ingest commit and the match leaves those articles already persisted (no longer "new") and **never evaluated** — violating R-6 restart-idempotency. The Outbox protects *match→deliver* but nothing protects *ingest→match*. **Fix:** make evaluation restartable — e.g. a per-article `evaluated_at` watermark / "to-evaluate" marker queried by `EvaluateAlerts`, not a fired event. | [`04`](04-domain-model.md) §7, [`06`](06-db-design.md) §7 | ✅ Resolved |

### 🟡 Medium

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **C** | **Multi-word keyword semantics are contradictory/undefined.** §5.1 says a match is when a keyword "equals a token" (single whole word, Q-7) *and* "multi-word keywords match as an ordered token **subsequence**" — subsequence (non-contiguous) is almost certainly wrong (should be a contiguous phrase), and the requirements (Q-1/Q-2/Q-7) only ever defined single-token whole-word matching. **Decide:** disallow spaces in keywords for MVP, or define contiguous phrase matching explicitly. | [`04`](04-domain-model.md) §5.1 | ✅ Resolved |
| **D** | **Duplicated delivery state with no system-of-record.** `attempts` and `last_error` live on **both** `notifications` and `outbox`, with no stated sync rule → drift. **Fix:** make `outbox` the mechanism-of-record for retries; `notifications` carries only terminal `status` (+ `last_error` on `failed`). | [`04`](04-domain-model.md) §3.5/3.6, [`06`](06-db-design.md) §3 | ✅ Resolved |
| **E** | **SQLite contingency overstates compatibility.** "Identical tables/constraints" is optimistic: `timestamptz`, `now()`, `uuid`, and `varchar` semantics all differ in SQLite — the porting delta is more than just dropping `SKIP LOCKED`. **Fix:** note the type/default substitutions, or pick portable types up front. | [`06`](06-db-design.md) §6 | ✅ Resolved |

### 🟢 Low

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **F** | New alerts do not match already-ingested articles (FR-5 = "new" articles). Valid scope, but unstated — a demo surprise ("created an alert, nothing fired"). State it. | [`04`](04-domain-model.md) §5.2 | ✅ Resolved |
| **G** | `ops/poll` / `ops/dispatch` return **202** with completion counts; 202 implies not-yet-processed. Use **200** since the work is done synchronously. | [`05`](05-api-design.md) §7 | ✅ Resolved |
| **H** | `POST /alerts` for an unknown/disabled user mixes `404`/`422`. Pick one: `404` for missing user, `422` for disabled. | [`05`](05-api-design.md) §5 | ✅ Resolved |
| **I** | `ON DELETE CASCADE` users→alerts→notifications conflicts with the "disable, never delete" stance and the value of notification history (FR-13); a stray delete would erase history. Consider `RESTRICT`. | [`06`](06-db-design.md) §3 | ✅ Resolved |

### ✅ Affirmed sound
- Module/aggregate decomposition matches ADR-002; isolation + port boundaries respected.
- Pure `KeywordMatcher` (side-effect-free, unit-testable) — correct for AD-7/AC-2.
- One-transaction match→enqueue with `ON CONFLICT DO NOTHING` — correct idempotency primitive (FR-7/R-6).
- Channel snapshot on the notification (N3) protects delivery history from later alert edits.
- Thorough FR/NFR/AC traceability in all three docs.

### Recommendation
Fix **A** and **B** (correctness/reliability) and resolve **C/D** before implementation;
**E/F/G/H/I** are cheap doc edits. None require redesign — the architecture holds.

---

## RF-002 — Independent architect review: inter-module communication

> **Date:** 2026-06-13 · **Reviewer of record:** external architect · **Evaluated by:** Solution Architect (AI-assisted)
> **Subject:** proposal that modules communicate exclusively via MediatR in-process domain events.
> **Outcome:** ✅ **Ratified** → [`adr/ADR-002-architect-review.md`](adr/ADR-002-architect-review.md) Accepted by human 2026-06-13 (D-005, V-003). 4 accepted, 2 partial, 2 rejected. Pointers applied to `03`/`02`.

### Evaluation vs. ADR-001

| # | Recommendation | Disposition | Note |
|---|----------------|-------------|------|
| 1 | No cross-module internal refs; Shared Kernel + public ports only | ✅ Accepted | Best contribution; fills an ADR-001 gap (M-2) |
| 2 | API calls ports directly; MediatR module↔module only | ✅ Accepted | Idiomatic hexagonal (M-5) |
| 3 | Synchronous cross-module query via public port | ✅ Accepted | Renamed `ISubscriptionQuery` → `IAlertQuery` (M-3) |
| 4 | Explicit named module decomposition | ✅ Accepted | Names reconciled to domain (M-1) |
| 5 | MediatR as the in-process module bus | 🟡 Partial | OK for crash-tolerant fan-out; **not** the durable delivery handoff (M-4) |
| 6 | MediatR → broker migration path | 🟡 Partial | True, but the **Outbox** is the primary broker seam; MediatR is secondary |
| 7 | Modules communicate **exclusively** via MediatR fire-and-forget | ❌ Rejected | **Collides with the DB Outbox**; non-durable for delivery → voids NFR-2/AD-4/R-6. Also self-contradicts #3 |
| 8 | Event names `AlertCreated` / `AlertDispatched` | ❌ Rejected | Corrupts ubiquitous language: Alert = a *rule* (FR-4), a match yields a *Notification*. Use `ArticleIngested` / `ArticleMatched` / `NotificationEnqueued` (M-6) |

### Central finding (🔴)
**RF-002-A — "exclusive MediatR" undermines the chosen durability model.** ADR-001
deliberately puts the match→delivery handoff on a **DB Outbox** (one-transaction
enqueue, timer-leased dispatch) for at-least-once delivery and restart-idempotency.
MediatR `Publish` is in-memory and non-durable; routing the delivery handoff through
it would silently drop notifications on a crash between publish and handler.
**Resolution:** MediatR is admitted as an in-process orchestration bus for
crash-tolerant steps **only**; the delivery boundary stays on the Outbox (ADR-002 M-4).

### Decision
Synthesized into **ADR-002 (Proposed)**. No ADR-001 guarantee is given up; the review's
isolation/boundary rules are adopted; "exclusive MediatR" and the event naming are
rejected. **No document or code is amended until the human ratifies ADR-002.**



> **Date:** 2026-06-13 · **Reviewer:** Principal Engineer (AI-assisted, `reviewer` agent)
> **Artifacts:** [`03-architecture-decision.md`](03-architecture-decision.md), cross-checked vs [`02-architecture-options.md`](02-architecture-options.md), [`01-requirements-analysis.md`](01-requirements-analysis.md)
> **Verdict:** ⚠️ **Conditionally accept** — direction sound; documented as if cost-free. 3 high risks, 3 medium inconsistencies, 2 low process notes.
> **Resolution (2026-06-13, solution-architect):** all findings actioned in `02`/`03`/`current-status.md`. ADR-001 remains *Accepted*; conditions now met. See per-finding status.

### 🔴 High

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **H-1** | **Timebox cost understated.** Brief is 3–4h total with Steps 1–6 all docs; Option B pulls in PostgreSQL container, EF Core migrations, two `BackgroundService` timers, outbox lease/retry/backoff, *and* a separate SPA (build tooling, JSON API, CORS). ADR books this as "~30–60 min" — not credible. Resurrects R-1 (High), which is absent from Consequences. No fallback/cut-line named. | [03:84-86](03-architecture-decision.md#L84) | ✅ **Resolved** — added a *Timebox contingency (R-1)* section to `03` (ordered cut-line: SQLite → drop SPA → single timer → mock-only); replaced the false "~30–60 min" precision. |
| **H-2** | **"No double-send" claim is false.** `FOR UPDATE SKIP LOCKED` prevents concurrent double-*lease*, not double-*send*: sender succeeds → crash before status commit → row still `pending` → re-sent on restart. That's at-least-once (NFR-2 accepts it), and `unique(alert_id,article_id)` dedups rows, not sends. Design fine; claim wrong. | [03:54-56](03-architecture-decision.md#L54), [02:150](02-architecture-options.md#L150) | ✅ **Resolved** — reworded in `03` and `02`: "no concurrent double-lease; delivery is at-least-once per NFR-2; FR-7 dedups rows, not sends; exactly-once out of scope." |
| **H-3** | **Scoring matrix not reproducible from its stated weights.** Totals A=33/B=41/C=35 cannot be derived from the cell scores under any single consistent ★ weighting. Reads as back-fitted to a decision the human had already directed. | [02:175-184](02-architecture-options.md#L175) | ✅ **Resolved** — matrix rewritten in `02` with explicit weights (★★★=3, ★★=2), per-cell arithmetic shown, totals corrected to **A=38 / B=46 / C=41**, and framed as corroborating (not making) the human-directed choice. |

### 🟡 Medium

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **M-1** | **Stale "SQLite / server-rendered" text** contradicts ratified PostgreSQL (Q-11) + SPA (Q-10). | [02:159](02-architecture-options.md#L159) | ✅ **Resolved** — line now reads PostgreSQL (Q-11) + JSON API for the SPA admin (Q-10). |
| **M-2** | **AD-6 scoring contradicts Consequences.** Option B scores a perfect 3 on "single deployable," but the ADR admits the SPA is "a second deployable surface." | [02:183](02-architecture-options.md#L183) vs [03:87-88](03-architecture-decision.md#L87) | ✅ **Resolved** — B's AD-6 score dropped 3 → **2** with footnote (SPA = second deployable surface); now consistent with the ADR consequences. |
| **M-3** | **Driver numbering (AD-1, AD-7, AD-2…) advertises a retrofit;** reinforces H-3's back-fit impression. | [02:33-41](02-architecture-options.md#L33) | ✅ **Resolved (deviation)** — *not* renumbered: AD-IDs are stable identifiers referenced across `02`/`03`, so renumbering is destructive churn. Instead added a note that IDs are chronological identifiers (AD-7 = later human steer) and the table is sorted by weight. Architect's call over the reviewer's literal suggestion. |

### 🟢 Low (process hygiene)

| ID | Finding | Location | Status |
|----|---------|----------|--------|
| **L-1** | `04/05/06` exist as 1-line stubs while status lists Step 4 as "next." No work duplicated, but blurs "what's done." | [current-status.md:17](current-status.md#L17) | ✅ **Resolved** — `current-status.md` now flags 04/05/06 as empty **(stub)** placeholders, not completed work. |
| **L-2** | `review-findings.md` was an empty header — Review Rule paper-trail missing. | this file | ✅ Resolved by RF-001. |

### ✅ Affirmed as sound
- Ports/adapters decomposition; matching as a pure, side-effect-free domain function (testability).
- Outbox-enqueue in the same transaction as the match (no lost notifications); observable lifecycle (NFR-3/AC-3).
- DB-level dedup `unique(source,guid)` + `unique(alert_id,article_id)` for restart-idempotency (R-6).

### Recommendation
ADR-001 may remain **Accepted**. Before Step 4: fix H-2/H-3/M-1/M-2 wording (≈10 min) and add the H-1 timebox contingency. No redesign required.
