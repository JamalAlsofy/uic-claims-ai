# Security

Do not open a public issue for a vulnerability. Email jamal.alsoofy2030@gmail.com with
steps to reproduce. I answer within two working days.

## Rules this repository holds itself to

- The `app_ai` database user cannot write to the core schema. The two statements at the
  bottom of `db/01_app_user.sql` exist to prove it, and they are meant to fail.
- No personal data is sent to a model provider on the triage path. The payload is built
  field by field in `TriagePayload` for that reason.
- No credentials in the tree. CI greps for them on every push.
- Provider keys are read once at startup from the environment. Rotating one is a restart,
  not a redeploy.

## Out of scope for this repository

Authentication and authorisation. The API runs behind the insurer's existing gateway,
which already authenticates officers; this service trusts `X-Officer-Id` from that gateway
and is not designed to face the internet directly.
