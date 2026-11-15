-- Owned by app_ai. Nothing here is referenced by the core system, which is the point:
-- we can drop the whole schema and the core will not notice.

CREATE TABLE app_ai.ai_suggestions (
  suggestion_id   NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  claim_id        NUMBER         NOT NULL,
  category        VARCHAR2(40)   NOT NULL,
  confidence      NUMBER(4,3)    NOT NULL CHECK (confidence BETWEEN 0 AND 1),
  risk_score      NUMBER(5,2)    NOT NULL CHECK (risk_score BETWEEN 0 AND 100),
  flags_json      CLOB           CHECK (flags_json IS JSON),
  summary_ar      VARCHAR2(2000),
  requires_review CHAR(1)        DEFAULT 'Y' NOT NULL CHECK (requires_review IN ('Y','N')),
  model_name      VARCHAR2(80)   NOT NULL,
  input_tokens    NUMBER,
  output_tokens   NUMBER,
  latency_ms      NUMBER,
  created_at      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL
);

CREATE INDEX ix_suggestions_claim ON app_ai.ai_suggestions (claim_id, created_at DESC);

-- Officer feedback. Separate table on purpose: "what the system thought" and
-- "what a person decided" must never be the same row.
CREATE TABLE app_ai.claim_decisions (
  decision_id    NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  claim_id       NUMBER        NOT NULL,
  suggestion_id  NUMBER,
  decision       VARCHAR2(20)  NOT NULL CHECK (decision IN ('ACCEPT','OVERRIDE')),
  final_category VARCHAR2(40),
  comment_ar     VARCHAR2(1000),
  decided_by     VARCHAR2(120) NOT NULL,
  decided_at     TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL
);

CREATE INDEX ix_decisions_claim ON app_ai.claim_decisions (claim_id, decided_at DESC);

CREATE TABLE app_ai.audit_trail (
  audit_id   NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  action     VARCHAR2(40)  NOT NULL,
  entity     VARCHAR2(30)  NOT NULL,
  entity_id  NUMBER,
  actor      VARCHAR2(120),
  model_name VARCHAR2(80),
  payload    CLOB          CHECK (payload IS JSON),
  created_at TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL
);

CREATE INDEX ix_audit_entity ON app_ai.audit_trail (entity, entity_id, created_at DESC);

-- Supporting query for the daily review report (override rate by category).
CREATE OR REPLACE VIEW app_ai.v_acceptance AS
SELECT s.category,
       COUNT(*)                                                   AS suggestions,
       SUM(CASE WHEN d.decision = 'ACCEPT' THEN 1 ELSE 0 END)     AS accepted,
       ROUND(SUM(CASE WHEN d.decision = 'ACCEPT' THEN 1 ELSE 0 END)
             / COUNT(*) * 100, 1)                                 AS accept_pct
  FROM app_ai.ai_suggestions s
  LEFT JOIN app_ai.claim_decisions d ON d.claim_id = s.claim_id
 WHERE s.created_at > SYSDATE - 30
 GROUP BY s.category;
