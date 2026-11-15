# 001 — Deterministic rules before the model

**Status:** accepted, Oct 2026

## Context

A triage call costs roughly $0.0004 at current prices. At 400 claims a day that is
$4.80 a month, which is nothing. So cost was not the reason.

The reasons were time, auditability and trust. During the pilot design review the head
of claims asked one question: "if a claim is escalated to the fraud desk, what do I tell
the auditor?" A rule that says *four or more claims in twelve months* is an answer. A
model that said 0.83 is not.

## Decision

Rules run first. The model is called only when no rule matches.

## Consequences

- Around 60-70% of claims are resolved without a network call, based on a sample of 200
  historic claims from staging.
- The rules are now a maintenance item. They live in one file, each one is a class with
  its own test, and the thresholds are named constants with the source written next to them.
- Some claims that a model would have classified correctly get a coarse rule answer instead.
  For fast-track claims this is acceptable: the officer confirms the category anyway.
