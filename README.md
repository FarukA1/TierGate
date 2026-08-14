# TierGate

[![TierGate.Core](https://img.shields.io/nuget/v/TierGate.Core?label=TierGate.Core)](https://www.nuget.org/packages/TierGate.Core)
[![TierGate.AspNetCore](https://img.shields.io/nuget/v/TierGate.AspNetCore?label=TierGate.AspNetCore)](https://www.nuget.org/packages/TierGate.AspNetCore)
[![License](https://img.shields.io/github/license/FarukA1/TierGate)](LICENSE)

Tiered rate-limiting and quota tracking for .NET — subscription-tier-aware, Table Storage-first, fail-closed by default.

Extracted and generalized from a tiered rate-limiting pattern built and run in production for a real SaaS API.

## Why

Every serious SaaS API needs subscription-tier-aware rate limiting: different request-per-minute limits and monthly quotas per plan, enforced consistently, with clear response headers and upgrade prompts when a caller hits a ceiling. The built-in `Microsoft.AspNetCore.RateLimiting` middleware has no distributed backend or quota/tier concept; the established `AspNetCoreRateLimit` package is Redis-oriented and defaults to fail-open. Nothing packages dual-window (rate + quota) tracking, tier resolution, and a Table Storage-first backend with fail-closed-by-default together — which matters specifically for Azure Functions Consumption-plan APIs, where Table Storage is already available and Redis is an extra service to run and pay for.

## Packages

| Package | Status |
|---|---|
| [`TierGate.Core`](src/TierGate.Core) | Rate-limit stores (`InMemoryRateLimitStore`, `TableStorageRateLimitStore`), `RateLimitWindow`, and the generic `TierGate` policy-gate helpers. |
| [`TierGate.AspNetCore`](src/TierGate.AspNetCore) | `UseTierGate` middleware — tier resolution, window checks, response headers, RFC7807 errors — plus `GateResult` → `IActionResult` conversion for controller-level gating. |

## Demo

[`demos/TierGate.AspNetDemo`](demos/TierGate.AspNetDemo) is a runnable ASP.NET Core API showing both packages end to end with `InMemoryRateLimitStore`.

## License

MIT
