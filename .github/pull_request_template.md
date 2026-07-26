## Summary

<!-- Describe the intent and the smallest useful scope of this change. -->

## Architecture and dependency impact

- [ ] No business behavior was changed, or the affected module is documented below.
- [ ] Dependency direction remains compliant with `docs/architecture/DEPENDENCY_RULES.md`.
- [ ] Public contracts and versioning impact are documented when applicable.

## Validation

- [ ] `dotnet restore TradeMind.sln`
- [ ] `dotnet build TradeMind.sln --configuration Release`
- [ ] `dotnet test TradeMind.sln --configuration Release --no-build`
- [ ] Coverage was generated for test changes.
- [ ] Dependency validation passed.

## Review notes

<!-- Add risks, migration notes, operational concerns, or known limitations. -->
