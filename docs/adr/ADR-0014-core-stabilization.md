# ADR-0014: Core Stabilization and Quality Gates

- Status: Accepted
- Date: 2026-07-26
- Decision owners: TradeMind maintainers

## Context

TradeMind has grown into a .NET 9 modular monolith with multiple domain and
application modules, PostgreSQL/pgvector infrastructure, and a growing test
suite. The platform needs one visible quality baseline for dependency direction,
package versions, editor behavior, coverage, and pull-request review. This
stabilization work must not add business behavior or introduce a new runtime
boundary.

## Decision

We will keep the modular monolith and add repository-level guardrails:

1. Package versions are centrally declared in `Directory.Packages.props`.
2. Shared compiler behavior remains in `Directory.Build.props`, with nullable,
   implicit usings, latest analysis, and warnings treated as errors.
3. `.editorconfig` supplies consistent formatting and analyzer preferences.
4. `scripts/validate-dependencies.ps1` runs in CI and checks domain isolation
   and project naming rules.
5. GitHub Actions performs restore, dependency validation, Release build, test
   execution with Coverlet, and Cobertura aggregation with the pinned
   ReportGenerator repository tool.
6. Architecture diagrams, ADRs, CODEOWNERS, and issue/PR templates are stored
   with the code and reviewed like source.

## Consequences

The repository gains a repeatable baseline for local development and CI. A new
package or project must be added to the central configuration and must pass the
dependency check. Coverage is published as a build artifact; it is an
engineering signal and not a substitute for behavior-focused tests. The CI
workflow remains a single solution build and test job, which preserves the
current modular-monolith deployment model.

## Alternatives rejected

- Splitting modules into services would increase operational complexity without
  a current business requirement.
- Adding runtime dependency injection rules would duplicate the existing
  composition root and would not replace compile-time project boundaries.
- Enforcing a hard coverage percentage now would incentivize shallow tests
  while the platform is still establishing module-level baselines.
