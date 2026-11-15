-- Wrapper over the core's own escalation procedure.
-- We do not write to ins.* ourselves; this keeps the core team in control of the
-- business rules (which statuses may be escalated, what audit the core requires).

CREATE OR REPLACE PROCEDURE app_ai.pr_flag_for_review (
  p_claim_id IN NUMBER,
  p_actor    IN VARCHAR2
) IS
  v_status VARCHAR2(20);
BEGIN
  SELECT status
    INTO v_status
    FROM ins.claims
   WHERE claim_id = p_claim_id
     FOR UPDATE;                        -- blocks a concurrent settlement on the same claim

  IF v_status IN ('PAID', 'CLOSED') THEN
    RAISE_APPLICATION_ERROR(-20010, 'CLAIM_NOT_OPEN');
  END IF;

  ins.pkg_claims.pr_escalate(p_claim_id => p_claim_id, p_reason => 'AI_TRIAGE_REVIEW');

  INSERT INTO app_ai.audit_trail (action, entity, entity_id, actor, payload)
  VALUES ('FLAG_REVIEW', 'CLAIM', p_claim_id, p_actor,
          '{"source":"triage","reason":"model_or_rule_flagged"}');

  COMMIT;
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    RAISE_APPLICATION_ERROR(-20011, 'CLAIM_NOT_FOUND');
END pr_flag_for_review;
/

-- Test after install:
--   exec app_ai.pr_flag_for_review(1, 'TEST');
--   select * from app_ai.audit_trail order by created_at desc fetch first 3 rows only;
-- Then try it twice on a PAID claim and confirm you get CLAIM_NOT_OPEN.
