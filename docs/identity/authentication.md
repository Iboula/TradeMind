# Authentication

TradeMind is a resource server. It validates credentials presented by clients;
it does not issue JWTs, manage passwords, implement OAuth login, or contain a
vendor identity SDK.

## JWT bearer

JWT bearer validation is configured with an authority, audience, issuer and
lifetime validation settings. The claim mapper requires a stable subject,
organization and tenant claims. Roles are mapped through the explicit
TradeMind role table. Direct permission and `scope` claims are allow-listed
against `TradeMindPermissions`; unknown values grant nothing. Claim and
permission counts are bounded.

## API key

Clients send `X-TradeMind-Api-Key`. The API host parses the public identifier and
secret, then the infrastructure validator loads only the stored hash metadata
and compares the supplied secret with a fixed-time PBKDF2-SHA512 verification.
The raw secret is returned once on creation or rotation and is never persisted,
logged or included in read DTOs.

Development/test authentication is registered only for the Test environment,
requires explicit configuration and is rejected by the Production startup
guard. It is not a production fallback.

Invalid or missing credentials receive a generic response. Authentication
details and raw tokens are excluded from logs and audit payloads.
