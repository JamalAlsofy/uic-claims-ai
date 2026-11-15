# 004 — The officer signs every decision

**Status:** accepted, Oct 2026

## Decision

Nothing in this codebase emits a payment decision. The strongest output is a suggestion
with `requiresHumanReview` set.

## Why not full automation

The commercial argument for automation is that it removes 70% of the queue. The operational
reality is that the remaining 30% get more expensive, because a wrong automated decision
costs more to unwind after payment than to review before it.

There is also a people problem that is easy to underestimate: an officer who feels watched
by a tool works around it. An officer who feels the tool is a second pair of eyes uses it.

## Implementation notes

- `ai_suggestions` and `claim_decisions` are separate tables, not columns.
- `decided_by` is read from the `X-Officer-Id` gateway header. Taking it from the request
  body would let anyone attribute a decision to a colleague.
- This layer never calls the core's approve/reject endpoints. Not even in tests.
- Acceptance rate is measured for improving the rules and the prompt, never as a target
  to hit by reducing review.
