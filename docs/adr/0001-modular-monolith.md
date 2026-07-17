# ADR 0001: Adopt a DDD modular monolith

- Status: Accepted
- Date: 2026-07-17

## Context

TradeMind spans trading journals, risk controls, market data, strategy research, alerts and AI analysis. These capabilities have different domain models but initially share one product team and deployment lifecycle. Starting with distributed services would add operational and consistency costs before the module boundaries are proven.

## Decision

Build TradeMind as a .NET 9 modular monolith organized around bounded contexts.

Each module owns its domain model, application use cases, infrastructure adapters, API endpoints and PostgreSQL schema. Modules may communicate through explicit contracts and in-process domain or integration events. A module must never reference another module's Domain or Infrastructure project and must never read another module's tables directly.

The initial bounded contexts are:

- Identity and Access
- Trading Journal
- Risk Management
- Market Data
- Strategy Lab
- Alerts
- AI Analysis

Shared building blocks remain deliberately small and contain only stable technical abstractions or universally valid domain primitives.

## Consequences

### Positive

- One deployment and simpler local development.
- Strong transactional consistency within a module.
- Enforceable boundaries through project references and architecture tests.
- Modules can later be extracted when scaling or ownership justifies it.

### Negative

- A single process remains a shared failure and scaling unit.
- Boundary discipline must be enforced continuously.
- Cross-module workflows require explicit orchestration and eventual-consistency patterns.

## Guardrails

- Architecture tests validate dependency rules.
- Every module owns a separate database schema.
- Public module contracts are versioned and minimal.
- No generic repository abstraction; repositories model aggregate persistence.
- External providers are accessed through module-owned ports and adapters.
