# Runbook

## Provider is down or returning errors

Symptom: `Provider returned 503` in logs, officers see "خدمة التحليل غير متاحة".

Impact: none on processing. Every claim falls back to manual review with
`AI_UNAVAILABLE`.

1. Check the provider status endpoint with the key that is in the secret store.
2. 401 — rotate the key. It is a 10-minute change: secret store, then restart the pods.
3. 429 — either the retry policy is too aggressive or the budget is misconfigured.
   Confirm `Ai:DailyCallBudget` matches the current plan.
4. More than 30 minutes of failures — set `Ai:Enabled=false` and tell the operations
   manager. Nothing else needs to happen; the screen already handles it.

## Override rate is climbing

Symptom: `app_ai.v_acceptance` shows `accept_pct` under 60 for a category that used to
be above 80.

1. `dotnet run --project tests/Eval` — is the golden set still passing?
2. If the eval passes but production disagrees, the golden set is stale. Pull 20 recent
   overrides and add the ones that are genuinely wrong.
3. If the eval fails, check the last change to `src/infrastructure/Ai/Prompts/`. Prompts
   are code; treat a prompt diff as a behaviour change and roll it back the same way.

## Daily five-minute check

```sql
-- suggestions waiting more than two days for a decision
select count(*) from app_ai.ai_suggestions s
 where s.created_at < systimestamp - interval '48' hour
   and not exists (select 1 from app_ai.claim_decisions d where d.claim_id = s.claim_id);

-- provider latency, last 24h
select round(avg(latency_ms)) avg_ms, max(latency_ms) worst_ms, count(*) calls
  from app_ai.ai_suggestions
 where created_at > systimestamp - interval '1' day
   and model_name <> 'rules-engine';
```

## Key rotation

Every 90 days: create the new key, update the secret, restart, keep the old key alive for
24 hours, revoke. The client reads the key at startup only, so a rolling restart is enough.
