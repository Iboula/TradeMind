# OTLP

The OTLP exporter is optional and supports `Grpc` and `HttpProtobuf`. TradeMind configures an endpoint and headers only inside the process. No collector is required for startup or request handling, and exporter availability is not treated as a business dependency.

```mermaid
flowchart LR
    TM[TradeMind Activities and Metrics] --> OT[OpenTelemetry SDK]
    OT -->|gRPC or HTTP Protobuf| C[Optional OTLP Collector]
    C --> T[Tempo or another trace backend]
    C --> B[Another metrics backend]
```

Collector delivery is not claimed by local tests. The repository only validates configuration and application behavior with exporters disabled or with an in-process listener.
