# Telemetry Threat Model

| Risk | Mitigation | Remaining risk |
|---|---|---|
| JWT, API key or password leakage | No credential tags, headers or bodies are logged; API secrets are hashed and one-time | A future custom exporter must preserve the same policy |
| Trading payload leakage | No request/response serialization; only bounded artifact metadata | A new manual log statement could regress the boundary |
| SQL parameter leakage | EF sensitive data logging remains disabled; no SQL tags | Database provider diagnostics need review when enabled |
| Metric-cardinality explosion | Explicit allow-list and length guard | A newly approved dimension needs review |
| Tenant or session exposure | Excluded from metrics, redacted from health | Trace/log backends require tenant-aware access controls |
| Correlation spoofing | Input is bounded and non-authoritative; Activity W3C context is separate | Support tooling must not use correlation as authorization |
| Oversized trace headers or baggage | ASP.NET/server limits and bounded application metadata | Reverse-proxy limits must be configured operationally |
| Collector compromise or OTLP interception | OTLP is disabled by default; HTTPS is supported; headers never leave config | Collector and backend remain deployment responsibilities |
| Exporter outage | Exporters are optional and not readiness dependencies | Backpressure and backend loss can still hide telemetry |
| Sampling hides failures | Parent-based sampling and explicit failure recording | Unsampled child spans are still unavailable by design |
| Health endpoint disclosure | Minimal status-only responses | Network policy must protect detailed infrastructure access |
| Malicious metric-label injection | Only enum-like bounded dimensions are accepted | New dimensions require architecture review |

The residual operational risk is access control and retention in the chosen telemetry backend. TradeMind does not claim that a backend is compliant merely because the application emits safe fields.
