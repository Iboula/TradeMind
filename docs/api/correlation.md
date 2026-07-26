# Correlation

`CorrelationIdMiddleware` reads `X-Correlation-ID`, validates its length and allowed characters, and returns the effective value in the response header. Missing or unsafe values are replaced with a deterministic hash derived from the request trace identifier and the current `TimeProvider` timestamp.

The effective identifier is stored in `HttpContext.Items`, included in ProblemDetails and passed to application calls that support tracing. Request logging records method, path, endpoint, status, duration and correlation ID. Payloads, prompts, prices and account values are excluded by default.
