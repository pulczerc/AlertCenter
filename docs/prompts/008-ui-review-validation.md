# Context

Reviewer pass over the UI design, then a step-by-step human decision on every
finding + the open framework question. Continues [`007-ui-design.md`](007-ui-design.md).

- Agent: **reviewer** (`.claude/agents/reviewer.md`, Principal Engineer).
- Artifact: [`07-ui-design.md`](../07-ui-design.md), cross-checked vs
  [`05-api-design.md`](../05-api-design.md), [`04-domain-model.md`](../04-domain-model.md).
- Findings: [`review-findings.md`](../review-findings.md) **RF-004**.

---

# Iteration 1 — review (RF-004)

## Prompt

> /review — UI Design — Use reviewer agent. Review generated artifacts.

## Output

**RF-004** — verdict *conditionally accept*; 0 high, 3 medium, 5 low. The mediums
are UI↔contract mismatches: **A** Alerts list shows an owner name absent from
`AlertDto`; **B** disabled-owner alert can't be edited (picker hides the owner);
**C** ops "last poll/dispatch" stats aren't in `GET /ops/health`. Lows: **D**
missing by-alert filter; **E** no auto-refresh; **F** no failed-notification
remediation; **G** keyword limits not surfaced; **H** SPA deep-link fallback.

## Human Review

Accepted: "do decision step by step".

---

# Iteration 2 — step-by-step decisions

## Prompt

Nine decisions, one at a time (8 findings + Q-12 framework).

## Output / Decisions

| # | Item | Decision |
|---|------|----------|
| A | owner name on Alerts list | **Embed `ownerName` in `AlertDto`** (amends `05`, additive) |
| B | edit alert with disabled owner | **Block editing** + re-enable notice |
| C | ops last-run stats | **Session-only** (health contract unchanged) |
| D | notifications by-alert filter | **Add it** |
| E | auto-refresh | **Optional off-by-default toggle** (15/30s polling) |
| F | failed notification remediation | **State as terminal scope boundary** (no re-queue) |
| G | keyword limits in UI | **Show limits + inline guard** |
| H | SPA routing | **History mode + server fallback** to `index.html` |
| Q-12 | SPA framework | **React + Vite + TypeScript** |

## Human Review

Accepted: all nine as decided above.

Rejected: —

Modified: applied A–H to `07`; added `ownerName` to `AlertDto` in `05`; recorded
Q-12 as decided.

# Decision

Logged **V-005** and **D-008**. UI Design marked **Complete & validated**; the design
set (04–07) is fully validated; **implementation (Step 7) is unblocked.**
