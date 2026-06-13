# Decision Log

| # | Date | Step | Decision | Status |
|---|------|------|----------|--------|
| D-001 | 2026-06-12 | Product Analysis | Generated `01-requirements-analysis.md` from vague brief. 7 ambiguities (Q-1..Q-7) raised with recommended defaults. | ✅ Validated by human |
| D-002 | 2026-06-12 | Product Analysis | Human confirmed all 7 recommended defaults (Q-1..Q-7) below. These are now binding inputs to the Architecture step. | ✅ Validated by human |
| D-003 | 2026-06-13 | Architecture | Human rejected the timebox-first Option A and directed a **Hexagonal (Ports & Adapters) modular monolith + DB Outbox** (Q-9). Ratified sub-decisions: **Q-8** stack = .NET 8 (Web API + EF Core/Npgsql); **Q-10** admin UI = SPA + JSON API; **Q-11** datastore = PostgreSQL. ADR-001 *Accepted*. | ✅ Validated by human |
| D-004 | 2026-06-13 | Architecture (review) | Applied Reviewer findings **RF-001** to `02`/`03`/`current-status.md`: added a Timebox contingency cut-line (H-1), corrected the at-least-once delivery claim (H-2), made the scoring matrix reproducible with corrected totals A=38/B=46/C=41 (H-3), fixed the stale SQLite line (M-1), dropped B's single-deployable score for the SPA (M-2), clarified AD-ID numbering (M-3, deviation: not renumbered), and marked 04–06 as stubs (L-1). ADR-001 stays *Accepted*. | ✅ Logged |

## Open items requiring human decision

From `01-requirements-analysis.md` §6:

- **Q-1** Keyword match logic: OR vs AND — OR
- **Q-2** Match scope: title only vs title+summary — title+summary
- **Q-3** Channel config: per-user vs system-wide — per-alert choice, system creds
- **Q-4** Alert ownership: per-user vs global — per-user
- **Q-5** Delivery: live vs mocked — mocked, pluggable sender
- **Q-6** Admin auth in MVP: yes vs no — minimal/none
- **Q-7** Matching: whole-word vs substring — whole-word, case-insensitive
