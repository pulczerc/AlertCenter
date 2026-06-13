# Database Design

> **Author:** Solution Architect (AI-assisted)
> **Date:** 2026-06-13 · **Status:** Draft — awaiting human validation (Step 6 of the design phase)
> **Inputs:** [`04-domain-model.md`](04-domain-model.md), [`05-api-design.md`](05-api-design.md), [`adr/ADR-001`](adr/ADR-001-hexagonal-monolith-outbox.md), [`adr/ADR-002`](adr/ADR-002-architect-review.md)
> **Datastore:** **PostgreSQL** (Q-11) via EF Core / Npgsql. The DDL below is *schema
> design*, illustrative — not application code.

---

## 1. ER overview

```
 users ───< alerts ───< alert_keywords
   │           │
   │           └────────────┐
   │                        │ (match)
 (owner)        articles ───┤
                            ▼
                      notifications ──1:1── outbox
```

- `users 1—* alerts` (Q-4) · `alerts 1—* alert_keywords`
- `notifications` is the match record linking one `alert` + one `article` (FR-6)
- `outbox 1—1 notifications` — the durable dispatch work-item (ADR-001)

---

## 2. Enumerations

Modeled as **`text` columns + `CHECK` constraints** (EF-friendly, trivially
portable to the SQLite contingency) rather than native PG enums:

| Logical enum | Allowed values |
|--------------|----------------|
| `channel` | `email`, `slack` |
| `notification_status` | `pending`, `sent`, `failed` |
| `outbox_status` | `pending`, `done`, `dead` |

---

## 3. Schema (DDL)

```sql
-- ── Alerts module ────────────────────────────────────────────────────────────
CREATE TABLE users (
    id          uuid PRIMARY KEY,
    name        varchar(120) NOT NULL,
    email       varchar(254) NOT NULL,
    enabled     boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_users_email UNIQUE (email)          -- Invariant U1
);

CREATE TABLE alerts (
    id          uuid PRIMARY KEY,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT, -- protect history (RF-003-I)
    channel     text NOT NULL CHECK (channel IN ('email','slack')),  -- AL2
    enabled     boolean     NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_alerts_user    ON alerts(user_id);
CREATE INDEX ix_alerts_enabled ON alerts(enabled) WHERE enabled;       -- active-alert scans

CREATE TABLE alert_keywords (
    id          uuid PRIMARY KEY,
    alert_id    uuid NOT NULL REFERENCES alerts(id) ON DELETE CASCADE,
    keyword     varchar(60) NOT NULL,        -- original term
    normalized  varchar(60) NOT NULL,        -- lower/trimmed match key (Q-7)
    CONSTRAINT uq_alert_keyword UNIQUE (alert_id, normalized)          -- AL3 dedup
);
CREATE INDEX ix_alert_keywords_alert ON alert_keywords(alert_id);

-- ── Ingestion module ─────────────────────────────────────────────────────────
CREATE TABLE articles (
    id            uuid PRIMARY KEY,
    source        varchar(64)  NOT NULL,
    guid          varchar(512) NOT NULL,
    title         text         NOT NULL,
    summary       text         NOT NULL DEFAULT '',
    link          text         NOT NULL,
    published_at  timestamptz  NULL,
    ingested_at   timestamptz  NOT NULL DEFAULT now(),
    evaluated_at  timestamptz  NULL,        -- match watermark; NULL = not yet evaluated (RF-003-B)
    CONSTRAINT uq_articles_source_guid UNIQUE (source, guid)           -- FR-3, R-6
);
CREATE INDEX ix_articles_ingested ON articles(ingested_at);
-- restartable matching: the evaluation backlog, cheap to scan
CREATE INDEX ix_articles_unevaluated ON articles(ingested_at) WHERE evaluated_at IS NULL;

-- ── Notifications module ─────────────────────────────────────────────────────
CREATE TABLE notifications (
    id          uuid PRIMARY KEY,
    alert_id    uuid NOT NULL REFERENCES alerts(id)   ON DELETE RESTRICT, -- protect history (RF-003-I)
    article_id  uuid NOT NULL REFERENCES articles(id) ON DELETE RESTRICT,
    channel     text NOT NULL CHECK (channel IN ('email','slack')),    -- snapshot (N3)
    status      text NOT NULL DEFAULT 'pending'
                CHECK (status IN ('pending','sent','failed')),         -- FR-10
    created_at  timestamptz NOT NULL DEFAULT now(),
    sent_at     timestamptz NULL,
    last_error  text NULL,        -- copied from outbox only when status='failed' (RF-003-D)
    CONSTRAINT uq_notification_alert_article UNIQUE (alert_id, article_id)  -- FR-7, N1
);
-- NOTE: retry bookkeeping (attempts) lives ONLY on `outbox` — the dispatch
-- mechanism-of-record. `notifications` holds the business-visible terminal status.
CREATE INDEX ix_notifications_status  ON notifications(status);
CREATE INDEX ix_notifications_created ON notifications(created_at DESC);    -- history view
CREATE INDEX ix_notifications_alert   ON notifications(alert_id);

CREATE TABLE outbox (
    id              uuid PRIMARY KEY,
    notification_id uuid NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
    status          text NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending','done','dead')),
    attempts        int  NOT NULL DEFAULT 0,
    available_at    timestamptz NOT NULL DEFAULT now(),  -- backoff gate
    leased_until    timestamptz NULL,                    -- lease held by a dispatcher
    last_error      text NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_outbox_notification UNIQUE (notification_id)          -- O1 (1:1)
);
-- partial index drives the lease query cheaply:
CREATE INDEX ix_outbox_due ON outbox(available_at) WHERE status = 'pending';
```

---

## 4. The evaluate→enqueue transaction (ADR-001 §Decision.4, RF-003-B)

One article is evaluated against all active alerts and the results are written in
**one transaction** that *also* advances the article's `evaluated_at` watermark —
so the whole step is restartable: a crash before commit leaves `evaluated_at` NULL
and the article is re-picked next cycle; after commit it is never re-evaluated.

```sql
BEGIN;
  -- for each matched alert (0..N): notification + its outbox row
  INSERT INTO notifications (id, alert_id, article_id, channel, status)
  VALUES (:id, :alert, :article, :channel, 'pending')
  ON CONFLICT (alert_id, article_id) DO NOTHING;        -- idempotent (FR-7, AC-2)

  INSERT INTO outbox (id, notification_id, status, available_at)
  SELECT :outboxId, :id, 'pending', now()
  WHERE EXISTS (SELECT 1 FROM notifications WHERE id = :id);  -- only if just inserted

  -- ... repeat per matched alert ...

  UPDATE articles SET evaluated_at = now() WHERE id = :article;  -- watermark
COMMIT;
```

> `ON CONFLICT DO NOTHING` + the `evaluated_at` watermark make re-processing the
> same article (or the same `(alert, article)` pair) harmless on restart (R-6) — no
> double notification, no orphan outbox row, no lost match.

---

## 5. Outbox dispatch (lease — concurrency-safe)

```sql
-- lease a batch (PostgreSQL): no two dispatchers grab the same row
WITH due AS (
    SELECT id FROM outbox
    WHERE status = 'pending'
      AND available_at <= now()                       -- visibility-timeout gate
      AND (leased_until IS NULL OR leased_until < now())  -- belt-and-suspenders (RF-003-A)
    ORDER BY available_at
    FOR UPDATE SKIP LOCKED
    LIMIT :batch
)
UPDATE outbox o
   SET leased_until = now() + interval '30 seconds',
       available_at = now() + interval '30 seconds'   -- KEY: push availability so a
                                                       -- re-select can't grab a leased,
                                                       -- not-yet-sent row after commit
  FROM due
 WHERE o.id = due.id
RETURNING o.id, o.notification_id;
```

> **Why both:** `SKIP LOCKED` only protects rows while the lease txn holds the lock.
> After it commits, the row is unlocked again — so the lease must *also* push
> `available_at` (a visibility timeout). Without it (the original draft), a second
> dispatcher re-selected the leased-but-unsent row → double-dispatch (RF-003-A). On a
> dead dispatcher the lease simply expires and the row becomes due again — at-least-once.

Per leased entry the dispatcher sends via `INotificationChannel`, then:
- **success** → `outbox.status='done'`; `notifications.status='sent', sent_at=now()`.
- **failure & attempts+1 < max** → `attempts=attempts+1`,
  `available_at = now() + backoff(attempts)`, release lease (stays `pending`).
- **failure & attempts+1 ≥ max** → `outbox.status='dead'`;
  `notifications.status='failed', last_error=…`.

> **Reliability scope (RF-001 H-2):** SKIP LOCKED prevents concurrent
> double-*lease*; delivery is **at-least-once** (a crash after send, before the
> status commit, re-sends on restart). Exactly-once is out of scope (NFR-2).

---

## 6. SQLite contingency (ADR-001 Timebox cut #1)

If swapped to SQLite the **shape** is identical but several types/defaults must be
substituted (it is not a literal copy):

| PostgreSQL | SQLite substitution |
|------------|---------------------|
| `uuid` | `TEXT` (store the canonical UUID string) |
| `timestamptz` | `TEXT` ISO-8601 UTC (or INTEGER epoch) |
| `now()` default | `CURRENT_TIMESTAMP`, or set in app via `ClockPort` |
| `varchar(n)` | `TEXT` (length enforced in the domain) |
| `FOR UPDATE SKIP LOCKED` lease | single-writer `UPDATE … WHERE status='pending' AND available_at<=now() … RETURNING` loop |

`CHECK`-based enums, **partial indexes**, and `ON CONFLICT … DO NOTHING` are all
SQLite-compatible. Dropping `SKIP LOCKED` is safe because the dispatcher is
single-instance at demo scale (A-1); the `available_at` visibility timeout (§5)
still prevents re-pickup of an in-flight row.

---

## 7. Matching strategy (data access)

At demo scale, `EvaluateAlerts` selects **un-evaluated** articles
(`evaluated_at IS NULL`, via `ix_articles_unevaluated`), loads **active** alerts +
their keywords (`alerts.enabled AND users.enabled`, via `ix_alerts_enabled`), and
runs the **pure `KeywordMatcher`** (Domain §5) in memory. Each article is finalized
by the §4 transaction (enqueue matches + set `evaluated_at`), so the pass is
**restartable** and exactly-evaluates each article once (RF-003-B). No SQL-side text
search is needed (regex/FTS is out of scope, A-3). If the ruleset ever grows, an
inverted keyword index is the additive next step.

---

## 8. Retention & volume

- **Demo scale (A-1):** low volume; no partitioning/archival in MVP.
- `outbox` rows with `status='done'` may be pruned by a periodic job; `dead` rows
  are kept for the ops view (NFR-3).
- `articles`/`notifications` are retained (history feeds FR-13).

---

## 9. Repository / port mapping (ADR-001)

| Table(s) | Port |
|----------|------|
| `users` | `UserRepositoryPort` |
| `alerts`, `alert_keywords` | `AlertRepositoryPort`, `IAlertQuery` |
| `articles` | `ArticleRepositoryPort` |
| `notifications` | `NotificationRepositoryPort` |
| `outbox` | `OutboxPort` |

Schema changes ship as **EF Core migrations**; the `.example` config documents
connection-string shape with no secrets (AC-5, NFR-4).

---

## 10. Requirements traceability

| Constraint / table | Satisfies |
|--------------------|-----------|
| `uq_articles_source_guid` | FR-3, R-6, AC-1 |
| `uq_notification_alert_article` | FR-7, AC-2 |
| `notifications.status` + `outbox` lifecycle | FR-10, NFR-2, AC-3 |
| `alerts.channel` CHECK + `alert_keywords` | FR-4, Q-1…Q-3, Q-7 |
| `users.enabled`, `alerts.enabled` | FR-11, FR-12 |
| `ix_outbox_due` + SKIP LOCKED lease | NFR-2, AD-4 |
| no secret columns; env config | NFR-4, AC-5 |

> **Review handoff:** challenge with the Reviewer agent before implementation;
> findings → [`review-findings.md`](review-findings.md).
