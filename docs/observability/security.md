# Observability Security

Telemetry is treated as a secondary data surface. The implementation rejects sensitive tag names, truncates metadata, avoids raw request payloads and excludes identity/session values from metric dimensions.

Health responses are redacted. Exporter endpoints and headers are not returned through API diagnostics. OTLP uses explicit HTTP(S) endpoint validation. Protected diagnostic access must be placed behind HTTPS and the existing identity/authorization boundary in Production.

Correlation headers are accepted only when they satisfy the existing bounded character policy. The server-generated Activity trace remains authoritative for distributed tracing; a client correlation ID is business-support metadata, not a trusted security identity.
