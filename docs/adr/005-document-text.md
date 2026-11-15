# 005 — Document text leaves the network only when explicitly enabled

**Status:** accepted, Oct 2026

## Context

Triage only needs structured fields, so it never touches document contents. The summary
feature is different: it has to send extracted text from a medical or police report to a
provider. That is a much bigger decision than sending an amount.

## Decision

The summary endpoint is off by default (`Ai:Enabled` gates the whole provider client, and
the summary route additionally requires the caller to hold the `claims.summary` scope).
When it is enabled, the text is passed through a redaction step first.

## Redaction

Rule-based, deliberately boring: national ID patterns, phone numbers, and the insured name
taken from the claim record are replaced with `[REDACTED]` before the call. It is
implemented behind `IDocumentRedactor` so that a better implementation — a named-entity
model, or a local model that never leaves the building — can replace it without touching
the endpoint.

## Known gaps

- The redactor is regex-based and will miss names in free text it has not seen before.
- No token-level audit of what was redacted. The audit row records that redaction ran,
  not exactly what was removed.
- If the answer from legal is "no document text leaves at all", the plan is a local 7B
  model in the same data centre. The client already speaks the protocol Ollama exposes,
  so this is a deployment change.
