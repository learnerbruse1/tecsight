## Agent skills

### Issue tracker

Issues are tracked as local markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default triage labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout — `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Grilling

Use the `grilling` skill to stress-test a plan, decision, or idea. Before grilling, read `CONTEXT.md`, `docs/agents/domain.md`, and relevant `docs/adr/`; use the glossary vocabulary and avoid inventing synonyms. Settled decisions that affect architecture should be recorded under `.scratch/<feature>/` or `docs/adr/` as appropriate.
