# Terminal Discovery

On real-gateway startup the host:

1. resolves and verifies the configured executable path;
2. reads the product/file version and extracts a build number;
3. reads the PE machine type and compares it with the configured architecture;
4. checks whether the matching process is already running;
5. optionally starts the configured process with shell execution disabled;
6. waits within the startup deadline, then performs protocol handshake and heartbeat;
7. records terminal version, build, architecture, protocol, account environment and health in an immutable snapshot.

If `AutoStartTerminal` is false, a non-running terminal is an explicit startup failure. If the host starts it, shutdown requests a graceful close within the shutdown timeout and only addresses the process owned by the host. A manually started terminal is never killed by the host.

Version, build and architecture failures are safe generic errors. Paths may be present in diagnostics during local troubleshooting, but credentials, login, server, account number and raw terminal packets are never logged.
