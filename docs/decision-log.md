# Decision Log

| # | Date | Step | Decision | Status |
|---|------|------|----------|--------|
| D-001 | 2026-06-12 | Product Analysis | Generated `01-requirements-analysis.md` from vague brief. 7 ambiguities (Q-1..Q-7) raised with recommended defaults. | ✅ Validated by human |
| D-002 | 2026-06-12 | Product Analysis | Human confirmed all 7 recommended defaults (Q-1..Q-7) below. These are now binding inputs to the Architecture step. | ✅ Validated by human |

## Open items requiring human decision

From `01-requirements-analysis.md` §6:

- **Q-1** Keyword match logic: OR vs AND — OR
- **Q-2** Match scope: title only vs title+summary — title+summary
- **Q-3** Channel config: per-user vs system-wide — per-alert choice, system creds
- **Q-4** Alert ownership: per-user vs global — per-user
- **Q-5** Delivery: live vs mocked — mocked, pluggable sender
- **Q-6** Admin auth in MVP: yes vs no — minimal/none
- **Q-7** Matching: whole-word vs substring — whole-word, case-insensitive
