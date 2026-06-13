# API Design

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-13 · **Status:** Draft — awaiting human validation (Step 5 of the design phase)
> **Inputs:** [`01-requirements-analysis.md`](01-requirements-analysis.md), [`04-domain-model.md`](04-domain-model.md), [`adr/ADR-001`](adr/ADR-001-hexagonal-monolith-outbox.md), [`adr/ADR-002`](adr/ADR-002-architect-review.md)
> **Feeds:** [`06-db-design.md`](06-db-design.md), UI design (later)
> **Scope:** the JSON contract of the inbound HTTP adapter. No controller code.

---

## 1. Principles & conventions

- **Style:** REST-ish JSON over HTTP; resource nouns; verbs via HTTP methods.
- **Base path / versioning:** `/api/v1`. Breaking changes ⇒ `/api/v2`.
- **Hexagonal boundary (ADR-002 M-5):** controllers are an **inbound adapter** and
  call **application use cases / module ports directly** — *no MediatR at the API
  edge* (MediatR is module↔module only).
- **Media type:** `application/json; charset=utf-8`. Bodies are UTF-8.
- **Identifiers:** server-generated UUIDs (string in JSON).
- **Timestamps:** ISO-8601 UTC, `Z`-suffixed (e.g. `2026-06-13T10:00:00Z`).
- **Enums (lower-case strings):** `channel ∈ {"email","slack"}`;
  `status ∈ {"pending","sent","failed"}`.
- **Auth (Q-6):** **none in MVP** — a single trusted admin surface. Every route is
  written so an `Authorization` header can be required later **without contract
  changes**. Documented as a known gap, not an oversight (NFR-4 still applies to
  *secrets*, which never appear in the API).
- **Idempotency:** creation is not auto-idempotent in MVP; uniqueness is enforced by
  the domain (duplicate email → `409`).
- **Pagination (lists):** `?page` (1-based, default 1) + `?pageSize` (default 25,
  max 100). Responses wrap items in an envelope (§3.1).
- **Errors:** RFC 7807 `application/problem+json` (§7).

---

## 2. Resource map

| Resource | Endpoints | Requirement |
|----------|-----------|-------------|
| **Users** | list / create / get / enable-disable | FR-11 |
| **Alerts** | list (filter) / create / get / update / enable-disable | FR-4, FR-12 |
| **Notifications** | list (filter) / get — **read-only** | FR-6, FR-10, FR-13 |
| **Ops** (demo/observability) | trigger poll, trigger dispatch, health | NFR-3, AC-1/AC-3 |

> Poll/match/dispatch are **internal** timer-driven use cases (ADR-001); the **Ops**
> endpoints expose manual triggers purely so the demo doesn't wait for the 5-min
> cadence (NFR-5). They are not part of the subscriber-facing surface and would be
> auth-gated first.

---

## 3. Shared shapes

### 3.1 List envelope
```json
{
  "items": [ /* resource objects */ ],
  "page": 1,
  "pageSize": 25,
  "total": 137
}
```

### 3.2 DTOs
```jsonc
// UserDto
{ "id": "uuid", "name": "Ada", "email": "ada@x.io", "enabled": true,
  "createdAt": "2026-06-13T10:00:00Z" }

// AlertDto
{ "id": "uuid", "userId": "uuid", "keywords": ["openai","merger"],
  "channel": "email", "enabled": true, "createdAt": "2026-06-13T10:00:00Z" }

// NotificationDto
{ "id": "uuid", "alertId": "uuid", "articleId": "uuid", "channel": "slack",
  "status": "sent", "createdAt": "2026-06-13T10:01:00Z",
  "sentAt": "2026-06-13T10:01:05Z", "attempts": 1, "lastError": null,
  "article": { "title": "...", "link": "https://...", "source": "reuters",
               "publishedAt": "2026-06-13T09:55:00Z" } }
```
> `NotificationDto.article` is an embedded read-model summary so the admin
> Notifications view (FR-13) renders without an N+1 fan-out.

---

## 4. Endpoints — Users (FR-11)

### `GET /api/v1/users`
List users. Query: `enabled` (bool, optional), `page`, `pageSize`.
→ `200` list envelope of `UserDto`.

### `POST /api/v1/users`
```json
{ "name": "Ada Lovelace", "email": "ada@example.com" }
```
Validation: `name` 1–120 chars; `email` valid & unique.
→ `201` `UserDto`, `Location: /api/v1/users/{id}`.
Errors: `400` invalid; `409` email already exists (Invariant U1).

### `GET /api/v1/users/{id}` → `200` `UserDto` · `404` if absent.

### `PATCH /api/v1/users/{id}`
Partial update; MVP supports enable/disable (FR-11) and name.
```json
{ "enabled": false }
```
→ `200` `UserDto` · `404` · `400`. (Disabling a user deactivates matching for all
their alerts — Domain U2/AL `Active`.)

> No hard `DELETE` in MVP — disable instead (preserves notification history).

---

## 5. Endpoints — Alerts (FR-4, FR-12)

### `GET /api/v1/alerts`
Query: `userId` (uuid), `enabled` (bool), `channel`, `page`, `pageSize`.
→ `200` list envelope of `AlertDto`.

### `POST /api/v1/alerts`
```json
{ "userId": "uuid", "keywords": ["openai", "merger"], "channel": "email" }
```
Validation:
- `userId`: **unknown** user → `404`; **existing but disabled** user → `422` (RF-003-H);
- `keywords`: non-empty array, each a **single token**, 1–60 chars, **no whitespace**
  (RF-003-C); de-duplicated case-insensitively (AL1/AL3);
- `channel ∈ {email,slack}` (AL2).
→ `201` `AlertDto`, `Location`. Errors: `400` (shape), `404` (unknown user),
`422` (disabled user / empty or whitespace keyword).

### `GET /api/v1/alerts/{id}` → `200` `AlertDto` · `404`.

### `PATCH /api/v1/alerts/{id}`
Update keywords / channel / enabled.
```json
{ "keywords": ["openai","acquisition"], "channel": "slack", "enabled": true }
```
→ `200` `AlertDto` · `404` · `400`/`422`.
> Changing `channel` affects **future** matches only; existing notifications keep
> their snapshotted channel (Domain N3).

---

## 6. Endpoints — Notifications (FR-6, FR-10, FR-13) — read-only

### `GET /api/v1/notifications`
Query: `status` (`pending|sent|failed`), `alertId`, `userId`, `from`/`to` (ISO date),
`page`, `pageSize`. Default sort: `createdAt desc`.
→ `200` list envelope of `NotificationDto`.

### `GET /api/v1/notifications/{id}` → `200` `NotificationDto` · `404`.

> No create/update/delete: notifications are produced by the matching engine and
> mutated only by the dispatcher (server-internal). The API observes them (AC-3/AC-4).

---

## 7. Endpoints — Ops (NFR-3) — demo & observability

| Method · Path | Maps to use case | Response |
|---------------|------------------|----------|
| `POST /api/v1/ops/poll` | `PollFeeds` (ingest now) | `200 {"ingested": 12, "new": 4}` |
| `POST /api/v1/ops/dispatch` | `DispatchOutbox` (drain pending) | `200 {"dispatched": 3, "failed": 0}` |
| `GET /api/v1/ops/health` | liveness + DB/outbox depth | `200 {"status":"ok","outboxPending":5}` |

> These run **synchronously** and return `200` with the completed counts (RF-003-G).

> These are **manual triggers** of the same use cases the timers call — they don't
> bypass the domain or the outbox (M-4). First thing to put behind auth.

---

## 8. Error model (RFC 7807)
```json
{
  "type": "https://alertcenter/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "email is not a valid address",
  "errors": { "email": ["must be a valid email address"] }
}
```

| Status | When |
|--------|------|
| `400` | malformed body / invalid field shape |
| `404` | resource (or referenced parent) not found |
| `409` | uniqueness conflict (duplicate user email) |
| `422` | semantically invalid (e.g. empty keyword set, disabled user) |
| `500` | unexpected; never leaks secrets/stack to the client (NFR-4) |

---

## 9. Endpoint → use case → port mapping (ADR-002 M-5)

| Endpoint | Application use case | Port(s) touched |
|----------|----------------------|-----------------|
| Users CRUD | `ManageUsers` | `UserRepositoryPort` |
| Alerts CRUD | `ManageAlerts` | `AlertRepositoryPort`, `IAlertQuery` |
| Notifications GET | `ViewNotifications` | `NotificationRepositoryPort` (read model) |
| `ops/poll` | `PollFeeds` | `FeedSourcePort`, `ArticleRepositoryPort` |
| `ops/dispatch` | `DispatchOutbox` | `OutboxPort`, `NotificationSenderPort`/`INotificationChannel` |

---

## 10. Cross-cutting

- **Validation** at the adapter boundary (shape) + domain invariants (semantics);
  domain rules are authoritative.
- **Content negotiation:** JSON only in MVP.
- **CORS:** allow the SPA origin only (Q-10); configurable, no wildcard with creds.
- **Observability:** structured request logs + the `ops/health` outbox depth (NFR-3).
- **Versioning discipline:** additive fields are non-breaking; renames/removals bump `v`.

---

## 11. Requirements traceability

| FR/NFR/AC | Endpoint(s) |
|-----------|-------------|
| FR-11 (users) | §4 |
| FR-4, FR-12 (alerts) | §5 |
| FR-6/FR-10/FR-13 (notifications + status) | §6 |
| NFR-3 / AC-1 / AC-3 (observe ingestion & delivery) | §7 |
| NFR-4 (no secrets exposed) | §8, §10 |
| AC-4 (admin can list users/alerts, view history) | §4–§6 |

> **Review handoff:** challenge with the Reviewer agent before implementation;
> findings → [`review-findings.md`](review-findings.md).
