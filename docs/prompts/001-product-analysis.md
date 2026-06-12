# Context

Step 1 of the mandatory process (Product Analysis). Triggered via `/analyze`
skill → product-analyst agent persona (`.claude/agents/product-analyst.md`).
Input: the vague brief in `.claude/CLAUDE.md`.

# Prompt

Act as Senior Product Analyst. From the vague AlertCenter brief (RSS source,
keyword alerts, Email/Slack channels, admin for Users/Alerts/Notifications,
3–4h timebox), produce `docs/01-requirements-analysis.md` covering: problem
statement, personas, functional & non-functional requirements, assumptions,
ambiguities, risks, proposed MVP scope, acceptance criteria, and open questions
requiring human decision. Do not write production code or design infrastructure.

# Output

`docs/01-requirements-analysis.md` generated. Highlights:
- 13 functional requirements (FR-1..FR-13) across ingestion, matching, delivery, admin.
- 5 non-functional requirements (timebox, reliability, observability, security, cadence).
- 6 assumptions, 6 risks, 7 ambiguities (Q-1..Q-7) with recommended defaults.
- MVP in/out scope and 5 acceptance criteria.

# Human Review

Accepted: Full document accepted. All 7 ambiguity defaults (Q-1..Q-7) confirmed by human in `decision-log.md`.

Rejected: None.

Modified: None.
