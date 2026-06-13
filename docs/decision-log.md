# Decision Log

| # | Date | Step | Decision | Status |
|---|------|------|----------|--------|
| D-001 | 2026-06-12 | Product Analysis | Generated `01-requirements-analysis.md` from vague brief. 7 ambiguities (Q-1..Q-7) raised with recommended defaults. | ✅ Validated by human |
| D-002 | 2026-06-12 | Product Analysis | Human confirmed all 7 recommended defaults (Q-1..Q-7) below. These are now binding inputs to the Architecture step. | ✅ Validated by human |
| D-003 | 2026-06-13 | Architecture | Human rejected the timebox-first Option A and directed a **Hexagonal (Ports & Adapters) modular monolith + DB Outbox** (Q-9). Ratified sub-decisions: **Q-8** stack = .NET 8 (Web API + EF Core/Npgsql); **Q-10** admin UI = SPA + JSON API; **Q-11** datastore = PostgreSQL. ADR-001 *Accepted*. | ✅ Validated by human |
| D-004 | 2026-06-13 | Architecture (review) | Applied Reviewer findings **RF-001** to `02`/`03`/`current-status.md`: added a Timebox contingency cut-line (H-1), corrected the at-least-once delivery claim (H-2), made the scoring matrix reproducible with corrected totals A=38/B=46/C=41 (H-3), fixed the stale SQLite line (M-1), dropped B's single-deployable score for the SPA (M-2), clarified AD-ID numbering (M-3, deviation: not renumbered), and marked 04–06 as stubs (L-1). ADR-001 stays *Accepted*. | ✅ Logged |
| D-005 | 2026-06-13 | Architecture (amendment) | Evaluated independent architect review (**RF-002**) and adopted **ADR-002** — explicit module boundaries + inter-module communication. Accepted: isolation rule, API-calls-ports-directly, public-port queries, named modules. Partial: MediatR as in-process bus (fan-out only, **not** the delivery handoff). Rejected: "exclusive MediatR" (collides with DB Outbox durability) and `AlertCreated`/`AlertDispatched` naming. Durability boundary stays on the Outbox (M-4). Applied pointers to `03`/`02`. | ✅ Validated by human |
| D-006 | 2026-06-13 | Design (review fixes) | Produced design drafts `04-domain-model`, `05-api-design`, `06-db-design`; Reviewer pass **RF-003** found 2 high / 3 medium / 4 low. Applied all 9: outbox visibility-timeout lease (A), `evaluated_at` restartable matching (B), single-token keywords (C), de-duplicated delivery state (D), SQLite type-substitution note (E), no-back-matching scope (F), ops `200` (G), `404`/`422` split (H), `ON DELETE RESTRICT` for history (I). | ✅ Logged — drafts await human validation |
| D-007 | 2026-06-13 | Design (human validation) | Step-by-step human review of the design phase confirmed 6 decisions **as drafted**: (1) **single-token keywords** (phrases deferred); (2) **future-only matching** (no back-matching new alerts); (3) **keep `/ops/poll`+`/ops/dispatch`+`/ops/health`** triggers; (4) **two-record delivery model** (notifications status + separate outbox; attempts on outbox only); (5) **channel snapshot** on notifications; (6) **`ON DELETE RESTRICT`** on history FKs. No edits required. Logged V-004. | ✅ Validated by human |
| D-008 | 2026-06-13 | UI Design (review + validation) | Reviewer pass **RF-004** on `07-ui-design` (3 medium / 5 low); resolved all via step-by-step human decision: **A** `ownerName` added to `AlertDto` (amends `05`, additive); **B** block editing disabled-owner alerts; **C** ops stats session-only; **D** add by-alert filter; **E** off-by-default auto-refresh; **F** `failed` terminal (no re-queue); **G** keyword-limit inline guard; **H** history mode + server fallback. **Q-12 SPA framework = React + Vite + TypeScript.** Logged V-005. | ✅ Validated by human |
| D-009 | 2026-06-13 | Implementation plan (review fixes) | Produced `08-implementation-plan` (4 streams + parallelization); Reviewer pass **RF-005** (1 high / 4 medium / 3 low). Applied all 8: re-anchored to a minimum-shippable vertical slice with per-wave estimates + cut-line (A/B), wired evaluate into one ingestion timer (C), **decided OutboxMessage rendered at enqueue → new outbox `payload` column** (D, rippled to `06`/`04`), reframed parallelism (E), prioritized tests (F), scheduled dev seeder (G), noted manual EF schema config (H). | ✅ Logged — plan reviewed; awaiting go-ahead to code |

## Open items requiring human decision

From `01-requirements-analysis.md` §6:

- **Q-1** Keyword match logic: OR vs AND — OR
- **Q-2** Match scope: title only vs title+summary — title+summary
- **Q-3** Channel config: per-user vs system-wide — per-alert choice, system creds
- **Q-4** Alert ownership: per-user vs global — per-user
- **Q-5** Delivery: live vs mocked — mocked, pluggable sender
- **Q-6** Admin auth in MVP: yes vs no — minimal/none
- **Q-7** Matching: whole-word vs substring — whole-word, case-insensitive

Raised during design:

- **Q-12** SPA framework (from `07-ui-design.md` §2) — ✅ **decided: React + Vite + TypeScript** (D-008, 2026-06-13). Razor server-rendered remains the timebox-contingency fallback.
