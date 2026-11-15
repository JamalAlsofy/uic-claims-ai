# Database scripts

Run in order, as the account noted in each file. There is no automatic migration:
the first script creates a database user, which is a DBA action and a change-control
item at any insurer.

| File | Runs as | Effect |
|---|---|---|
| `01_app_user.sql` | SYSDBA | Creates `app_ai` and grants read-only access to two core tables |
| `02_view.sql` | `app_ai` | The single read contract (`v_claim_context`) |
| `03_ai_tables.sql` | `app_ai` | Suggestions, officer feedback, audit |
| `04_procedure.sql` | `app_ai` | Escalation wrapper with a row lock |

Rebuilding from scratch is safe: `drop user app_ai cascade;` and start again.
The core schema is never touched by any of these.
