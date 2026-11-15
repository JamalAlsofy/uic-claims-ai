# 003 — The provider is a replaceable dependency

**Status:** accepted, Oct 2026

## Context

The provider decision will be made by people who are not in this repository: procurement,
and whoever answers the data-protection question. I did not want a rewrite when they answer.

## Decision

`IAiClient` in `core` is the only seam. `OpenAiCompatibleClient` in `infrastructure` is the
only implementation. It speaks the chat-completions dialect, which is also what Azure OpenAI
in compat mode, Ollama's `/v1` endpoint, and vLLM expose — so a locally hosted model is a
configuration change, not a code change.

## Selection criteria I would score providers on

| Criterion | Weight | Why |
|---|---|---|
| Arabic extraction quality | 30% | claim documents are Arabic, summaries are read by Arabic speakers |
| Contractual no-training clause | 25% | the question every insurer asks |
| Cost per 1,000 claims | 20% | small, but it decides the architecture at scale |
| Structured output support | 15% | removes a class of parsing failures |
| Data residency options | 10% | depends on the answer from legal |

## Consequences

- No `using OpenAI.*` outside `infrastructure/`. An architecture test enforces it.
- The eval harness runs against whatever `AI_MODEL` points at, so changing model is a
  measured decision rather than an opinion.
