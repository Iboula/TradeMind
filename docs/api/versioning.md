# API Versioning

TradeMind uses route-based versioning. The first public contract is under `/api/v1` and each request envelope carries `schemaVersion` where the module contract needs explicit evolution.

An unsupported schema version is rejected with HTTP 400 and a structured `UNSUPPORTED_SCHEMA_VERSION` error. The version endpoint is `GET /api/v1/system/version` and reports the API version, application name, core release and UTC build timestamp without secrets or infrastructure details.

New versions are additive where possible. A breaking change receives a new route version and dedicated immutable DTOs. Existing versions remain stable for their documented support period. Internal domain versions and API schema versions are deliberately separate.
