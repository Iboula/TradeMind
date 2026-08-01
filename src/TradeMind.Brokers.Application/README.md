# Brokers Application

This project contains the provider-neutral application boundary for broker operations.

`IBrokerConnector` is the only execution port. The registry exposes immutable descriptors in deterministic connector order. `BrokerExecutionValidator` evaluates identity, tenant, permissions, execution mode, plan and risk approval, account state, instrument rules, quantity limits and connector capabilities before a connector is invoked.

`BrokerExecutionService` applies bounded instance-level concurrency, linked cancellation and operation timeouts. It delegates durable idempotency, normalized execution records and audit to ports. The default registrations are in-memory idempotency, no-op audit and no-op record storage; infrastructure replaces them with PostgreSQL implementations when persistence is explicitly enabled.

The application project references only neutral domain contracts, approved analytical result contracts, execution-session application contracts and observability abstractions. It has no EF Core, Npgsql, ASP.NET Core or broker SDK dependency.
