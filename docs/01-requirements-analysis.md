# Requirements Analysis

> **Author:** Product Analyst (AI-assisted)
> **Date:** 2026-06-12
> **Status:** Draft — awaiting human validation
> **Source brief:** `.claude/CLAUDE.md` (vague brief)

---

## 1. Problem Statement

Users need to be notified when public news events match topics they care about,
without manually monitoring news sites. **AlertCenter** ingests news from RSS
feeds, matches incoming articles against user-defined keyword rules, and delivers
notifications through Email and Slack. An admin surface manages users, alert
rules, and the notification history.

---

## 2. Personas

| Persona | Goal | Needs |
|---------|------|-------|
| **Subscriber** | Stay informed on specific topics | Create keyword alerts, choose channel, receive timely notifications |
| **Administrator** | Operate the system | Manage users, view/curate alerts, audit notification delivery |
| **Operator (implicit)** | Keep ingestion healthy | Confirm feeds are polling and matches are firing |

---

## 3. Functional Requirements

### 3.1 Event Source — RSS Ingestion
- **FR-1** The system SHALL poll a configured set of RSS feeds (seed: Reuters, BBC) on a schedule.
- **FR-2** The system SHALL parse feed items into a normalized article (title, summary, link, source, published-at, guid).
- **FR-3** The system SHALL de-duplicate articles already seen (by feed guid / link).

### 3.2 Alerts — Keyword Matching
- **FR-4** A user SHALL create an alert consisting of one or more keywords and a target channel.
- **FR-5** The system SHALL match each new article against active alerts using keyword matching (case-insensitive, whole-word by default).
- **FR-6** A match SHALL generate a notification record linking the alert and the article.
- **FR-7** The system SHALL NOT generate duplicate notifications for the same (alert, article) pair.

### 3.3 Channels — Delivery
- **FR-8** The system SHALL deliver matched notifications via **Email**.
- **FR-9** The system SHALL deliver matched notifications via **Slack**.
- **FR-10** Each notification SHALL record delivery status (pending / sent / failed).

### 3.4 Admin
- **FR-11** Admin SHALL list, create, and disable **Users**.
- **FR-12** Admin SHALL list and manage **Alerts**.
- **FR-13** Admin SHALL view **Notifications** history with status.

---

## 4. Non-Functional Requirements

- **NFR-1 (Timebox)** Buildable within the 3–4h timebox → favors a monolith, minimal infra, mockable channels.
- **NFR-2 (Reliability)** At-least-once delivery acceptable; duplicate suppression handled by FR-7.
- **NFR-3 (Observability)** Notification status and ingestion runs SHALL be inspectable (logs or admin view).
- **NFR-4 (Security)** Channel secrets (SMTP creds, Slack webhook/token) SHALL be configuration, never hard-coded.
- **NFR-5 (Polling cadence)** Default poll interval configurable; sensible default (e.g. 5 min) to avoid hammering sources.

---

## 5. Assumptions

- **A-1** Single-tenant, low user count (demo scale) — no horizontal scaling needed.
- **A-2** Email and Slack can be **mocked/stubbed** for the demo (real creds optional). Demonstrating the delivery abstraction matters more than live sending.
- **A-3** Keyword matching is plain text, not regex or semantic/NLP matching, for MVP.
- **A-4** Authentication for the admin surface can be minimal (single admin / no full RBAC) within the timebox.
- **A-5** RSS feeds are publicly reachable and return standard RSS/Atom.
- **A-6** "Real-time" means near-real-time via polling, not push/streaming.

---

## 6. Ambiguities (need human answer)

| # | Ambiguity | Default assumption if unanswered |
|---|-----------|----------------------------------|
| **Q-1** | Is keyword match ANY keyword (OR) or ALL keywords (AND)? | OR (any keyword matches) |
| **Q-2** | Match scope: title only, or title + summary/body? | Title + summary |
| **Q-3** | Per-user channel config, or system-wide channel config? | Per-alert channel choice; system-wide credentials |
| **Q-4** | Does an alert belong to one user, or are alerts global? | Alert belongs to a user (user → alerts → notifications) |
| **Q-5** | Is live sending required, or is mocked delivery acceptable for the demo? | Mocked with a pluggable sender interface |
| **Q-6** | Auth required for admin UI in MVP? | No / minimal for timebox |
| **Q-7** | Whole-word vs substring matching? | Whole-word, case-insensitive |

---

## 7. Risks

| # | Risk | Impact | Mitigation |
|---|------|--------|------------|
| **R-1** | Timebox overrun (3–4h) | High | Mock channels, monolith, scaffold-friendly stack |
| **R-2** | RSS feed flakiness / format variance | Med | Use a battle-tested feed parser; tolerate per-feed failures |
| **R-3** | Duplicate notifications spamming users | Med | Enforce unique (alert, article) constraint (FR-7) |
| **R-4** | Secret leakage (SMTP/Slack) | High | Env-based config; never commit secrets |
| **R-5** | Scope creep (regex/NLP, multi-tenant, RBAC) | High | Hard-line MVP boundary (Section 9) |
| **R-6** | Polling overlap / re-processing on restart | Med | Persist last-seen guids; idempotent ingestion |

---

## 8. MVP Scope (Proposed)

**In:**
- RSS polling of a small configured feed list (Reuters/BBC seeds)
- Article normalization + de-duplication
- Keyword alerts (OR, case-insensitive, title+summary) owned by a user
- Match → notification generation with dedupe
- Email + Slack delivery via a **pluggable sender interface** (mock-first)
- Admin views: Users, Alerts, Notifications (list + basic create/disable)

**Out (explicitly deferred):**
- Regex / semantic / NLP matching
- Multi-tenancy, full RBAC, SSO
- Per-user delivery scheduling / digests
- Additional channels (SMS, push, webhooks)
- Feed auto-discovery / OPML import
- Real-time push (websockets/streaming)

---

## 9. Acceptance Criteria (MVP "done")

- **AC-1** Given a configured feed, when the poller runs, then new articles are stored once (no dupes).
- **AC-2** Given an active alert with keyword "X", when an article whose title/summary contains "X" is ingested, then exactly one notification is created.
- **AC-3** Given a created notification, when delivery runs, then its status transitions pending → sent (or failed) and is visible in the admin Notifications view.
- **AC-4** Given the admin UI, an admin can list users, list/create alerts, and view notification history.
- **AC-5** No channel secret appears in source or committed config.

---

## 10. Open Questions for Human Decision

The items in **Section 6** (Q-1 … Q-7) require a human decision before/at the
architecture step. Recommended defaults are listed; please confirm or override.

---

## 11. Handoff to Architecture

This document satisfies the **Agent Invocation Rule** precondition
(`requirements-analysis.md exists`). Next mandatory step: **Architecture
Alternatives** (`/architect`), which must consume the confirmed answers to
Section 6.
