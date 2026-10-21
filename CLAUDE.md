# RateWatch

RateWatch is a practice application.

## Tech stack

- Backend: ASP.NET Core
- Frontend: Vue 3
- Tests: xUnit
- Persistence (later): EF Core with SQLite

## Rules

### Local only
- All development stays local.
- Never push code, create remote repositories, or publish anything without explicit permission.

### Git
- Do not make Git commits automatically.
- Suggest commit messages, then wait for approval before committing.

### Checkpoints
- Work in small checkpoints.
- Before each checkpoint, explain the proposed changes and wait for approval before implementing.

### Approach
- Use lightweight specification-driven development, domain-driven design, and test-driven development where appropriate.
- Prefer simple architecture. Avoid unnecessary abstractions and features.

### Decision log
- Record important architectural decisions in `docs/DECISIONS.md`, along with actual AI suggestions that were accepted or rejected.
- Do not invent decisions or log every interaction.

### Scope
- Do not implement alert functionality yet.
