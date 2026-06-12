
# AlertCenter - Feature Design & Build from a Vague Brief

## Goal
Demonstrate AI-assisted engineering, not just coding.

## Timebox
3-4 hours.

## MVP Scope
Event source: RSS feeds (Reuters/BBC)
Alert types: Keyword matching
Channels: Email, Slack
Admin: Users, Alerts, Notifications

## Mandatory Process
1. Product Analysis
2. Architecture Alternatives
3. Human Decision
4. API Design
5. DB Design
6. UI Design
7. Implementation
8. Review
9. Validation

## Documentation Requirements
Every major step must produce:
- Prompt
- Output
- Human validation
- Decision

## Agent Invocation Rule
Never implement before:
- requirements-analysis.md exists
- architecture decision exists

## Review Rule
Every generated code artifact must be challenged by Reviewer Agent.

## Git Rules
After every completed milestone:
- review modified files
- generate commit message
- summarize milestone
- request approval

Never execute git commit without explicit approval.

Commit format:
<type>(<scope>): <description>

Allowed types:
docs
feat
fix
refactor
test
chore
