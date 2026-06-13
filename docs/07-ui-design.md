# UI Design

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-13 · **Status:** Draft — awaiting Reviewer + human validation (Step 6)
> **Inputs:** [`01-requirements-analysis.md`](01-requirements-analysis.md) (FR-11…13, AC-4),
> [`05-api-design.md`](05-api-design.md), [`04-domain-model.md`](04-domain-model.md),
> [`adr/ADR-001`](adr/ADR-001-hexagonal-monolith-outbox.md) (Q-10 SPA + JSON API)
> **Scope:** the **admin** SPA. No component code — layout, flows, states, contracts.

---

## 1. Scope & principles

- **Audience:** the **Administrator/Operator** persona ([`01`](01-requirements-analysis.md) §2).
  There is **no subscriber-facing UI** in the MVP (out of scope).
- **Surface (FR-11–13, AC-4):** manage **Users**, manage **Alerts**, view
  **Notifications** history. Plus a light **Ops** panel (validated decision D-007 #3).
- **Client/server split (Q-10):** a **SPA** that consumes only the `/api/v1` JSON
  contract ([`05`](05-api-design.md)) — no server-side rendering, no direct DB access.
- **Auth (Q-6):** **none in MVP.** The shell is built so a login/token gate can be
  added later without restructuring routes.
- **Near-real-time (A-6):** lists refresh on navigation + a manual **Refresh**; no
  websockets/push.
- **Keep it boring:** table-driven CRUD, inline validation, explicit empty/loading/
  error states. No charts, no theming work.

---

## 2. Open sub-decision — SPA framework (Q-12)

ADR-001 fixed "SPA + JSON API" but **not** the framework. Recommendation below;
flagged for human ratification (does not block the layout/flows in this doc).

| Option | Notes | Timebox |
|--------|-------|---------|
| **React + Vite + TypeScript** (recommended) | Ubiquitous, fast scaffold, typed DTOs mirror §05; large hiring/help pool. | ★★★ |
| Vue 3 + Vite | Equally fine; smaller boilerplate. | ★★★ |
| Server-rendered Razor (contingency) | The ADR-001 *Timebox cut #2* fallback — drop the SPA if time is short. | ★★ |

> Data layer: a thin typed `apiClient` wrapping `fetch`; one module per resource
> (`usersApi`, `alertsApi`, `notificationsApi`, `opsApi`) mapping 1:1 to §05 endpoints.

---

## 3. Information architecture

```
┌───────────────────────────────────────────────┐
│  AlertCenter ▸ admin            [⟳ Refresh]     │   top bar
├──────────┬────────────────────────────────────┤
│ Users    │                                     │
│ Alerts   │      <active section content>       │   left nav + content
│ Notifs   │                                     │
│ Ops      │                                     │
└──────────┴────────────────────────────────────┘
```

Routes (SPA, history mode):
`/users` · `/alerts` · `/notifications` · `/ops` — default redirect `/` → `/notifications`
(the operator's "is it working?" view).

---

## 4. Screens

### 4.1 Users (FR-11)

**List** — `GET /api/v1/users?enabled=&page=&pageSize=`

```
Users                                   [+ New user]
Filter: [ enabled ▾ ]                    page 1/2 ◀ ▶
┌────────────────────┬───────────────────┬─────────┬──────────┐
│ Name               │ Email             │ Status  │ Actions  │
├────────────────────┼───────────────────┼─────────┼──────────┤
│ Ada Lovelace       │ ada@example.com   │ ●Enabled│ [Disable]│
│ Alan Turing        │ alan@example.com  │ ○Disabled│[Enable] │
└────────────────────┴───────────────────┴─────────┴──────────┘
```

- **New user** → modal/form: `name`, `email` → `POST /users`.
  Inline errors: `400` field shape; `409` → "email already in use".
- **Enable/Disable** → `PATCH /users/{id} {enabled}` (optimistic; revert on error).
  Disabling warns: *"Disables matching for all this user's alerts."*
- No hard delete (D-007 #6).

### 4.2 Alerts (FR-12, FR-4)

**List** — `GET /api/v1/alerts?userId=&channel=&enabled=&page=`

```
Alerts                                   [+ New alert]
Filter: [ user ▾ ] [ channel ▾ ] [ enabled ▾ ]
┌──────────────┬───────────────────────┬─────────┬─────────┬──────────────┐
│ Owner        │ Keywords              │ Channel │ Status  │ Actions      │
├──────────────┼───────────────────────┼─────────┼─────────┼──────────────┤
│ Ada Lovelace │ openai, merger        │ ✉ email │ ●Enabled│ [Edit][Disable]│
│ Alan Turing  │ turing                │ # slack │ ●Enabled│ [Edit][Disable]│
└──────────────┴───────────────────────┴─────────┴─────────┴──────────────┘
```

- **New / Edit** form:
  - **Owner** — user picker (enabled users only).
  - **Keywords** — chip/tag input; each chip is **one token, no spaces**
    (D-007 #1). Reject a chip containing whitespace inline; de-dupe
    case-insensitively (AL3). ≥1 required.
  - **Channel** — radio: `email` / `slack`.
  - Submit → `POST /alerts` or `PATCH /alerts/{id}`.
  - Errors: `404` unknown user, `422` disabled user / empty-or-whitespace keyword.
- **Hint near save:** *"Applies to news ingested from now on — existing articles
  aren't back-matched."* (D-007 #2 — prevents the "nothing happened" surprise.)

### 4.3 Notifications (FR-13, FR-10) — read-only

**List** — `GET /api/v1/notifications?status=&alertId=&userId=&from=&to=&page=`
(default sort `createdAt desc`).

```
Notifications                                   [⟳]
Filter: [ status ▾ ] [ user ▾ ] [ from ▢ ] [ to ▢ ]
┌────────────┬───────────────────────────┬────────┬─────────┬───────────┐
│ When       │ Article                   │ Channel│ Status  │           │
├────────────┼───────────────────────────┼────────┼─────────┼───────────┤
│ 10:01:05   │ OpenAI announces merger…  │ ✉      │ ✅ sent │ [details] │
│ 10:00:40   │ Turing test revisited…    │ #      │ ⏳ pending│[details] │
│ 09:58:12   │ Markets dip on…           │ ✉      │ ❌ failed│ [details] │
└────────────┴───────────────────────────┴────────┴─────────┴───────────┘
```

- **Status badge:** `pending ⏳` / `sent ✅` / `failed ❌` (FR-10).
- **Details** drawer (`GET /notifications/{id}`): article title + **link (opens
  source)**, source, published-at; channel; status; `sentAt`; on `failed`,
  `lastError`. Read-only — notifications are system-produced (no create/edit/delete).

### 4.4 Ops (NFR-3, D-007 #3) — demo & health

```
Ops / Health
Status: ● ok        Outbox pending: 5
[ Poll feeds now ]  [ Dispatch outbox now ]
Last poll: ingested 12 (new 4)   Last dispatch: 3 sent, 0 failed
```

- `GET /ops/health` on load + refresh.
- **Poll feeds now** → `POST /ops/poll`; **Dispatch outbox now** → `POST /ops/dispatch`;
  both show the returned counts in a toast. Buttons disabled while in-flight.
- Banner: *"Operator tools — unauthenticated in MVP."*

---

## 5. Cross-cutting states

| State | Treatment |
|-------|-----------|
| **Loading** | skeleton rows / spinner on the action button |
| **Empty** | friendly empty state + primary CTA (e.g. "No alerts yet — create one") |
| **Validation (`400`/`422`)** | inline, field-level, from the RFC-7807 `errors` map (§05 §8) |
| **Conflict (`409`)** | inline on the offending field ("email already in use") |
| **Not found (`404`)** | toast + refresh the list |
| **Server (`5xx`)** | non-blocking error toast with a Retry; never surfaces stack/secret |
| **Optimistic toggles** | enable/disable update immediately, revert on failure |

---

## 6. Reusable components

`DataTable` (sort/paginate) · `FilterBar` · `ResourceForm` (+ field error binding) ·
`KeywordChipsInput` (single-token guard) · `StatusBadge` · `ConfirmDialog` ·
`Toast` · `DetailsDrawer` · `Pagination` (mirrors §05 list envelope `page/pageSize/total`).

---

## 7. Endpoint coverage (UI → API)

| Screen action | API (§05) |
|---------------|-----------|
| Users list / create / enable-disable | `GET/POST /users`, `PATCH /users/{id}` |
| Alerts list / create / edit / enable-disable | `GET/POST /alerts`, `PATCH /alerts/{id}` |
| Notifications list / details | `GET /notifications`, `GET /notifications/{id}` |
| Ops health / poll / dispatch | `GET /ops/health`, `POST /ops/poll`, `POST /ops/dispatch` |

Every screen maps to existing endpoints — **no new API surface required.**

---

## 8. Accessibility & responsiveness (MVP-level)

Semantic tables, labelled inputs, keyboard-reachable actions, visible focus, status
conveyed by **text + icon** (not colour alone). Single-column reflow under ~720px.
No i18n in MVP (English only).

---

## 9. Out of scope

Subscriber/self-service UI · auth/login screens · charts/analytics/dashboards ·
real-time push · feed-source management UI · bulk import · theming. (Mirrors
[`01`](01-requirements-analysis.md) §8.)

---

## 10. Requirements traceability

| FR / AC | Screen |
|---------|--------|
| FR-11 (users: list/create/disable) | §4.1 |
| FR-4 / FR-12 (alerts: list/manage) | §4.2 |
| FR-10 / FR-13 (notifications + status history) | §4.3 |
| NFR-3 (observe ingestion/delivery) | §4.4 |
| AC-4 (admin can list users, list/create alerts, view history) | §4.1–4.3 |

> **Open for human decision:** Q-12 SPA framework (§2). **Review handoff:** challenge
> with the Reviewer agent before implementation; findings → [`review-findings.md`](review-findings.md).
