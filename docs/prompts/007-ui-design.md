# Context

Step 6 of the mandatory process (UI Design) — the last design artifact before
implementation. Produced by the **solution-architect** agent (design only, no code).

- Inputs: [`01-requirements-analysis.md`](../01-requirements-analysis.md) (FR-11…13,
  AC-4), [`05-api-design.md`](../05-api-design.md) (the contract the SPA consumes),
  [`04-domain-model.md`](../04-domain-model.md), [`adr/ADR-001`](../adr/ADR-001-hexagonal-monolith-outbox.md)
  (Q-10 = SPA + JSON API).
- Honors the validated design decisions D-007 (single-token keyword chips,
  no-back-matching hint, ops panel, read-only notifications).

---

# Prompt

> start design ui and log / record everything into the proper files

Produce `docs/07-ui-design.md`: the admin SPA for Users / Alerts / Notifications over
the validated `/api/v1` contract; layout, flows, states — no component code.

# Output

[`07-ui-design.md`](../07-ui-design.md):
- Scope/principles — admin-only SPA, consumes `/api/v1`, no auth (Q-6), no subscriber UI.
- Information architecture + routes (`/users`, `/alerts`, `/notifications`, `/ops`).
- Screens with ASCII wireframes: Users (list/create/enable-disable), Alerts
  (list/filter + form with single-token keyword chips + no-back-match hint),
  Notifications (read-only history + status badges + details drawer), Ops (health +
  poll/dispatch triggers).
- Cross-cutting states (loading/empty/validation/409/404/5xx) bound to the RFC-7807
  error model; reusable components; UI→API endpoint coverage (no new API surface);
  a11y/responsiveness; out-of-scope; FR/AC traceability.
- **Open sub-decision Q-12** — SPA framework (recommend **React + Vite + TS**; Razor
  is the timebox-contingency fallback).

# Human Review

Accepted: after the Reviewer pass + step-by-step decisions — see
[`008-ui-review-validation.md`](008-ui-review-validation.md).

Rejected: —

Modified: RF-004 fixes A–H applied; Q-12 decided (React + Vite + TS).

# Decision

Validated (**V-005 / D-008**). The full design set (04–07) is now validated;
implementation (Step 7) is unblocked.
