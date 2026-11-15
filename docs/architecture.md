# Architecture

## Layers

```
core            ClaimContext, TriageSuggestion, rules, output parser
                depends on: Microsoft.Extensions.Logging.Abstractions only
                |
infrastructure  OpenAiCompatibleClient, FilePromptCatalog, Oracle repository, DI
                depends on: core, Dapper, Oracle.ManagedDataAccess, resilience handlers
                |
api             endpoints, background worker, configuration
                depends on: both
```

`ArchitectureTests` fails the build if `core` ever picks up a reference to the infrastructure
assembly or to a vendor SDK. It has already stopped me once, when I was about to reach for
`HttpClient` from the parser to fetch a schema.

## Request path

1. `POST /api/ai/claims/{id}/triage`
2. `OracleClaimRepository.FindAsync` reads one row from `v_claim_context`.
3. `TriageRuleSet.Evaluate` runs three rules in order: already-settled, repeat-claimant,
   fast-track. First match wins and the model is never called.
4. Otherwise the claim is serialised by `TriagePayload` — field by field, not from the record,
   so adding a column to the view cannot start leaking it.
5. The model returns JSON. `TriageOutputParser` validates category against a known list,
   clamps the score, and forces `RequiresHumanReview` when confidence is under 0.6 or the
   flag is missing.
6. A policy-limit check is applied *after* parsing: a confident model does not get to
   override a limit breach.
7. The suggestion is inserted, the audit row is written, and escalation goes through
   `app_ai.pr_flag_for_review`.
8. Any exception in steps 4-5 produces `UNCLASSIFIED` with a reason flag. The endpoint
   still returns 200 with a suggestion an officer can act on.

## Why the failure path returns 200

An officer who sees a red error box stops working and calls support. An officer who sees
"could not analyse, review manually" keeps working. The second behaviour is what lets the
AI layer be deployed on a Monday morning.

## Concurrency

`pr_flag_for_review` takes `FOR UPDATE` on the claim row and refuses PAID/CLOSED claims
with `-20010`. Two officers escalating the same claim serialise on the lock instead of
racing. That logic lives in the database rather than the API because the core system
can also settle claims, and only the database sees both writers.

## Data handling

- The payload to the provider contains a claim number, a branch code, an amount, an age in
  days and two counters. No name, no national ID, no phone, no document text.
- The document summary flow does send extracted text. That is a separate decision recorded
  in ADR-005 and it is off by default.
- Everything the model is told is kept in `src/infrastructure/Ai/Prompts/*.md`, versioned
  with the code so a change in behaviour is visible in a diff.
