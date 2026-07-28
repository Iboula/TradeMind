# Prometheus

When `TradeMind:Observability:Metrics:Prometheus:Enabled` is true, the API maps the configured path, default `/metrics`, using the OpenTelemetry Prometheus exporter. The endpoint exposes bounded metric dimensions and never receives tenant, actor, session or correlation identifiers as labels.

The endpoint is intended to be protected by network policy or an authenticated reverse proxy in Production. This sprint does not make infrastructure metrics anonymously available as a security assumption; deployments must choose the appropriate ingress policy.

The repository includes a scrape configuration at `observability/prometheus/prometheus.yml`. It points to the local API and contains no credentials.
