# Changelog

## 0.9.2 — 2026-11-14
- Split the single project into core / infrastructure / api. Architecture tests added.
- Prompt-injection cases added to the golden set (3 cases, all currently passing).
- Fixed: model output with an unknown category was being stored as-is.

## 0.9.1 — 2026-11-08
- Background worker no longer retries a claim forever when the view returns it but the
  detail read fails; it logs and moves on.
- Daily call budget added after a loop in local testing burned through two days of quota
  in four minutes.

## 0.9.0 — 2026-10-30
- First end-to-end slice: view, rules, provider call, suggestion, feedback, audit.
- Angular review screen.
