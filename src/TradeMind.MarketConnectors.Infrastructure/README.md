# TradeMind Market Connector Core

The Market Connector Core accepts provider-agnostic `MarketSnapshot` instances from `TradeMind.Market.Abstractions`. It owns connector discovery, ingestion, idempotence, PostgreSQL persistence, latest-snapshot queries, and connector health. It does not know how any external provider transports or produces market data.

## Canonical payload and hash

Snapshots are mapped to a versioned canonical DTO before serialization. Property order is fixed by the DTO schema, dictionaries are sorted with ordinal comparison, enums use stable names, and every `DateTimeOffset` is normalized to UTC round-trip form. The persisted JSONB document contains the schema version. SHA-256 is calculated from canonical business content only, excluding the persistence schema wrapper and technical database fields.

## PostgreSQL idempotence

The logical key is `(ConnectorId, SnapshotId)`. A unique PostgreSQL index is the final concurrency guarantee across threads, processes, and pods. The repository catches only PostgreSQL unique violation `23505` for that named constraint, clears failed tracking state, then reloads the winning row. An equal SHA-256 becomes `Duplicate`; a different hash becomes `Conflict`. No process-local lock, cache, or semaphore participates in correctness.

Indexable metadata is stored in `market_snapshot_records`; the full canonical document is stored once in the one-to-one `market_snapshot_payloads` JSONB row. This keeps latest-snapshot and health queries narrow while retaining lossless reconstruction of the canonical model.

`CapturedAt` describes when the market observation was captured and drives snapshot freshness. `ReceivedAt` describes when TradeMind received it and drives connector health.

## Scope

This module exposes application handlers and infrastructure registration only. HTTP endpoints, provider SDKs, authentication signatures, streaming transports, and all trade execution or order mutation remain outside this lot.
