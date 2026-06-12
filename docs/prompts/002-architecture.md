# Context

- Step 2 of the mandatory process (Architecture Alternatives).
- Agent: **solution-architect** (`.claude/agents/solution-architect.md`) — create
  alternatives, compare tradeoffs, recommend one, draft an ADR candidate; never
  write implementation code.
- Inputs: validated [`01-requirements-analysis.md`](../01-requirements-analysis.md)
  (FR-1…13, NFR-1…5) and binding human decisions Q-1…Q-7
  ([`decision-log.md`](../decision-log.md) D-002).

---

# Iteration 1 — initial /architect run

## Prompt

> /architect — Use solution-architect agent. Generate architecture alternatives
> and recommendation.

## Output

- [`02-architecture-options.md`](../02-architecture-options.md) — drivers, shared
  component decomposition, **Option A** (synchronous monolith), **Option B**
  (in-process outbox), **Option C** (services + broker), comparison matrix.
- Recommendation: **Option A** (synchronous monolith), optimized for the 3–4h
  timebox, with Option B's seams designed in.
- Sub-decisions raised: Q-8 stack, Q-9 wiring, Q-10 UI, Q-11 datastore.

## Human Review

**Rejected (the recommendation and the A/B/C framing as weighted).** Verbatim
steering (2026-06-13):

> "Before I answer for Human decisions, I have some points related to the 3
> options: I don't fully accept the A/B/C options as presented. The goal is a
> clean, long-term maintainable architecture, not just a timebox-optimized
> solution.
>
> My proposal is: **modular monolith with a Hexagonal (Ports & Adapters)
> architecture + DB Outbox pattern.**
>
> Key points:
> - The domain/application layer must remain clean and independent from
>   infrastructure concerns.
> - External integrations (news polling, message sending like email/Slack,
>   persistence) should be accessed only through ports.
> - Asynchronous delivery should be handled via a DB Outbox pattern
>   (pending → dispatch → sent/failed).
> - This refines Option B by introducing proper boundaries and improving
>   testability and separation of concerns.
> - It also provides a natural evolution path toward a message broker /
>   distributed system if needed later.
>
> I do not want a distributed system (Option C is overkill for the scope), and
> Option A is too tightly coupled between domain and infrastructure. Option B is
> the closest direction, but only acceptable if strengthened with explicit
> ports/adapters and a proper outbox model."

---

# Iteration 2 — revision per human steering

## Prompt (derived from the steering above)

Revise the architecture: elevate maintainability/testability to a top driver;
recast Option B as a **Hexagonal (Ports & Adapters) modular monolith + DB
Outbox** and make it the recommendation; keep A (rejected: coupling) and C
(rejected: overkill) as the design-space bookends; re-score; carry the remaining
sub-decisions forward.

## Output

- [`02-architecture-options.md`](../02-architecture-options.md) (revised) — adds
  §0 human-steering note and driver **AD-7 (maintainability/testability ★★★)**;
  hexagonal component decomposition (pure domain · application+ports ·
  adapters); Option B recast as **Hexagonal monolith + DB Outbox** and
  recommended; re-scored matrix (**B 41**, C 35, A 33).
- [`03-architecture-decision.md`](../03-architecture-decision.md) — **ADR-001:
  Hexagonal modular monolith with a DB Outbox**, recording the human override and
  the inward-only dependency rule. Status: *Proposed* (style decided).
- Q-9 (architecture style/wiring) marked **Decided** = Hexagonal + Outbox.
  Q-8 (stack), Q-10 (UI), Q-11 (datastore) remain open with recommendations.

## Human Review

**Accepted** (2026-06-13) — architecture style **Hexagonal modular monolith + DB
Outbox** confirmed, and the remaining sub-decisions ratified via Step-3 Q&A:

- **Q-8 stack** → **.NET 8** (ASP.NET Core Web API + EF Core / Npgsql)
- **Q-10 admin UI** → **SPA + JSON API** (separate frontend over the .NET API)
- **Q-11 datastore** → **PostgreSQL** (enables `FOR UPDATE SKIP LOCKED` outbox leasing)

Logged as decision **D-003** and validation **V-002**. ADR-001 marked *Accepted*.

Rejected: —

Modified: stack table reworked to Web API + Npgsql + SPA (Razor dropped in favor
of SPA per Q-10); outbox leasing noted to use PostgreSQL `SKIP LOCKED`.
