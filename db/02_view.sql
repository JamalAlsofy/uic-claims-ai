-- The single read contract between the core and the AI layer.
-- If INS changes its tables, this view is the only thing that has to change here.

CREATE OR REPLACE VIEW app_ai.v_claim_context AS
SELECT c.claim_id,
       c.claim_no,
       c.branch_id,
       c.claim_date,
       c.claim_type_code                            AS claim_type,
       c.status                                     AS claim_status,
       c.claimed_amount,
       p.sum_insured,

       CASE WHEN c.claimed_amount > p.sum_insured THEN 1 ELSE 0 END AS above_limit_flag,
       TRUNC(SYSDATE) - TRUNC(c.claim_date)                        AS age_days,

       (SELECT COUNT(*)
          FROM ins.claims prior
         WHERE prior.insured_id = c.insured_id
           AND prior.claim_date > ADD_MONTHS(c.claim_date, -12))    AS claims_12m

  FROM ins.claims c
  JOIN ins.policies p ON p.policy_id = c.policy_id
 WHERE c.deleted_flag = 'N';

-- Synonym so the .NET side can keep using app_ai.claim_context even if the view moves.
CREATE OR REPLACE SYNONYM app_ai.claim_context FOR app_ai.v_claim_context;

-- Known cost: the claims_12m subquery runs per row. Measured at ~40ms for a 50-row
-- batch on the staging copy. If the officer screen ever paginates over open claims
-- we will need to replace it with a pre-aggregated table refreshed every 15 minutes.
