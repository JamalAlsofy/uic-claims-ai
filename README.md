# Claims triage layer

An AI triage service that sits next to an existing Oracle-backed insurance core without
touching it. A rules engine handles the obvious cases; a language model handles the rest;
an officer makes every decision.

I built this after looking at what actually blocks AI projects at insurers. It is not model
quality. It is the fact that nobody wants to be the person who changed the claims table.
So the design constraint here was: **the core schema is read-only from our side, and no
payment decision is ever automated.** Everything else follows from that.

```
Officer screen  ->  ASP.NET Core API  ->  rules engine (deterministic)
                            |                    |
                            |                    +--> handled ~60-70% of claims, no tokens spent
                            |
                            +--> model provider (only for ambiguous claims)
                            |
                            +--> app_ai schema (suggestions, feedback, audit)
                                        |
                                    read-only view  ->  ins.claims (existing core)
```

## Status

Working prototype, not in production. Version 0.9.2.

- [x] Layered solution: `core` (no third-party deps) / `infrastructure` / `api`
- [x] Rules engine with its own tests and a guard against payment decisions
- [x] Provider abstraction; OpenAI-compatible, also works with Ollama and vLLM
- [x] Strict output parsing with fallback to the manual path on any failure
- [x] Oracle layer: one view in, one procedure out
- [x] Angular review screen (zoneless, signals)
- [x] Golden-set harness wired into CI
- [ ] Retrieval over policy wording (Oracle 23ai vectors) — designed, not written
- [ ] Load test against a realistic claim volume
- [ ] Shadow-mode comparison against the current manual queue

## Design decisions

The four that matter are written up in [`docs/adr/`](docs/adr/):

1. **Rules before the model.** Deterministic logic first, model only for what is left. Reasons:
   it is auditable, it is free, and 60-70% of claims never need a prompt.
2. **No writes to the core schema.** Reads come through `app_ai.v_claim_context`. The single
   state change we need (escalation) goes through the core team's own procedure.
3. **The provider is replaceable.** `IAiClient` is the only seam. Swapping to a locally hosted
   model is a config change, which matters when the data-protection question comes up.
4. **The officer signs.** `ai_suggestions` and `claim_decisions` are separate tables.
   `decided_by` comes from the gateway header, never from the request body.

## Running it

```bash
docker compose up
# Oracle comes up on 1521, the API on 5080
curl localhost:5080/health
```

DB scripts run in order inside the container for a fresh volume; on an existing database
run them by hand as described in [`db/README.md`](db/README.md).

Without Docker you need an Oracle instance and the .NET 10 SDK:

```bash
dotnet build Uic.Claims.Ai.sln
dotnet run --project src/api
```

The API is configured with `Ai:Enabled=false` locally, so it runs on rules only. That is a
useful default: the manual path is the one that has to work when everything else fails, so
I develop against it.

There is an `api.http` file if you use Rider or VS Code's REST client.

## Tests

```bash
dotnet test                          # unit + architecture tests
dotnet run --project tests/Eval      # golden set, structural mode without a key
AI_API_KEY=... dotnet run --project tests/Eval   # live evaluation
```

The eval harness has 14 labelled cases including three prompt-injection attempts. It exits
non-zero if schema validity drops below 99%, accuracy below 0.85, or any injection succeeds.
A separate run of Stryker covers the rules engine; the threshold is 60% and I got it to 74%
on the last run.

## What is deliberately not here

- **No fallback to a second provider.** If the primary is down, claims go to the manual queue.
  A second provider doubles the surface area for a failure mode that already has a safe answer.
- **No caching of suggestions.** A suggestion older than a day is misleading; recomputing is cheap.
- **No streaming for triage.** Streaming is used for the document summary only, where the
  officer is reading, not deciding.
- **No fine-tuning.** At a few hundred claims a month there is not enough signal, and the
  golden set is a better investment.

## Things I would do differently

- The `claims_12m` subquery in the view runs per row. Fine at batch size 20, wrong at 200.
  If this reaches production I would replace it with a nightly aggregate.
- `DailyCallBudget` counts in process memory. With two replicas the real ceiling doubles.
  It needs Redis before horizontal scaling, and I left a comment saying so rather than
  pretending it is finished.
- I started with a single project and split it into three after the first hundred lines,
  which is why the git history has a restructuring commit in it.

## Repository layout

```
src/core            domain, rules, prompt contracts. No vendor SDKs.
src/infrastructure  Oracle, provider client, prompt loading, DI
src/api             minimal API endpoints, background worker
web/                Angular review screen
db/                 Oracle scripts, run in order
tests/Unit          xUnit + NSubstitute, plus architecture tests
tests/Eval          golden set harness used as a CI gate
docs/               architecture, ADRs, runbook
```

## License

MIT.

---

Jamal Al-Sofi — systems analyst, .NET and Angular, Oracle integration.
jamal.alsoofy2030@gmail.com · [github.com/JamalAlsofy](https://github.com/JamalAlsofy)
