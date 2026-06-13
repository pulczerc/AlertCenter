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

## 2. SPA framework (Q-12) — ✅ decided: React + Vite + TypeScript

ADR-001 fixed "SPA + JSON API" but not the framework. **Decided by human
(2026-06-13, D-008): React + Vite + TypeScript.**

| Option | Notes | Timebox |
|--------|-------|---------|
| ✅ **React + Vite + TypeScript** | Ubiquitous, fast scaffold, typed DTOs mirror §05; large hiring/help pool. | ★★★ |
| Vue 3 + Vite | Equally fine; smaller boilerplate. *(not chosen)* | ★★★ |
| Server-rendered Razor | ADR-001 *Timebox cut #2* fallback if the SPA can't fit the box. *(contingency only)* | ★★ |

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

Routes (SPA, **history mode**):
`/users` · `/alerts` · `/notifications` · `/ops` — default redirect `/` → `/notifications`
(the operator's "is it working?" view).

> **Implementation constraint (RF-004-H):** history-mode deep links require the .NET
> host to **fall back to `index.html`** for any non-`/api/*` route (SPA fallback
> middleware), so e.g. a refresh on `/alerts` resolves to the SPA, not a 404.

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

- The owner column reads **`AlertDto.ownerName`** (additive field, RF-004-A) — no
  client-side user join needed.
- **New** form:
  - **Owner** — user picker (enabled users only).
  - **Keywords** — chip/tag input. Helper text states the rules: **single word, no
    spaces, ≤60 chars, ≥1 required** (RF-004-G). Reject a violating chip inline
    (whitespace / too long) *before* submit; de-dupe case-insensitively (AL3).
  - **Channel** — radio: `email` / `slack`.
  - Submit → `POST /alerts`. Errors: `404` unknown user, `422` disabled user /
    empty-or-whitespace keyword.
- **Edit** form (`PATCH /alerts/{id}`): same fields, **except** if the alert's owner
  is **disabled** the form is **blocked** (RF-004-B) with a notice — *"Owner is
  disabled; re-enable the user to edit this alert"* + a link to the user. (Owner is
  not reassignable on edit.)
- **Hint near save:** *"Applies to news ingested from now on — existing articles
  aren't back-matched."* (D-007 #2 — prevents the "nothing happened" surprise.)

### 4.3 Notifications (FR-13, FR-10) — read-only

**List** — `GET /api/v1/notifications?status=&alertId=&userId=&from=&to=&page=`
(default sort `createdAt desc`).

```
Notifications                     [⟳] [Auto-refresh: off ▾]
Filter: [ status ▾ ] [ user ▾ ] [ alert ▾ ] [ from ▢ ] [ to ▢ ]
┌────────────┬───────────────────────────┬────────┬─────────┬───────────┐
│ When       │ Article                   │ Channel│ Status  │           │
├────────────┼───────────────────────────┼────────┼─────────┼───────────┤
│ 10:01:05   │ OpenAI announces merger…  │ ✉      │ ✅ sent │ [details] │
│ 10:00:40   │ Turing test revisited…    │ #      │ ⏳ pending│[details] │
│ 09:58:12   │ Markets dip on…           │ ✉      │ ❌ failed│ [details] │
└────────────┴───────────────────────────┴────────┴─────────┴───────────┘
```

- **Filters:** status, user, **alert** (RF-004-D — API supports `alertId`), date range.
- **Auto-refresh (RF-004-E):** an **off-by-default** toggle (off / 15s / 30s) that
  polls `GET /notifications` on an interval (A-6 polling; no websockets).
- **Status badge:** `pending ⏳` / `sent ✅` / `failed ❌` (FR-10).
- **Details** drawer (`GET /notifications/{id}`): article title + **link (opens
  source)**, source, published-at; channel; status; `sentAt`; on `failed`,
  `lastError`. Read-only — notifications are system-produced (no create/edit/delete).
- **`failed` is terminal (RF-004-F):** the outbox already retried with backoff before
  marking it dead; the MVP UI offers **no manual re-queue** (a deliberate scope boundary).

### 4.4 Ops (NFR-3, D-007 #3) — demo & health

```
Ops / Health
Status: ● ok        Outbox pending: 5            (from GET /ops/health)
[ Poll feeds now ]  [ Dispatch outbox now ]
Last poll: ingested 12 (new 4)   Last dispatch: 3 sent, 0 failed   (session-only)
```

- `GET /ops/health` on load + refresh → **only** `status` + `outboxPending`.
- **Poll feeds now** → `POST /ops/poll`; **Dispatch outbox now** → `POST /ops/dispatch`;
  both show the returned counts in a toast. Buttons disabled while in-flight.
- **Last poll / Last dispatch lines are session-only (RF-004-C):** populated from the
  POST responses during this session; they do **not** persist across reload (the
  health contract is unchanged).
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

> **Q-12 SPA framework:** ✅ decided — React + Vite + TypeScript (§2, D-008).
> **Reviewer pass:** RF-004 applied (A–H resolved). See [`review-findings.md`](review-findings.md).
