# Contributing

TradeMind contributions should preserve the modular architecture and keep the codebase easy to reason about.

## Before starting

- Work from the requested branch.
- Check `git status`.
- Read the relevant Domain, Application, Infrastructure, API, and test files before editing.
- Review ADRs for decisions that affect the change.

## Contribution expectations

- Keep changes focused.
- Add tests for behavior changes.
- Update documentation for architectural, workflow, or operational changes.
- Avoid drive-by refactors.
- Avoid changing public contracts unless the task requires it.
- Do not introduce secrets.

## Pull request contents

A good PR description includes:

- What changed.
- Why it changed.
- How it was tested.
- Any migration or operational impact.
- Known risks or follow-up work.

## Review standards

Reviewers should focus on:

- Correctness.
- Module boundaries.
- Domain invariants.
- Persistence and migration safety.
- Test coverage.
- Security and secret handling.
- CI compatibility.

## Documentation changes

Documentation should be specific to TradeMind. Avoid empty stubs, vague templates, or generic process text that does not describe this repository.
