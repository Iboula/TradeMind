# Idempotency

Command-like POST routes for trading workspace builds, Trading Assistant questions and paper-trading simulations require the `Idempotency-Key` header.

The in-memory `IIdempotencyStore` stores the normalized request hash, status code, content type and response bytes. The first request executes the application operation. A later request with the same key and identical normalized body receives the captured response. Reusing a key with another body returns HTTP 409. Entries expire after the configured TTL and expired entries are pruned during access.

The store is thread-safe and coordinates concurrent requests for one key without a static lock or a global semaphore. It is intentionally single-instance: it does not provide cross-pod guarantees. A future distributed store must preserve the same hash, conflict and replay semantics before horizontal scaling of command endpoints.

Keys are bounded by length and the request body is bounded by the API payload limit. Hashes contain request bytes only; secrets are not separately logged or included in log messages.
