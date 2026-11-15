# Contributing

Small repo, three rules.

1. Prompts are code. A change under `src/infrastructure/Ai/Prompts/` needs an eval run
   pasted into the pull request, before and after.
2. Threshold changes need a test. The rules engine touches money; a silent threshold edit
   is the worst kind of bug.
3. Nothing writes to `ins.*`. CI checks this, and it is not negotiable.

Commit messages: `type(scope): summary` — `feat`, `fix`, `chore`, `docs`, `test`, `refactor`.
