# Distributed Locking

The broker execution lock scope is `tenant + broker + account + instrument`. `PostgreSqlBrokerExecutionLock` uses a PostgreSQL session advisory lock, a bounded acquisition timeout and cancellation. The lease retains the connection until `DisposeAsync`, making ownership explicit and ensuring the lock is released with the session.

The lock key is a deterministic SHA-256-derived 64-bit value. Two pods using the same PostgreSQL database and scope cannot acquire the scope concurrently. A different tenant is a different scope. When persistence is disabled, the application registers an unavailable lock rather than a process-local semaphore; this is fail-closed for any future Live path.
