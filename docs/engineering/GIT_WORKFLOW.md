# Git Workflow

TradeMind uses short-lived branches and CI validation.

## Branches

- `main` represents the protected integration line.
- `feature/*` branches contain active feature work.
- The KnowledgeHub MVP work is currently on `feature/knowledgehub-mvp`.

## Commit style

Use concise conventional prefixes:

- `feat:` for product behavior.
- `fix:` for bug fixes.
- `test:` for test coverage.
- `docs:` for documentation.
- `refactor:` for behavior-preserving restructuring.
- `chore:` for maintenance.

Examples:

```text
docs: add engineering foundation documentation
test: add PostgreSQL pgvector integration coverage
fix: quote KnowledgeHub vector search columns
```

## Atomic commits

Each commit should contain one coherent reason to change. Avoid mixing production behavior, broad formatting, and unrelated documentation in the same commit.

## Pull requests

Open a PR from the feature branch. The PR should explain what changed, why, how it was validated, and any risk. Do not merge until CI passes and review feedback is addressed.

## Conflict handling

Resolve conflicts by preserving the intended behavior from both sides. Do not overwrite user work or unrelated changes.

## Push policy

Push only after local validation for substantial changes. Documentation-only changes should still be checked for broken links, accidental secrets, and CI compatibility.
