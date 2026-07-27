# Identity Infrastructure

This module contains PostgreSQL persistence, PBKDF2-SHA512 API-key hashing, JWT
claim mapping and credential validation. Raw API-key secrets are transient and
are never persisted, logged or returned by read operations. Provider-specific
authentication wiring remains composed by the API host.
