# Domain Model

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-13 · **Status:** Draft — awaiting human validation (Step 4 of the design phase)
> **Inputs:** [`01-requirements-analysis.md`](01-requirements-analysis.md) (FR-1…FR-13, NFR-1…NFR-5, Q-1…Q-7), [`adr/ADR-001`](adr/ADR-001-hexagonal-monolith-outbox.md), [`adr/ADR-002`](adr/ADR-002-architect-review.md)
> **Feeds:** [`05-api-design.md`](05-api-design.md), [`06-db-design.md`](06-db-design.md)
> **Scope:** the *domain* (entities, value objects, aggregates, invariants, events) — the innermost hexagon layer (ADR-001 §Decision.1). No infrastructure, no code.

---

## 1. Ubiquitous language

| Term | Meaning | Not to be confused with |
|------|---------|-------------------------|
| **Article** | A normalized news item ingested from a feed (title, summary, link, source, guid, published-at). | A notification |
| **Feed / Source** | A configured RSS endpoint (e.g. Reuters, BBC) that yields articles. | — |
| **User** | The owner of alerts; the subscriber who receives notifications. | An admin operator (out of scope, Q-6) |
| **Alert** | A **user-defined keyword rule** with a target channel. A *rule*, persistent. | A notification, an event |
| **Keyword** | One whole-word token an alert matches on (case-insensitive). | A substring (rejected, Q-7) |
| **Match** | The event that an Article satisfies an Alert. Produces a Notification. | — |
| **Notification** | The record that *Alert X matched Article Y*, plus its delivery status. | An Alert |
| **Outbox entry** | The durable dispatch work-item that drives delivery of one Notification. | The Notification itself |
| **Channel** | A delivery medium: **Email** or **Slack**. | — |

> **Naming guard (ADR-002 M-6):** an *Alert* is a rule; a *match* yields a
> *Notification*. Events are `ArticleIngested` / `ArticleMatched` /
> `NotificationEnqueued` — never `AlertCreated`/`AlertDispatched`.

---

## 2. Modules & aggregates (ADR-002 M-1)

Each module is its own hexagon; only the **Shared Kernel** and **public ports**
cross boundaries (M-2).

| Module | Aggregate root(s) | Owns | Public port(s) |
|--------|-------------------|------|----------------|
| **Ingestion** | `Article` | feed polling, normalization, article dedup | (none outbound to peers) |
| **Alerts** | `User`, `Alert` | users, keyword rules, **matching logic** | `IAlertQuery` |
| **Notifications** | `Notification` (+ `OutboxEntry`) | notification lifecycle, the **Outbox**, dispatch | (raises events) |
| **Channels** | — (adapters only) | Email/Slack senders | `INotificationChannel` |

**Shared Kernel** (cross-module value types only): identifiers (`UserId`,
`AlertId`, `ArticleId`, `NotificationId`), the `Channel` enum, and the domain
event contracts. No entity or behaviour is shared.

---

## 3. Entities & value objects

Notation: **(E)** entity · **(VO)** value object · **(AR)** aggregate root.
`?` = nullable. All timestamps are UTC instants (via `ClockPort`, never `DateTime.Now`).

### 3.1 Ingestion — `Article` (AR/E)

| Field | Type | Notes |
|-------|------|-------|
| `Id` | ArticleId (VO) | surrogate identity |
| `Source` | string | feed key, e.g. `reuters`, `bbc` (FR-2) |
| `Guid` | string | feed-provided guid; falls back to link if absent |
| `Title` | string | FR-2 |
| `Summary` | string | FR-2; may be empty |
| `Link` | Url (VO) | canonical article URL |
| `PublishedAt` | instant? | from feed; nullable if feed omits it |
| `IngestedAt` | instant | set on persist |

- **Invariant A1:** `(Source, Guid)` is unique across all articles (FR-3, R-6).
- **Invariant A2:** `Title` non-empty; `Link` is a valid absolute URL.
- Articles are **immutable** after ingestion (no edits/deletes in MVP).

### 3.2 Alerts — `User` (AR/E)

| Field | Type | Notes |
|-------|------|-------|
| `Id` | UserId (VO) | |
| `Name` | string | display name |
| `Email` | EmailAddress (VO) | unique; also the Email-channel target |
| `Enabled` | bool | disable instead of delete (FR-11) |
| `CreatedAt` | instant | |

- **Invariant U1:** `Email` is unique and well-formed.
- **Invariant U2:** a **disabled** user's alerts do not match (see Alert.Active).

### 3.3 Alerts — `Alert` (AR/E)

| Field | Type | Notes |
|-------|------|-------|
| `Id` | AlertId (VO) | |
| `OwnerUserId` | UserId | per-user ownership (Q-4) |
| `Keywords` | Set\<Keyword\> | ≥1; the match terms (FR-4) |
| `Channel` | Channel (VO/enum) | per-alert target (Q-3) |
| `Enabled` | bool | |
| `CreatedAt` | instant | |

- **Invariant AL1:** at least one `Keyword` (FR-4).
- **Invariant AL2:** `Channel ∈ {Email, Slack}`.
- **Invariant AL3:** keywords are **deduplicated, case-insensitively** within an alert.
- **Derived `Active` = `Alert.Enabled AND owner.Enabled`** — only active alerts participate in matching (FR-5).

### 3.4 Alerts — `Keyword` (VO)

| Field | Type | Notes |
|-------|------|-------|
| `Text` | string | original term as entered |
| `Normalized` | string | lower-cased, trimmed — the comparison key (Q-7) |

- **Invariant K1:** `Normalized` is non-empty after trim/lowercase.
- Equality is by `Normalized`.

### 3.5 Notifications — `Notification` (AR/E)

| Field | Type | Notes |
|-------|------|-------|
| `Id` | NotificationId (VO) | |
| `AlertId` | AlertId | the rule that matched |
| `ArticleId` | ArticleId | the article that matched |
| `Channel` | Channel | snapshot of the alert's channel at match time |
| `Status` | NotificationStatus (VO/enum) | `Pending → Sent \| Failed` (FR-10) |
| `CreatedAt` | instant | match time |
| `SentAt` | instant? | set on success |
| `Attempts` | int | delivery attempts (mirrors outbox) |
| `LastError` | string? | last failure reason |

- **Invariant N1:** `(AlertId, ArticleId)` is unique (FR-7) — no duplicate notifications.
- **Invariant N2:** `Status` follows the state machine in §4.1 (no `Sent → Pending`, etc.).
- **Invariant N3:** `Channel` is **snapshotted** at creation, so later edits to the Alert don't rewrite delivery history.

### 3.6 Notifications — `OutboxEntry` (E, part of the Notification aggregate)

The durable dispatch work-item (ADR-001 §Decision.4). Created **in the same
transaction** as its Notification.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | OutboxId (VO) | |
| `NotificationId` | NotificationId | 1:1 with Notification |
| `Status` | OutboxStatus | `Pending → Done \| Dead` |
| `Attempts` | int | incremented per dispatch try |
| `AvailableAt` | instant | next eligible dispatch time (backoff) |
| `LeasedUntil` | instant? | lease held by a dispatcher (SKIP LOCKED) |
| `LastError` | string? | |

- **Invariant O1:** exactly one OutboxEntry per Notification.
- **Invariant O2:** a dispatcher may only act on entries where
  `Status = Pending AND AvailableAt ≤ now` and it holds the lease.
- **Invariant O3 (atomicity):** Notification + OutboxEntry are written together or
  not at all — the match→enqueue transaction (ADR-001; prevents lost notifications).

### 3.7 Channels — `Channel` (VO/enum, Shared Kernel)

`Channel = Email | Slack`. Carries no behaviour in the domain; the **adapter**
behind `INotificationChannel` (Channels module) performs the actual send
(mock-first, Q-5).

---

## 4. Lifecycles (state machines)

### 4.1 `Notification.Status`

```
        match created
   ─────────────────────►  (Pending)
                              │  dispatch attempt
                 success ─────┼─────► (Sent)      [terminal]  → SentAt set
                              │
       failure & attempts<max │  (stays Pending, AvailableAt pushed out — retry)
                              │
       failure & attempts≥max └─────► (Failed)    [terminal]  → LastError set
```

- `Pending` is the only non-terminal state. `Sent` and `Failed` are terminal.
- Retry/backoff is driven by the OutboxEntry; the Notification reflects the outcome.

### 4.2 `OutboxEntry.Status`

```
 (Pending) ──lease──► [sending] ──ok──► (Done)  [terminal; Notification→Sent]
     ▲                          │
     └── attempts<max, backoff ─┘  (fail → ++Attempts, AvailableAt += backoff, release lease)
 (Pending) ── attempts≥max ─────────► (Dead)  [terminal; Notification→Failed]
```

> `Done` entries may be pruned; `Dead` entries are retained for the admin/ops view (NFR-3).

---

## 5. Domain services (pure, no I/O)

### 5.1 `KeywordMatcher.Matches(article, alert) : bool` — the core rule

Implements FR-5 with the confirmed semantics:

- **Scope (Q-2):** tokenize `article.Title + " " + article.Summary`.
- **Tokenization (Q-7):** split on word boundaries, lower-case → set of word tokens.
- **Match (Q-1, OR):** `true` if **any** `alert.Keyword.Normalized` equals a token
  (whole-word, case-insensitive). Multi-word keywords match as an ordered token
  subsequence.
- **Purity:** no DB, no clock, no network — fully unit-testable (AC-2, AD-7).

### 5.2 `EvaluateMatches(article, activeAlerts) : IEnumerable<Match>`

For one article, returns the alerts it matches. The application layer turns each
`Match` into a `Notification` + `OutboxEntry` (idempotently, honoring N1).

---

## 6. Relationships

```
User (1) ───owns──► (N) Alert ──┐
                                 │ match (KeywordMatcher)
Article (1) ────────────────────┤
                                 ▼
                          Notification (N)         (unique alert_id+article_id)
                                 │ 1:1
                                 ▼
                          OutboxEntry (1)
```

- `User 1—* Alert` (Q-4).
- `Alert *—* Article` resolved **through** `Notification` (the match record).
- `Notification 1—1 OutboxEntry`.

---

## 7. Domain events (ADR-002 M-3/M-4/M-6)

| Event | Raised by | Carries | Consumed by | Transport |
|-------|-----------|---------|-------------|-----------|
| `ArticleIngested` | Ingestion | ArticleId (+ refs) | Alerts (evaluate) | MediatR in-process (crash-tolerant fan-out) |
| `ArticleMatched` | Alerts | AlertId, ArticleId, Channel | Notifications (enqueue) | leads into the **durable txn**, not the trigger |
| `NotificationEnqueued` | Notifications | NotificationId | (observability/log) | MediatR (best-effort) |

> **Durability boundary (M-4):** the actual **delivery** of a Notification is driven
> by the **Outbox dispatcher** (a timer leasing OutboxEntries), **never** by a
> MediatR event. MediatR coordinates *in-process orchestration*; the Outbox
> guarantees *at-least-once delivery* across crashes (NFR-2, R-6).

---

## 8. Requirements traceability

| Element | Satisfies |
|---------|-----------|
| `Article` + Invariant A1 (`unique(source,guid)`) | FR-2, FR-3, R-6, AC-1 |
| `Alert` (keywords + channel, per-user) | FR-4, Q-1…Q-4 |
| `KeywordMatcher` (OR / whole-word CI / title+summary) | FR-5, Q-1/Q-2/Q-7, AC-2 |
| `Notification` + Invariant N1 (`unique(alert,article)`) | FR-6, FR-7, AC-2 |
| `Notification.Status` + `OutboxEntry` | FR-10, NFR-2, AC-3 |
| `Channel` VO + `INotificationChannel` | FR-8, FR-9, Q-5 |
| `User.Enabled`, `Alert.Enabled` | FR-11, FR-12 |
| Notification history (read model) | FR-13, AC-4 |

---

## 9. Out of scope (domain)

Regex/NLP matching (A-3), multi-tenancy/RBAC (A-4), digests/scheduling per user,
extra channels, feed auto-discovery. Mirrors [`01`](01-requirements-analysis.md) §8.

> **Review handoff:** per the project Review Rule, this model must be challenged by
> the Reviewer agent before it feeds implementation. Findings → [`review-findings.md`](review-findings.md).
