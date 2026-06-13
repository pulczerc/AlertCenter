# Current Status — Handoff State

> **Project:** AlertCenter (Feature Design & Build from a Vague Brief)
> **Last updated:** 2026-06-13
> **Updated by:** AI-assisted session (Claude)
> **Current branch:** `main` (clean, pushed to origin)

---

## Where we are in the mandatory process

| # | Step | Status |
|---|------|--------|
| 1 | Product Analysis | ✅ **Complete & validated** |
| 2 | Architecture Alternatives | ✅ **Complete & validated** |
| 3 | Human Decision | ✅ **Complete** (ADR-001 Accepted; Q-8…Q-11 ratified) |
| 4 | API Design | 🟦 **Drafted** — awaiting Reviewer + human validation |
| 5 | DB Design | 🟦 **Drafted** — awaiting Reviewer + human validation |
| 6 | UI Design | ⬜ Pending (to be `docs/07-ui-design.md`) |
| 7 | Implementation | ⬜ Pending (unblocked: architecture decision exists) |
| 8 | Review | ⬜ Pending |
| 9 | Validation | ⬜ Pending |

> **Design docs (renumbered, human-directed 2026-06-13):** a **Domain Model** was
> added ahead of the API/DB designs. Current set:
> [`04-domain-model.md`](04-domain-model.md), [`05-api-design.md`](05-api-design.md),
> [`06-db-design.md`](06-db-design.md) — all **Drafts**. The old empty stubs
> (`04-api-design.md`, `05-db-design.md`, `06-ui-design.md`) were removed. **UI
> Design** (mandatory Step 6) is still to come as `07-ui-design.md`.

---

## What's done

- **Step 1 — Product Analysis** complete and human-validated.
  - `docs/01-requirements-analysis.md` — 13 FRs, 5 NFRs, assumptions, risks, MVP scope, acceptance criteria.
  - `docs/prompts/001-product-analysis.md` — prompt/output trail, marked Accepted.
  - `docs/decision-log.md` — D-001 (validated), D-002 (7 confirmed decisions).
  - `docs/validation-log.md` — V-001 logged.
- **Committed:** `c1ea376` — `docs(analysis): add product requirements analysis and confirmed decisions`.
- **Step 2 — Architecture Alternatives** & **Step 3 — Human Decision** complete and human-validated.
  - `docs/02-architecture-options.md` — drivers (incl. AD-7 maintainability), Hexagonal component decomposition, Options A/B/C, re-scored matrix (B 46, C 41, A 38).
  - `docs/03-architecture-decision.md` — **ADR-001 (Accepted):** Hexagonal modular monolith + DB Outbox.
  - `docs/prompts/002-architecture.md` — 2-iteration prompt trail (incl. human steering, verbatim), marked Accepted.
  - `docs/decision-log.md` — D-003 (validated). `docs/validation-log.md` — V-002 logged.
- **Committed:** `4350bb2` — `docs(architecture): adopt hexagonal monolith + DB outbox (ADR-001)`.
- **Architecture amendment — ADR-002 (Accepted 2026-06-13):** evaluated an independent
  architect review (RF-002) on inter-module communication. Adopted explicit module
  boundaries + MediatR (crash-tolerant fan-out only); **kept the DB Outbox as the
  durable delivery boundary** (rejected "exclusive MediatR"). `decision-log.md` — D-005;
  `validation-log.md` — V-003.

> **Binding architecture baseline = ADR-001 + ADR-002.** Both constrain Step 4 onward.

---

## Binding decisions (confirmed by human) — inputs to Architecture

| # | Question | Decision |
|---|----------|----------|
| Q-1 | Keyword match logic | **OR** (any keyword matches) |
| Q-2 | Match scope | **Title + summary** |
| Q-3 | Channel config | **Per-alert choice; system-wide credentials** |
| Q-4 | Alert ownership | **Per-user** (user → alerts → notifications) |
| Q-5 | Delivery | **Mocked, pluggable sender interface** |
| Q-6 | Admin auth in MVP | **Minimal / none** (timebox) |
| Q-7 | Matching | **Whole-word, case-insensitive** |

---

## Architecture decisions (confirmed by human, 2026-06-13)

| # | Question | Decision |
|---|----------|----------|
| Q-9 | Architecture style & wiring | **Hexagonal modular monolith + DB Outbox** |
| Q-8 | Implementation stack | **.NET 8** (ASP.NET Core Web API + EF Core / Npgsql) |
| Q-10 | Admin UI | **SPA + JSON API** |
| Q-11 | Datastore | **PostgreSQL** |

---

## Next action

**Step 4 — API Design** (`docs/04-api-design.md`): define the JSON API contracts
mirroring the application use cases and ports (Users, Alerts, Notifications;
poll/dispatch are internal). Then Step 5 DB Design, Step 6 UI Design — before any
implementation code.

> ✅ **Agent Invocation Rule:** an architecture decision now exists (ADR-001
> Accepted), so implementation is unblocked — but Steps 4–6 must still precede it.

---

## Process reminders (from `.claude/CLAUDE.md`)

- Every major step produces: Prompt → Output → Human validation → Decision.
- Every generated code artifact must be challenged by the Reviewer Agent.
- Never run `git commit` without explicit human approval.
- Timebox: 3–4 hours total.
