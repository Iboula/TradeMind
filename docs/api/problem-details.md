# ProblemDetails

The host maps known failures to RFC-compatible ProblemDetails responses with:

- `type`, `title`, `status`, `detail` and `instance`;
- `traceId` and `correlationId` extensions;
- an `errors` collection for field-level validation failures.

The mapping is intentionally conservative:

| Failure | Status |
| --- | ---: |
| Invalid request or schema | 400 |
| Unsupported configured module | 501 |
| Caller cancellation | 408 |
| Server request timeout | 504 |
| Unexpected failure | 500 |

Internal exception messages are not returned for unexpected failures. Stack traces, connection strings, local paths and secrets are never part of the response contract. Details are available through structured server logs correlated by request and correlation identifiers.
