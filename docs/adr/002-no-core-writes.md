# 002 — No writes to the core schema

**Status:** accepted, Oct 2026

## Context

The core claims system has been running for years. Nobody currently on the team wrote it.
It has triggers we cannot see from the outside, and at least one nightly job that assumes
`claims.status` only changes through the application.

## Decision

The `app_ai` user gets `SELECT` on two tables and `EXECUTE` on one procedure. That is the
entire grant list in `db/01_app_user.sql`.

## Alternatives rejected

**Direct UPDATE.** Would bypass whatever trigger logic the core relies on. Also makes the
AI layer a suspect in every future data incident.

**A copy of the data in a separate database.** Tempting, but it creates a second version of
the truth, a nightly job, and a second set of PII to protect. The latency it would add also
kills the one thing the feature is for.

## Consequences

- Every new write requirement becomes a conversation with the core team, which is slower
  but means the core team knows what we do to their data.
- The grant file contains two statements that must fail. They are the test. If someone
  widens the grants in a hurry, the next person to run that file will find out.
