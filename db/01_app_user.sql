-- Run once as SYSDBA, with the core team present.
-- Everything the AI layer can do is decided here. Start from nothing and add.

CREATE USER app_ai IDENTIFIED BY "&app_ai_password"
  DEFAULT TABLESPACE users
  QUOTA 512M ON users;

GRANT CREATE SESSION      TO app_ai;
GRANT CREATE TABLE        TO app_ai;
GRANT CREATE VIEW         TO app_ai;
GRANT CREATE PROCEDURE    TO app_ai;
GRANT CREATE SEQUENCE     TO app_ai;

-- Read side. Only the columns the triage logic actually needs; no name, no ID, no contact.
GRANT SELECT ON ins.claims          TO app_ai;
GRANT SELECT ON ins.policies        TO app_ai;

-- Write side is exactly one procedure. Not the package, the procedure.
GRANT EXECUTE ON ins.pkg_claims.pr_escalate TO app_ai;

-- Verification. Connect as app_ai and run both statements. Both MUST fail:
--   update ins.claims set status = 'PAID' where claim_id = 1;   -- ORA-01031
--   drop table ins.claims;                                      -- ORA-01031
-- If either succeeds, stop and fix the grants before going further.
