# Deployment

The bridge host is a standalone ASP.NET Core process and is suitable for a
private network segment. Configure the host through
`TradeMind:Brokers:MetaTrader5:BridgeHost` and the client through
`TradeMind:Brokers:MetaTrader5:BridgeClient`.

Production-like defaults are TLS required, mutual TLS authentication,
demo-only mode, bounded concurrency and a finite replay window. The local
demo test host opts into plaintext explicitly because its transport is an
in-memory `WebApplicationFactory`; that setting must not be copied to a
shared environment.

Secrets are runtime concerns. Use the deployment secret store for private
keys, certificates and service tokens. Do not put credentials in appsettings,
test fixtures, logs or bridge payloads. The repository contains no real
terminal credentials and no native MT5 package.

Before a future adapter deployment, verify:

- TLS and certificate validation are active;
- authentication mode matches the service identity configuration;
- the environment and account are Demo;
- shared idempotency/replay storage is selected for multiple hosts;
- only the intended bridge endpoints are network reachable;
- telemetry redaction is active.
