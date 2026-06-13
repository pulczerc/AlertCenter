# Review Findings

> Baseline rule: **every** Reviewer pass is logged here in full (all findings,
> every time), regardless of severity or outcome. Newest review on top.

---

## RF-001 — Step 3: ADR-001 (Architecture Decision)

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
