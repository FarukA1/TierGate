# TierGate

Tiered rate-limiting and quota tracking for .NET — subscription-tier-aware, Table Storage-first, fail-closed by default.

Extracted and generalized from a tiered rate-limiting pattern built and run in production for a real SaaS API.

## Why

Every serious SaaS API needs subscription-tier-aware rate limiting: different request-per-minute limits and monthly quotas per plan, enforced consistently, with clear response headers and upgrade prompts when a caller hits a ceiling. The built-in `Microsoft.AspNetCore.RateLimiting` middleware has no distributed backend or quota/tier concept; the established `AspNetCoreRateLimit` package is Redis-oriented and defaults to fail-open. Nothing packages dual-window (rate + quota) tracking, tier resolution, and a Table Storage-first backend with fail-closed-by-default together — which matters specifically for Azure Functions Consumption-plan APIs, where Table Storage is already available and Redis is an extra service to run and pay for.

## Packages

| Package | Status |
|---|---|
| [`TierGate.Core`](src/TierGate.Core) | Rate-limit stores (`InMemoryRateLimitStore`, `TableStorageRateLimitStore`), `RateLimitWindow`, and the generic `TierGate` policy-gate helpers. |
| [`TierGate.AspNetCore`](src/TierGate.AspNetCore) | ASP.NET Core middleware. Scaffolded, not yet implemented. |

## License

MIT
